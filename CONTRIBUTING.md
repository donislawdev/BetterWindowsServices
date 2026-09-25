# Contributing to Better Windows Services

This tool runs as an administrator on machines that matter, and most of what it does is a promise
about what it will and will not touch. So the rules below are about keeping those promises, and
the test suite enforces most of them - a pull request that breaks one fails before anyone reads
it.

Three kinds of contribution are worth more than code, and each is described first.

## Evidence from a machine that is not ours

Every number and every shape this tool was built against comes from a handful of machines. Yours
is different, and that difference is the most useful thing you can send.

- **The tool and `sc.exe` disagree about a service.** Part of the suite compares the two entry by
  entry on the machine it runs on, and a disagreement on your machine is a bug here until shown
  otherwise. Paste `bws show NAME --json` and the output of `sc qc NAME`, `sc query NAME` and
  `sc qtriggerinfo NAME`.
- **A plan was wrong.** A preview that named the wrong dependents, missed one, or ran something
  the preview did not show. The preview and the execution are the same plan, so a difference
  between them is the worst bug this product can have. Paste the `--dry-run` output and what
  happened.
- **A snapshot diff reported a change nobody made**, or missed one somebody did. Paste both
  snapshots if you can, or the diff output if you cannot - a snapshot describes the whole machine,
  and only you can decide whether it can leave it.
- **A shape the tool did not expect.** A service name with a space, a launch path with forward
  slashes, a display name in a script the tool lined up badly, a machine where a whole family of
  reads is refused. The tool is meant to report every one of these as it is - if it did something
  else, that is a bug.

Open an issue with the output. If the output holds something about your machine you would rather
not publish, say what kind of thing it was and what the tool said about it, and that is usually
enough to start.

## Translations of the window

The window is the half of this product that is translated. The command line is English only, by
decision, so that a script written on one machine reads the same on every other.

**A translation is reachable without any change to the code.** The window reads the language of
the Windows session it runs in and looks for `gui.<code>.json` - `gui.pl.json`, `gui.de.json` -
first inside itself and then in a `languages` folder beside the executable. A file you drop there
is picked up on the next start, so you can test your translation on your own machine before it
ever reaches this repository. A key the file does not carry falls back to English rather than to
the key itself, so a partial translation is usable while it is being finished.

Start from [`src/Bws.Gui/Resources/gui.en.json`](src/Bws.Gui/Resources/gui.en.json), which is the
file the window ships with and the only complete list of keys. Four things to know before you
spend an evening on it:

- **Keep the placeholders.** `{0}`, `{1}` in a sentence stand for values the code hands over, and
  a translation keeps the same ones. A sentence that asks for a value the code never hands it is
  shown untouched, placeholders and all, rather than taking the window down - so a mistake is
  visible on screen and harmless.
- **A sentence that counts something has a singular and a plural form** - the keys ending in
  `.one` and `.many` - because *1 entries* is the kind of mistake a tool for administrators is not
  allowed to make. Translate both, even where your language would say them the same way.
- **No semicolons, and no dash other than the plain hyphen**, in any sentence. That is the house
  rule for every piece of prose in this repository, and it applies to a translation as it does to
  the English.
- **A file with letters outside ASCII has to be named in one test before it can enter the
  repository.** A guard sweeps every shipped file for text outside plain ASCII and fails on a file
  that is not on its list with a reason beside it, because a sentence in another language once
  reached a public file that way. Your translation will be on that list, with the reason *a
  translation of the window into Polish* - say so in the pull request and it is a one-line change,
  or add it yourself in `tests/Bws.Architecture.Tests/PublicSurfaceGuards.cs`.

To ship inside the window rather than beside it, the file is also listed in
`src/Bws.Gui/Bws.Gui.csproj` as an embedded resource with the same two attributes `gui.en.json`
has there - the comment above that line says why both are load-bearing. A built-in language wins
over a file dropped beside the program, so once yours ships, the `languages` folder is only for
the next translator.

## Machines and shapes we cannot see

The list of services the tool treats as ones the machine cannot run without - the ones a plan
warns about before taking them down - is a starter list of seven, and it says so in
`src/Bws.Core/Planning/CriticalEntries.cs`. If a server role you run adds one, an issue naming it
and saying what stopped working is a contribution that reaches every user of that role.

The same goes for a service whose process the tool could not end for a reason it did not name,
a trigger kind it called `unknown`, or a signature state it could not put a word to.

## What is not accepted

These are the promises, and a change that breaks one is refused whatever else it brings:

- **A write that does not go through a plan.** Every operation that changes the machine builds a
  plan that can be previewed, turned into a command line, executed and undone. There is no second
  path, not even a temporary one.
- **A preview that differs from the execution**, in any step, order or reason.
- **Anything that reaches the network.** No telemetry, no update check, no crash reporting, no
  client of any service. A guard reads the shipped assemblies and fails on a network type. The one
  place the tool may look at a path on another machine is behind `--follow-network`, and it is off
  by default.
- **Reading or writing the registry where the service control manager has an API for it.** The
  registry is where the tool reads nothing and writes nothing.
- **Identity by display name or by account name.** A service is its internal name and an account
  is its SID. Display names and account names are translated into the language of the machine and
  are only ever shown to a person. Breaking this does not crash anything - it makes a snapshot
  from a German server compare wrongly against a Polish one, silently.
- **Silence on a failed read.** A field the tool could not read is reported as not read, never as
  empty and never left out. A listing that quietly skipped ten services looks complete and will be
  compared as if it were.
- **A file that applies itself.** Loading a snapshot or an import fills a form and nothing more.
  Only a plan somebody confirmed changes the machine.
- **The core knowing that an interface exists.** `Bws.Core` references neither the window nor the
  command line, and a guard reads the compiled assemblies to make sure.
- **A capability that exists in one surface only.** The window and the command line are two
  clients of one engine and share one query language. A feature belongs in the engine, and reaches
  both.
- **A change to a public name.** Query field names and values, JSON field names, the snapshot
  format, command line switches and exit codes are contracts that live in other people's scripts
  and saved files. They can grow. They cannot be renamed or given a different meaning - a renamed
  one is kept as an alias and marked, and a snapshot format change bumps its schema version.
- **A 32-bit build.** A 32-bit process reads redirected paths and registry keys and gets a
  quietly wrong answer.
- **A new dependency without an argument for it.** The tool runs as an administrator on
  production machines, and a short list of dependencies is a claim it makes, not housekeeping.
- **A Mica or Acrylic window background.** Measured: a translucent background turns ClearType off
  and every row of text in the list goes soft.

## Building and checking

You need the .NET 10 SDK, on Windows. There is nothing else to install.

```
dotnet build BetterWindowsServices.slnx -c Release
dotnet test BetterWindowsServices.slnx -c Release
```

The tests in `tests/Bws.Integration.Tests` read the real service control manager of the machine
they run on, run the built `bws.exe` as a separate process and compare it against `sc.exe`. They
are read-only, and their numbers are about your machine - the suite holds performance budgets that
were measured on ours, so a slow machine can turn one of them red without anything being wrong.
The continuous integration workflow runs everything except that project, plus the part of it
marked as holding anywhere.

The coverage floor is checked by a script in the repository, and it is the same script the
workflow runs:

```
pwsh -File tests/coverage-gate.ps1 -Scope anywhere
```

A red run keeps its own output under `TestResults\` and prints the names of the failing tests, so
that what went wrong survives the run that found it.

### Conventions the test suite enforces

These are guards rather than preferences. Each one exists because the thing it forbids happened at
least once, and most of them read the compiled assemblies or the source tree rather than trusting
a declaration:

- **Everything inside the repository is English**, including comments and commit messages. The
  criterion is the place, not the reader.
- **A flat hyphen, never an em or en dash, and no semicolons in prose.** Code and quoted syntax are
  the only exceptions.
- **Text a user sees is a key into a language file, never a literal in code.** The keys are English
  too. A guard reads every shipped source file for a literal string written to the console.
- **Data goes to standard output, everything else to standard error**, and exactly one place in the
  command line tool writes to the data channel.
- **The core writes nothing to the console.** It returns results, and the layer above decides what
  happens to them.
- **A shipped assembly starts no process**, with two named exceptions in the window, each in a file
  of its own: restarting itself as administrator, and handing the support page to the browser -
  through the desktop, when the window has administrator rights, so that the browser does not.
- **Every broad `catch`, every `async void`, every native call that drops its answer, and every
  place that does two things at once is on a list with the argument for it beside it** - and the
  list may not name a place that stopped doing it.
- **Every collection keyed by a string says how its keys compare.** Service names compare without
  case, and a dictionary that forgot to say so is a bug waiting for a machine that spells one
  differently.
- **No view invents an appearance value of its own.** Colours, sizes, spacing and text in the
  window's markup come from named resources in the theme files, and a guard reads the markup for
  a literal.
- **No file grows past the longest one there was.** The ceilings only ever go down. Adding to the
  longest file starts with finding a seam.
- **The window publishes as one file**, and both shipped programs point at one icon.
- **Every package that ships is named in `THIRD-PARTY-NOTICES.md`**, and the notices name no
  package that no longer ships.

## Two things that are the maintainer's alone

- **The version number.** Raising it is a declaration to users, not housekeeping. It is never part
  of a contributed pull request - the maintainer raises it in the pull request that prepares a
  release, together with closing the changelog.
- **A change to one of the promises above.** They can be discussed in an issue. A pull request that
  quietly relaxes one is closed with a pointer here.

## Licence and conduct

Contributions are licensed under **GPL-3.0**, the same as the project. By opening a pull request
you agree that your contribution ships under it.

Behaviour here is covered by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Security problems go
through [SECURITY.md](SECURITY.md) rather than the issue tracker.
