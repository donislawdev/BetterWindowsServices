# Changelog

All notable changes to Better Windows Services are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file is for people who use the tool. Changes that only matter to somebody working on
it - internal structure, measurements, test guards - are kept in a developer changelog that
is not part of this repository.

## [Unreleased]

### Changed

- A minimised window no longer reads the list of services every second. It reads it again the
  moment it is restored, so the list is current as soon as it is on screen. A window started
  minimised, from a shortcut set to "Run: Minimized", reads it once and then waits the same way.
- Stopping, starting and restarting a service, from the window or with `bws`, no longer waits
  about a quarter of a second longer than the service needs on every step. A service that stops
  in 10 ms is now reported done after a few tens of milliseconds, where it used to be reported
  after about 260, so a restart with a cascade of dependents finishes seconds sooner. The
  `milliseconds` of each step in the JSON of a plan is smaller for the same reason. How long a
  service is given before it is reported as not responding has not changed.
- Reading who depends on each entry - the Required by column, `required:` in a query and
  `bws list --required-by` - asks about several entries at once and takes a fraction of the time
  it did.
- After a plan is carried out, after something is installed or removed, and when a column or a
  query needs something the window has not read yet, the window no longer verifies every
  signature again. It keeps what it already knows about files whose path has not changed and
  verifies only new or changed ones. F5 still verifies everything again, so a file replaced by
  someone else in the meantime shows its new verdict after the next F5.
- The text on the plan sheet is sharper - it is drawn with ClearType like the rest of the window.
  The sheet's shadow looks the same, but it no longer makes the whole sheet redraw with every blink
  of the cursor. Where the window is drawn without the graphics card, as it is over Remote Desktop,
  that cost a third to half of a processor core for as long as the box a name is typed into was
  waiting.
- A plan with more than twenty commands shows them in one field that can be selected and copied,
  instead of a box and a Copy button for every line. *Copy all* still takes all of them. The same
  goes for the way back after a run. A plan over a whole scope opens noticeably faster.
- When a plan touches several entries this machine does not work without, the sheet names them all
  in one sentence instead of one sentence each. On a plan over a whole scope those sentences used
  to push every step off the sheet.
- The plan for a large selection appears sooner. Working out what stopping or restarting it
  involves asks Windows about each entry's dependents once instead of several times, and the
  plan for stopping every service on the test machine appeared in about 100-170 ms instead of
  about 185-275.

### Fixed

- A plan over a selection that includes an entry this machine does not work without - stopping
  a whole scope, for example - asked for a name to be typed, and typing it changed nothing: the
  button stayed off. Such a selection cannot be carried out, and the sheet now says so, with the
  entries named above it and a sentence saying to deselect them or deal with each one on its own.
  A plan for one such entry still asks for its name, as before.
- While a plan is being carried out, Escape and the close mark on the sheet no longer put the
  sheet away. Putting it away used to let a second plan be carried out beside the first, after
  which Interrupt and the guard against closing the window followed only the second one. The close
  mark is greyed while the run goes on and says why. A preview or the details of a row asked for
  during a run are refused, with the reason in the line at the bottom of the window.
- A plan that could not be carried out at all no longer reports "Done. All 0 entries are where you
  asked." The sheet says nothing was carried out.
- The list is read again after a plan has run even when the window was in the middle of another
  reading at that moment. The Startup type column used to keep showing the old value until F5.
  F5 pressed while signatures are being verified is carried out once they are done, instead of
  doing nothing.
- After a reading of the list fails once and the next one works, the window says so straight away.
  It used to go on saying it could not read the list until something on the machine changed.
- A force stop no longer ends a process under services that still need it. While services depending
  on the entry are running outside the plan, `bws kill` and the window's Force stop refuse and name
  them. The same goes for running services that depend on anything sharing the entry's process.
  `bws kill NAME --dependents` still stops the dependents as part of the plan, and if one of them
  will not stop, the process is not ended.
- `bws kill NAME --force --dependents` is refused. Its preview listed the dependents as stopping,
  while the run skipped their stops along with the polite one and ended the process under them.
- A force stop asks the entry itself to stop before the other services sharing its process, and asks
  those only if the entry did not stop. A service sharing the process that refused to stop used to
  make the plan skip the entry's own polite stop and end the process at once, and when the entry
  would have stopped on its own, the others had been stopped for nothing. In the JSON of a run, the
  steps not needed are reported with `"skippedBecause": "processStays"`, and the run still counts as
  completed.
- The preview of a force stop names a critical service arriving with `--dependents`, a service
  sharing the process that does not accept a stop, and one that is disabled and could not be
  started again by `--restart`.
- The way back after a force stop includes the services that ended with the process without stopping
  on their own. After `bws kill NAME --restart --force` it used to tell you to stop a service that was
  running before and after.
- After a stop refused because other running services depend on the entry, or because of missing
  rights, the window no longer offers Force stop, which could not help with either.
- The equivalent command of a force stop planned with its dependents includes `--dependents`.
- Restarting a service that takes longer than the limit to stop no longer leaves it stopped. The
  limit - `--timeout`, and "Wait up to" on the plan sheet - now counts time without progress: a
  service that keeps reporting progress is watched for as long as it takes, and one that sits still
  is given up on once the limit has passed since it last moved. The start that brings a service
  back waits for it to finish stopping instead of being refused while it is still stopping.
- A stop asked of a service that is already stopping waits for it instead of failing, and a start
  asked of one still stopping waits for it to stop first and then starts it.
- A service that stops again while starting is reported at once as not started, with its exit code
  and what Windows says about it, instead of after the whole limit as having run out of time.
- The plan to start a disabled service warns that Windows will refuse it, and says how to change
  the startup type first. The plan to start a paused service warns that a start does not resume it.
- The second Ctrl+C during `bws stop`, `start`, `restart` or `kill` takes effect while a step is
  still being waited for, instead of only after it.
- The note under a run that says the manager took longer than `--timeout` to answer appears only
  when the manager really did, not for a step that simply kept making progress.
- The window's reason for offering Force stop after a stop that was given up on no longer names a
  number of seconds that could be wrong. The line under a step being waited for says the limit is
  about progress.
- A force stop is refused when the process is one Windows marks critical, or when a service living
  in it has "restart the computer" among its recovery actions - ending such a process takes the whole
  machine down. It is refused as well when those recovery actions cannot be read.
- The preview of a force stop says when Windows will start a service living in the process again by
  itself once the process is ended, when it will run a program named in a service's recovery
  actions, and when a service has a recovery action of a kind the tool cannot name. In the JSON of a
  plan these are the warnings `recoveryRestarts`, `recoveryRunsProgram` and `recoveryUnnamed`. Until
  now `bws kill` reported such a service stopped while Windows was already starting it again.
- A force stop whose service Windows starts again at once is reported straight away as the process
  ended and the service running again, with its new process, instead of after the whole limit as
  having run out of time. In the JSON of a run that step is `"outcome": "failed"` with `errorCode` 0.
- Just before the process is ended, a force stop looks at it once more. If a service has started
  inside it since the preview, or a running service outside it has started to depend on something
  inside it, the process is not ended and the step says why.

## [0.3.0] - 2026-09-25

### Added

- "Automatic (delayed)" in the startup type menu, and the word `delayed` on the command line:
  `bws start-type <name> delayed`. An entry that belongs to a load order group is refused, with
  the name of the group, because Windows does not let such an entry start late. The Print
  Spooler is one of them.
- Setting a running entry to Disabled says that it keeps running until it is stopped or the
  machine restarts, and offers "Also stop it", drawn in red like "Force stop...". That adds a step
  to the same plan which stops the entry after its startup type is set, so there is one plan and
  one confirmation. On the command line the same is `--stop`, accepted next to `disabled` only.
- Setting a stopped entry to Automatic or Automatic (delayed) says that it starts at the next
  restart of the machine.
- The JSON of a plan carries `delayedAuto` next to `startType`, the same way `bws list --json`
  does, and `alsoStop` for a plan that also stops.
- A Help button in the top right corner, also opened with F1. It opens the query language page
  on the project's website in your browser.
- After the list is exported, the foot of the window says how many entries went into which file.
  The sentence goes away as soon as you change the search.
- "Set startup type" in the row menu, with the same four settings as the action bar.
- Each startup setting in both menus says what it means when you point at it.
- The machine overview says in one sentence what the tool does.

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
- The details panel reads the signature, publisher, file version, file hash, memory and
  "Required by" of its entry when it opens, instead of showing "not read" and asking you to
  turn on a column. While it reads, those lines say "reading...". A file on another machine is
  still not read, and the panel says why.
- The details panel takes a third of the window, between a lower and an upper limit, instead
  of a fixed width. While it is open, the Description column leaves the list, because the
  panel shows the description in full. The column comes back when the panel closes, and your
  choice of columns is kept.
- "What it runs" comes second in the details panel, before "About the entry".
- "Show the list" on the machine overview clears the search box, so it always shows the whole list
  rather than the answer to the last number you clicked.
- On the orphans card of the overview, the second number counts only entries whose file is gone
  and that are not set to start automatically, so the two numbers add up to every missing file.
- The sentence about the number the overview cannot count yet no longer talks about planned work.
- Export offers a file name after the tab you are on: services.csv, drivers.csv or
  services-and-drivers.csv.
- The rules for the filters are in one place, on the Filters button: two filters in one row show
  both, and rows narrow each other. The row labels no longer repeat the same tooltip.
- The row menu names its actions the way the action bar does: "Stop...", "Start...",
  "Restart...", "Force stop..." and "Force restart...". Each still opens a plan first.
- "Show the list" on the machine overview is a blue button, so it is easy to find the first time
  the window opens.
- In the plan panel, the name of the entry stands out in bold in the title and in every step.
- The search box has a magnifier and a lighter fill. Before, it was darker than the window.
- Every text box has a light blue line under it while you type in it. Before, the line took the
  accent colour of your Windows.
- A mistake in the query, or a note about something still being read, no longer takes a line under
  the search box, so the window no longer moves when you start typing. While you are in the box,
  a panel under it shows every message in full, over the filters. When you leave the box, the box
  keeps its red edge and a short form of the message at its right end.
- The column headings are semibold and grey, with a line under them, so they no longer look like
  one more row.
- A button that cannot be pressed has no fill, and one that can has a slightly lighter fill than
  before.
- The state and startup type marks in the list and in the details panel are a little larger.
- Tab in the search box writes the highlighted suggestion, the same as Enter, and keeps the cursor
  in the box - so `sta`, Tab, Tab gives `status:running`. When there is nothing to write, or the
  list shows the example questions, Tab moves on to the next control as before. While an input
  method is still composing a character, neither Tab nor Enter writes a suggestion.
- The list of suggestions opens when you type, not when you only move the cursor with the arrow
  keys or a click. Down still opens it wherever the cursor is.

### Removed

- "Copy display name" and "Copy description" from the row menu. Both are still copied by "Copy
  everything".

### Fixed

- The details panel no longer stays open over the machine overview.
- The numbers on the machine overview light up under the pointer where you can see it.
- Opening the details panel on another entry no longer shows the previous entry's "no longer
  in the listing" notice.

## [0.2.0] - 2026-09-24

First release. What the tool does is described in the [README](README.md).
