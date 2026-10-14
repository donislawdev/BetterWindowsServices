#!/usr/bin/env python3
"""Decide a run from OSV-Scanner JSON reports, and prepare what it scans.

    python .github/scripts/osv_gate.py pins pip-report.json semgrep.installed.txt
    python .github/scripts/osv_gate.py canary canary.json
    python .github/scripts/osv_gate.py verdict osv.json --pins semgrep.installed.txt

.github/workflows/osv-scanner.yml runs the three in that order. Each one exits 0
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
identifiers) in anyio 4.9.0, idna 3.9.0 and python-multipart 0.0.9. The Semgrep job of pull request 61 had
installed anyio 4.15.1, idna 3.20 and python-multipart 0.0.32, and those 66
packages scanned exactly as installed carried none. The resolver's guess is not
what runs, and a gate built on it would have blocked every pull request that day
over packages nothing installs. So the workflow asks pip what it WOULD install,
without installing it, and this script turns pip's answer into the list that is
scanned with resolution turned off.

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
import subprocess
import sys

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


def load(path, what):
    """A JSON object from a file, or None with the reason printed."""
    try:
        with open(path, encoding="utf-8") as handle:
            data = json.load(handle)
    except (OSError, ValueError) as exc:
        print("osv gate: cannot read %s %s: %s" % (what, path, exc), file=sys.stderr)
        return None
    if not isinstance(data, dict):
        # A valid JSON document that is not an object - a bare list, a string - would
        # otherwise reach .get() and raise, which reads as a crash in this script rather
        # than a bad input to it.
        print("osv gate: %s %s is not a JSON object" % (what, path), file=sys.stderr)
        return None
    return data


def pins(pip_report, out):
    """Turn `pip install --dry-run --report` into name==version lines."""
    report = load(pip_report, "pip report")
    if report is None:
        return 2
    lines = []
    for item in report.get("install") or []:
        metadata = (item or {}).get("metadata") or {}
        name, version = metadata.get("name"), metadata.get("version")
        if name and version:
            lines.append("%s==%s" % (name, version))
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
    """Every (source path, package, vulnerabilities) in a report, paths with forward slashes."""
    for result in report.get("results") or []:
        source = str(((result or {}).get("source") or {}).get("path", "")).replace("\\", "/")
        for entry in (result or {}).get("packages") or []:
            yield source, (entry or {}).get("package") or {}, (entry or {}).get("vulnerabilities") or []


def canary(path):
    """Refuse a run whose scanner cannot find the two advisories it must find."""
    report = load(path, "canary report")
    if report is None:
        return 2
    found = {(p.get("ecosystem"), p.get("name"), p.get("version"))
             for _, p, vulnerabilities in packages(report) if vulnerabilities}
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
    """The directories of every .csproj git tracks, with forward slashes."""
    listed = subprocess.run(["git", "ls-files", "-z", "--", "*.csproj"],
                            capture_output=True, check=True).stdout.decode("utf-8")
    return sorted({name.rsplit("/", 1)[0] if "/" in name else "."
                   for name in listed.split("\0") if name})


def read_counts(report, expected):
    """How many packages the report read from each expected file, by its path in the checkout."""
    counts = {path: 0 for path in expected}
    for source, _, _ in packages(report):
        for path in expected:
            if source == path or source.endswith("/" + path):
                counts[path] += 1
    return counts


def verdict(path, pinned):
    """Fail on any advisory, and on a scan that did not read every file it was meant to."""
    report = load(path, "scan report")
    if report is None:
        return 2
    try:
        projects = tracked_projects()
    except (OSError, subprocess.CalledProcessError) as exc:
        print("osv gate: cannot ask git for the projects: %s" % exc, file=sys.stderr)
        return 2
    if not projects:
        print("osv gate: git tracks no .csproj here, so this is not the repository it was"
              " written for", file=sys.stderr)
        return 2

    # A "./" in front is taken off as a prefix, never as characters - lstrip("./") would also eat
    # the dot of ".github" and look for a file that does not exist.
    pinned = pinned.replace("\\", "/")
    pinned = pinned[2:] if pinned.startswith("./") else pinned
    expected = ["%s/packages.lock.json" % p for p in projects] + [pinned]
    counts = read_counts(report, expected)
    unread = [p for p, n in counts.items() if n == 0]
    vulnerable = [(source, p, v) for source, p, v in packages(report) if v]

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
            print("  %s %s %s  in %s" % (p.get("ecosystem", "?"), p.get("name", "?"),
                                         p.get("version", "?"), source))
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

    judging = commands.add_parser("verdict", help="fail on any advisory or an incomplete scan")
    judging.add_argument("report")
    judging.add_argument("--pins", required=True, help="the list the pins command wrote")

    args = parser.parse_args(argv)
    if args.command == "pins":
        return pins(args.report, args.out)
    if args.command == "canary":
        return canary(args.report)
    return verdict(args.report, args.pins)


if __name__ == "__main__":
    raise SystemExit(main())
