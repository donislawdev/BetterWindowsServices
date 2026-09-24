# Changelog

All notable changes to Better Windows Services are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file is for people who use the tool. Changes that only matter to somebody working on
it - internal structure, measurements, test guards - are kept in a developer changelog that
is not part of this repository.

## [Unreleased]

### Added

- "Automatic (delayed)" in the startup type menu, and the word `delayed` on the command line:
  `bws start-type <name> delayed`. An entry that belongs to a load order group is refused, with
  the name of the group, because Windows does not let such an entry start late. The Print
  Spooler is one of them.
- Setting a running entry to Disabled says that it keeps running until it is stopped or the
  machine restarts, and offers "Also stop it". That adds a step to the same plan which stops the
  entry after its startup type is set, so there is one plan and one confirmation. On the command
  line the same is `--stop`, accepted next to `disabled` only.
- Setting a stopped entry to Automatic or Automatic (delayed) says that it starts at the next
  restart of the machine.
- The JSON of a plan carries `delayedAuto` next to `startType`, the same way `bws list --json`
  does, and `alsoStop` for a plan that also stops.
- A Help button in the top right corner, also opened with F1. It lists the keyboard shortcuts -
  each item does what its key does - opens the query language page and the project's website in
  your browser, and shows the version of the program, which you can copy.

### Changed

- Setting a startup type now also sets or clears the delayed start, the way `sc.exe config`
  does. Before, Automatic on a delayed entry left it delayed and said it was already there.
- The way back after changing the startup type of a delayed entry is now
  `bws start-type <name> delayed`. Before, there was no way back for such an entry.
- When the details panel could not read some of its fields, it now says that F5 reads the
  machine again and the panel tries once more.
- The window starts with the cursor in the search box, so you can type a query straight away. The
  same happens after "Show the list" on the overview. The list of suggestions under the box does
  not open by itself.
- Folding the filters away with the Filters button is remembered, so the window opens the way you
  left it. The first time the window opens, the filters are showing.
- The Description column comes last in the usual columns, so the state and the startup type stand
  next to the name even in a maximised window. A layout you have already arranged keeps its order -
  "Restore the usual columns" under Columns gives you the new one.
- The row menu shows Ctrl+C next to "Copy everything".
- The "Also stop it" offer in a startup type plan is drawn in red, like "Force stop...".
- The details panel reads the signature, publisher, file version, file hash, memory and
  "Required by" of its entry when it opens, instead of showing "not read" and asking you to
  turn on a column. While it reads, those lines say "reading...". A file on another machine is
  still not read, and the panel says why.
- The details panel takes a third of the window, between a lower and an upper limit, instead
  of a fixed width. While it is open, the Description column leaves the list, because the
  panel shows the description in full. The column comes back when the panel closes, and your
  choice of columns is kept.
- "What it runs" comes second in the details panel, before "About the entry".

### Fixed

- The details panel no longer stays open over the machine overview.
- Opening the details panel on another entry no longer shows the previous entry's "no longer
  in the listing" notice.

## [0.2.0] - 2026-09-24

First release. What the tool does is described in the [README](README.md).
