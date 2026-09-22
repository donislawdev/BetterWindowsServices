#!/usr/bin/env python3
"""Decide a run from a Semgrep JSON report.

    semgrep scan --config p/default --metrics=off --oss-only --time --json --output semgrep.json
    python .github/scripts/semgrep_gate.py semgrep.json

`--time` is not decoration: without it the report's rule list is empty and the
rule floor below cannot be applied. The gate says so on its last line rather
than assuming.

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

A scan that read too few files blocks as well, and so does one carrying too few
rules. Both numbers are printed on every run rather than only when they fire.

THAT SENTENCE USED TO PROMISE MORE THAN THE CODE DID, and it is worth leaving
the correction visible. Until 2026-09-22 it said a collapse from three hundred
files to a handful is the same failure arriving quietly - and then the code
refused only ZERO. A review pointed at the gap. A pull request could add a
`.semgrepignore`, exclude the source tree, leave one harmless file and collect a
green verdict, which is precisely the quiet failure the paragraph described.
Prose is guarded by nothing, including prose about guards. The floors are in
BLOCKING and MINIMUM_FILES below, with the measurements that set them.

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

# FLOORS AGAINST COLLAPSE, NOT EQUALITY CHECKS, and the difference is the whole design.
#
# Zero was the only number refused here until 2026-09-22, when a review pointed out that the
# prose above promised more than the code did: it said a collapse from three hundred files to a
# handful is the same failure arriving quietly, and then nothing looked at the number. A pull
# request can add a `.semgrepignore`, exclude src/ and site/, leave one harmless file, and
# collect a green verdict from a scan that checked nothing.
#
# WHY A LOW FLOOR RATHER THAN THE MEASURED VALUE. Both numbers move for legitimate reasons, and
# a gate that goes red for a legitimate reason is a gate people learn to bypass. Measured on
# this repository:
#
#   files   301 (before the Python scripts existed), 306, 309 - it tracks the tree
#   rules   131, 374, 419 - it tracks WHICH LANGUAGES ARE IN THE TREE, not the ruleset alone.
#           Two .py files took it from 131 to 374 by pulling in Python's rules.
#
# So these refuse a collapse and say nothing about a drift of tens. Raising them is a deliberate
# act that means measuring again, exactly like the pinned scanner version.
MINIMUM_FILES = 200

# THIS ONE IS A PARTIAL ANSWER TO SOMETHING THAT CANNOT BE FIXED HERE, and it is worth being
# honest about which part. `--config p/default` resolves a ruleset from Semgrep's registry at
# run time, so its contents can change without a commit in this repository - a real gap in a
# merge gate. It cannot be closed by vendoring the rules: the Semgrep Rules License, read on
# 2026-09-22, says "This license does not allow you to distribute the rules, or to make them
# available to others as a service", and this repository is public.
#
# What a floor DOES catch is the ruleset collapsing - the registry answering with a fraction of
# what it used to, which would otherwise look like a clean scan. What it does NOT catch is the
# ruleset being REPLACED by a different set of the same size. Nothing available here catches
# that, and pretending otherwise would be worse than the gap.
#
# The count needs `--time` on the scan, because the report carries an empty rule list without it.
MINIMUM_RULES = 50


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


def rule_count(report):
    """How many rules the scan carried, or None when the scan was run without --time.

    None rather than zero, because the two mean opposite things. Without --time
    the report's rule list is empty for everybody, so zero there would fail every
    run. With --time, an empty list is a ruleset that resolved to nothing.
    """
    time = report.get("time")
    if not isinstance(time, dict) or "rules" not in time:
        return None
    return len(time.get("rules") or [])


def describe(result):
    extra = result.get("extra") or {}
    start = result.get("start") or {}
    message = " ".join(str(extra.get("message", "")).split())
    return "%s:%s  [%s] %s\n      %s" % (
        result.get("path", "?"), start.get("line", "?"),
        extra.get("severity", "?"), result.get("check_id", "?"), message[:300])


def report_lines(blocking, passing, errors, scanned, rules):
    lines = []
    if scanned == 0:
        lines.append("no files were scanned, so this report says nothing about the code.")
        lines.append("  A wrong or unreachable config produces exactly this: exit zero, a JSON")
        lines.append("  file, and nothing in it. Check the --config argument and the network.")
    elif scanned < MINIMUM_FILES:
        lines.append("only %d file(s) were scanned, and this tree has hundreds." % scanned)
        lines.append("  A .semgrepignore REPLACES semgrep's own default patterns rather than")
        lines.append("  adding to them, so one file can take most of the tree out of the scan")
        lines.append("  and leave every other signal looking healthy. Either the exclusions")
        lines.append("  changed or the scan ran somewhere else.")
    if rules is not None and rules < MINIMUM_RULES:
        lines.append("only %d rule(s) were carried, against a floor of %d."
                     % (rules, MINIMUM_RULES))
        lines.append("  p/default is fetched from the registry at run time, so this can change")
        lines.append("  without a commit here. A count this low is the ruleset having collapsed,")
        lines.append("  not the code having improved.")
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
    lines.append("semgrep: %d blocking, %d other, %d scan error(s), %d file(s) scanned, %s"
                 % (len(blocking), len(passing), len(errors), scanned,
                    "%d rule(s)" % rules if rules is not None
                    else "rule count unknown (scan ran without --time)"))
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
    rules = rule_count(report)
    for line in report_lines(blocking, passing, errors, scanned, rules):
        print(line)
    thin = scanned < MINIMUM_FILES or (rules is not None and rules < MINIMUM_RULES)
    return 1 if (blocking or errors or thin) else 0


if __name__ == "__main__":
    raise SystemExit(main())
