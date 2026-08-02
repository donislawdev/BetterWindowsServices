# Changelog

All notable changes to Better Windows Services are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file is for people who use the tool. Changes that only matter to somebody working on
it - internal structure, measurements, test guards - are kept in a developer changelog that
is not part of this repository.

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

- **Privileges, service identity and permissions are read and searchable.** `--json` now
  carries `requiredPrivileges`, `sidType` and `securityDescriptor` for every entry, and the
  query language gained `privilege`, `sidtype` and `sddl`. `--query "privilege:debug"` finds
  everything that asked to keep the right to attach to any process, and
  `--query "sidtype:restricted"` finds the services running under the tighter kind of
  identity. Read on every listing, because all three together cost about a sixth of a
  second on a machine with 810 entries.
  - **A service that declares no privileges is not a restricted one.** Declaring them is
    how a service asks for the rest to be taken away, so declaring none keeps everything
    the account has.
  - The permissions come as the text form Windows itself reads and writes, with owner,
    group and the permission list. It differs from `sc sdshow` in two ways on purpose: it
    has the owner and group, which `sc` does not print, and not the audit list, which needs
    a privilege that even an administrator session does not have switched on.
  - Not shown in the table. The three would add width to every row without changing what
    any existing column means, and the listing is already wide.

- **`--memory` shows how much memory each running entry's process is using.** A `MEMORY`
  column, and `requiredPrivileges`-style detail in `--json` with both the working set and
  the commit figure. `--query "memory:>100MB"` finds the heavy ones and switches the
  reading on by itself.
  - **A number belonging to several services says so.** Services sharing one process all
    report that process's memory, which is correct and adds up to several times the truth -
    so the cell reads `36.1 MB (shared by 5)` and the machine readable output carries the
    count alongside the figures.
  - Sizes in a query need their unit: `memory:>500MB`, `memory:<1GB`, `memory:100MB-1GB`.
    Writing `memory:>500` is refused with a message rather than read as 500 bytes, which
    would match every running service while looking like it had filtered. Units are powers
    of 1024, the same as Task Manager and `Get-Process`.
  - Off by default, and not to save time - it takes a few milliseconds. It is the one thing
    the tool reports that is a measurement rather than a setting, so a plain listing stays a
    description of how the machine is configured.

- **`bws snapshot create` freezes the whole machine into a file you can keep.**

  ```
  bws snapshot create before-the-deployment.json --note "before the deployment"
  ```

  JSON, one field per line, keys in a fixed order, so two snapshots of an unchanged machine
  differ in exactly one line - the time. Put them in a repository and `git diff` tells you
  what a patch Tuesday did. Without a file name it writes one into the current directory,
  named after the machine and the moment.
  - **Always complete.** Signatures and a SHA-256 of every binary are read every time. A
    listing skips them for speed and a snapshot cannot: one without them, compared against
    one with them, would report the whole machine as changed.
  - **The file says how it was taken** - machine, operating system, time, who ran it, and
    whether they had administrator rights. That last one matters: without elevation Windows
    hands over fewer entries, so comparing such a snapshot against an elevated one would
    show services as removed that nobody removed. The tool says so when it writes one.
  - **Written safely.** The file appears complete or not at all, never half-written, and a
    failed write leaves whatever was there untouched. Pointing it at a directory that does
    not exist is refused rather than created.
  - Memory is deliberately not in it. It is different a second later and would be noise in a
    document whose whole purpose is being compared with another one.

- **Three more things about every entry**, in `--json` as well as in snapshots:
  `errorControl` (how hard Windows takes it when the entry fails during boot),
  `loadOrderGroup`, and `binaryHash`.

- **Non-English names print as themselves in `--json`.** Display names on a Polish or German
  install used to come out as escape sequences - correct JSON that nobody could check
  against `services.msc`.

- **`bws snapshot diff EARLIER LATER`** says what changed between two snapshots, field by
  field. It answers two of the three comparisons this tool is for: what a patch Tuesday did,
  and why staging and production behave differently.
  - **Configuration and running state are reported apart.** Two snapshots taken a day apart
    differ in what was running on dozens of entries, and none of that is drift. Printed
    together they bury the handful of settings that actually moved.
  - **What could not be compared is said, never skipped.** An entry only one of the two
    snapshots could see is listed as "cannot tell" rather than as removed - without
    administrator rights Windows hands over fewer entries, so a missing one may never have
    been removed. A field one snapshot could not read is named beside its entry instead of
    being reported as a change from a value to nothing.
  - **`--exit-code`** ends with code 5 when anything differs, for a pipeline step that has to
    fail on drift. Off by default, so a script that only wants the differences printed is not
    tripped by finding some. Entries nobody could see and fields nobody could read do not
    count as differences.
  - **`--json`** gives the same comparison for a script, with a `differs` flag that answers
    the same question as the exit code.
  - **`--live`** compares the file against this machine as it is now, which answers "what
    changed since that snapshot". It reads signatures and hashes like `snapshot create`
    does, because the file on the other side has them.
  - Without `--live` it reads two files and nothing else. Comparing two snapshots does not
    touch the service control manager, so it needs no rights over the machine running it.
  - **A note for anything running this on a schedule:** on a live machine the running state
    moves on its own. Entries start themselves for their own reasons within minutes, which
    is why what is running and how things are set up are reported apart.

- **There is a window.** It opens, reads the machine and shows every entry the service
  control manager knows about, with its name, display name, status, start type, account and
  process id. It is dark, and dark is the only way it comes.
  - **A value that could not be read does not look like a value that is not there.** An empty
    cell means the service genuinely has none. Anything else says so in words, because those
    two facts say opposite things about a service and a blank cell cannot tell you which.
  - Read only. Every change to a service goes through a plan, so there is nothing here to
    type into.
  - The list does not refresh itself yet, and cannot be sorted. Those are the next steps.

- **One box above the list, for searching and for the query language.** Type a word and the
  list narrows as you type. Type `start:auto !status:running` and you get the same entries
  as `bws list --query "start:auto !status:running"` - it is the same language, so anything
  you work out in one can be pasted into the other.
  - **A half-typed query is not a mistake.** Nothing turns red while you are still writing
    `status:`. A real mistake is reported under the list, in words, naming what would have
    worked - and the list you were looking at stays where it is rather than emptying.
  - **When the answer is not the whole answer, it says so** under the count. That covers
    entries judged on something the machine would not let us read, expressions that ran out
    of time, and questions about things the window has not read.
  - **Two switches beside the box.** `Regex` reads a word on its own as a regular
    expression. `Drivers` puts kernel drivers in or out - and turning it off writes
    `!type:driver` into the box where you can see it, edit it, or copy it into a terminal.
    Type that yourself and the switch moves on its own.
  - **Signatures and memory cannot be asked about here yet.** `signed:no` and
    `memory:>100MB` are real questions and the window says plainly that nobody read those
    for a listing, rather than answering with an empty list that reads like "there are
    none". The command line answers them today with `--signatures` and `--memory`.

- **The list keeps itself up to date.** Stop a service from anywhere - `services.msc`, a
  terminal, an installer - and the window notices within about a second. Nobody has to press
  anything.
  - **A row that changed is lit for a few seconds**, so you can see what happened even if you
    were looking at another part of the screen.
  - **It does not move under your hand.** While the mouse is over the list or the keyboard is
    in it, nothing joins or leaves - a row you are reaching for stays where it was. Cells
    still update, so you can watch a service stop while your cursor is on it. The line under
    the list says when it is holding still like this, and it settles as soon as you move away.
  - **Your selection and your place in the list survive** every refresh.
  - **F5 still does what you expect**, and does more than the automatic refresh: it re-reads
    the settings too, so a start type you changed elsewhere shows up at once.
  - It costs about a fiftieth of a second per check on a machine with 810 entries, and it
    stops entirely while the window is minimised.

- **`--follow-network`, and by default the tool no longer reaches off your machine.** A
  service can be set up to run a program from a network share. Until now, checking whether
  that program was there meant contacting the other machine - and if it was not answering,
  the listing sat there for **twenty one seconds** for that one service. Worse, the
  connection signs in as whoever is running the tool, so pointing a service at a share is
  enough to make an audit hand your credentials to it.
  - Now such a service is reported with its path as usual, and the question of whether the
    file is there comes back as **not checked**. Never as missing: this tool does not say a
    file is gone when it never looked.
  - Add `--follow-network` to `bws list` or `bws snapshot create` when you genuinely run
    services from a share and want them checked like any other.
  - Only paths of the `\\server\share` form count. A drive letter mapped to a share cannot
    be told apart from a local disk without contacting it, so those are still followed.

### Changed

- **The delayed start setting is now recorded for every service, not only automatic ones.**
  Windows lets you mark any service as delayed, and it only does anything on an automatic
  one - but it is stored either way, and a snapshot that could not see it would miss the
  moment somebody set it. Eight services on the machine this was measured on carry the
  setting while not being automatic, `WinRM` and `MSDTC` among them.
  - The listing still marks `(delayed)` only where it changes what the start type means.
    Writing it next to `Manual` would claim something the setting does not do there.
  - `--json` and snapshots now carry `delayedAuto` for every service rather than `null` for
    most of them. `sc qc` does not show it outside automatic services, and neither does the
    table - the difference is in what gets recorded, not in what gets claimed.

- **Taking a snapshot is about four times faster.** `bws snapshot create` measured
  5.5-9.1 seconds and now measures 1.7-2.1 seconds on a machine with 810 entries and 544
  distinct binaries. It reads exactly the same things and reports exactly the same answers -
  a snapshot taken by the old version and one taken by this one differ only in their
  timestamp. `bws list --signatures` and any query about signatures got the same speed-up,
  since they do the same work.
  - The gain comes from asking about several files at once rather than one after another.
    How many at once follows the number of processors, so a small machine asks for less.
  - Times will differ on your machine. The number of services, the speed of the disk and how
    many binaries are signed through a Windows catalogue all move it.

### Fixed

- **The search field is dark, like the rest of the window.** It came up white, with grey
  hint text, on a dark window - and so did the two tick boxes beside it, in a less obvious
  way. All three now take the dark theme the rest of the window uses.
- **The usage text lists every switch again.** `--signatures` was missing from it, and
  `--timing` was shown only for `list` although it works everywhere.
- **A switch given without its value says so**, instead of reporting itself as an unknown
  option and sending you looking for a typo you did not make.
- **One unreadable file no longer ends the whole run.** A binary whose certificate cannot
  be parsed now costs its own answer, reported as unreadable, rather than the other eight
  hundred entries and a failing exit code.
- **The program no longer claims to be version 1.0.0.** Nothing had ever declared a
  version, so the build tools filled one in. It now reports `0.1.0`, which is what an
  unreleased tool should say.

### Known limits

- Local machine only. Remote management is not in this version.
- **`bws snapshot create` no longer replaces a file that is already there.** It says so and
  stops, without spending the second it takes to read every signature first. Add `--force` if
  replacing the file is what you meant. Until now it overwrote whatever was at that path -
  snapshot or not - and reported success.
- **`SECURITY.md`** in the repository: how to report a vulnerability privately, what is in
  scope, and what this tool deliberately does not claim to protect.
- **Everything that reads the machine got about twice as fast.** `bws list` went from a bit
  under six tenths of a second to a bit under a quarter, and the window now shows its list in
  around eight tenths of a second rather than a second and a third. Measured on a machine with
  810 services. Nothing about what the tool reports changed - the entries were checked one
  against the other, all 810 of them, and they read the same.
- **The window uses about 100 MB.** Measured rather than estimated, on a machine with 810
  services. Roughly half of that is the Windows user interface framework itself: an empty
  window with nothing in it already costs 55 MB, and a plain list of 810 rows costs 70 before
  any of this tool's own code runs. The data behind the list - every service, every setting
  shown - is under a megabyte of it. The command line tool over the same services uses about
  16 MB.
- **The window takes about eight tenths of a second to show the list**, of which about half is
  spent before any of this tool's code runs at all: starting a .NET process and putting an
  empty window on the screen.
- **The window is dark, and it stays dark whatever Windows is set to** - including when
  Windows is set to a high contrast theme. Checked rather than assumed: the window keeps its
  own colours and stays readable, but it does not follow that setting.
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
