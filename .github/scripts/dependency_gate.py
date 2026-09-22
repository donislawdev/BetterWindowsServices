#!/usr/bin/env python3
"""Decide a pull request from GitHub's dependency review data.

    gh api "repos/$REPO/dependency-graph/compare/$BASE...$HEAD" > deps.json
    python .github/scripts/dependency_gate.py deps.json

Why this exists next to actions/dependency-review-action
--------------------------------------------------------
The action already fails a pull request that ADDS a dependency with a known
vulnerability, and it does that well. It cannot do the other half. Its own
documentation is explicit: if it cannot detect the licence for a dependency it
will inform you, but it will not fail.

This project is GPL-3.0 and it ships two executables, so an unidentified licence
is not an informational note. It is the one answer nobody can act on, because
whether the thing may be distributed at all is exactly what it fails to say. The
same data is read here and an unknown licence blocks, like a denied one.

Where the list below comes from
--------------------------------
Not from judgement exercised here. docs/02, under the GPL-3.0 decision of
2026-08-01, records the criterion: every new dependency has to be compatible
with GPL-3.0, MIT, BSD, Apache 2.0 and LGPL qualify, and GPL-2.0-only together
with non-commercial or research-only licences do not. GPL-2.0-only is therefore
ABSENT on purpose. It is famously incompatible with GPL-3.0 and it is exactly
the kind of entry that looks fine in a list of open source licences and is not.

Why this lives in .github/scripts and not in tools
---------------------------------------------------
tools/ is outside this repository by an owner decision of 2026-08-01, on the
grounds that it is never shipped. This script is the opposite: CI checks the
repository out and runs it, so it has to travel with the repository. That also
puts it under the sweeps in tests/Bws.Architecture.Tests/PublicSurfaceGuards.cs,
which is correct for a file the world can read.

Scope
-----
Only dependencies a pull request ADDS. Removing something never creates an
obligation, and re-checking what is already in the tree would make every pull
request answer for decisions taken years ago.
"""
import argparse
import json
import sys

# SPDX identifiers this project may distribute alongside its own code.
#
# The first three are what this repository actually has. Measured 2026-09-22 by
# reading the dependency graph rather than the package listings: seven NuGet
# packages, carrying Apache-2.0, MIT and one compound "Apache-2.0 AND MIT".
# The rest of the list is the criterion from docs/02 written out, so that a
# dependency arriving under BSD or LGPL is a normal event rather than a red
# build somebody has to come back to.
ALLOWED = frozenset({
    "0BSD", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "CC0-1.0", "ISC",
    "MIT", "MIT-0", "MPL-2.0", "Unlicense", "Zlib",
    "LGPL-2.1-only", "LGPL-2.1-or-later", "LGPL-3.0-only", "LGPL-3.0-or-later",
    "GPL-3.0-only", "GPL-3.0-or-later",

    # The last two are for an ecosystem this repository grew on the day the list was written
    # and which it did not have when the list was first copied: .github/dependabot.yml now
    # watches a pip requirements file, so a Python package can reach a pull request here.
    # Without these two, a dependency under either would be reported as DENIED rather than
    # unknown - a false alarm, which is the one failure that teaches people to bypass a gate.
    # Both are compatible with GPL-3.0 and both are on the equivalent list in this owner's
    # Python project.
    "PSF-2.0", "Python-2.0",
})

# Packages whose licence GitHub cannot resolve and which a person has already
# looked at. A name here is a decision with a reason written beside it, not a
# way to make a red build green - and it names one package, never a pattern and
# never a whole ecosystem.
#
# Empty today, and that is the honest state rather than an oversight: every
# dependency in this tree reports a licence.
EXCEPTIONS = {}

# GitHub reports no licence for an action. Measured here on 2026-09-22 on this
# repository's own graph: five of five actions came back without one, while
# seven of seven NuGet packages carried a real identifier.
#
# They are skipped, and the reason is that an action is CI machinery that never
# reaches a user, so it creates no distribution obligation - which is the thing
# an unknown licence is dangerous for. Keeping them would block every pull
# request that touches a workflow, for ever, and a gate that always fires is a
# gate people learn to bypass.
#
# WHAT DOES NOT HOLD HERE, AND IT HOLDS IN THE OTHER REPOSITORY THIS SCRIPT CAME
# FROM. There, the skip is paid for by a guard refusing any action pinned to a
# tag rather than a commit. This repository pins every action to a full commit
# SHA today, by hand, and NOTHING CHECKS THAT. So the skip below is a little
# more generous than it reads. Backlog row 404.
SKIPPED_ECOSYSTEMS = frozenset({"actions"})


def added(review):
    return [d for d in review
            if str(d.get("change_type", "")) == "added"
            and str(d.get("ecosystem", "")).lower() not in SKIPPED_ECOSYSTEMS]


def parts_of(expression):
    """The individual identifiers in an SPDX expression.

    Both AND and OR are split the same way and every part has to be allowed.
    For AND that is simply correct. For OR it is stricter than the licence
    requires, because an OR lets the user pick the half they like - and that is
    deliberate: being wrong this way can only block a dependency somebody then
    records a decision about, while the generous reading can let one through
    unnoticed. A blocked dependency asks a question. An admitted one does not.

    The compound this repository already has is an AND: Microsoft.Windows.CsWin32
    reports "Apache-2.0 AND MIT", and both halves are on the list.
    """
    flat = str(expression).replace(" AND ", " OR ")
    return [p.strip("() ") for p in flat.split(" OR ") if p.strip("() ")]


def verdict(dependency):
    """("ok" | "unknown" | "denied", licence) for one added dependency."""
    licence = dependency.get("license")
    name = str(dependency.get("name", "?"))
    if name in EXCEPTIONS:
        return "ok", licence
    if licence is None or not str(licence).strip():
        return "unknown", licence
    parts = parts_of(licence)
    # An expression that is not blank and yields no identifiers is the vacuous
    # truth this gate exists to refuse. `all()` over an empty list is True, so
    # without this line a licence of "()" - not null, not empty, and carrying no
    # identifier at all - comes back "ok" while nothing was ever checked.
    #
    # MEASURED RATHER THAN REASONED ABOUT, 2026-09-22, after a review flagged it:
    # verdict({"license": "()"}) returned "ok", and so did "( )". The REST schema
    # promises "string or null" and promises nothing about SPDX syntax, so a
    # malformed value is the API behaving as documented rather than a bug
    # upstream. It is the same shape as a scan that read no files, and it is
    # refused for the same reason.
    if parts and all(part in ALLOWED for part in parts):
        return "ok", licence
    return "denied", licence


def split(review):
    blocked, passed = [], []
    for dependency in added(review):
        state, licence = verdict(dependency)
        row = (state, str(dependency.get("name", "?")),
               str(dependency.get("version", "?")), licence,
               str(dependency.get("scope", "?")))
        (passed if state == "ok" else blocked).append(row)
    return blocked, passed


def report_lines(blocked, passed):
    lines = []
    if blocked:
        lines.append("blocked - a licence that is denied or could not be determined:")
        for state, name, version, licence, scope in blocked:
            lines.append("  %-8s %s %s  licence=%s  scope=%s"
                         % (state, name, version, licence, scope))
        lines.append("")
        lines.append("An unknown licence blocks on purpose. This project is GPL-3.0 and ships")
        lines.append("two executables, so 'we could not tell' is the one answer nobody can act")
        lines.append("on. Read the LICENSE file inside the pinned package - not the label on a")
        lines.append("package listing - then record the decision in this file, in ALLOWED or in")
        lines.append("EXCEPTIONS, and add the notice to THIRD-PARTY-NOTICES.md if it ships.")
    if passed:
        lines.append("allowed (%d): %s" % (
            len(passed), ", ".join("%s %s (%s)" % (n, v, lic) for _s, n, v, lic, _sc in passed)))
    lines.append("dependency gate: %d blocked, %d allowed" % (len(blocked), len(passed)))
    return lines


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("review", help="JSON from the dependency review API")
    args = parser.parse_args(argv)
    try:
        with open(args.review, encoding="utf-8") as handle:
            review = json.load(handle)
    except (OSError, ValueError) as exc:
        # A gate that cannot read its input has not passed anything. The endpoint
        # answers 403 for some repository shapes, and that has to look like a
        # failure rather than an empty list of problems.
        print("dependency gate: cannot read %s: %s" % (args.review, exc), file=sys.stderr)
        return 2
    if not isinstance(review, list):
        # The endpoint answers with a list. An object here is an error document
        # that arrived with a 200, and reading .get() off its rows would raise.
        print("dependency gate: expected a list of dependencies, got %s"
              % type(review).__name__, file=sys.stderr)
        return 2
    blocked, passed = split(review)
    for line in report_lines(blocked, passed):
        print(line)
    return 1 if blocked else 0


if __name__ == "__main__":
    raise SystemExit(main())
