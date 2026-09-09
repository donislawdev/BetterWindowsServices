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

- **The preview says when a plan would take down something the machine needs.** Seven entries -
  the two halves of RPC, the account manager, key isolation, the session manager and plug and play
  - are named on the sheet before you press anything: *PlugPlay is one this machine does not work
  without. Stopping it takes the machine down, not just the service.*
  - **It says so on an ordinary stop or restart, not only on a forced one.** Until now that
    sentence appeared only where a plan ended a process, so `bws stop PlugPlay` said nothing about
    it.
  - **A cascade is named too.** Asking to stop something with `--dependents` can take down an entry
    you never typed, and that is the case with the least warning attached to it.
  - **Disabling one has its own sentence**, because the harm lands later: *the manager will not
    start it at the next boot - not on demand either - and the machine comes up without it.*
  - **In the window, those plans ask you to type the entry's name** before the button comes alive,
    the same confirmation a plan that ends a process already asked for.

- **The window says how long the step it is on has been going, and lets you change how long it
  waits.** *Step 1 of 3: stop Spooler - 12 s of 60*, and a box on the plan reading *Wait up to
  [60] seconds for each step*.
  - Sixty seconds unless you say otherwise, which is what the terminal has always used.
  - **The box is only on plans that have something to wait for.** Setting a start type moves no
    service, so there is no state to arrive at and no box.
  - It goes off screen while the run is happening, because the number is read when you press.

- **`bws` says when it is not running as an administrator.** A session without those rights is
  handed fewer entries by Windows and refused more of what it does get, so the answer is not the
  whole machine - and nothing in the tool can say what is missing from it. The window has said this
  since it could plan anything, and the terminal now does too.

- **`bws --help` says what a snapshot holds.** Every launch path and file hash, the account each
  entry runs as, its privileges and its security descriptor, plus the name of the machine and of
  the account that took it - written with whatever permissions its directory gives it. A directory
  other people can read is one they can read all of that in.

### Fixed

- **The window publishes as one file.** A self-contained single-file publish used to leave
  `Bws.Gui.exe` beside five native libraries belonging to Windows Presentation Foundation, about
  eight megabytes of them - so copying just the executable to another machine copied a program that
  was missing part of itself. Measured: it costs nothing noticeable at start-up, including on the
  first run of a new version, which is the run that unpacks them.

- **A file that is not a snapshot is refused in this tool's words.** The message used to carry the
  reader's own, including a clause about `isFinalBlock` and a tail reading
  `Path: $ | LineNumber: 0 | BytePositionInLine: 0`. It now says what is wrong and where, with the
  line and position counted from one the way an editor counts them.

- **The program has an icon.** A bean, in the same green the window uses for a service that is
  running. It shows in the title bar, on the taskbar, in Alt+Tab, in the Start menu and beside the
  file in Explorer, for both the window and the command line program.
  - **It is drawn at ten sizes rather than the five Windows asks for as a minimum**, so that at
    125, 150, 250 and 400 per cent display scaling Windows finds an exact match and never has to
    resize one for you. On a display at 150 per cent - a common setting - the taskbar asks for 36
    pixels, which is a size most programs do not supply.
  - **It holds up on a light taskbar and on a dark one.** The dark outline carries the shape
    against a light background and the green fill carries it against a dark one.

- **The window offers a forced stop, and only where one is called for.** A stop that gives up, or
  that Windows refuses outright, now carries a way out under its own sentence: *Force stop...* -
  or *Force restart...* when it was a restart that failed. Nothing is ended by pressing it. It
  opens a fresh plan naming the process and everything that would go with it, exactly as the
  terminal's `bws kill` does.
  - **There is no other way in.** Force stop is not in the action bar and not in a row's menu, so
    ending the process behind a healthy service is not something you can do by mistake.
  - **The button names what it will do:** *End process 1408*, rather than "carry this out".
  - **Where the process holds more than the one service you picked - or holds a service this
    machine does not work without - the entry's name has to be typed before the button comes
    alive.** On this machine 105 of 110 service processes hold exactly one service, so most of the
    time there is nothing extra to type.
  - **Enter cannot end a process.** The keyboard lands on the box you have to type into, or on the
    way out when there is none - never on the button.

- **`bws kill NAME` - for a service that will not stop.** It asks politely first and ends the
  process behind the entry only if that does not work, so a service that stops on its own is never
  ended. Both steps are in the preview and the second one says it is conditional.
  - **The preview names the process by number, and everything else living in it.** Ending a process
    takes every other service in it with it, whether or not they stopped first - so those are steps
    too, asked to stop politely before the process goes away, and the plan says what happens to the
    ones that do not.
  - `--force` skips asking politely. It changes the plan rather than the run, so the preview shows
    one step instead of several and you see the difference before anything happens.
  - `--restart` brings the entry back once the process is gone, along with anything that shared it.
  - **Entries this machine does not work without are warned about, not refused.** The warning says
    ending them stops the machine rather than the service. You are still allowed to do it.
  - Refused outright where there is nothing to name: no process, a process number no service ever
    has, or a list of what dies that Windows would not let us read in full.
  - `--dry-run` works here like everywhere else, and this is the one command where the preview
    matching the run matters most.

- **A stop that was never going to be accepted now says so before you press anything.** Some
  services do not take a stop at all - Windows refuses the request outright instead of trying - and
  until now that only showed up afterwards, as an error number. The preview names it: *"Dnscache is
  not accepting stop requests, so the stop will be refused rather than time out."*
  - It names the entries **in the way** as well as the one you asked about. One of those refusing a
    stop is what makes the rest of the plan unreachable, and that is worth knowing first.
  - A service that is simply already stopped is **not** described this way. It is not refusing
    anything - it has already arrived.
  - `bws stop NAME --dry-run --json` carries it as `"kind": "doesNotAcceptStop"` beside the
    sentence, so a script can branch on it without reading English.

- **When the tool gives up waiting, it names the process still holding the service.** *"gave up
  after 60 s, still stopping, held by process 4812"* in the terminal, and the same fact in the
  window's plan sheet. A service stuck part-way through stopping will not move because you ask it
  again, so the process is the only thing left to look at.
  - Every result in `bws stop NAME --json` now carries `processId` beside its status, so a runbook
    can read the number instead of matching the sentence. It is `null` where there is no process,
    and the fields beside it - `skippedBecause`, `errorCode` - say which kind of nothing it was.

- **Right-click a column heading to narrow the list to a value, or to put that column away.** On a
  column the query language knows values for - Status, Start, Signature, Entry type, Triggers and
  the rest - the menu lists them, and clicking one writes it into the search box where you can read
  and edit it, exactly as the filter buttons do. Ticking two shows both.
  - On a column with no values to offer, the menu still opens and offers to hide the column.

- **"Required by" - what breaks if you stop a service.** Windows is asked directly rather than
  guessed at from what everything else declares, so services grouped by a load order name are
  counted too.
  - A column in the window, off until you turn it on, because it costs a call per entry.
  - `requiredby:spooler` in the search box or on the command line, and `dependson:rpcss` for the
    direction the tool could already show and could not be asked about.
  - `bws list --required-by` fills it for a listing. `bws show` and `bws snapshot create` read it
    every time, without a switch.

- **The window opens on what the machine looks like, not on several hundred rows.** On a profile
  that has never put it away, the first thing you see is a handful of numbers rather than an
  alphabetical list - and clicking any of them puts its question in the search box and shows you
  the matching entries.
  - **"Did not come up" means what it says.** The obvious query answers 7 or 8 on the machine this
    was built on and only one of those is actually down. The headline is that one, and the rest are
    named under it: per-user templates, which never run because a session copy runs instead, and
    entries waiting for a trigger, which are stopped because nothing has asked for them. Both are
    clickable, so nothing is hidden.
  - Orphans - set to start automatically with the file gone - are counted, and beside them the
    entries that merely point at a file that is not there, whatever their start type. On this
    machine that is 0 and 3, which is why both are shown.
  - **How many entries are not from a stock Windows is not counted, and the screen says so.** It
    needs the stock baseline, which arrives later. A number it cannot stand behind would be worse
    than no number.
  - **Overview** in the bar over the list brings it back at any time.

- **Per-user services are folded under the template they came from**, in the window. Windows makes
  a copy of some services for every session that logs on, with a random-looking tail on the name -
  on the machine this was built on that is 23 entries out of 798, and on a machine with several
  people logged on it is that many times over.
  - The copies are drawn under the template, which carries a count beside its name - "1 instance",
    or "4 instances, 2 running". The template itself never runs, so the count is what tells you
    whether the family is working.
  - **Show every instance** puts them all back on rows of their own.
  - The line under the list says how many were folded, so the number over the list and the number
    of rows you can see never disagree in silence.
  - **Acting on a folded row acts on the whole family, and the plan says so before anything runs.**
    Picking one and pressing Stop opens a plan naming the template and every copy under it - a
    preview shorter than the run is the one thing a plan must never be.

- **`bws show NAME`**, on the command line: everything the tool knows about one entry, in four
  sections. The command line half of what the window shows in its details panel - and it reads
  more, because over one entry the expensive parts are cheap.
  - It reads the signature, the required privileges, the security descriptor and the memory every
    time, without being asked. Over one entry that costs about sixty milliseconds. Over the whole
    machine the same reading costs a second, which is why the listing still asks first.
  - Fields that are genuinely empty are left out, so a report is what there is rather than a wall
    of "none". **`--full` prints those as well.** A field nobody could read is printed either way,
    in both modes - leaving one out would look like an answer.
  - `--json` gives the same document `bws list --json` gives for that entry, on its own rather
    than inside an array of one.
  - A name that is not there ends with code 2 and a sentence naming it, the same as `bws stop`
    does. A query that matches nothing still ends with code 0, because an empty answer is an
    answer and a name that does not exist is a mistake.

- **`peruser:` in the query language**, in the window and on the command line alike. Windows makes
  a copy of certain services for every signed-in session, and what the manager holds is two
  different things wearing nearly the same name: a **template**, which never runs, and one
  **instance** per session, which does the work.
  - `peruser:no` leaves them out, `peruser:yes` shows only that family, and `peruser:template` or
    `peruser:instance` picks one side of it.
  - **This changes an answer you were already getting.** Asking for automatic entries that are not
    running counted every template, because a template is automatic and never runs. On the machine
    this was measured on that question answered 8, four of which were templates whose instances
    were running beside them. Adding `peruser:no` answers 4.
  - The tool tells the two apart by what Windows marks them as, not by the suffix in the name. On
    the same machine the name would have been wrong about two entries out of 798 - and one of the
    two it would have filed away as session noise is the machine's power service.

- **Start type...**, in the bar over the list: Automatic, Manual or Disabled for the entries you
  picked. Like everything else in that bar it shows you a plan first - the title says what it
  would set and to what - and nothing changes until you press Carry this out.
  - Boot and System are not offered: they belong to drivers, which this tool does not operate
    on. Automatic (delayed) is not offered either, because it is a separate setting rather than
    a start type.
  - **There is a way back.** Once a start type has been set, the panel tells you what to type
    to put the entry back where the run found it, the same way it does after a stop or a start.
  - **One case says nothing instead, on purpose.** An automatic entry can also be marked to
    start late, after the boot rush, and neither this window nor the command line has a word
    for that state - so after changing the start type of one of those, the panel offers no way
    back rather than a line that would leave it starting at boot instead of after it.

- **`bws start-type NAME automatic|manual|disabled`**, on the command line. The same change the
  window makes, with the same plan in front of it and the same `--dry-run` as every other write.
  - `--json` and `--timing` work here as everywhere. `--dependents` and `--timeout` do not, and
    the tool says so rather than ignoring them: nothing comes down with a setting and there is
    no state to wait for.
  - The plan in `--json` now carries a `startType` field on every step and every result. It is
    the type that step writes, and `null` on a stop, a start or a restart.
  - The panel in the window shows the line to paste beside a start type change, which it could
    not do while there was no verb for it.

- **A selection full of drivers no longer fills the panel with the same sentence.** Refusals that
  say the same thing arrive as one line with a count and every name on it, instead of one line
  each. Refusals that name other entries - a cascade standing in the way, an entry that could not
  be started again - are still listed one by one, because each of those is a different fact.

- **Export...**, beside Refresh, writes what the list shows to a CSV file. What is on screen is
  what lands in the file: the entries the scope and your search left, the columns you have on,
  in the order you dragged them into and the order you sorted by. Values carrying a comma, a
  quote or a line break are quoted the way the format says, and the file starts with the mark
  that stops a spreadsheet guessing the encoding wrongly. If you want stable field names
  rather than the headings you see, `bws list --json` is the one to use.

- **Restart as administrator**, beside the line that says the session has none. Windows asks you
  to confirm, and answering no leaves the window you already had - it says so rather than
  closing anyway. The button is only there when the session actually lacks the rights. Your
  columns and the order you sorted them in come back on their own - anything typed in the
  search box does not.

- **The list opens in the order you left it in.** Click a heading, close the window, open it
  again - the same column, the same direction. Each of the three lists remembers its own, so
  sorting drivers by start type does not reorder your services. A column you have since turned
  off is not sorted by: the file keeps it, and turning the column back on brings the order back
  with it.

- **A bar over the list: Stop..., Start..., Restart... and Refresh.** Until now the only way to an
  operation was the right mouse button, which you had to know about. The three dots are the promise:
  every one of them shows you a plan first and changes nothing until you press Carry this out. They
  are off while nothing is picked, and say so if you rest on them.
- **The filters fold away.** The **Filters** switch at the left of that row hides the chips and gives
  the room back to the list - measured on a 1050-pixel-high window, the first row moves up by about
  five rows' worth. Nothing is hidden by folding them: a filter you clicked is written into the
  search box, so it is still on screen as text. They start open, and the fold is not remembered
  between runs.

- **Restore the usual columns**, the last item in the **Columns** list. What you choose there is
  kept and comes back the next time you open the window, so turning on a handful of columns to
  look at something once used to leave them there - with no way back except turning each one off
  again. There is deliberately no "turn them all on": the signature columns send the window to
  open every binary on the machine, which takes seconds rather than milliseconds.
- **A greyed-out Carry this out says why it is greyed out.** Rest on it and it names the first
  thing in the way: no administrator rights, a run already under way, a plan that has already
  been carried out, or a plan with nothing in it that could be done.

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
- **The list takes more than one row.** Shift and Ctrl pick a range or a scattered handful,
  the way every other list in Windows does. A right click on a row you have not picked
  selects that one instead, and a right click inside your selection keeps it.
- **Copying acts on everything you picked.** Ctrl+C and all four items of the row menu now
  work on the whole selection rather than on one row, one entry under another. An entry with
  nothing to say in that field is left out rather than pasted as a blank line.
- **The row menu shows what an operation WOULD do, without doing it.** Pick some entries and
  ask what stopping, starting or restarting them would do: you get the steps in the order
  they would happen, which entries come along that you did not pick, which of your entries
  cannot be operated on and why, and a warning for anything worth knowing first - a service
  that starts automatically and would come back after a restart, one that shares its process
  with others, or dependants that are in the way.
- **And the command lines that ask for the same thing**, one per entry, in the order that
  works - ready to paste into a terminal or a runbook. They are written by the same part of
  the tool the command line itself uses, so they are commands it really accepts.
- **And a button under the plan that carries it out.** The panel opens by telling you
  nothing has happened, and nothing does until you press it - the menu items stay worded as
  questions, so the only road to a change goes past the steps you just read. While it runs
  the window stays usable and says which step it is on, and a second button stops it before
  the next one. Closing the window during a run does not abandon it: the window waits, and
  anything the run took down on the way is put back first.
- **Afterwards it says what did not work, and how to get back.** Every entry that would not
  move is named with the reason the system gave, and under it are the commands that would
  put the machine back where the run found it - worked out as where each entry started
  against where it ended, so a restart that finished where it began offers nothing.
- **It refuses when it cannot do it, before you press anything.** Running without
  administrator rights, the button is dead and the panel says why and what to do about it -
  rather than letting you press it and handing back a column of refusals from Windows.
- **The panel is its own surface now**, with a heading you can find, sections that appear only
  when they have something in them, and the button joined to the plan it acts on rather than
  floating at the bottom of the window.

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
  a list of twenty-six, six of them on to begin with. Twelve are things the window could read
  all along and had nowhere to put: what it says about itself, what kind of entry it is, the command it launches and the
  file that command really runs, what it depends on, what starts it, which privileges it asks
  for, its SID type, its error control, its load order group, its security descriptor, and
  whether it is set to run and is not. Drag a heading to move a column and its edge to resize
  it.
- **The window can now tell you who signed a service's file, and what it is using.** Five more
  columns: the signature, who signed it, the file's own version, its hash, and how much memory
  the running process holds. They are off to begin with, and turning one on is what sends the
  window to look - opening several hundred files takes a moment, so it is not done for people
  who never ask for it. Until then the columns say nobody has looked, rather than showing you a
  blank that reads like "there is nothing here".
- **The search box can ask about signatures too, and so can the filter buttons.** `signed:no`
  finds what Windows would not run quietly - unsigned, expired, revoked or altered since it was
  signed. Three buttons under **Signature** ask the same thing without typing: trusted, not
  trusted, and the one that matters in an audit - **could not check**.
- **A service that disagrees with itself can now be found, in both directions.** The window has
  shown "set to run and is not" as a column for a while and there was no way to search for it.
  There is now, and the opposite case has been given a name of its own: a service switched off
  that is running anyway. `mismatch:stopped` and `mismatch:running`, or the two buttons under
  **Disagrees with itself**. They are kept apart on purpose, because what you do about them is
  opposite: one you start or investigate, the other you decide whether to stop or re-enable.
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

- **The snapshot format is version 4.** Snapshots now carry what depends on each entry. A file
  written by an older build is refused by name rather than half-read, so take a fresh snapshot of
  anything you want to compare against from here on.

- **The opening screen is three cards rather than a column of lines.** Each card carries one number
  large enough to read across a desk, the words it counts at full strength, and underneath them
  whatever has to be said about that number - the per-user templates and the trigger-started entries
  left out of "did not come up", and the entries pointing at a missing file beside the orphan count.
  Every number on a card is still a question you can click.

- **A filter and a button no longer look the same.** They did the same thing to within one pixel: the
  same rounded rectangle, the same height, the same size of type - for a chip that changes your
  question and a button that opens a plan to stop a service. A filter is now an outlined pill and a
  button is a filled rectangle, so the two rows read as two kinds of thing without being read.

- **The button that matters most on a screen is the only coloured one.** When the tool is running
  without administrator rights, **Restart as administrator** is the only way forward and it used to
  look like the seventh button in a row. It and **Carry this out** in the plan now carry the one
  saturated colour in the window, and nothing else does.
  - **Show the list** on the opening screen gave that colour up. It is the way off the screen rather
    than what the screen is for, and the loudest thing on it was pointing at the door.

- **The plan panel separates what stays from what scrolls.** A line of text cut in half by the edge
  of the scrolling area, with nothing marking that edge, read as two sentences overlapping. There is
  a rule there now - and the warning about missing administrator rights moved down beside the button
  it explains, instead of sitting three sections above it.

- **The scrollbar is wider.** The thumb was about three points across, which is hard to catch with a
  pointer. It is now twelve. Its length is still set by how much of the list fits on screen.

- **What "Show every instance" does is the first thing its tooltip says.** It used to open by
  explaining how Windows makes per-user copies and left what the switch does until the last sentence.

- **A signature and a trigger are said in words on the command line.** The listing and `bws show`
  say `Not signed` and `Device arrival` where they used to print the internal name of a value.
  - **`bws list --json` and snapshots are unaffected and will stay that way**, for the same reason
    the entry below gives about pending states: that is what a script matches on.

- **The numbers on the opening screen no longer touch the words beside them.** It read
  "113services running" and "0orphans" on every line.

- **On the command line, a state that takes two words is now written as two words.** The listing
  and `bws show` say `Start pending` where they used to say `StartPending`, which is how the window
  has always written it. The same goes for `Stop pending`, `Continue pending` and `Pause pending`.
  - **`bws list --json` and snapshots are not affected and will not be.** They still carry
    `StartPending`, because that is the name a script matches on and a saved snapshot compares
    against. If you parse the tool's output, parse the JSON.

- **Sentences that count something now say it in the singular when there is one of it.** The
  command line used to answer `Read 1 entries in 12 ms` and `1 entries were judged on a field that
  could not be read`. Eleven sentences were affected, including two that said "Those keep running"
  and "these drivers" about a single one.
  - **The line about an incomplete comparison is now up to three lines instead of one.**
    `snapshot diff` used to print one sentence holding both of its counts, which meant it also
    printed the half that was zero. Each half now gets its own line, and only when there is
    something to say.

- **The list now scrolls a row at a time instead of sliding between rows, and it does that to stop
  eating a processor core while you drag.** Scrolling used to spend two to three times more
  processor per step than it does now, measured over a list whose rows are already on screen, and
  on a machine with sixteen processors that showed up as the whole window using about five percent
  while a scrollbar was being dragged slowly.
  - **What you will see instead:** the scrollbar comes to rest on a row boundary rather than
    halfway through one, so a slow drag steps rather than glides. On a list of a few hundred
    entries a step is a row or two. On the full list of every entry the machine has, it is more.
  - **The first pass through a list you have not looked at yet costs the same either way.** This
    only helps once the rows have been on screen once.

- **Snapshots are now written in format version 3, and a snapshot written by an earlier build is
  refused rather than read.** No field was added or renamed. What changed is what four of them
  say: half the values in the document were written in one spelling and half in another, so one
  file carried `OwnProcess`, `Running` and `Automatic` beside `unrestricted`, `normal`, `trusted`
  and `deviceArrival`. They are all written the way the tool's own text output writes them now.
  - **The same command used to give you two spellings of one value.** `bws show Spooler` printed
    `Service SID type: Unrestricted` while `bws show Spooler --json` printed
    `"sidType": "unrestricted"`. That is gone, in the listing, in `show` and in a snapshot alike.
  - **This affects `bws list --json` and `bws show --json` as well as snapshot files**, because all
    three are the same document. A script comparing `sidType`, `errorControl`, `signature.status`
    or a trigger's `kind` or `action` against a lower-case word has to be updated - and the four
    other values it may compare are unchanged.
  - **Searching is unaffected.** `sidtype:unrestricted` and `status:running` read the same as they
    always did, in the window and on the command line. The query language has never matched the
    listing's spelling and does not start now.
  - The refusal names the reason: the file uses version 2 and this build reads version 3. Nothing
    has been released yet, so no snapshot written by a published build exists.

- **Snapshots are now written in format version 2, and a snapshot written by an earlier build is
  refused rather than read.** The entries carry one more field - which side of the per-user family
  each one is on - and every field in that format is required, so a file without it is not a
  version of the same document.
  - The refusal names the reason: the file uses version 1 and this build reads version 2. That is
    the whole of the change you will see, and it happens before anything is compared.
  - **The alternative was worse and was rejected on purpose.** Letting the older file through
    would have put an empty value on one side of every comparison, and reported a change on 46
    entries of 798 that nothing on the machine had touched. A comparison that invents changes is
    the one thing this tool must not do.
  - Nothing has been released yet, so no snapshot written by a published build exists.

- **The plan panel calls a service what you call it.** The title of a preview read "What
  stopping pla would do", naming the entry the way the service manager does. It now carries the
  display name - "Performance Logs and Alerts" - with the manager's own name on the line under
  it. Both are there on purpose: the display name is the one you recognise, and the internal name
  is the one you would type into a command.
- **Counts read as English when there is one of something.** The line under the search box said
  "1 entries" on a list holding one, and the same fault stood in two of the sentences the window
  says about an answer. It also lost its full stop: it is a label rather than a sentence, and it
  reads "810 entries" now.

- **Services and drivers are two separate lists**, chosen with **Services**, **Drivers** and
  **Everything** above the search box. The window opens on Services, which is what `services.msc`
  shows and what people compare this against. The search box opens **empty**: the choice of list
  is not a search term, so `type:` now narrows *within* the list you are on rather than choosing
  between lists. Each list remembers its own columns, because a driver has no process id and no
  delayed start and a column of blanks is worse than no column. The command line is unchanged and
  still lists everything.
- **A tooltip over a cell now appears only when the text did not fit.** It is there so that text
  which had to be cut short is still readable in full, and one over a fully visible word repeated
  what was already on screen and covered the row underneath it.
- **The list stops asking the machine for a moment while you are scrolling it**, the way it
  already did while you are typing. Values catch up a quarter of a second after you stop. Nothing
  is hidden and nothing is stale for longer than that.
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

- **`bws kill` said "is a driver" about services that are not drivers.** A stopped service has no
  process to end, and instead of saying so the tool reached for the nearest sentence it knew. It
  now says what is actually true: there is no process to end, either because the entry is not
  running or because the number Windows gave is not one a service process can have. The same
  wording was missing in the window and is there now.

- **`bws kill` on an entry whose dependants could not all be read** said the same wrong thing.
  It now says that the list of what would go with the process is known to be short, which is why
  there is no plan to offer.


- **Columns that could only ever say "unknown" now fill in when you turn them on.** Memory,
  Signature, Publisher, File version and File hash are read from the machine only when something
  asks for them, and until now the only thing that could ask was a query typed into the search box.
  Turning the column on told nobody, so it showed "unknown" on every row for as long as the box
  stayed empty. Showing a column is now a way of asking.
  - The line under the list says which of them is being fetched while it happens, so a column that
    costs several seconds no longer stops the list without explaining itself.
  - Asking for memory no longer drags a signature check over every binary on the machine behind it.
    On a normal machine that is the difference between an answer that arrives immediately and one
    that takes about nine seconds.

- **The Columns menu no longer hides most of what it offers.** It was one list of thirty-two items
  against a menu that fits thirteen, so two whole groups - everything under About the entry and
  Advanced - were reachable only by scrolling. The menu is now four groups you open, and nothing is
  below the fold.

- **Filter chips stay inside a narrow window.** Below about a thousand pixels wide, a group of
  chips wider than the row was drawn straight past the right edge of the window - so "Could not
  check" showed as "Could r", "Runs while disabled" as "Runs", and one chip was cut through a
  letter. They were not clipped by a narrow column: they were standing outside the window, where
  nothing could read or click them.
  - Each group of chips now folds onto as many lines as it needs, under its own label.
  - The trade is stated rather than hidden: on a narrow window the filter row is now taller,
    because chips that used to be off the screen are on it. **Filters** at the left of the row
    folds the whole thing away.
  - Nothing changes at the size the window opens with, where every group already fitted.

- **The plan panel no longer becomes unusable after an unexpected error.** If anything went wrong
  while carrying a plan out - other than the one refusal the panel already knew about - the button
  stayed grey for the rest of the session, with a tooltip explaining that it was grey because a run
  was in progress. Nothing was running. Closing the panel and opening another plan brought it back,
  which is not something anybody would think to try.
  - The failure itself is still reported in the line under the list, as it always was.
  - The button comes back live rather than reading "already done", because nothing knows how far
    the run got. Pressing it again asks each entry where it is before doing anything to it, so an
    entry already where you asked for it is reported as skipped rather than touched twice.

- **The window keeps answering while it works out a plan for a large selection.** Selecting
  everything and asking what stopping it would do took about a quarter of a second with the window
  frozen for all of it - measured at 224-240 ms over 799 entries. The work is the same and takes the
  same time - the window is simply no longer holding still for it.
  - Asking for one thing and changing your mind before the first answer arrives now shows the
    second answer rather than whichever finished first.

- **A step of a plan can no longer report a negative time, or give up on a service that is still
  working.** Both were worked out from the clock on the wall, so correcting the time, arriving at
  daylight saving, or resuming a suspended machine moved them. Durations are now measured with a
  count that only goes forward.

- **A preview no longer fails when Windows names the same dependent service twice.** It came back as
  an error message rather than a preview, with nothing to say the failure was in working the plan
  out rather than on the machine.

- **A failure with several causes says all of them instead of "One or more errors occurred".** The
  listing and the signature pass both read many entries at once, so this is the shape most failures
  in this tool actually have - and the sentence explaining what happened was one of the ones being
  dropped. Repeated causes are said once.

- **A column width that no screen could show is refused instead of breaking the list.** Hand editing
  the layout file to something like `1e300` left every column unusable with nothing on screen to say
  why. Such a value now falls back to the width the theme gives, and the column is named in the line
  under the list, exactly as an unreadable one already was.

- **Pointing `snapshot create` at a folder this account cannot write to now ends with the code for a
  bad argument rather than the code for a failure inside the tool.** A script could not tell the two
  apart.

- **A file far too large to be a snapshot, or to be a saved column layout, is refused with a
  sentence rather than read into memory first.** A whole machine is about one megabyte, and the
  limits are sixty four megabytes and one respectively - so this refuses files that are not ours
  rather than ones that are large.

- **Closing the window while a plan is being carried out no longer writes the column layout twice.**

- **Two drivers no longer show a file path where their name should be.** Windows stores some names
  as a pointer into a binary's resources, and for `Tcpip6` and `tcpipreg` on an ordinary machine
  that pointer leads nowhere - so the list read `@todo.dll,-100;Microsoft IPv6 Protocol Driver`
  instead of a name, and sorted both of them above everything else. They now read
  `Microsoft IPv6 Protocol Driver` and `tcpipreg`, and sit where those names belong.
  - `sc.exe` and `Get-Service` still print the raw text for both, so this is a deliberate
    difference from them rather than a disagreement. The internal name is unchanged everywhere it
    is used to identify an entry.
  - A snapshot taken now records the readable name, so a snapshot from before this change and one
    from after it will differ on these two entries even though the machine did not.

- **The button on the opening screen no longer changes colour when you point at it.** It was blue
  standing still and went dark under the pointer, which looked like something going wrong.

- **Pointing at a card on the opening screen no longer draws a hard-edged box inside it.** The
  highlight follows the shape of the card and leaves room around the words.

- **A screen reader now names the numbers on the opening screen.** All six of them arrived as
  unnamed buttons, so the first screen anybody meets said nothing to anybody who cannot see it.

- **A word the command did not have room for is no longer called an unknown option.** Typing
  `bws start type Spooler manual` - the hyphen missed out of `start-type` - answered
  `Unknown option: Spooler, manual`, which sent you looking for a typo in two words you had spelled
  correctly. It now says `start takes one name. Nothing here can use: Spooler, manual.` The same
  goes for a second name after `stop`, a name after `list`, and a third file after
  `snapshot diff`, each answered with what that command does take.

- **Closing the window before the list had finished loading no longer forgets which column you had
  sorted by.** The order was saved when the window closed, but it was applied only after the list
  had arrived - so shutting the window inside that first second wrote down "no order at all" and
  your sorted list came back unsorted. Turning off the column you were sorted by had the same
  effect the next time anything was saved.

- **The window no longer disappears when something goes wrong in it.** Anything unexpected -
  during a refresh, while a plan is open, in the middle of typing - used to end the program and
  leave you with the system's own crash box. It now says what happened in the line under the list,
  the same place the window says everything else it could not do, and stays open with your query,
  your selection and your plan where you left them.
  - **A translation file that is not readable no longer stops the program starting.** A file
    dropped beside the program with broken JSON in it, or one the account may not read, means the
    window opens in English rather than not opening at all. A translation with a broken hole in a
    sentence shows the sentence unformatted instead of taking the window down mid-session.

- **`bws list --query ""` and `bws list --query=` are now mistakes rather than everything.** Both
  used to print the whole machine and exit successfully, which is what a script gets when a shell
  variable expands to nothing - a filter that quietly did not filter, reported as a clean run. They
  now say the option needs a value and exit 2.
  - **Leaving `--query` off is unchanged and still means everything.** The difference is between
    not asking to narrow and asking to narrow with nothing, and only the second is a mistake.

- **`bws show` without a name, and `bws snapshot diff` with one file, no longer read the whole
  machine before telling you what you left out.** Both answered correctly and both spent about half
  a second enumerating eight hundred entries first. The answer is the same and now arrives at once.

- **Comparing two snapshots no longer reports a change when nothing changed.** Dependencies,
  declared privileges and triggers were compared as text, so the same set in a different order -
  or a privilege one machine spells `SeSystemTimePrivilege` and another spells
  `SeSystemtimePrivilege` - came out as drift. With `--exit-code` that failed a pipeline over
  nothing. They are compared as sets now, and what a real difference shows you is still exactly
  what each snapshot holds.

- **Exporting the list to CSV no longer hands a spreadsheet something to run.** A service whose
  name or description starts with `=`, `+`, `-` or `@` was written into the file as-is, and a
  spreadsheet reads such a cell as a formula. The values in that file come from whatever installed
  the service, which in an audit tool is the thing being examined. They are now written so they
  open as text.

- **Asking a second question about signatures or memory no longer re-reads the whole machine.**
  Typing `signed:no` and then `memory:>500MB` sent the window off to read every service again and
  verify every signature a second time - several seconds of work for an answer it already had,
  with the list frozen for the whole of it.

- **Selecting hundreds of entries and changing their start type no longer asks the manager about
  every one of them first.** The preview worked out an order that a configuration change does not
  need, and paid one round trip to the service manager per selected entry to do it.

- **The details panel notices its service has gone even after you click another row or switch
  between Services and Drivers.** It follows the entry it was opened on, but the check for "this
  entry has left the list" was looking at whichever row was selected - so after a scope switch it
  stopped checking anything at all.

- **Pressing Ctrl+C at the exact moment a run finishes no longer ends the program without printing
  the report.** A window of a few microseconds remained from an earlier fix of the same race.

- **A preferences file with damaged bytes in it is moved aside rather than quietly half-read.** It
  was read with a decoder that silently substitutes a placeholder for anything it does not
  understand - the same choice the snapshot reader deliberately refuses to make.

- **The white outline on Services / Drivers / Everything no longer appears when you click.** It is a
  keyboard focus ring and it was lighting up for the mouse as well, because clicking a button gives
  it the keyboard focus too. It is drawn by the framework now, which shows it only while you are
  actually using the keyboard - so tabbing still shows you where you are.

- **On a machine the manager hands over nothing for, the message saying so no longer flickers.**
  The sentence in the middle of the empty list was replaced by "reading the manager" and put
  back once a second, for as long as you stood there reading it. The same fault was fixed for a
  search that matched nothing a few days earlier - this was the one case that fix did not cover.
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
