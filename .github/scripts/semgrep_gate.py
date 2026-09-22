#!/usr/bin/env python3
"""Decide a run from a Semgrep JSON report.

    semgrep scan --config p/default --metrics=off --oss-only --json --output semgrep.json
    python .github/scripts/semgrep_gate.py semgrep.json

Why this exists rather than `semgrep --severity ERROR --error`
--------------------------------------------------------------
Because that flag does not mean what it reads like. `--severity` accepts INFO,
WARNING and ERROR only, while a rule may declare the newer scale - and rules in
the registry do. Measured elsewhere with a probe rule carrying `severity: HIGH`:
unfiltered it produced 19 findings, and with `--severity ERROR` it produced
ZERO. A gate built on that flag silently ignores exactly the severities it was
asked to block.

Why the exit code of the scanner is not the answer either
----------------------------------------------------------
Two separate measurements, both on this owner's other repositories, both with
semgrep 1.175.0, and each one produces a green signal out of a scan that did not
happen:

  2026-08-27. The scan failed its own rule validation, printed "RPC subprocess
  failed" four times, EXITED 0, and wrote a 23 byte file containing
  "<ERROR: missing output>" instead of JSON.

  2026-09-08. Pointed at a config that does not exist, `semgrep scan` EXITED 0
  and wrote a perfectly valid JSON report with zero results, zero errors and an
  empty list of scanned paths.

A gate reading the exit code calls both of those a clean tree. This one reads
the report, and refuses both: the first will not parse, and the second scanned
nothing. That is the whole design in two measurements - a scanner that fails can
succeed, so the report is the answer and the exit code is not.

What blocks
-----------
Any finding whose severity is ERROR, HIGH or CRITICAL. Everything else is
printed and passes, because a WARNING here is usually a rule seeing a shape it
cannot resolve - a path built from a validated argument, a dynamic call from a
fixed table - and a gate that fires on those is a gate nobody reads.

A scan error blocks too. A rule that failed to run is not a rule that found
nothing, and "the scan was green" must never mean "the scan did not happen".
Only entries at level "error" count: a clean run of this repository carries four
entries at level "warn", and none of them is a reason to stop anybody.

A scan that read no files blocks as well, and the number read is printed on
every run rather than only when it is zero. Zero is the loud case. A collapse
from three hundred to a handful is the same failure arriving quietly, and only a
number on every line makes that visible.

What a healthy run of THIS repository looks like
-------------------------------------------------
Measured 2026-09-22 against commit 693bced, exported to a clean directory so
that exactly the 516 files a checkout carries were present, semgrep 1.177.0 with
`--config p/default`:

    131 rules, 301 files, 0 findings, 0 errors at level error, 4 at level warn

301 rather than 516 because semgrep's own default ignore patterns exclude
tests/, which is 212 of this repository's files. That is not a fault and not a
setting of ours - it is the tool's default, and it is worth knowing before
somebody reads "301" as a broken scan. The files that ARE read are src/, site/,
.github/ and the root.

Findings decided once and recorded
-----------------------------------
Nothing is suppressed in this file. A finding this project has looked at and
settled carries a `nosemgrep` comment at the line, with the reason beside it.
That is deliberate: a list here would quieten this gate alone, while the same
report can also be read on semgrep.dev, and a decision only half the readers can
see is not recorded, it is hidden.
"""
import argparse
import json
import sys

BLOCKING = ("ERROR", "HIGH", "CRITICAL")


def split(report):
    """(blocking, passing, scan_errors) out of a semgrep JSON report."""
    blocking, passing = [], []
    for result in report.get("results") or []:
        severity = str((result.get("extra") or {}).get("severity", "")).upper()
        (blocking if severity in BLOCKING else passing).append(result)
    errors = [e for e in report.get("errors") or []
              if str(e.get("level", "")).lower() == "error"]
    return blocking, passing, errors


def scanned_count(report):
    """How many files the scan actually read."""
    return len((report.get("paths") or {}).get("scanned") or [])


def describe(result):
    extra = result.get("extra") or {}
    start = result.get("start") or {}
    message = " ".join(str(extra.get("message", "")).split())
    return "%s:%s  [%s] %s\n      %s" % (
        result.get("path", "?"), start.get("line", "?"),
        extra.get("severity", "?"), result.get("check_id", "?"), message[:300])


def report_lines(blocking, passing, errors, scanned):
    lines = []
    if scanned == 0:
        lines.append("no files were scanned, so this report says nothing about the code.")
        lines.append("  A wrong or unreachable config produces exactly this: exit zero, a JSON")
        lines.append("  file, and nothing in it. Check the --config argument and the network.")
    if errors:
        lines.append("scan errors (a rule that could not run is not a rule that passed):")
        lines += ["  %s: %s" % (e.get("type", "?"),
                                " ".join(str(e.get("message", "")).split())[:200])
                  for e in errors]
    if blocking:
        lines.append("blocking findings (%s):" % ", ".join(BLOCKING))
        lines += ["  " + describe(r) for r in blocking]
        lines.append("")
        lines.append("If one of these is settled rather than wrong, put a nosemgrep comment on")
        lines.append("the line with the reason beside it, so every reader of this report sees")
        lines.append("the same decision - not just this gate.")
    if passing:
        lines.append("other findings (reported, not blocking):")
        lines += ["  " + describe(r) for r in passing]
    lines.append("semgrep: %d blocking, %d other, %d scan error(s), %d file(s) scanned"
                 % (len(blocking), len(passing), len(errors), scanned))
    return lines


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("report", help="the JSON file semgrep --output wrote")
    args = parser.parse_args(argv)
    try:
        with open(args.report, encoding="utf-8") as handle:
            report = json.load(handle)
    except (OSError, ValueError) as exc:
        # A missing or unparsable report is a failed gate, never a pass. It means
        # the scan step did not produce what this one was promised, and that is
        # precisely the shape the 2026-08-27 measurement above found.
        print("semgrep gate: cannot read %s: %s" % (args.report, exc), file=sys.stderr)
        return 2
    if not isinstance(report, dict):
        # A valid JSON document that is not a report object - a bare list, a
        # string - would otherwise reach .get() and raise, which looks like a
        # crash in this script rather than a bad input to it.
        print("semgrep gate: expected a report object, got %s"
              % type(report).__name__, file=sys.stderr)
        return 2
    blocking, passing, errors = split(report)
    scanned = scanned_count(report)
    for line in report_lines(blocking, passing, errors, scanned):
        print(line)
    return 1 if (blocking or errors or scanned == 0) else 0


if __name__ == "__main__":
    raise SystemExit(main())
