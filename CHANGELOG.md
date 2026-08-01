# Changelog

All notable changes to Better Windows Services are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file is for people who use the tool. Changes that only matter to somebody working on
it - internal structure, measurements, test guards - are in
[CHANGELOG-DEV.md](CHANGELOG-DEV.md).

## [Unreleased]

Nothing has been released yet. Everything below is what the tool does today.

### Added

- **`bws list`** shows every entry the service control manager knows about, as a table for
  a person or with `--json` for a script. Drivers are included, which is why the count is
  larger than the one `services.msc` shows.
- **`--query`** narrows the listing with a language shared by the command line and, later,
  the window. Fields: `name`, `display`, `type`, `status`, `start`, `account`, `pid`,
  `trigger`, `path` and `file`. Values combine with spaces for "all of these" and commas
  for "any of these", `!` excludes, and `none`, `any` and `?` ask whether a field is empty,
  filled or could not be read. Text fields take wildcards and regular expressions. A bare
  word searches names, display names, accounts and launch paths.
- **`bws stop`, `bws start` and `bws restart`** carry out one named entry's change through
  a plan: what will happen, in what order, and what came of every step.
- **`--dry-run`** on every write command shows the plan and changes nothing.
- **`--dependents`** on `stop` and `restart` takes down the entries that would break as
  well. Without it the plan has one step and names who is standing in the way.
- **`--timeout`** caps how long any one step is watched. It is a ceiling, not a deadline:
  an entry that keeps reporting progress is given the time it asks for.
- **Trigger start is shown and searchable.** An entry that is set to start automatically,
  is not running, and waits for a trigger is doing what it was told to do rather than
  failing - the listing says `on trigger` and `--query trigger:any` finds them.
- **`--signatures` shows who signed each binary and whether Windows trusts it.** Off by
  default, because verifying a whole machine takes several seconds where the rest of a
  listing takes a third of one. A query about signatures switches it on by itself, so
  `--query "signed:no"` needs no flag. `signed:no` covers everything Windows would not run
  quietly - unsigned, expired, revoked, untrusted or tampered with - and each of those can
  be asked for by name. `publisher` finds who signed it, so `!publisher:microsoft` answers
  "what on this machine did not come from Microsoft".
- **The launch path is read, and a missing file is reported.** `--query file:missing` finds
  entries whose executable is not on disk, and `file:missing start:auto` is the orphan a
  cleanup would care about. The listing marks these `file missing` next to the start type.
- **`--timing`** reports how long reading took, on the error channel.
- **`--json`** output keeps data and diagnostics apart: only data goes to standard output,
  everything else to standard error, including when a run fails.
- **Exit codes** for scripts and monitoring: `0` done, `1` the tool failed, `2` the command
  was wrong, `3` the plan ran and something did not get where it was going, `4` somebody
  stopped the run by hand.
- **Ctrl+C has three levels**, each saying what the next one costs. The first stops going
  forward and still puts back what was taken. The second leaves things as they are and
  still prints the report. The third ends the process.
- **A refusal carries the system's number as well as its sentence.** The sentence is in the
  language of the machine, so the number is the half a script should read.

### Known limits

- Local machine only. Remote management is not in this version.
- Windows only, 64-bit only, Windows Server 2019 or Windows 10 1809 and above.
- Operations on drivers are refused rather than attempted.
- The tool does not raise its own privileges. When the manager refuses, it says so and
  ends with exit code 3.
- For a service hosted inside `svchost`, the launch path names the host, so "the file is
  there" and "who signed it" both say less than they do for a service with a process of
  its own.
- Certificate revocation is not checked. It would need to reach the network, and this tool
  never does.
- Where a file carries both its own signature and an entry in a Windows catalogue, and the
  two name different signers, the publisher shown is the one inside the file. That can
  differ from what PowerShell reports, and it is the one that stays the same when the file
  is looked at on another machine.
