#!/usr/bin/env python3
"""Decide a run from OSV-Scanner reports, and prepare what it scans and uploads.

    python .github/scripts/osv_gate.py pins pip-report.json semgrep.installed.txt
    python .github/scripts/osv_gate.py canary canary.json
    python .github/scripts/osv_gate.py sarif osv.sarif --pins semgrep.installed.txt \
        --pins-from .github/requirements-semgrep.txt
    python .github/scripts/osv_gate.py verdict osv.json --pins semgrep.installed.txt

.github/workflows/osv-scanner.yml runs the four in that order. Each one exits 0
when it is satisfied, 1 when it found what it exists to find, and 2 when it could
not read what it was given - and 2 is never a pass.

Why the scanner's own exit code is not the answer
--------------------------------------------------
OSV-Scanner exits 0 for "nothing found", 1 for "found something" and 128 for "no
packages identified", per its own output documentation, read 2026-10-14. Two of
the three ways it can see nothing at all therefore exit 0 or look like a choice
between pass and fail. A scan of a lock file the restore never wrote, a parser
that reads a file as empty, a database that did not answer - each of them is
"nothing found" to anybody reading the exit code. So this script reads the
report, and refuses a report that did not read every file it was meant to.

Every report is checked for its SHAPE before anything is read out of it, and a
wrong shape is a 2 with the place named, never a traceback. That was missing
from the first version and a review of pull request 62 pointed at it: a report
whose results were a string would have crashed on the first lookup and exited 1,
which reads as "found something" rather than "could not read".

What is scanned, and why none of it is found by searching
----------------------------------------------------------
OSV-Scanner does not read .csproj files. For .NET it reads packages.lock.json,
packages.config and deps.json, and this repository commits none of them. So the
workflow restores with lock files turned on and scans those, one per project
that git tracks - the owner's decision of 2026-10-14 rather than committing nine
lock files that GitHub's dependency graph would not read either.

For the scanner semgrep, only the version is pinned in the repository, and the
first measurement made the reason for the `pins` step obvious. Asked to resolve
semgrep==1.180.0 by itself, OSV-Scanner reported eleven advisories (22
identifiers) in anyio 4.9.0, idna 3.9.0 and python-multipart 0.0.9. The Semgrep
job of pull request 61 had installed anyio 4.15.1, idna 3.20 and
python-multipart 0.0.32, and those 66 packages scanned exactly as installed
carried none. The resolver's guess is not what runs, and a gate built on it
would have blocked every pull request that day over packages nothing installs.
So the workflow asks pip what it WOULD install, without installing it, and this
script turns pip's answer into the list that is scanned with resolution off.

Where an alert points
---------------------
Both kinds of file the scan reads exist only inside the job - the lock files
restore wrote and the list pip answered with. An alert pointing at one of them
points at nothing in the repository. So the `sarif` step moves every location
to the tracked file that caused it: a project's lock file to that project's
.csproj, and the pip list to the requirements file it was resolved from. Only
the location fields are rewritten - the results, rules and fingerprints the
scanner wrote stay as they are. A location that belongs to neither is refused,
because it means the scan read something this gate does not know about.

The canary
----------
Every run first scans two packages with published advisories - Newtonsoft.Json
12.0.3 through the lock file parser, and requests 2.19.1 through the
requirements parser. Both must come back vulnerable. Without that, a green
verdict cannot tell "nothing is vulnerable" from "the database did not answer"
or "the parser read nothing", and those look identical from outside.

What blocks
-----------
Any advisory on any package, whatever its severity. That is the owner's wording
of 2026-10-14 - "blocks the pull request if it found something" - and it is
already how restore treats NuGet packages here. A finding that is settled rather
than wrong is not silenced in this file. OSV-Scanner reads an osv-scanner.toml
with an ignore entry, a reason and a date it expires, which every reader of the
scan sees - a list here would quieten this gate alone.
"""
import argparse
import json
import os
import subprocess
import sys
from urllib.parse import unquote, urlparse

# The two packages the canary scan must find vulnerable, as (ecosystem, name, version). Chosen
# for advisories that are years old and fixed long since, so the answer cannot be withdrawn
# by a database that changes its mind about something recent.
CANARIES = (
    ("NuGet", "Newtonsoft.Json", "12.0.3"),
    ("PyPI", "requests", "2.19.1"),
)

# The one package the pip answer has to contain. A resolution that came back without it
# resolved something else - a wrong requirements file, an empty one, a pip that failed and
# wrote a report anyway.
PINNED_TOOL = "semgrep"

# A ceiling on what is read, far above anything real. Measured 2026-10-14 on this repository:
# the JSON report with every package listed is about 120 KB and the SARIF about 30 KB. A file
# a thousand times larger is not a report of this scan, and it is refused before it is read.
MOST_BYTES = 64 * 1024 * 1024


class Malformed(Exception):
    """A report whose shape is not the one this gate reads. Always a 2, never a pass."""


def load(path, what):
    """A JSON object from a file, or None with the reason printed."""
    try:
        size = os.path.getsize(path)
        if size > MOST_BYTES:
            print("osv gate: %s %s is %d bytes, over the %d this gate reads"
                  % (what, path, size, MOST_BYTES), file=sys.stderr)
            return None
        with open(path, encoding="utf-8") as handle:
            data = json.load(handle)
    except (OSError, ValueError, RecursionError) as exc:
        # RecursionError is what the parser raises for nesting deep enough to exhaust the
        # stack. Nothing this scanner writes comes close, so it is an unreadable file like any.
        print("osv gate: cannot read %s %s: %s" % (what, path, exc), file=sys.stderr)
        return None
    if not isinstance(data, dict):
        print("osv gate: %s %s is not a JSON object" % (what, path), file=sys.stderr)
        return None
    return data


def field(record, key, kind, where, required=True):
    """record[key], refused unless it is of `kind`. A missing optional field reads as empty."""
    if not isinstance(record, dict):
        raise Malformed("%s is not an object" % where)
    if key not in record or record[key] is None:
        if required:
            raise Malformed("%s has no %s" % (where, key))
        return kind()
    value = record[key]
    if not isinstance(value, kind):
        raise Malformed("%s.%s is a %s, not a %s" % (where, key, type(value).__name__, kind.__name__))
    return value


def items(record, key, where, required=False):
    """record[key] as a list of objects, each one checked."""
    values = field(record, key, list, where, required)
    for index, value in enumerate(values):
        if not isinstance(value, dict):
            raise Malformed("%s.%s[%d] is not an object" % (where, key, index))
    return values


def refused(exc):
    print("osv gate: the report is not in the shape this gate reads - %s" % exc, file=sys.stderr)
    return 2


def pins(pip_report, out):
    """Turn `pip install --dry-run --report` into name==version lines."""
    report = load(pip_report, "pip report")
    if report is None:
        return 2
    try:
        lines = []
        for index, item in enumerate(items(report, "install", "pip report", required=True)):
            where = "install[%d].metadata" % index
            metadata = field(item, "metadata", dict, "install[%d]" % index)
            lines.append("%s==%s" % (field(metadata, "name", str, where),
                                     field(metadata, "version", str, where)))
    except Malformed as exc:
        return refused(exc)
    names = {line.split("==")[0].lower() for line in lines}
    if PINNED_TOOL not in names:
        print("osv gate: pip would install %d package(s) and %s is not one of them, so this"
              " is not the answer to the pinned requirements file." % (len(lines), PINNED_TOOL),
              file=sys.stderr)
        return 2
    with open(out, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(sorted(lines, key=str.lower)) + "\n")
    print("pins: %d package(s) pip would install, written to %s" % (len(lines), out))
    return 0


def packages(report):
    """Every (source path, package, vulnerabilities) in a report, the whole shape checked first.

    A list rather than a generator, so that a malformed record late in the report is refused
    before anything is printed about the records before it.
    """
    found = []
    for r, result in enumerate(items(report, "results", "report")):
        where = "results[%d]" % r
        source = field(field(result, "source", dict, where), "path", str, where + ".source")
        for p, entry in enumerate(items(result, "packages", where)):
            at = "%s.packages[%d]" % (where, p)
            package = field(entry, "package", dict, at)
            for key in ("ecosystem", "name", "version"):
                field(package, key, str, at + ".package")
            found.append((source.replace("\\", "/"), package, items(entry, "vulnerabilities", at)))
    return found


def canary(path):
    """Refuse a run whose scanner cannot find the two advisories it must find."""
    report = load(path, "canary report")
    if report is None:
        return 2
    try:
        found = {(p["ecosystem"], p["name"], p["version"])
                 for _, p, vulnerabilities in packages(report) if vulnerabilities}
    except Malformed as exc:
        return refused(exc)
    missed = [c for c in CANARIES if c not in found]
    for ecosystem, name, version in CANARIES:
        print("canary: %s %s %s %s" % (ecosystem, name, version,
                                       "found vulnerable" if (ecosystem, name, version) in found
                                       else "NOT FOUND"))
    if missed:
        print("The scanner did not report a package that has had a published advisory for years.")
        print("  Until it does, no result of this run means anything - a clean scan and a scan")
        print("  that never reached the database, or never parsed the file, read the same.")
        return 1
    return 0


def tracked_projects():
    """Every .csproj git tracks, with forward slashes."""
    listed = subprocess.run(["git", "ls-files", "-z", "--", "*.csproj"],
                            capture_output=True, check=True).stdout.decode("utf-8")
    return sorted(name for name in listed.split("\0") if name)


def relative(path):
    """A path as the workflow passes it, without a leading "./" and with forward slashes.

    The prefix is taken off as a prefix, never as characters - lstrip("./") would also eat the
    dot of ".github" and look for a file that does not exist.
    """
    path = path.replace("\\", "/")
    return path[2:] if path.startswith("./") else path


def owners(pinned, pinned_from):
    """{file the scan reads: tracked file it stands for}, or a Malformed for an ambiguous tree."""
    mapping = {}
    for project in tracked_projects():
        folder = project.rsplit("/", 1)[0] if "/" in project else "."
        lock = "%s/packages.lock.json" % folder
        if lock in mapping:
            # Two projects in one folder would share one lock file, so a finding in it could not
            # be laid at either. Nothing in this repository is shaped like that today.
            raise Malformed("%s and %s share %s" % (mapping[lock], project, lock))
        mapping[lock] = project
    mapping[relative(pinned)] = relative(pinned_from) if pinned_from else relative(pinned)
    return mapping


def matching(path, mapping):
    """The key of `mapping` that `path` names, the longest one when several do, or None."""
    path = path.replace("\\", "/")
    hits = [key for key in mapping if path == key or path.endswith("/" + key)]
    return max(hits, key=len) if hits else None


def read_counts(found, expected):
    """How many packages the report read from each expected file, by its path in the checkout."""
    counts = {path: 0 for path in expected}
    for source, _, _ in found:
        key = matching(source, counts)
        if key is not None:
            counts[key] += 1
    return counts


def file_path(uri):
    """The path a SARIF location URI names, whether it is a file URI or already a plain path."""
    return unquote(urlparse(uri).path) if uri.startswith("file:") else uri


def sarif(path, pinned, pinned_from):
    """Move every location to the tracked file that caused it, in place."""
    if not os.path.isfile(pinned_from):
        print("osv gate: %s is not a file here, so nothing can point at it" % pinned_from,
              file=sys.stderr)
        return 2
    log = load(path, "SARIF file")
    if log is None:
        return 2
    try:
        mapping = owners(pinned, pinned_from)
        artifact_locations = []
        for r, run in enumerate(items(log, "runs", "sarif", required=True)):
            where = "runs[%d]" % r
            for a, artifact in enumerate(items(run, "artifacts", where)):
                artifact_locations.append(field(artifact, "location", dict, "%s.artifacts[%d]" % (where, a)))
            for n, result in enumerate(items(run, "results", where)):
                for l, location in enumerate(items(result, "locations", "%s.results[%d]" % (where, n))):
                    at = "%s.results[%d].locations[%d]" % (where, n, l)
                    physical = field(location, "physicalLocation", dict, at)
                    artifact_locations.append(field(physical, "artifactLocation", dict, at + ".physicalLocation"))
        moved, unmapped = {}, []
        for location in artifact_locations:
            uri = field(location, "uri", str, "artifactLocation")
            key = matching(file_path(uri), mapping)
            if key is None:
                unmapped.append(uri)
                continue
            location["uri"] = mapping[key]
            moved[mapping[key]] = moved.get(mapping[key], 0) + 1
    except Malformed as exc:
        return refused(exc)
    except (OSError, subprocess.CalledProcessError) as exc:
        print("osv gate: cannot ask git for the projects: %s" % exc, file=sys.stderr)
        return 2
    if unmapped:
        print("These locations are in no file this scan was meant to read, so the alert could")
        print("not be laid at anything in the repository. Nothing was rewritten:")
        for uri in sorted(set(unmapped)):
            print("  " + uri)
        return 2
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(log, handle, indent=2)
    for owner, count in sorted(moved.items()):
        print("sarif: %3d location(s) now point at %s" % (count, owner))
    print("sarif: %d location(s) moved to tracked files" % sum(moved.values()))
    return 0


def verdict(path, pinned):
    """Fail on any advisory, and on a scan that did not read every file it was meant to."""
    report = load(path, "scan report")
    if report is None:
        return 2
    try:
        found = packages(report)
        expected = list(owners(pinned, None))
    except Malformed as exc:
        return refused(exc)
    except (OSError, subprocess.CalledProcessError) as exc:
        print("osv gate: cannot ask git for the projects: %s" % exc, file=sys.stderr)
        return 2
    if len(expected) < 2:
        print("osv gate: git tracks no .csproj here, so this is not the repository it was"
              " written for", file=sys.stderr)
        return 2

    counts = read_counts(found, expected)
    unread = [p for p, n in counts.items() if n == 0]
    vulnerable = [(source, p, v) for source, p, v in found if v]

    for file, count in counts.items():
        print("read: %4d package(s)  %s" % (count, file))
    if unread:
        print("These files were meant to be scanned and the report holds no package from them.")
        print("  A restore that wrote no lock file, a path that changed, or a parser that read")
        print("  the file as empty all look like a clean scan from the exit code alone:")
        for file in unread:
            print("  " + file)
    if vulnerable:
        print("Packages with a published advisory - every one of them blocks:")
        for source, p, v in vulnerable:
            print("  %s %s %s  in %s" % (p["ecosystem"], p["name"], p["version"], source))
            for advisory in v:
                summary = " ".join(str(advisory.get("summary", "")).split())
                print("      %s  %s" % (advisory.get("id", "?"), summary[:160]))
        print("")
        print("Raise the package past the fixed version. If one is settled rather than wrong,")
        print("record it in an osv-scanner.toml with the reason and an expiry date, so every")
        print("reader of the scan sees the same decision - not just this gate.")
    total = sum(counts.values())
    print("osv: %d vulnerable package(s), %d of %d file(s) read, %d package(s) scanned"
          % (len(vulnerable), len(expected) - len(unread), len(expected), total))
    return 1 if (vulnerable or unread) else 0


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    commands = parser.add_subparsers(dest="command", required=True)

    making = commands.add_parser("pins", help="turn a pip --dry-run report into the list to scan")
    making.add_argument("report")
    making.add_argument("out")

    checking = commands.add_parser("canary", help="refuse a scanner that misses known advisories")
    checking.add_argument("report")

    pointing = commands.add_parser("sarif", help="point every SARIF location at a tracked file")
    pointing.add_argument("report")
    pointing.add_argument("--pins", required=True, help="the list the pins command wrote")
    pointing.add_argument("--pins-from", required=True, help="the requirements file it came from")

    judging = commands.add_parser("verdict", help="fail on any advisory or an incomplete scan")
    judging.add_argument("report")
    judging.add_argument("--pins", required=True, help="the list the pins command wrote")

    args = parser.parse_args(argv)
    if args.command == "pins":
        return pins(args.report, args.out)
    if args.command == "canary":
        return canary(args.report)
    if args.command == "sarif":
        return sarif(args.report, args.pins, args.pins_from)
    return verdict(args.report, args.pins)


if __name__ == "__main__":
    raise SystemExit(main())
