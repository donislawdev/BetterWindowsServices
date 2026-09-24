# Better Windows Services - a services.msc replacement with search, dry runs, and a diff of what changed since yesterday

[![CI](https://github.com/donislawdev/BetterWindowsServices/actions/workflows/build.yml/badge.svg)](https://github.com/donislawdev/BetterWindowsServices/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/donislawdev/BetterWindowsServices?sort=semver)](https://github.com/donislawdev/BetterWindowsServices/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/donislawdev/BetterWindowsServices/total)](https://github.com/donislawdev/BetterWindowsServices/releases)
[![License: GPLv3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6)
[![Website](https://img.shields.io/badge/website-betterwindowsservices.donislawdev.com-e4573f)](https://betterwindowsservices.donislawdev.com/)

**Better Windows Services** is `services.msc` rebuilt for people who administer Windows machines -
a window and a command line over one engine. Ask the machine a question instead of scrolling eight
hundred rows. See what a change will do **before you press anything**. And the part `services.msc`
never had: **freeze every service into a file, and a week later ask what changed.**

⭐ **If it found the service that was not supposed to be running, leave a star.** That is how the
next administrator finds out it exists.

**What it can do**

- **Ask, instead of scroll** - `start:auto !status:running` is *what should be up and is not*. One
  language, in the search box and in `bws list --query`, with the fields offered as you type.
- **Show the plan before anything happens** - every stop, start, restart and start type change is
  previewed with the services it takes down with it, and the preview is what runs.
- **Tell you what drifted** - `bws snapshot create` freezes the machine into a file, and
  `bws snapshot diff` says what changed since, configuration apart from what merely started or stopped.
- **Stop a service that will not stop** - it asks politely first and ends the process only if that
  fails, naming every other service that lives in it.
- **Show what `services.msc` hides** - who signed the binary and whether Windows trusts it, triggers,
  privileges, the security descriptor, the process and its memory. As columns, and as things to search on.
- **Say when it could not read something** - a field it could not read is reported as not read, never
  shown as empty. A snapshot taken without administrator rights says so inside the file.
- **Fit into scripts** - `--json` on every command, an exit code for every ending, and `5` when a diff
  finds drift.
- **Run from one file, offline** - no installer, no .NET to install, and it never talks to the
  internet. Reading needs no administrator rights. GPL-3.0.

![A real session in the Better Windows Services window: two entries picked and the plan for starting them shown before anything runs, the Print Spooler found by typing part of its name and its plan carried out, the menu of column groups, and the filter buttons narrowing the list until nothing matches.](.github/bws-in-action.gif)

*A real session, on a Windows installed in Polish - the display names come from Windows, so they
are in its language. Every plan is shown before anything happens, and the command line in it asks
for exactly the same thing.*

![Clicking the Star button at the top of the Better Windows Services repository page: the counter goes from Star 0 to Starred 1.](.github/star-the-repo.gif)

---

## Download and run

Grab the latest build from the
**[Releases page](https://github.com/donislawdev/BetterWindowsServices/releases/latest)**:

| File | What it is |
|---|---|
| `BetterWindowsServices-win-x64.zip` | The window, one self-contained executable - no .NET to install |
| `bws-cli-win-x64.zip` | The command line, one self-contained executable, for scripts and CI |

Windows 10 1809 or Windows Server 2019 and later, 64-bit. Each archive unzips to a folder holding
the executable, the licence and the notices for the borrowed code - run
`BetterWindowsServices.exe`, or `bws.exe` from a terminal. No installer, nothing written to the
registry, and no administrator rights needed to look. Changing anything needs an elevated session,
and both halves say so instead of failing quietly - the window offers *Restart as admin*.

The executables carry an Authenticode signature with a timestamp, so Windows names the publisher
rather than an unknown one. SmartScreen can still warn on a brand new build until enough people
have run it - that reputation is earned over downloads and is not something a signature buys
outright.

### Check what you downloaded

Every release publishes, beside each archive: a bill of materials naming everything inside it
that somebody else wrote, a signed attestation of that document, and one file of SHA-256 sums.
Run these in the folder you downloaded into. The first two need nothing at all. The last two need
the [GitHub CLI](https://cli.github.com/), and because the `.sigstore.json` beside each archive is
passed to `--bundle`, they check it against what GitHub signed rather than by asking GitHub.

The first two match a whole line - the digest **and** the file name it is written against - rather
than looking for the digest anywhere in the file. A sums file that lists your digest under somebody
else's name would otherwise pass.

<!-- verify-commands -->
```powershell
if (-not ((Get-Content SHA256SUMS) -match ('^' + (Get-FileHash BetterWindowsServices-win-x64.zip -Algorithm SHA256).Hash.ToLower() + '\s+\*?BetterWindowsServices-win-x64\.zip$'))) { throw 'BetterWindowsServices-win-x64.zip does not match SHA256SUMS' }
if (-not ((Get-Content SHA256SUMS) -match ('^' + (Get-FileHash bws-cli-win-x64.zip -Algorithm SHA256).Hash.ToLower() + '\s+\*?bws-cli-win-x64\.zip$'))) { throw 'bws-cli-win-x64.zip does not match SHA256SUMS' }
gh attestation verify BetterWindowsServices-win-x64.zip --repo donislawdev/BetterWindowsServices --predicate-type https://spdx.dev/Document/v2.3 --bundle BetterWindowsServices-win-x64.zip.sigstore.json
gh attestation verify bws-cli-win-x64.zip --repo donislawdev/BetterWindowsServices --predicate-type https://spdx.dev/Document/v2.3 --bundle bws-cli-win-x64.zip.sigstore.json
```
<!-- /verify-commands -->

**`--predicate-type` is not optional and leaving it out looks like a broken release.** The tools
ask for build provenance by default, and a signed archive deliberately has none: a person signed
those bytes on their own machine with a card in a reader, and an attestation saying a workflow
produced them would be a lie. What is attested is what is inside the archive. Without the flag one
spelling answers "no attestation found" and another returns 404.

These four commands are not decoration: a workflow runs them, out of this file and unchanged, every
time a release is published. If the command you are about to type has stopped working, that
workflow is what turns red.

`bws license --components` answers the same question from inside the program, with no internet and
nothing to download - what it carries, which version, under which licence.

> **Early release.** Both halves do everything on this page, and an automated suite runs on every
> commit. What is not there yet is under [Honest limits](#honest-limits) - most of it is the second
> half of the audit story: a stock Windows baseline, a change journal, and restoring from a snapshot.

**One promise to hold it to:** nothing changes without showing the plan first, and the preview is
what runs. A preview that differs from what then happened is the worst bug this tool can have -
report it.

---

## Two minutes with it

**In the window:** start `BetterWindowsServices.exe`. It opens on a few numbers about this
machine - what is running, what should have started and did not, what points at a file that is
gone. Click one and its question lands in the search box as a query you can edit. Right-click a
row, choose *What stopping would do*, and the plan appears: the steps, what comes down with them,
a warning if one of them is something the machine needs, and the `bws` line that asks for the same
thing. The button under it is named after what it will do - *Stop Winmgmt* - and runs exactly that
plan, and afterwards the panel says what did not work and how to get back.

![The opening screen of Better Windows Services: three cards of numbers about this machine - services running, services set to start automatically that did not come up, and services set to start automatically whose file is gone - with smaller counts under two of them, and a Show the list button.](site/assets/window-overview.png)

![The list of services with the query !type:driver in the search box and four rows picked, and over it the plan for stopping them: four steps in order, a note that three of the four are already stopped so nothing would change for them, a bws stop line for each entry with a Copy button, a box for how many seconds to wait for each step, and a Stop 4 entries button.](site/assets/window-plan.png)

**From the command line:**

```
bws list --query "start:auto !status:running"     what should be up and is not
bws list --query "file:missing"                   services whose binary is gone
bws list --query "signed:no peruser:no"           unsigned, without the per-session copies
bws show Spooler                                  everything known about one entry
bws stop Winmgmt --dry-run --dependents           what stopping it would take down
bws kill Spooler --dry-run                        what ending its process would take with it
bws start-type Spooler manual --dry-run           what taking it off automatic would do
bws snapshot create before.json                   freeze the machine before a change
bws snapshot diff before.json --live --exit-code  what has changed since, 5 if anything has
```

A preview is the plan, printed:

```
$ bws stop Winmgmt --dry-run --dependents
Plan: stop Winmgmt  (2 steps)
  1. stop  vmms      (would break otherwise)
  2. stop  Winmgmt   (asked for)

Warnings:
  - Stopping Winmgmt also stops one other entry: vmms
  - Winmgmt starts automatically, so it will be back after the next restart.
```

Run it without `--dry-run` and those are the steps, in that order, with the same reasons beside
them. Every command speaks `--json` as well, and every ending has an exit code your script can
branch on - the full list is in the [reference](#reference) below.

<details>
<summary><strong>Table of contents</strong></summary>

- [Why this exists](#why-this-exists)
- [What changed since yesterday - snapshots and drift](#what-changed-since-yesterday---snapshots-and-drift)
- [Nothing happens without a plan](#nothing-happens-without-a-plan)
- [Ask the machine a question - the query language](#ask-the-machine-a-question---the-query-language)
- [Honest limits](#honest-limits)
- [Antivirus and EDR](#antivirus-and-edr)
- [Requirements](#requirements)
- [Building from source](#building-from-source)
- [Reference](#reference)
- [Questions](#questions)
- [Where this is](#where-this-is)
- [Contributing](#contributing)
- [Licence](#licence)

</details>

---

## Why this exists

`services.msc` is a snap-in that has barely changed since Windows 2000, and administrators spend
hours a week in it hitting the same walls:

- **No search.** The only navigation is jumping to the first letter of a display name.
- **No filtering.** There is no way to ask *show me what is set to start automatically and is not
  running*.
- **One row at a time.** Restarting twelve services is twelve right-clicks and twelve waits.
- **"Stopping..." forever**, with no way out except Task Manager and guessing the process.
- **No history.** Nobody knows who set that start type to Disabled, or when.
- **Hidden data.** The process, the triggers, the privileges, the security descriptor, who signed
  the binary - all of it needs `sc.exe` from memory, or the registry.
- **No command line parity.** `sc.exe` has a syntax from the nineties (`sc config X start= auto`,
  and the space after the equals sign is mandatory), and `Get-Service` does not show the account
  a service runs as, let alone its description.

The other tools each fix part of it. Process Hacker and System Informer show a rich list and can
force a kill, and have no audit and no command line. `sc.exe` and `PsService` can do everything and
tell you nothing. PowerShell's `*-Service` cmdlets are scriptable and shallow. Nobody combines a
searchable list, a preview before every change, and a record of what changed over time - and the
record is the reason to keep this on a machine after the first afternoon.

---

## What changed since yesterday - snapshots and drift

**This is the part `services.msc` has never done.**

A snapshot is the whole machine at one moment: every service and driver, with its start type,
account, launch path and arguments, the hash and the signer of the file it runs, its dependencies
and what depends on it, its triggers, its required privileges, its SID type and its security
descriptor, plus the name of the machine and of the account that took it, and whether that account
was an administrator. It is JSON with one field per line, so it goes into a repository and
`git diff` reads it.

```
bws snapshot create before.json --note "before the patch"
```

Then change something, wait a week, or apply the patch. Ask what is different:

```
$ bws snapshot diff before.json --live --exit-code
Changed (2):
  Spooler  Print Spooler
    startType: Manual -> Automatic
    status: Stopped -> Running  (running state, not configuration)
  wlidsvc  Microsoft Account Sign-in Assistant
    status: Stopped -> Running  (running state, not configuration)

Added 0, removed 0, changed 2. Fields differing: 1 in configuration, 2 in running state.
```

Two things about that output are the point. **Configuration and running state are reported
apart**, because two snapshots taken a day apart differ in what happens to be running and almost
none of it is drift - the start type that changed is the line to read. And **what could not be
compared is listed, not hidden**: a field one of the two snapshots never read, because that
session had no rights to it, comes out as *not compared* under its entry, and the summary counts
those entries, so a diff that says *nothing changed* means nothing changed in what both sides
could see.

`--exit-code` ends with `5` when anything differs, so a scheduled task can take a snapshot at
deployment and page somebody the first night the machine drifts. Compare two files instead of a
file and the machine to answer *what did the Tuesday patch do*, or *why does staging differ from
production* - take one on each and diff them anywhere.

A snapshot describes the whole machine, down to every launch path and security descriptor, so file
it accordingly. It is written with whatever permissions its directory already has, and the tool
narrows nothing.

---

## Nothing happens without a plan

Every operation that writes builds a plan first, and the same plan serves five purposes: the
preview, the cascade of dependent services, the refusal before you press anything, the command
line that asks for the same thing, and the way back afterwards.

- **The preview is the execution.** `--dry-run` prints the steps in order, each with its reason -
  *asked for*, or *would break otherwise*. Without `--dry-run` those same steps run in that same
  order. There is no second code path for the real thing.
- **What comes down with it is in the plan.** The services that would break are steps of their own
  (`--dependents` on the command line, always in the window), and a plan that would take down
  something the machine needs says so in its warnings.
- **A refusal comes before the button.** A driver, a service the manager will not accept a stop for,
  a process the system protects - refused when the plan is built, with the reason, not after a wait.
  The window greys the button and says why beside it.
- **Force stop is two steps, and the second is conditional.** `bws kill` asks the service to stop and
  ends its process only if that does not work. The preview names the process and every other service
  living in it, because ending a process takes all of them. `--restart` brings them back, `--force`
  skips the polite step - and the preview shows one step instead of two, so the difference is visible
  before anything happens.
- **Afterwards, the way back.** A report ends with what did not work and the commands that put things
  back - and the window offers *Copy all* over them.
- **A start type change moves nothing on its own.** It changes what happens at the next boot, leaves
  the service as it was, and the report says so. The one exception is a stop you ask for in the same
  plan - `--stop` beside `disabled`, or *Also stop it* in the window.

The window and the command line are two clients of one engine. The plan the window shows is the
plan `bws` would print, and the window prints the `bws` line beside it so you can take it to a
script.

---

## Ask the machine a question - the query language

One language, in the search box and in `bws list --query`. A bare word searches the name, the
display name, the account and the launch path. A field narrows it:

| Field | Asks about | Values |
|---|---|---|
| `status` | running state | `running`, `stopped`, `paused`, `pending` |
| `start` | start type | `automatic` (`auto`), `delayed`, `manual`, `disabled`, `boot`, `system` |
| `type` | kind of entry | `driver`, `ownProcess`, `sharedProcess` |
| `account` | the account it runs as, as the manager spells it | text, e.g. `localsystem` |
| `file` | whether the file it runs is on disk | `present`, `missing` |
| `signed` | what Windows thinks of the file's signature | `yes`, `no`, `trusted`, `notSigned`, `expired`, `revoked`, `tampered` |
| `publisher` | who signed the file | text |
| `mismatch` | configuration and state that disagree | `stopped` (should run, does not), `running` (disabled, runs anyway) |
| `trigger` | starts on a condition | `network`, `device`, `ip`, `domain`, `firewall`, `policy`, and the actions `start`, `stop` |
| `peruser` | per-user service family | `yes`, `no`, `template`, `instance` |
| `requiredby` | what breaks if this stops | a service name |
| `dependson` | what this needs | a service name |
| `privilege` | a privilege the entry asks for | e.g. `SeDebugPrivilege` |
| `sidtype`, `sddl`, `pid`, `memory`, `path`, `name`, `display`, `description` | the rest | every field and value is on the [query language page](https://betterwindowsservices.donislawdev.com/query-language/), and a mistyped field name makes `bws` list them all |

Text fields match *contains* by default, `name:=spooler` is exact, `name:spool*` takes wildcards,
and `name:/^Sql.*/` is a regular expression. `!` negates a term, a comma is *or* inside a field,
and quotes protect anything with spaces or colons: `account:"NT SERVICE\McmSvc"`. Every field
accepts `none`, `any` and `?` - the last one finds the entries where the tool could not read that
field, which is a question `services.msc` cannot even ask.

A mistyped value is an error with the nearest valid value suggested, never an empty result that
looks like an answer. And a query that would narrow nothing is refused on the command line, so a
script does not silently operate on the whole machine because of a typo.

---

## Honest limits

A tool that quietly fails to cover something is worse than one that says what it cannot do.

**Not built yet - the second half of the audit story:**

- **Snapshots are on the command line only.** The window shows, searches and changes, and it does
  not take or compare snapshots. Use `bws snapshot` beside it.
- **No stock Windows baseline.** The opening screen has a place for *how many of these are not
  from a clean install* and says it is not counted yet rather than showing a number it cannot
  stand behind.
- **No change journal.** Who changed a start type, and when, is not read from the event log yet.
  A snapshot taken at deployment and a diff against the machine answers *what* changed, not *who*.
- **No restore from a snapshot.** A diff tells you what drifted, and putting it back is by hand or
  by script from the commands the diff shows you.
- **No creating, deleting or repointing a service.** The launch path is read and reported, and it
  is not edited.
- **No recovery actions.** Not read, not shown, not searchable.

**By design:**

- **Local machine only.** Remote management is not in this version. To compare two machines, take a
  snapshot on each and diff the two files.
- **Windows only, 64-bit only**, Windows 10 1809 or Windows Server 2019 and later.
- **Operations on drivers are refused rather than attempted**, because stopping a kernel driver is
  often not reversible without a restart.
- **The tool does not raise its own privileges.** When the manager refuses, it says so and ends
  with exit code 3. The window offers to restart itself as administrator, and does nothing else
  about it.
- **The window is dark, and it stays dark whatever Windows is set to** - including a high contrast
  theme. Checked rather than assumed: it keeps its own colours and stays readable, and it does not
  follow that setting.
- **Certificate revocation is not checked.** It would need to reach the network, and this tool
  never does.

**Things Windows does that this tool reports as they are:**

- For a service hosted inside `svchost`, the launch path names the host, so *the file is there*
  and *who signed it* both say less than they do for a service with a process of its own.
- Where a file carries both its own signature and an entry in a Windows catalogue and the two
  name different signers, the publisher shown is the one inside the file - the one that stays the
  same when the file is looked at on another machine, and it can differ from what PowerShell
  reports.
- Without administrator rights the manager lists fewer entries and refuses more of what it lists.
  Both halves say so at the top, and a snapshot records it, because comparing an elevated snapshot
  against one taken without rights would otherwise report entries as removed that nobody removed.

**Measured, so you know what to expect** - on one machine with 810 services, and your figures
will differ: `bws list` reads them in about a quarter of a second, the window shows the list in
about eight tenths, and the window uses about 100 MB, half of which is the Windows user interface
framework before any of this tool's own code runs. The command line over the same machine uses
about 16 MB.

---

## Antivirus and EDR

This tool does what an administrator does with services, and some of that is what security
software watches for: it stops and starts services, it can end the process behind one, and it reads
every service's security descriptor and required privileges. An EDR that flags a process ending
`svchost.exe` is doing its job, and a forced stop here can look exactly like that.

What it does **not** do, so you can check the claim: it injects nothing into any process, it packs
or obfuscates nothing, it installs nothing and leaves nothing running, and it opens no network
connection. It is open source under GPL-3.0 and you can build both executables yourself from this
repository.

If your policy needs an exclusion, exclude the two executables by file rather than a folder, and
prefer the command line on a server - it is the smaller of the two and does everything the window
does. The one thing worth treating with the same care as the tool itself is a snapshot file: it
describes the whole machine.

---

## Requirements

- Windows 10 version 1809 or later, or Windows Server 2019 or later, 64-bit.
- Nothing to install. Both executables carry their own .NET runtime.
- Administrator rights to change anything. Reading, searching, snapshots and diffs work without,
  with fewer entries visible and the tool saying so.

The window keeps one file of its own, the layout you left the list in, under
`%APPDATA%\BetterWindowsServices`. A snapshot goes where you tell `bws snapshot create` to put it,
and into the current directory when you do not.

---

## Building from source

.NET 10 SDK, on Windows.

```
git clone https://github.com/donislawdev/BetterWindowsServices
cd BetterWindowsServices
dotnet build BetterWindowsServices.slnx -c Release
dotnet test BetterWindowsServices.slnx -c Release
```

Some tests read the real service control manager of the machine they run on and compare what this
tool says against what `sc.exe` says. They are read-only. The single-file executables the
releases carry are the two publishes below, each one file with the runtime inside:

```
dotnet publish src/Bws.Cli/Bws.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish src/Bws.Gui/Bws.Gui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

They land as `bws.exe` and `BetterWindowsServices.exe` under each project's
`bin/Release/.../win-x64/publish/`. The `.pdb` files beside them are debugging symbols, and nothing
needs them to run.

---

# Reference

The short version of `bws --help`, which is the authoritative one and is longer.

## Commands

```
bws list [--query TEXT] [--signatures] [--memory] [--required-by] [--follow-network] [--json] [--timing]
bws show NAME [--full] [--follow-network] [--json] [--timing]
bws stop|start|restart NAME [--dry-run] [--dependents] [--timeout SECONDS] [--json] [--timing]
bws kill NAME [--force] [--restart] [--dry-run] [--dependents] [--timeout SECONDS] [--json] [--timing]
bws start-type NAME automatic|delayed|manual|disabled [--stop] [--dry-run] [--json] [--timing]
bws snapshot create [FILE] [--note TEXT] [--follow-network] [--force] [--json] [--timing]
bws snapshot diff EARLIER LATER [--exit-code] [--json] [--timing]
bws snapshot diff EARLIER --live [--exit-code] [--json] [--timing]
bws license [--components]
bws --help
bws --version
```

| Switch | What it does |
|---|---|
| `--query TEXT` | on `list`, narrow the listing with the query language |
| `--dry-run` | print the plan and change nothing. The plan is the same one an execution runs |
| `--dependents` | put the services that would break into the plan as steps of their own |
| `--timeout SECONDS` | how long to wait for one step to reach the state it asked for, sixty unless you say otherwise. Running out is the end of watching, not a failure, and the report says where the entry was left |
| `--signatures` | read who signed each binary and whether Windows trusts it. Several seconds over a whole machine, so it is off unless asked, and a query about signatures turns it on by itself |
| `--memory` | read what each running entry's process is using. Off by default because it is a measurement, not a setting |
| `--required-by` | read which entries break if one is stopped, asked of Windows directly. A call per entry, so off unless asked. `show` and `snapshot create` always read it |
| `--follow-network` | let the tool look at a launch path on another machine. Off by default for safety: one unreachable share costs twenty one seconds and the connection authenticates as you |
| `--json` | the same document, machine readable, on standard output |
| `--timing` | how long each part of the read took, on standard error |
| `--force` | on `kill`, end the process straight away without asking politely. On `snapshot create`, write over a file that is already there |
| `--restart` | on `kill`, bring the entry back once the process is gone, with everything that shared it |
| `--stop` | on `start-type`, and only beside `disabled`, stop the entry in the same plan once the setting is written. A startup type changes the next boot and nothing now, so without it a running entry set to disabled keeps running |
| `--exit-code` | on `snapshot diff`, end with 5 when anything differs |
| `--live` | on `snapshot diff`, compare the file against this machine as it is now rather than against a second file |
| `--note TEXT` | on `snapshot create`, what the snapshot was taken for, kept inside the file |
| `--full` | on `show`, print the fields that are genuinely empty as well |
| `--components` | on `license`, turn the notice into every component inside this executable with its version, its licence and where it came from. It reads nothing - no service manager, no disk, no network - so it answers on a machine with no internet |

Data goes to standard output and everything else to standard error, so `bws list --json | jq`
works and a warning never lands in your JSON.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | done, and everything arrived where it was going |
| `1` | the tool failed - it could not do what was asked for a reason that is about the tool, not about the plan |
| `2` | the command line was wrong. A mistyped command is offered the one you probably meant |
| `3` | the plan was good, it ran, and something in it did not get where it was going - the manager refused, or the session had no rights to it |
| `4` | somebody stopped the run by hand. Non-zero even when every step still arrived, so a wrapper does not treat an interrupted run as clean |
| `5` | a comparison ran and found differences. Only with `--exit-code`, because drift is what this tool is for finding, and finding it is not a failure |

Ctrl+C has three levels, each saying what the next one costs: the first stops going forward and
still puts back what was taken, the second leaves things as they are and still prints the report,
the third ends the process.

## The window

`BetterWindowsServices.exe`, one argument it knows: `--catalogue` opens a developer's sheet that
shows every component of the window in every state, and reads nothing from your machine.

Three lists on the switch above the search box - *Services*, *Drivers*, *Everything* - each saying
how big it is. The search box takes the query language and suggests as you type. The filter
buttons write into the box. *Columns* chooses what the list shows, a right-click on a column heading
narrows the list to that value or puts the column away, and the layout you leave is the layout it
opens in. A row's menu previews every operation before offering it, and copies the name, the
display name, the description or everything. *Export...* writes the rows on screen, in the columns
you have on and the order you sorted them into, to a CSV file. Ctrl+C over the list copies
everything about the chosen entry. Escape backs out of the innermost thing first. *Donate*, at the
right end of the row above the search box, opens the project's support page in your browser - and
in a window running as administrator it asks the desktop to open it, so the browser does not get
those rights.

![The Better Windows Services window with the three lists above the search box - Services 335, Drivers 465, All 800 - the query !type:driver in the box, the filter buttons open in rows for state, startup type, signature, disagreements and triggers, the row of operation buttons, and the list with display name, description, status, startup type and account.](site/assets/window-list.png)

---

## Questions

### How is this different from `sc.exe`, `Get-Service` or `services.msc`?

`sc query` and `Get-Service` tell you what is running. `services.msc` lets you change it, one row at
a time, with no way to ask a question and no record afterwards. This tool asks the question
(`start:auto !status:running`), shows you the plan before it acts, and keeps a snapshot you can
diff a week later. Where it disagrees with `sc.exe` about a service, that is a bug - part of the
test suite runs both and compares them entry by entry.

### Does it need the internet?

Never. No telemetry, no update check, no crash reporting, no account, no client of anything. The
one time it can touch a network at all is when a service's launch path points at another
machine's share and you pass `--follow-network` to let it look there - off by default, and the
help says why. The *Donate* button in the window hands one address to your browser when you press
it, and your browser is what connects.

### Does it need administrator rights?

To look, no. To change, yes. Without them the manager shows fewer entries and refuses more, and
both halves say so at the top rather than pretending the list is the whole machine. The window
offers *Restart as admin*. It never elevates itself.

### Is it free? Can I use it at work?

Yes to both. GPL-3.0, no strings attached. A snapshot you take is your file.

### Will it break my server?

Not by itself. Nothing is changed without a plan you have seen, drivers are refused, and a plan
that would take down something the machine needs says so. What it cannot protect you from is
carrying out a plan you did not read. The list of services the machine needs is a starter list of
seven - the two halves of RPC, the account manager, key isolation, the session manager and plug and
play - and Remote Desktop is not on it, so over a remote session, read the plan.

### Is this a Microsoft product?

No. Better Windows Services is an independent open source project and is not affiliated with,
endorsed by or sponsored by Microsoft. Windows is a trademark of the Microsoft group of companies.

### What about the second half - who changed it, and putting it back?

Not yet, and it is the next thing. A change journal read from the event log, a stock Windows
baseline per build, and restoring configuration from a snapshot with a dry run and a checkbox per
item are the plan. Until then a snapshot at deployment and a diff against the machine answer
*what* changed, which is most of the question.

---

## Where this is

**Working end to end:** the window and the command line over the same engine, the query language,
plans with preview, cascade, refusal and the way back, stop, start, restart, force stop, force
restart and start type, snapshots and diffs between files and against the live machine, signatures,
privileges, security descriptors, triggers, memory, per-user folding, keyboard and screen reader in
the window, CSV export, JSON and exit codes on the command line.

**Not there yet:** everything under [Honest limits](#honest-limits), above all the stock baseline,
the change journal and restore from a snapshot.

Found a problem, or a service this tool reports differently from `sc.exe`? The
[issue tracker](https://github.com/donislawdev/BetterWindowsServices/issues) is open, and a diff
between the two is the most useful thing you can paste into it.

---

## Contributing

The most useful contribution is evidence: a machine where the tool and `sc.exe` disagree, a
service whose plan was wrong, a snapshot diff that reported a change nobody made. Open an issue
with the output. [CONTRIBUTING.md](CONTRIBUTING.md) says what a change has to satisfy before it can
go in, what the test suite enforces, and what is not accepted. Behaviour here follows the
[Code of Conduct](CODE_OF_CONDUCT.md), and security problems go through
[SECURITY.md](SECURITY.md) rather than the issue tracker.

---

## Licence

Copyright (C) 2026 DonislawDev. The project is built with an AI-assisted workflow.

Released under the GNU General Public License, version 3 - see [LICENSE](LICENSE). Free, no
account, no telemetry, and it never talks to the internet. Other people's code is redistributed
with the program, and what it is and what its licences require is written down in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Better Windows Services is not affiliated with, endorsed by or sponsored by Microsoft. Windows is a
trademark of the Microsoft group of companies, and the name is used here only to say what the tool
manages.

If it saved you an afternoon, [donislawdev.com/support](https://donislawdev.com/support/) is where
that can be said in a way that keeps the next afternoon funded - the *Donate* button in the window
opens that page in your browser. The program itself opens no socket - your browser is what
connects.
