# Developer changelog

Everything that changed, in more detail than [CHANGELOG.md](CHANGELOG.md) carries. That
file answers "what can I do with the tool now". This one answers "what was changed, why,
what did it cost, and what will it break".

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## How to read this file, and how to write in it

This project is built entirely by an assistant, one slice at a time, and it is meant to
last for years. That makes this file something a normal changelog is not: **it is the
memory of sessions that no longer exist.** A future session reads it to find out what was
already tried, what was measured, and what a change would break.

So four rules for entries here, and they are not style preferences:

1. **One slice, one entry, with its commit.** The commit is how anybody gets from a
   sentence here to the code that implements it.
2. **Mark anything that touches a frozen contract with `[contract]`.** The list of frozen
   contracts is in `docs/02-DECYZJE-TECHNICZNE.md`. Breaking one is allowed and costs a
   version bump and a migration - doing it without noticing is what this marker prevents.
3. **Numbers come with what produced them.** A measurement without its subject ages into a
   claim nobody can check. "322-329 ms over 810 entries" is useful, "fast enough" is not.
4. **Record what was deliberately left out.** The most expensive question a later session
   asks is "did anybody think about this", and the cheapest answer is a line saying yes and
   why the answer was no.

**Versions are the owner's call, never the assistant's.** Nothing here moves out of
`[Unreleased]` without them saying so.

## [Unreleased]

Nothing released. The whole history below is one day, 2026-08-01. **Entries inside each
heading are in the order the work happened**, oldest first, rather than by importance -
because in this project the order is part of the reasoning, and several entries only make
sense as answers to the one above them.

### Added

- **Scaffold: six projects, .NET 10, x64 only** (`ce98bb9`). Architecture guards written
  before any behaviour existed, because a guard written afterwards finds rules already
  broken. Verified by actually breaking each rule and watching a test go red.
  - Found doing it: **the compiler drops unused project references from metadata**, so a
    guard reading the compiled assembly cannot see a reference nobody has called yet.
    Two mechanisms are needed - compiled metadata and the project files - and neither
    replaces the other. Written up in `docs/06`, part 2.

- **`bws list`, table and JSON** (`b24cb13`). **[contract]** Field names come from the
  binding column of `docs/03`.
  - Measured: **811 entries, 472 drivers, 339 services**, agreeing with
    `sc.exe query type= all` exactly. Read in about 200 ms against a budget of a second.
  - Found doing it: a type mask built from the four obvious service kinds came back **81
    entries short** with nothing to say so. Per-user services carry extra bits on top of
    the Win32 kind.
  - Corrected an earlier count: 869 and 397 came from a script counting registry subkeys,
    which counts something else. Sprostowanie in `docs/05`.

- **User-facing text moved out of the code** (`a47f0fc`) into an embedded resource.
  - Found doing it: **a file named `something.<language>.json` is treated by MSBuild as a
    satellite resource**, built into its own assembly with the language stripped from the
    resource name. The build stayed green and the program died on its first line. Every
    language file needs the culture mechanism switched off explicitly and a logical name
    given. Written up in `docs/02`, known pitfalls.

- **GPL-3.0 and the product name** (`d37fe3c`, `edea737`). `ADR-12`, `ADR-12a`. The `Bws`
  prefix stops being provisional.

- **The query language** (`2cb2aea`, fixed in `5bd9c96`). **[contract]** Syntax version 1.
  The full language from `docs/07`: seven fields, wildcards, regular expressions, comma
  alternatives, negation, and the reserved words `none`, `any` and `?`.
  - Measured: evaluation in **3-14 ms** against a budget of 50 ms.
  - Regular expressions come from the user, so they go to the non-backtracking engine
    first and fall back to the ordinary one with a timeout. Measured on subjects of 24, 32
    and 40 characters: `(a+)+b` is accepted by the linear engine and answers at once,
    `(a+)+\1b` is rejected and then times out every time. Both paths are real and both are
    exercised.
  - The timeout is applied on the linear path too, where it is unreachable in theory:
    without it, picking the wrong engine **hangs the test run instead of failing it**, and
    a hang tells nobody anything. Found by making that mistake.
  - Also added: the delayed-start flag, as **its own field** rather than a sixth start
    type. The manager answers the start type and the delay through two different calls, so
    "automatic, and nobody could find out whether it is delayed" is a state that happens.

- **Output channels and exit codes pinned by guards** (`f12b399`). **[contract]** Only data
  on standard output, everything else on standard error, including on a failed run.
  Verified by inserting both mistakes and watching three tests go red.

- **The service control manager double** (`61826aa`), in `tests/Bws.Core.Tests/Fakes/`.
  Every specimen read off a real machine with the tool's own JSON output rather than
  invented, except those marked synthetic. Verified by breaking the refusal signal and
  confirming that **only** the specimen-catalogue test noticed.

- **`dependsOn`** (`463ea9c`), free: it arrives in the configuration buffer already being
  read. Measured: **210 of 339 services declare at least one**, longest list five. Names
  are kept verbatim including the leading `+` that marks a load order group.

- **The plan pattern** (`17a229b`, `9d4d611`), `ADR-11`. Preview, cascade, preflight
  warnings, refusals. **[contract]** The plan's JSON shape.
  - The cascade is computed by `EnumDependentServices` rather than by inverting
    `dependsOn`, because the manager knows about load order groups and the inverted list
    would not. Measured: **it is transitive**, so one question about the target is enough.
    The order it returns is not documented, so ordering is done by set containment.
  - Found in the first real dry-run, not by reading code: stopping `BFE` pulls in two
    kernel drivers, so the plan was refusing drivers as targets and listing them as steps
    at the same time. **A contradiction inside a preview is worse than no preview**, so a
    cascade needing a driver stopped is now a refusal.
  - Environment note: **`sc enumdepend` silently truncates to three entries** and exits
    234 without retrying with a bigger buffer. It is not a source of truth for comparison.

- **Carrying a plan out** (`4323cc4`), with a result per step. Waiting follows the
  service's own promise - `dwWaitHint` plus `dwCheckPoint` - refreshed on every move, so a
  slow healthy shutdown is not announced as a failure. `--timeout` is only a ceiling.
  - **Execution recalculates nothing.** It takes the steps the preview showed and never
    asks the manager about dependents a second time. Drift between planning and running
    surfaces as a step the manager refuses, never as a quiet extra step.

- **Interrupted runs** (`e251af5`). **[contract]** Exit code 4, and three levels of Ctrl+C.
  - The middle level was added after a run on a virtual machine showed two presses killing
    the process halfway through putting things back, leaving a service down and saying
    nothing. The difference between "I am leaving it" and "I am not telling you what I
    left" is the whole reason it exists.
  - All three verified with a real signal, not by reasoning. The first assessment said it
    could not be done from here because the signal reaches this session's console - which
    was wrong. A helper detaches itself, allocates its own console, and the signal then
    covers only the helper and the process under test. The helper lives in `tools/` and its
    method is written up in `docs/04`.

- **Trigger start** (`421a9a1`), the first of `S4`'s four families of expensive data.
  **[contract]** `triggers` and `notRead` in the listing.
  - Measured: **45-75 ms over 810 entries**, 249-285 ms in total against a budget of a
    second. That settled the design: they are read every time, and no on-demand machinery
    was built on a guess about the cost.
  - **122 entries carry at least one**, the same number a registry script had reached
    independently.
  - The interop metadata has no name for trigger type seven, and it is the **second most
    common kind on the machine** - 88 of them. Left as `unknown` it would have made the
    second largest group indistinguishable from anything unnameable. Mapped after
    `sc qtriggerinfo` called every sample "CUSTOM SYSTEM STATE CHANGE EVENT". One trigger
    is still unnamed and `sc.exe` cannot name it either.
  - **`notRead` exists so that `null` does not mean two things.** "No triggers" and "nobody
    looked" say opposite things about a stopped service.

- **The launch path and orphan detection** (`41aca8c`), `C13`. **[contract]** `binaryPath`,
  `binaryFile` and `binaryOnDisk` in the listing, `path` and `file` in the query language,
  and the free-word search widened to include the path.
  - Zero new Win32 calls: the value was already in the configuration buffer.
  - Measured: **45-60 ms**, 322-329 ms in total. The isolated cost of asking the disk was
    34-39 ms, which the design was based on, and the rest is the resolving. **Timing one
    call is not timing the family it belongs to.**
  - Six shapes over 825 entries: 294 with a drive letter, 237 with `\SystemRoot\`, 216
    relative, 45 quoted, 29 empty, 4 with the kernel object prefix. **Checking the raw
    string against the disk reports 785 of 825 as missing** - a tool built on the obvious
    approach would call almost the whole machine broken and look like it worked.
  - An unquoted path with spaces is ambiguous, so every prefix is tried shortest first.
    Cutting at the first space breaks two entries on this machine. **NOT MEASURED:**
    whether the manager resolves such a command in exactly that order. Finding out would
    mean planting a file and starting a service.
  - Deliberate difference from `sc.exe`: for the 29 drivers naming no path, `sc qc` shows
    an empty field and so do we - but we additionally say which file the driver runs, from
    the default the manager applies.
  - **`orphan` deliberately did not become a word in the query language.** It is written
    `file:missing start:auto` out of parts that already existed. Measured: that pairing is
    **empty** on this machine while `file:missing` alone is five entries, so a single
    orphan answer would have been an empty list and those five could not have been asked
    about at all.
  - The free-word search widening happened **inside syntax version 1**, by the owner's
    decision, because saved queries cannot exist until phase four. From the first slice
    where a query can be saved, widening the scope becomes a version change.
  - Guards verified by breaking the behaviour on purpose. The naive split reddened three
    unit tests and **no integration test**, because every integration test was a count and
    the mistake is wrong about two entries in 810. Closed with a guard that asks a property
    instead of a number. Written up in `docs/04`.
  - The same exercise caught one of the new tests passing for the wrong reason: it searched
    for a vendor name that also appears in the specimen's display name, so it passed with
    the path removed from the search entirely. Exactly the fixture trap `ADR-10` warns
    about.

### Changed

- **Putting a service back is not going forward** (`8f7106f`). Found by a run on a virtual
  machine: an interrupted restart left the service stopped, because starting the target
  again was marked as progress to be abandoned rather than as something being given back.
  One step was conflating two different questions - **"who asked for this step" is not
  "does this step give something back"**. The preview now reads `stop X (asked for)` and
  `start X (put back)`.

- **A restart that could not put the service back is refused** (`d16cde8`). Found by
  execution, not by dry-run: restarting an entry that is **disabled but running** stopped
  it and could not bring it up. A refusal rather than a warning, because a command without
  `--dry-run` has no moment where anybody reads a warning.
  - The boundary held on purpose: **we do not predict whether a start will succeed.** The
    manager is the authority and its reasons go beyond the start type. Only taking down
    something the data already says we cannot put back is refused.

- **Switches belong to verbs** (`fe709f0`, `b6f3fe9`). **[contract]** A switch on the wrong
  verb exits 2 and says where it works, instead of being accepted and ignored. Table in
  `docs/02`.
  - A second version of the same silence turned up while fixing the first: `--timing` was
    accepted everywhere and honoured only when listing.
  - `--dependents` does not belong on `start`, which was missed on the first pass. Starting
    is not the mirror of stopping.

- **A refusal carries its number everywhere** (`fe709f0`). **[contract]** `unreadable` in
  the listing changed shape from `{"startType": "text"}` to
  `{"startType": {"errorCode": 5, "message": "text"}}`.
  - Measured on two machines: the same refusal 1058 comes back in English on one and in
    Polish on the other, because Windows answers in the language of the machine. The
    number is stable, the sentence is not.
  - **Our own English "access denied" literal is gone.** There were two of them, not one.

- **Failing against a start type ignores an entry waiting for a trigger** (`41aca8c`).
  Owner's decision once the data existed. Measured: **four of the ten entries** it reported
  on a real machine were waiting rather than broken.
  - Cost, recorded because it was not visible when the decision was made: the answer now
    leans on a field that can be unread, so an automatic stopped entry whose triggers
    nobody read answers "I do not know". Triggers are consulted **only** where an
    accusation would otherwise be made, so an unread listing does not become 810 shrugs.
    A test holds that boundary.
  - Note for whoever reads the old wording: acceptance scenario one runs through the
    **query language**, not through this property, so its count of ten is unchanged. The
    property has no consumer in shipping code yet - the first will be the machine overview.

### Known gaps

Carried here rather than in a session's memory, because sessions end.

- **No `Version` property anywhere in the build.** The SDK therefore stamps `1.0.0.0`, so
  the binaries already claim a version nobody declared. Rule 11 makes this the owner's
  call.
- **The `unreadable` JSON shape has no guard.** Reading configuration through the manager
  is refused zero times on the machine available, so a live run produces no such entry. The
  `Reading<T>` type behind it is guarded. Closing this needs a test project for the CLI or
  a machine that refuses.
- **A failed configuration read is always marked as a refusal**, including when the real
  cause is the service disappearing between enumeration and the configuration query. No
  consequence for the listing, a real one for snapshots.
- **The delay flag is not read where it does nothing.** Affects snapshots, not filtering.
- **`errorControl` and `lpLoadOrderGroup`** are read into the buffer and thrown away.
- **`dwControlsAccepted`** likewise - a service's own declaration of whether it accepts
  being stopped, which would make a fifth preflight warning. Open question: whether it
  enters the public JSON contract or is read only when building a plan.
- **`ControlServiceEx` can carry a reason for stopping** into the system event log. We use
  `ControlService` and leave that empty, which is a poorer operation than the API allows,
  in a product whose heart is audit.
- **The fourth state of a field has no word in the query language.** `docs/07` promises
  four and gives three. Deferred by the owner until background loading exists, and pinned
  by a test so it is not rediscovered as a bug.
