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

- **`bws --help` and `bws -h`** print how to use the tool, on standard output, and end
  successfully. Running `bws` with nothing after it does the same. The help now starts with
  three examples rather than a list of switches.
- **`bws --version`** says which version this is, and which shape of snapshot file it writes.
  The two move independently: a new build does not always change the file, and a changed file
  always matters to whatever reads it back. A log that recorded only the program version could
  not tell you afterwards which of the two had moved.
- **A mistyped command is offered the one you probably meant.** `bws lst` answers
  "There is no command lst. Did you mean list?" rather than calling it an unknown option.
- **`bws list`** shows every entry the service control manager knows about, as a table for
  a person or with `--json` for a script. Drivers are included, which is why the count is
  larger than the one `services.msc` shows.
- **`--query`** narrows the listing with a language shared by the command line and, later,
  the window. Fields: `name`, `display`, `description`, `type`, `status`, `start`, `account`,
  `pid`, `trigger`, `path` and `file`. Values combine with spaces for "all of these" and commas
  for "any of these", `!` excludes, and `none`, `any` and `?` ask whether a field is empty,
  filled or could not be read. Text fields take wildcards and regular expressions. A bare
  word searches names, display names, accounts and launch paths.
- **`bws stop`, `bws start` and `bws restart`** carry out one named entry's change through
  a plan: what will happen, in what order, and what came of every step.
- **The regex switch beside the search box is gone, and nothing was lost with it.** Put a
  word between slashes and it is a regular expression - `/^spool/` - and that was already
  the way to write one everywhere else in the query language. Without the slashes it is
  text, so a dot is a dot and a pasted path is a path. To search for text that has slashes
  in it, quote it: `"/foo/"`. The window and the command line now read this the same way,
  which they did not while the switch existed - the command line never had one.
- **Press Enter on a row and everything the window knows about that entry opens beside the
  list.** Five sections in the picker's own order, so a field is where you already looked for
  it, and every value says exactly what its column would say. **Escape closes the panel, and
  only closes the query box when there is no panel** - one press never takes both. Choosing a
  row does not open it, because the list is a tool for searching and a panel that appeared on
  every click would take a third of it away from anybody scrolling.
  - **The last section is the one that makes it honest: what this window did not read.**
    Signature, file version, file hash and memory are named there rather than left out, so a
    panel that looks complete cannot be one that quietly is not. The command line reads them
    today with `--signatures` and `--memory`.
  - A description of twelve hundred characters is readable here, wrapped, which is the one
    place in the window it is not a single trimmed line.
- **Ctrl+C over the list copies everything about the chosen entry**, as a label, a tab and a
  value per line - ready for a ticket or a spreadsheet. It copies the whole catalogue rather
  than the columns that happen to be on, because the ones that are off by default are the long
  ones worth pasting. The same thing is in the right-click menu as **Copy everything**, next to
  **Copy description**, which the menu gained at the same time.
- **The search box tells you what goes in it.** Point at it and it says what can be typed, that
  a word between slashes is a regular expression - `/^win.*svc$/` - and six questions to start
  from. **The Examples button is gone**, and its six questions are these: the language is now
  explained by the box it is typed into rather than by a control beside it.
- **You choose which columns the list shows.** The **Columns** button beside the filters opens
  a list of eighteen, six of them on to begin with. Twelve are things the window could read
  all along and had nowhere to put: what it says about itself, what kind of entry it is, the command it launches and the
  file that command really runs, what it depends on, what starts it, which privileges it asks
  for, its SID type, its error control, its load order group, its security descriptor, and
  whether it is set to run and is not. Drag a heading to move a column and its edge to resize
  it.
- **The list opens the way you left it.** Which columns are on, the order you dragged their
  headings into and any width you dragged yourself are kept between sessions, in
  `%APPDATA%\BetterWindowsServices\bws-preferences.json`. It is a small text file you can read,
  copy to another machine or delete - deleting it brings back the six columns the window starts
  with. A column you never touched keeps following the theme rather than being frozen at
  whatever it happened to be that day. If the file cannot be read - hand edited into something
  that is not, or written by a newer version of this program - the window says so in the line
  under the search box and shows the usual columns rather than guessing. A file it cannot read
  is moved aside with the date in its name, never overwritten, and one from a version it does
  not know is left exactly where it is.
- **Every entry now carries its description - the sentence that says what it is for.** It is the
  one column `services.msc` has that this tool did not, and it is the only answer to "what even
  is this" on a machine you have never seen. Turn on the **Description** column in the window,
  narrow the list with `--query description:printer`, or read it from `bws list --json`. Where an
  entry has no description at all the cell is empty, which is most drivers. Where Windows has one
  but cannot turn it into words, the tool says so rather than showing you the file path it failed
  to read - eight entries on the machine this was measured on. It is not a column in the terminal
  table, because a description is a paragraph and the table has seven columns to fit.
- **A snapshot comparison now tells you when it could not compare something.** If neither side
  could read a field, it is listed as not fully compared instead of counting as unchanged.
- **Searching with `*` now works on text that runs over more than one line.** Before, a wildcard
  silently matched nothing against such a value.
- **Turn on more columns than fit and the list scrolls sideways, with the first column staying
  put.** Before this, the columns beyond the width of the window did not go anywhere - they were
  squeezed until seven of them were twenty pixels wide, headings included, and nothing said so.
  Each column now keeps the width it was measured for and the row gets wider than the window
  instead. The leftmost column stays where it is while you scroll, so a row still says which
  service it belongs to when you have scrolled out to its security descriptor. With the six
  columns the window opens with, nothing changes: they fill the width as before and there is no
  scrollbar.
- **Clicking a column heading sorts the list**, and clicking it again reverses.
- **Pressing a letter while the list has focus jumps to the next entry beginning with it**,
  the way `services.msc` has always done it. Press it again to walk through the rest.
- **The dots beside a start type say what they mean** when you point at them, and the one
  for "this could not be read" is now a broken ring rather than a slightly different shade
  of the ring used for "disabled".
- **The title bar is dark**, like the rest of the window.
- **The search box no longer changes size while you type in it.**
- **After a write, the report ends with how to get back to where it started.** `bws stop
  Spooler` finishes by telling you that `bws start Spooler` puts it back, and a cascade
  hands the commands over in the order that works - the entry the others depend on first.
  Nothing is undone for you and nothing is remembered between runs: these are the commands
  you would type. A run that ended where it began, such as a restart that worked, says
  nothing here, because there is nothing to put back.
- **`--dry-run`** on every write command shows the plan and changes nothing. It offers no
  way back either, because it moved nothing.
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
  - **The filters are a row of chips under the box, in three groups.** Each one stands for one
    member of the query and clicking it writes that member into the box, where you can read it,
    edit it or copy it into a terminal - and typing the member yourself lights the chip. Chips
    in one group add up, chips in different groups narrow each other, and the label over each
    group says which. *(This began as two switches called `Regex` and `Drivers`. The first is
    gone because slashes already said it, and the second is now one of the chips.)*
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

- **The window opens with kernel drivers hidden**, which is what `services.msc` does and what
  people compare this against. It is not a hidden setting: the member `!type:driver` is in the
  search box where you can read it, the **Hide drivers** chip is lit, and the count says how
  many were held back. One `Escape`, or one click on the chip, gives you the whole machine
  back. The command line is unchanged and still lists everything.
- **`Escape` now backs out of the innermost thing first.** With the details panel open it
  closes the panel and leaves your query alone. With no panel it empties the box, as before.
  One press never does both, because a query costs more to type again than a panel does to
  reopen.
- **The window's list can be scanned instead of read.** Status and start type now carry a
  coloured mark beside the word rather than being one more column of grey text: filled means
  the service is doing something, hollow means it is not. Process ids line up on their last
  digit. Column headings are heavier and a size larger than the rows under them, and a line
  runs between one row and the next so a row can be followed across the window.
- **The window can be used without a mouse.** `Ctrl+F` puts the cursor in the search box and
  selects what is there, `Escape` empties it, and the arrow keys walk the list. `F5` still
  reads everything again. A key the window has no use for is passed on rather than swallowed.
- **The row the keyboard is on is now visible.** It carries an outline, which nothing in the
  list had before - the list showed which row was selected but not which one the arrow keys
  would move from.
- **Right-clicking a row offers to copy its name or its display name**, and the menu key on
  the keyboard opens the same menu. It selects the row you pointed at first, so the name you
  get is the row you clicked. Nothing in this menu changes a service.
- **A selected row, a row under the pointer and a row that just changed now look different
  from each other.** They were meant to since the window moved to its current control
  library and did not: the colours were set in a way the library's own row template
  overwrote on every row, so the list drew them in the library's colours - with selected and
  hovered sharing one colour - or, for a row that had just changed, not at all.
- **Something the window cannot do is now said out loud.** The clipboard belongs to whichever
  program took it last, so copying genuinely fails sometimes. It reports that under the list
  instead of doing nothing, and the message stays until you ask for something else.
- **An empty list now says why it is empty.** There were four different reasons for it and one
  blank rectangle: still reading, nothing matched what you asked for, the machine handed over
  nothing at all, or the list could not be read. Each says which, in the middle of the window,
  and the ones you can do something about say what - Escape to empty the box, F5 to read again.
  The column headings stay where they are while it does.
- **The entry count moved up beside the search box and got bigger.** It answers every keystroke
  you make there, and it used to be the smallest grey text in the bottom corner. It also thickens
  while a filter is on, so you can see that the list is narrowed without reading the number.
- **The window now tells you when it is running without administrator rights**, and tells you
  before anything else it has to say. It matters more than it sounds: without them Windows hands
  over a shorter list and refuses some of what it does hand over, so the count you are reading is
  not the whole machine. Every other note under the search box is about a list you would otherwise
  assume was complete.
  - **The start type now says what changes its meaning**, the same way the command line
    already did: delayed, on trigger, or file missing. A stopped automatic service that is
    waiting to be asked for is no longer indistinguishable from one that failed to start.
  - Text that does not fit ends in an ellipsis and carries the whole of itself in a tooltip.
    It used to be cut in the middle of a character, which read as something being broken.
  - The smallest text in the window went from 11 to 12 pixels, which is the smallest size
    Microsoft's guidance considers readable. It carries the entry count and both messages.

- **The PID column no longer cuts five-digit process ids in half.** It showed `14(` instead
  of `14052` in an ordinary window. Every column now has a width chosen for what it has to
  hold rather than for whatever happened to be on the first screenful.

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

- **Filters in one group now all light up at once.** Clicking Manual, then Disabled, then Boot
  left only one of them lit, while the list correctly showed all three. The list, the count and
  the command line were right the whole time - a query keeps both what it matches and how it
  was written, and collapsing repeated mentions of a field kept the first and dropped the rest.
  Only a clickable filter ever asks how a query was written, so nothing else could see it.
- **The message in an empty list no longer flickers.** With a query that matched nothing, the
  sentence saying so was replaced by "reading the manager" and put back once a second, because
  the list is read once a second and a list with no rows answered as though it had never been
  read. It now stays put while you read it.
- **The PID column sorts as numbers.** Clicking its heading put `103292` before `9`, because
  the column was ordered as text. Every column is now ordered by what it means rather than by
  how it is written.
- **A column heading too narrow for its name ends with an ellipsis** instead of being cut in
  the middle of a word. Cells have given way like this since the widths were measured, and
  headings had been left out - which nobody could see while every heading was short.
- **A command that runs much longer than `--timeout` now says why.** That switch caps how
  long the tool waits once the service manager has accepted a request, and it cannot cap the
  manager's own answer - which takes tens of seconds when a service never reports itself.
  `bws start X --timeout 1` could therefore run for half a minute, report the truth, and look
  exactly like a switch that does nothing. The report now names the step, what it took, and
  where the time went. `--timeout` also has an explanation in `--help`, which it never had.
- **A running service is no longer reported as having lost its binary.** When a service is
  registered without a file extension - `svchost -k TSLicensing` rather than `svchost.exe -k
  TSLicensing` - the tool could not find the file and said it was missing, while Windows was
  running the service from it perfectly well. Windows adds `.exe` to a name that has none,
  and the tool now does the same. Remote Desktop Licensing is registered this way on Windows
  Server, so `--query "file:missing"` there answered with one entry that was fine.
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
- **A long search with many `*` characters no longer ends the program.** A query with around
  a thousand wildcards used to end with an error message from inside .NET and an exit code
  that is not in the table. It now says the wildcard has too many parts and asks for fewer.
- **A search that says nothing is refused instead of returning everything.** `--query ""`
  and `--query name:""` used to answer with every entry on the machine and a success code,
  so a script with a typo in its query got the whole machine and a green light. A half-typed
  member such as `status:` is still fine - that is what a search box holds while you type.
- **A switch is no longer swallowed as another switch's value.** `bws list --query --json`
  used to search for the text `--json`, print an empty table and end successfully, with the
  switch you actually typed silently gone.
- **A damaged snapshot is answered with a sentence naming the file.** A file with an empty
  entry, or one naming the same service twice, used to end the comparison with a message
  from inside .NET that named neither of the two files being compared.
- **A launch path written with forward slashes is recognised as being on another machine.**
  `//server/share/x.exe` was treated as local, so the tool would reach for it - which can
  block for twenty seconds on an unreachable host and connects as whoever ran the tool.
  `--follow-network` is still how you ask for that on purpose.
- **A disk problem is no longer reported as your typo.** Reading a snapshot from a share that
  went away, or a file another program is holding open, now ends with the code that means the
  tool hit a problem rather than the one that means you mistyped the path.
- **Ctrl+C just after a plan finishes no longer risks ending the run without its report.**
- **The list no longer shows the same service twice** after a refresh in which two rows
  changed places.
- **A file you are about to replace is kept when it cannot be read as a snapshot.**
  `snapshot create --force` over something that is not a snapshot now moves it aside under
  its own name plus a timestamp, and says where it went, instead of destroying it. A file
  that IS a readable snapshot is replaced, because that is what asking for `--force` means.
- **The name the tool picks for you is protected too.** Without a file name it works one out
  from the machine and the moment, and that path used to overwrite whatever was already
  there without asking.
- **One refusal now has one number.** `unreadable.errorCode` in the JSON output carried the
  Windows number for some refusals and a .NET number for others - the same access denial was
  `5` in one place and `-2147024891` in another. A script keying on the number now works
  everywhere. **If you match on that field, check your values.**
- **A search about something the tool has not read says so.** Asking about a publisher when
  signatures were not read used to drop those entries silently. The result now reports
  itself as partial, the way a search about a refused field already did.
- **A query that narrows nothing is refused on the command line.** `--query "status:"` and
  `--query "="` used to answer with every entry on the machine and a success code, so a
  script with a typo in its query got the whole machine and a green light. They now end
  with the code that means the command was wrong. **This is a change in behaviour: if you
  have a half-written query in a script, it will start failing - which is the point.**
  The search box in the window is unchanged, because it holds `status:` between two
  keystrokes while you type.
- **An option given twice is refused.** `--query a --query b` used to search for `b` and say
  nothing about `a`. Applies to `--query`, `--note` and `--timeout`, which carry a value. A
  plain flag repeated still means what it meant once.
- **The listing table lines up when a name holds an unusual character.** A display name
  containing something stored as two units - an emoji in a product name, a rare ideograph -
  used to push every row after it in that column two spaces out. Characters from the Chinese,
  Japanese and Korean ranges take two columns on screen and are still counted as one, so a
  listing on those systems can still drift.
- **A snapshot gets the same file name whatever calendar your Windows uses.** Without a file
  name the tool works one out from the machine and the date, and it used to write that date
  in the system's calendar - so the same moment produced `20260803` on a Polish or American
  machine and `25690803` on a Thai one, while the date inside the file stayed the same. The
  name and the contents disagreed, and the files stopped sorting by time.
- **A snapshot that is not UTF-8 is refused instead of being guessed at.** Opening one in an
  editor set to your system's code page and saving it used to make the next comparison report
  hundreds of entries as changed when nothing had. Files saved as UTF-8 or UTF-16 with a byte
  order mark still read normally.
- **When a snapshot cannot be read, the message says which file and what is wrong with it**,
  rather than describing "the file" without naming one. Comparing two snapshots reads two of
  them, so a message about neither left you checking both.

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
