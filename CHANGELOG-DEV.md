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
5. **A timing figure carries its input size, and comes from more than one run.** Write
   "4620-7656 ms over 810 entries and 544 files", never "about five seconds". One run is a
   sample from a distribution nobody looked at, and every number here is a fact about one
   machine - somewhere else there are different services, a different disk and a different
   set of catalogues. Full rule in `docs/04`, "Jak wolno mierzyć wydajność".

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

- **Signatures and provenance** (`c5d008a`), `S4` family two. **[contract]** `signature` and
  `fileVersion` in the listing, `signed` and `publisher` in the query language,
  `--signatures` on `list`.
  - **The first family that actually needs ADR-13.** Measured over seven runs, first
    discarded as cold, on 810 entries and 544 distinct files: **4620-7656 ms, median
    4882**, against 338-353 ms for the whole manager read. Triggers and launch paths both
    looked like candidates for deferral and both turned out too cheap to bother.
  - **Windows signs its own files two ways and only one is visible from the file.**
    WinVerifyTrust given the file alone answers for 346 of 544 and says "no signature" for
    198 - **of which the system trusts 189**. Those are catalogue signed: hash the binary,
    find the catalogue listing that hash, verify against it. Stopping at the first step
    would call a third of the machine unsigned and look like it worked.
  - Verdict agrees with `Get-AuthenticodeSignature` on **all 535** files with a publisher.
    The publisher differs on **44** and deliberately so: a file can carry both signatures,
    PowerShell prefers the catalogue, this prefers the file. The embedded signature travels
    with the binary while a catalogue entry belongs to the machine reading it, so preferring
    the catalogue would make snapshots differ across machines for reasons that are not about
    services - ADR-14's failure mode one layer down.
  - Revocation checking is off. It reaches the network, which ADR-19 forbids, and it would
    make the answer depend on whether a certificate authority is reachable right now.
  - `orphan` had no word and neither does "signed" as a single idea: `signed:no` is a group
    covering unsigned, untrusted, expired, revoked and tampered, because an expired
    signature is still a signature and "yes, signed" about one is true and useless.
    `unknown` is deliberately outside that group - an unnamed result is a gap in our naming,
    not a finding about the file.
  - One question per distinct file rather than per entry: 810 entries point at 544 files.
  - **Found by a test, not by reading code.** The first version read the certificate with
    `X509CertificateLoader`, which loads files that *are* certificates rather than the
    certificate embedded in a signed binary. The throw was caught and **every file on the
    machine came back trusted with nobody's name on it**. Build green, unit tests green,
    caught by an integration guard asking whether a trusted file has a signer.
  - Guards verified by breaking the catalogue path on purpose: two integration tests go red,
    including the one written for exactly that. **Noted from that run:** after a failed
    build, `dotnet test --no-build` runs the previous binary and passes - a green test run
    following a red build means nothing.
  - A publisher cache was added and **measured not to help here**: seven runs with, five
    without, spreads overlapping. Kept anyway, because how many catalogues a machine has is
    a property of the machine, and removing it would be fitting the code to the one machine
    it was measured on.

- **The third family of S4: privileges, service SID type and the security descriptor** (`e2ff9a5`, specimens corrected in `234bf29`).
  **[contract]** Three fields added to `ScmEntry` and to the JSON: `requiredPrivileges`,
  `sidType`, `securityDescriptor`, plus `privilege`, `sidtype` and `sddl` in the query
  language. Names from the binding column of `docs/03`, except the query name `sddl`, which
  is named after the text form it searches so that the decoded permission list can have
  `security` when it exists.
  - **Measured, six runs each, same session, worktree built at the previous commit for the
    comparison:** 343-364 ms before, **476-551 ms after**, over 810 entries against a budget
    of a second. The family costs 130-190 ms. Spread inside a variant is 21 ms and 75 ms,
    both smaller than the difference, so the difference is real. The probe predicted 120-140
    and was low, the same way it was low for the launch path: it times the calls and not
    what happens to the answers.
  - **Three families in a row that ADR-13 does not apply to.** Triggers 45-75 ms, launch
    path 45-60 ms, permissions 130-190 ms, all read every time. Only signatures, at
    4620-7656 ms, need deferring. "Expensive data" in the specification turned out to name
    one family out of four.
  - **The descriptor gets a handle of its own, and that is measured rather than tidy.** It
    needs `READ_CONTROL`. Under a restricted token, five entries of 810 - `LSM`,
    `NetSetupSvc`, `pla`, `QWAVE`, `QWAVEdrv` - open for configuration and refuse when
    `READ_CONTROL` is added to the same request. Asking for both together would have taken
    the start type, the account and the launch path from those five in exchange for a field
    they were never going to give up. I predicted this would cost nothing and was wrong.
  - **No guard covers moving it back onto the shared handle.** Verified by doing it: all 328
    tests stayed green, because an elevated session refuses nothing. A guard would need the
    tool run under a restricted token from a test, through `runas` and a result file, which
    is the kind of flaky guard that gets switched off. There is a comment at the exact line
    where the mistake gets made, and this entry.
  - Three other deliberate breaks did go red as they should: truncating the privilege list
    to its first entry, mapping the restricted SID type to unrestricted, and adding
    `SACL_SECURITY_INFORMATION` to the request - the last of which fails the whole read with
    error 5 even elevated, which is why the audit list is deliberately not read.
  - **`SERVICE_SID_TYPE_NONE` is modelled as absence, not as a third value.** It is not a
    kind of identity, and putting it in the enumeration would have let `sidtype:none` be
    answered by an entry nobody could read.
  - **Privilege name casing varies between services on one machine** - `Schedule` declares
    `SeSystemTimePrivilege`, `Sense` declares `SeSystemtimePrivilege`. Comparisons are
    case-insensitive, which matters for the query language now and for the diff at S5.
  - **`privilege` is the first text field holding a list.** `TextsOf` on `QueryField`, any
    value matching. Joining the list into one string would have changed what the operators
    mean: an exact match could never match, and a wildcard could span two values.
  - **Deliberately left out:** decoding the descriptor into a readable permission list,
    which `01` promises for the details panel and which goes with S7. No table column - the
    three change no existing column's meaning, unlike a trigger or a missing file. No SACL,
    no writes.

- **S5 cut in two, and the first half built: `bws snapshot create`** (`83d803e`). **[contract]** A new
  public contract, the snapshot schema, at version 1. `ADR-6` and `ADR-18` both go from
  written to built.
  - **Why it was cut.** S5 as planned holds the schema, metadata, deterministic writing, the
    atomic write path that did not exist, the SHA-256, and then reading back, comparing per
    field, four read states in a diff and the 807-against-810 trap. That is twice any slice
    so far, and the rule says cut rather than deliver it swollen. The second reason matters
    more: the diff's design depends on how the file records "not read", and designing it
    against a real file is cheaper than against an imagined one.
  - **Measured, six runs:** **6399-7156 ms** over 810 entries, of which 479-544 ms is the
    manager and the rest is signatures and hashes. The file is **952 KB and 24 941 lines**.
    Two snapshots of an unchanged machine differ in **one line**, the timestamp.
  - **Signatures and hashes are read every time** (owner's decision). A snapshot is taken
    deliberately and kept for months, so being comparable beats being quick - and one
    without them compared against one with them reports the whole machine as changed.
  - **Four things were needed to make "diffs cleanly" true rather than intended:** keys
    sorted on the tree rather than by declaration order, ordinal rather than culture-aware
    sorting, entries sorted by name with a tie-break on the exact spelling, and UTF-8
    without a mark, Unix line endings, characters unescaped. Each of them is invisible in a
    single file and each makes two files of an unchanged machine differ on hundreds of lines.
  - **The entry-to-JSON mapping moved into the core**, where `docs/02` already said the
    snapshot model is the source for the listing's fields too. Two mappings would drift, and
    quietly: a field added to one, missing from the other, found out by whoever compares
    snapshots six months from now.
  - **`errorControl`, `loadOrderGroup` and `binaryHash` added** (owner's decision), because
    the schema is frozen and `D1` names them. The first two were sitting unread in the
    configuration buffer since S1. The hash costs 0.52 s in the pass that already opens
    every file.
  - **Found by a test, twice.** The rounding of the timestamp to whole seconds read
    correctly and did nothing - it took the ticks from one reading of the clock and the
    remainder from another - and only comparing two files showed it. And a snapshot could
    not be read back at all, because it drops a property the document type marked required;
    memory stopped being required, which is the right answer rather than a workaround.
  - **Found while writing a test:** a check for the JSON escape prefix matched real AMD
    driver store paths, where a folder is genuinely called `u0202642.inf_amd64_...` behind
    an escaped backslash. A test asking about a pattern in text rather than a property of
    the values found something it was not looking for.
  - **Nothing guards atomicity itself.** Verified by replacing write-beside-and-move with a
    direct write: all 383 tests stayed green, because the window where the difference exists
    is microseconds wide. What is guarded is the observable half - no temporary file after a
    successful write, quarantine that does not overwrite an earlier quarantine from the same
    second, the encoding and the line endings.
  - **Deliberately left out:** everything on the diff side, restoring (`D5`), baselines
    (`D6`), scheduled snapshots (`D4`), the safety snapshot before a write (`D3`), the YAML
    export `ADR-6` mentions, and the fields the tool still does not read at all - the
    description and the recovery actions.

- **The fourth family of S4, and the end of S4: process memory** (`2865397`). **[contract]** `memory`
  on `ScmEntry` and in the JSON as `{workingSet, commit, sharedBy}`, a `MEMORY` column, a
  `--memory` switch, and `memory` in the query language.
  - **Measured:** the pass costs **5-8 ms over 810 entries and 110 processes**, six runs.
    That closes open question 4 of `docs/02` - all four families now measured, and the
    spread across them is a hundredfold. `ADR-13` is justified by exactly one of the four.
  - **The population is not what the specification assumed.** 119 entries of 810 have a
    process, those 119 sit in **110 processes**, and **105 of those host exactly one
    entry**. The big `netsvcs` group with its 48 services exists in the configuration and
    not at runtime, because Windows splits svchost into a process per service when there is
    enough memory. I predicted about 200 entries with a process and was high by two thirds.
  - **The access mask is the whole slice.** `GetProcessMemoryInfo` names
    `PROCESS_QUERY_INFORMATION` with `PROCESS_VM_READ` in its first sentence, and accepts
    `PROCESS_QUERY_LIMITED_INFORMATION` instead. Measured over 110 processes: the limited
    right refused by **none**, the wider pair by **seven** - lsass, MsMpEng,
    MpDefenderCoreService, NisSrv, SecurityHealthService and two svchosts. It is also the
    only right under which "this only reads" is checkable: the limited mask cannot read or
    write anything inside the process.
  - **The first deliberate break was not caught, so a guard was added for it.** Swapping in
    the wider mask left all 352 tests green, because every test sampled entries that had
    answered and the seven refusals simply dropped out of the sample. The new guard asks the
    opposite question - did anything fail to answer - and names the eight services affected.
    It makes its strong claim only on an elevated session, and says so in the test: without
    elevation a refusal on somebody else's process is the system behaving normally, and a
    guard that fires on a correct run gets switched off.
  - **The second break was caught, by two tests:** running the pass after the filter. The
    sharing count is then taken over the filtered list, so a service in a shared process
    reports having it to itself - every megabyte figure stays right and only the sentence
    around it is false.
  - **Found by measuring, not by writing: the pass was being counted as filtering time.**
    2-3 ms without it and 7-8 ms with it, all under the word "filtered" - the same mistake
    the signature pass has its own line to avoid. It has one now. It also corrects my own
    note from the probe that this costs a fraction of a millisecond: that is what the calls
    cost, and the pass also builds 810 new records.
  - **Sizes are new to the query language**, because the specification's own showcase query
    is `memory:>500MB` and without units the field would be unusable. The unit is required:
    `memory:>500` is a refusal with a message, not a question about 500 bytes that would
    match every running service. Powers of 1024, matching Task Manager and `Get-Process`.
  - **`Query.NeedsSecondPass` became `Query.Needs`, a flags enum.** With one flag, a query
    about memory would have set off a signature verification measured in seconds to answer
    something that costs milliseconds.
  - **Deliberately left out:** the private working set, which is what Task Manager shows in
    its Memory column. It needs either the mask seven processes refuse or performance
    counters, whose **counter set names are translated** - `Proces` and `Proces w wersji 2`
    on this machine - which rule 3 rules out. Only the numeric-index route would be safe and
    nobody asked. Also left out: a second size field for the commit figure, memory history,
    and memory in a snapshot, which `D1` rightly does not ask for.

- **Found while doing it, and bigger than the slice: without elevation the manager
  enumerates fewer entries, not entries with holes.** 807 against 810, with `RoutePolicy`,
  `ZTDNS` and `ZTHELPER` missing entirely, and `sc.exe query type= all state= all` in the
  same token reports the same 807. This closes the open question in `docs/05` - "is there an
  entry invisible without elevation" - with a yes, and moves the problem to **S5**: a
  snapshot taken unelevated has three fewer entries, so comparing it against an elevated one
  would report three deletions nobody performed. Rule 8 guards silence in fields and cannot
  help, because what is silent is the whole row. The `D1` metadata recording the privilege
  level a snapshot was taken at stops being a formality.

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

- **S5a1: the second pass asks about several files at once** (`2f7e975`). No contract moves - same
  fields, same JSON, same switches, same exit codes. `ADR-22` records the decision. This is
  the first concurrency in the project, and it exists because the snapshot was over the
  budget `8.1` of the specification promises.
  - **Measured, five counted runs of each build, interleaved so both met the same weather:**
    the whole `snapshot create` went from **5540-9113 ms to 1723-2141 ms** over 810 entries
    and 544 distinct files, and the verification inside it from **4926-8500 ms to
    1100-1245 ms**. The budget is 5 s, so it is met with the worst run at 2.1 s. Unlooked
    for and worth as much: the spread fell from 73% to 24%, so the command became
    predictable as well as quick.
  - **The default degree is the logical processor count, and that came out of a sweep.**
    Degrees 1, 2, 4, 8, 16 and 32, five counted runs each, every degree run once per pass so
    none of them owned a good minute: fastest 4376, 2474, 1550, 1144, 1031, 1001 ms on a
    16-processor machine. Sixteen beats eight measurably - `[1144, 1360]` and `[1031, 1107]`
    do not touch. Thirty-two does not beat sixteen - `[1001, 1074]` overlaps `[1031, 1107]`,
    and a spread wider than the difference means there is no difference.
  - **The three questions `docs/04` said to close by measurement, closed by measurement.**
    29 376 comparisons of a live machine's verdicts against the single-threaded answer - a
    sweep, a pool run and a soak at the top degree - with **none differing**. And the one-off
    proof the whole design allows: a snapshot from the unchanged build against one from this
    build differ in **one line, the timestamp**, across 24 941 lines.
  - **Measured apart on purpose, and it paid:** `Parallel.ForEach` asked for 32 at once
    actually reached **18**, because the pool adds threads slowly. Had the sweep used the
    pool, a flat curve above sixteen would have been ambiguous between "Windows serialises
    this" and "the pool never got there" - opposite conclusions, one of which ends the slice.
    It cost nothing here because the gain has already flattened by sixteen, which is why the
    product uses the pool rather than threads of its own.
  - **Three phases, not one parallel loop.** The obvious shape - go wide over the entries
    with a concurrent dictionary sorting out the repeats - loses the property that makes this
    affordable, because that dictionary may run its factory more than once per key. Settling
    the distinct file set first makes one-question-per-file true by construction.
  - **Found by breaking it, and it changed a test rather than only confirming one.**
    Attaching every answer to the next file along left **all 289 unit tests and both halves
    of the new comparison green**: two runs of the same wrong code agree perfectly. Comparing
    runs only ever finds what wanders between runs. The gap is now covered by a guard with an
    oracle of its own - it recomputes the SHA-256 of every file the machine names and asks
    whether the answer hanging on that entry is that file's. Breaking the mapping again
    reddens it.
  - **A second break, a second lesson of the same shape.** Reordering the result left the
    whole integration class green and reddened only the unit test written for it, for the
    same reason. The comment claiming the integration test guarded order was corrected rather
    than left to be believed.
  - **The fake had to become thread-safe first.** It recorded questions in a plain list, so
    every existing test counting questions would have become one that fails once a month and
    passes on a re-run. A flaky guard teaches people to re-run instead of to look.
  - **The suite got faster, not slower.** Estimated at +7 s when the work was scoped, it went
    the other way: the integration project fell from **69 s to 29 s**, because the tests that
    read signatures sped up with the product. Two new integration tests and four new unit
    tests, 385 to 395.
  - **Also measured, because nobody had:** the three questions the pass asks, apart. Over 544
    files single-threaded and cold, signature **6186 ms**, file version **325 ms**, hash
    **464 ms**. The signature is 89% of it, and the hash figure agrees with the 0.52 s
    recorded when it was added.
  - **Established the same day, and without the virtual machine.** Whether the plateau
    follows the processor count or the storage queue was separated by **pinning the process
    to fewer processors**, which moves one of those two numbers and leaves the other alone.
    Pinned to two, the plateau is at degree 2 with a floor of 3453 ms. Pinned to four, degree
    4 and 2028 ms. Unpinned on sixteen, degree 16 and 1031 ms. **It follows the processor
    count at all three points**, so the work is bound by computing rather than by waiting on
    the disk, and `Environment.ProcessorCount` is the right number rather than a convenient
    one. No verdict differed under pinning either.
  - **The number for build agents, and it is not a comfortable one:** on **two processors**
    the whole command measures **4496-4980 ms against a 5 s budget**, five counted runs. It
    fits with **no margin** - the worst run is 99.6% of the promise. More services, a slower
    disk or a busy processor will exceed it, and a build agent is exactly that environment.
    That is the condition under which "raise the budget" comes back in `01`.
  - **Checked separately because the default depends on it:** `Environment.ProcessorCount`
    **does honour a limit imposed from outside** - under `start /affinity 3` it reports 2 and
    the default degree becomes 2 - so a constrained agent gets the right degree with no code
    change. It is cached at startup, so it does not see a process changing its own affinity
    later. The probe's own header said `processors 16` while pinned to 4 for exactly that
    reason, which looked like evidence of the opposite and would have bought a fix the
    product does not need.
  - **Deliberately left out:** a switch to set the degree, cancelling the pass mid-flight -
    `snapshot create` installs no signal handler today, so the first Ctrl+C ends the process
    exactly as before - caching between runs, which `ADR-13` warns against for an audit tool,
    and parallelising the first pass, which fits its budget at 457-544 ms.

- **S5b cut in two, and the first half built: `bws snapshot diff`** (`4081734`). **[contract]** Three at
  once: exit code 5, the switch surface gains `--exit-code` and a `snapshot diff` column, and
  the comparison's JSON shape is a new frozen contract. Closes open question 11 of the
  specification, which was written two slices earlier and had to be answered before any of
  this could be built.
  - **Why it was cut.** Three comparison modes, per-field breakdown, filtering, export, exit
    codes, the elevation trap and four read states is twice a slice again. But **two of the
    three modes are one mechanism** - snapshot against snapshot and machine against machine
    differ only in where the files came from - so the cut runs between "two files" and
    "against the live machine", not between features.
  - **Spelled `snapshot diff`, not `bws diff`, and that was nearly got wrong.** The choice
    was put to the owner as though it were open, without mentioning that `E1` already writes
    it - and the command surface is a frozen contract, so the shorter spelling would have
    been a breaking change bought by accident. Caught by reading the specification before
    writing the parser rather than after.
  - **The fixture came before the design, and that was the most valuable decision here.** A
    snapshot taken under a restricted token (`runas /trustlevel:0x20000`) holds **807 entries,
    `elevated: false` and five entries with `unreadable`** - both traps at once, in a real
    file. Designing against an imagined one would have cost a second rewrite, which is the
    same reasoning that cut S5 in the first place.
  - **The engine walks the document rather than a hand-kept list of fields.** A hand-kept list
    drifts from the schema the first time somebody adds a field, and drifts silently: the new
    field simply never shows up as changed. Walking the tree compares a field the day it is
    added. The tree comes from `SnapshotJson`, so writing and comparing cannot disagree about
    what an entry is made of.
  - **Comparing two files never opens the service control manager.** A pipeline step on a
    build agent has no business needing rights over that agent's services, and reading 810
    entries this path never looks at would spend half a second saying nothing.
  - **Found by running it against real files, twice, and neither would have come out of
    reading the code.** Entries with zero differences were landing under "changed" because
    they carried a field that could not be compared - the summary read "5 changed, 0
    differences" and `--exit-code` **would have returned 5 for a comparison that found
    nothing**, which is the same false alarm the whole elevation handling exists to prevent,
    arriving through the exit code. And **"nobody asked" and "the machine refused" are two
    different states that the format already tells apart**: the first is a fact about the run
    and is said once, the second is a fact about one entry - five services refuse their
    descriptor while eight hundred hand it over. Conflating them would have printed the same
    admission on all 810 rows, the shrug this project already avoided once with triggers.
  - **Found while writing a test:** the shared specimen is already running, so a test setting
    its status to running would have passed while checking nothing - the ADR-10 trap exactly.
    It stops the service now and asserts both sides of the change.
  - **Deliberately different from `git diff --no-index`:** metadata is kept out of the
    differences, because `takenAt` differs on every pair ever compared. What metadata does
    produce is a caveat when it **weakens** the comparison - different privilege level,
    machine, Windows version or tool version.
  - **Deliberately left out:** `--live`, filtering differences by field, export, restoring,
    baselines.

- **S5b2, and with it the whole of `D2`: `bws snapshot diff FILE --live`** (`23b9a25`). **[contract]**
  One switch, on one verb. The file is "before" and the machine is "after", which is the
  direction of the sentence this mode answers.
  - **A word rather than an inference from "only one file was given".** `E1` writes it that
    way and it is the better of the two: reading the machine costs a couple of seconds and
    needs rights over its services, so it is not something a command should start doing
    because an argument was left out. One file on its own is refused, and so are two files
    with `--live`.
  - **The acceptance test the plan asks for, run on the live machine** inside the four
    services the owner allowed. Snapshot, stop `GamingServices`, compare: **one entry, one
    field, `status: Running -> Stopped`, filed as running state, nothing else.** Restored
    afterwards, `sc.exe` confirming `RUNNING` and `AUTO_START` - the state read before it.
  - **Measured on the same run, and it is a fact about the product rather than the test:** a
    minute later, `XblAuthManager` and `msiserver` had started **by themselves** - one woken
    by Gaming Services coming back, one trigger started. **On a live machine the running
    state moves on its own within minutes.** That is the measured justification for reporting
    running state apart from configuration: without the split, every scheduled `--live` would
    be red for reasons that are not drift. Configuration differences throughout: none.
  - **The second pass is mandatory on the live side, and that is not obvious.** Without it
    all 810 entries land under "one side never read this" - and an uncompared field is not a
    difference, so the command would still report that nothing changed. **The first version
    of the test did not catch that**, and it was strengthened before the mutation entry
    written for it could prove anything. Found by asking what the test would miss rather
    than by watching it fail.

- **Five working methods taken from the owner's two Go projects** (2026-08-01, at his instruction, no commit of their own - `tools/`, `docs/` and
  `CLAUDE.md` are all excluded from the repository). None of it is in this repository - `tools/`, `docs/` and `CLAUDE.md` are all
  excluded - so this entry is the only record in version control that it happened.
  - **`tools/mutate/mutate.ps1`**: targeted mutation. Ten entries, ten caught. Every "verified
    by breaking it" claim this project has made was previously a sentence that died with the
    session that wrote it. Now it is a command. Checklist point 11.
  - **`tools/audit/audit.ps1`**: the bridge between the documents and the code, for the tables
    that exist in both. It found nothing on the day it was written, which is the point - the
    exit code table had been kept in step by hand an hour earlier. Checklist point 12.
  - **A regression surface table** in `docs/04`: 26 behaviours, 21 fully guarded, 3 partly,
    **2 with no guard at all**, each partial one carrying what it does not cover.
  - **Four states of every path** (normal, failure, teardown, degenerate but legal) as rule 10
    of `CLAUDE.md`, and the five dimensions of a shared value in `docs/04`.
  - **Two mistakes made while doing it, both worth more than the transfer.** The audit's own
    self test reported 2 of 4 because PowerShell unrolls a one element array and
    `PSCustomObject` has no `Count` - so every breakage producing exactly one complaint read
    as "found nothing". **The check lied in the direction that looks like diligence.** And
    adding a rule in the middle of a numbered list silently broke a reference to it fifteen
    screens up in the same file - fixed by appending instead, because a number once given
    stays given.

- **Three overdue items, closed together because they are one subject: the four read
  states** (`157f7af`). **[contract]** Found by doing what rule 4 asks at the end of a stage - walking
  the documents for sentences whose due date had passed. Two of the three said "at S5", and
  S5 had closed an hour earlier.
  - **The delayed start flag is read for every non-driver entry**, not only for automatic
    ones. **Measured, and it is the whole argument:** eight entries on this machine carry it
    **set while not being automatic** - `WinRM`, `MSDTC`, `PcaSvc`, `dcsvc`,
    `dmwappushservice`, `edgeupdatem`, `gupdate`, `gupdatem`. A snapshot could see none of
    them, so making one of those automatic later would show up as a start type change with no
    hint that the entry had been marked delayed all along. Reads go from 82 to 339.
  - **Cost: none measurable.** 451-481 ms before against 455-461 ms after, five counted runs
    of each build interleaved, over 810 entries. The spread inside each variant is wider than
    the gap between them, which by this project's own rule means there is no difference.
  - **Reading it and showing it were separated, and that was not in the original question.**
    The table would have started printing `Manual (delayed)` on eight entries - a sentence
    about a setting Windows ignores there. It now annotates only where the flag changes what
    the start type means. Found by reading the cell code before shipping, not after.
  - **`ReadOutcome.Denied` stopped claiming to be about permissions.** It said "a fact about
    our permissions" while the number beside it had always told the truth: 1060 when a
    service vanished between enumeration and the question, 5 for a real refusal. **Corrected
    the name's meaning rather than adding a fifth state** - the number already carries the
    distinction and reaches the JSON, and a new state would have to be learned by every
    consumer, the schema and the comparison. A guard holds that the number survives the trip.
  - **One of the three turned out to be already closed, and nobody knew.** The `unreadable`
    JSON shape was recorded as unguarded. It stopped being so when the listing's own mapping
    moved into the core - the listing and the snapshot now render through one type, and a
    test has held that shape against a refused specimen since. **A gap list is worth acting
    on partly because acting on it is how you find out an entry is stale.**
  - Found while writing the guard: `start:manual` covers **347 drivers** of 569 entries, and
    drivers carry no such flag at all. A test asserting "every manual entry has one" was
    asserting the wrong thing, and it failed for the right reason.

- **S6 cut into four, and the first quarter built: a window that shows the list** (`0666bf2`, fixed in `d478137`).
  `ADR-23` and `ADR-24` were written before the first screen existed, answering a question
  the owner asked from experience on other projects - interfaces drift into looking like two
  products. The answer is that **XAML is prose**, so the rule has to be checkable.
  - **Order inside the slice was a decision:** guard first, then the theme file, then the
    screen. The other way round means the first screen decides what the guard has to
    tolerate, which is how guards get weakened into decoration.
  - **Three appearance guards**, in the shape the output-channel ones already use. Every
    colour, spacing, type size and thickness lives in one file, and a value written into a
    view fails the build. Verified by writing `Margin="7"` into the window and watching it
    name the file, the line and both offending values.
  - **Dark only**, on the Fluent theme built into WPF. That narrows `ADR-4`, which said "for
    light and dark" - a sentence describing what the framework can do that read as a promise
    of both. Corrected where it stands rather than quietly contradicted. It also closed an
    open question about changing the theme from code, which was about a switcher nobody is
    building now.
  - **Hand-rolled view models, zero new dependencies** (owner's decision). WPF has the
    notification interface built in and the whole mechanism is twelve lines, against a
    dependency in a tool that runs with administrator rights.
  - **Found by running it, and it would not have come out of reading the code: the first
    window came up with an empty list and no error at all.** The view models were `internal`,
    and WPF data binding reaches public members through reflection - when it cannot, it
    reports nothing and shows nothing. The same family as every other trap here: silence
    instead of a refusal.
  - **Found in the same run:** a `DataGrid` column header cannot bind to the view model.
    Columns are not in the visual tree and inherit no data context, so the obvious binding
    silently shows an empty header. Text reaches the markup through the application's
    resources instead, keyed by the same keys the language file uses.
  - **A new test project, `tests/Bws.Gui.Tests`.** The view models are the half of a window
    that can fail on its own at three in the morning, and the half a screenshot cannot check.
    Five tests, three of them proved by mutation.
  - **Found on the owner's screenshot, and worth recording as a fact about the framework:
    the built-in Fluent theme does not style `DataGrid` selection.** A selected row came out
    light grey with dark text on a dark window, and the focused cell drew a border that reads
    as somewhere to type - on a list nothing can edit. That is "two products in one window"
    arriving from the one direction `ADR-24` assumed the framework covered. Selection is ours
    now, in the theme file, and it takes the whole row.
  - **Left open on purpose, and handed to the slice it belongs to:** maximised to 2560 px the
    star columns grow past the viewport and the last two fall off the right edge. **In an
    ordinary window the layout is correct** - the owner's screenshot has all six columns in
    place. Four different width schemes were tried and none changed the maximised behaviour,
    so the cause is deeper than the ratios and **it is not being guessed at further**. S6d
    brings configurable columns anyway, which makes it that slice's work rather than a patch
    here. **NOT ESTABLISHED:** whether the grid is handed the viewport width when the window
    changes state.
  - **Deliberately left out:** sorting and column reordering. Both belong with the slice that
    also has to answer what a sort does while the list refreshes underneath it, which `A10`
    has five rules about.

- **S6b: one box for searching, expressions and the query language** (`ff581db`), `A1` to `A3` plus the
  drivers switch (`A7`). 452 tests against 424, mutation 24 of 24, audit clean.
  - **`QueryParser.Parse` takes a second argument** turning bare words into regular
    expressions - the regex switch of `A2`. Members with a field keep their own operators
    either way, so the switch decides how the search half reads and leaves the language half
    alone. It lives in the parser rather than in the window because working out which part of
    the text is a bare word is the scanner's job, and a second copy of that in the interface
    would drift from this one in silence. **Not `[contract]`:** additive, default off, and the
    command line does not pass it.
  - **`Query.Excludes(field, value)` is new and public.** The drivers switch has no state of
    its own - it reads the query and writes into it - so it needs to ask whether the member it
    stands for is in the text. Asks about the value **as written**, because `type:driver`
    compiles to two symbols and neither of them is the word that was clicked. `QueryTerm` now
    keeps the written values alongside the compiled ones for exactly this.
  - **The drivers switch is a member of the query, not a state beside it** - the third of the
    three ways out named at the end of `docs/07`, chosen by the owner on 2026-08-02. Turning it
    off writes `!type:driver` into the box. Default on, so the window still opens showing what
    `bws list` shows, which is what S6a promised.
  - **It appends and removes at the end of the text only.** Cutting a member out of the middle
    means finding it in text that may hold quotes, which is the scanner's job again. Somebody
    who typed the exclusion first keeps it and the switch goes back to where it was instead of
    pretending. Pinned by `The_switch_will_not_undo_what_it_could_not_have_written`, so the
    limit stays known rather than being rediscovered as a bug.
  - **Signatures and memory are not read here, and the window says so in those words.** The
    command line answers `signed:no` by verifying every binary, measured at 1100-1245 ms over
    810 entries and 544 files - a price a listing pays once and a search box cannot pay per
    keystroke. Reading it in the background belongs to S6c with the rest of `A10`.
  - **One count, two meanings, and the sentence had to pick one.** `QueryResult.Unreadable`
    folds "the machine refused" together with "nobody looked". The window shows the sentence
    about the unread family **instead of** the one about a refusal, because the second would
    turn "nobody asked" into "you were not allowed" - the distinction this project spends most
    of its rules on. Cost: a query mixing an unread family with a genuinely refused field
    reports only the first.
  - **Measured, six process runs, first discarded as cold, eight queries interleaved per run:**
    filtering costs **0.42-2.25 ms over 810 entries** against the 50 ms of section 8.1. The
    worst case is a **bare word at 2.08-2.25 ms**, four free-search fields times 810 entries -
    a member with a field sits at 0.42-0.96 ms. Worst run is 4.5% of the budget.
  - **What that number does not cover, said plainly:** drawing the list. It measures the view
    model - reading the query, judging every entry, building the rows that are left. **NOT
    MEASURED:** what the grid costs when its source is replaced, which needs a window on a
    screen and a person watching it.
  - **The first measurement was worthless and the reason generalises.** In milliseconds it read
    `0 ms` for seven of eight queries - not a fast measurement, a measurement below the
    resolution of the instrument, and writing it down as zero would have been the instrument
    lying quietly. It also printed the row count read **after** the loop, so every line made
    the same claim about a different query. Both came out of looking at the output, not out of
    writing the code.
  - **Found by mutation, and the most valuable thing in this slice:** the parity test for
    expressions asked about `^sql`, and on a machine with no SQL Server **both sides answered
    with nothing**, so it passed while checking nothing. Turning the switch off in the view
    model left it green. **Two empty results are equal** - so every comparison of two results
    has to claim as well that the result is not empty, and for a switch, that it is not
    everything. This is `ADR-10`'s fake-test trap in a form with no fake in it.
  - **`QueryParityContractTests` is the only place that sees both interfaces**, and what it
    cannot prove is worth writing down: both sides call the same code in `Bws.Core.Querying`,
    so a bug in the engine agrees with itself. It proves the wiring, not the language. Two
    concessions to a live machine: comparison runs over the **intersection** of the two
    readings, and queries about running state compare with a tolerance, because the running
    state moves on its own - measured at S5b2.
  - **`Bws.Integration.Tests` now references `Bws.Gui`**, deliberately **without `UseWPF`**.
    Turning that on changes which namespaces are implicitly imported - it adds the WPF ones and
    drops `System.IO` - and thirty two lines of that project stopped compiling. It is not
    needed, because the view model knows nothing about WPF, which means the project now proves
    that by building at all. The shipped projects stay apart, held by `DeclaredReferenceGuards`.
  - **The view model changed shape.** `Entries` became `Rows`, a plain list swapped in one
    notification rather than an `ObservableCollection` refilled item by item, and the entries
    are kept so every keystroke can filter them again. Rows are now built on the interface
    thread with the filtering rather than off it with the reading - the cost measured above is
    what makes that acceptable.
  - **Deliberately left out, in the backlog with a destination:** match highlighting (item 14)
    and field autocomplete (item 15), both handed to S6d because both need what the clickable
    filters of `A5` need. Also **item 18**: the regex switch does not fit inside the query text,
    and a saved set is text - Phase 4 has to store the state beside it or expand bare words into
    `/expression/` when saving.
  - **Verified by the owner's screenshots**, six states of the window: `810 entries.` on an
    empty box, `108 of 810` for `start:auto`, `13 of 810` for `start:auto !status:running`,
    `339 of 810` for `!type:driver`, and a typo leaving the list alone while a red sentence
    names the nearest real value. **The strongest of them is the one nobody asked for:**
    `!type:driver` typed by hand unticked the Drivers box on its own, which is what tells a
    member of the query apart from a switch pretending to be one.
  - **`13` against the `12` the command line printed a minute earlier is not a disagreement.**
    The running state moves on its own, measured at S5b2, which is exactly why the parity check
    compares queries about it with a tolerance. Two numbers always equal would be a fact about
    this machine standing still, not about our filtering.
  - **High contrast settled, and the prediction was wrong about the direction.** Recorded
    before the run: the window would come apart in the middle, WPF swapping the control brushes
    while our nine stood still. What happened: **the window barely changes.** The system takes
    the title bar and the border, the content stays ours, dark and readable - `ThemeMode="Dark"`
    does not give way to high contrast, which was the one point written down as not predicted.
    Owner's decision: leave it. That is a consequence of `ADR-24` rather than a departure from
    it, and `ADR-24` now says so with what would overturn it. **Three brushes were not on that
    screenshot** - row selection and the two coloured lines, the very ones flagged as riskiest -
    so they stay **NOT SEEN**, as backlog item 20.

- **S6c: the list lives** (`cbb35cc`), which is `A10` minus the background reading of expensive data.
  479 tests against 452, mutation 29 of 29, audit clean.
  - **A probe before the slice knocked down a sentence this project had written down.**
    `docs/02` called `NotifyServiceStatusChange` "the right candidate" for `A10` and left NOT
    ESTABLISHED beside whether CsWin32 could generate it. It generates it without complaint -
    and the documentation the generator pastes into the generated file says outright that it
    **cannot report on driver services**, which on this machine is **471 of 810 entries**. It
    also allows one outstanding request per service, delivers through an APC to a thread
    parked in an alertable wait, and is spent after a single notification. The sentence was
    written without reading what the function covers, and has been struck out saying so.
  - **Measured, five interleaved pairs, cold pair discarded:** the cheap reading costs
    **13-22 ms over 810 entries** against **423-500 ms** for a full one. The spread inside
    either variant is narrower than the gap, so the difference is real. That is what makes a
    **one second** tick reasonable - under two per cent of one processor.
  - **New primitive, `IScmCatalog.ReadStatuses`:** name, status and process id for everything,
    from one enumeration. Three members rather than a whole entry, because everything else is
    configuration that somebody has to change deliberately. What makes a full reading expensive
    is the handle opened per entry, not the enumeration - so leaving that out is the whole
    saving rather than a tuning of it.
  - **The view model changed shape again, and this time it was not a choice.** Swapping the
    whole list in on one notification - right for filtering - drops the selection and sends the
    scroll to the top on every refresh, which is the first thing `A10` forbids. So `EntryRow`
    stopped being immutable, took its identity from the service name (`ADR-14`) and started
    notifying per cell, and the visible collection is now **reconciled entry by entry**: drop
    what is unwanted, then insert what is missing where it belongs. The rows stay the same
    objects throughout, and that is the entire mechanism - a selection is a reference to one.
  - **The cost of that was measured rather than assumed:** filtering still runs in
    **0.43-2.40 ms over 810 entries** across six runs, the same as before the change. It still
    does not cover the grid drawing itself, and no test here can.
  - **Freezing during interaction is announced, not silent.** While the mouse is over the list
    or the keyboard is in it, nothing joins or leaves and cells keep moving - so a row can read
    Stopped under a query about running services. The line under the list says so, because a
    list quietly disagreeing with its own query would be rule 8's silence arriving from the one
    direction where it looks like politeness.
  - **A name nobody knows costs a full reading.** The cheap reading knows an entry appeared and
    nothing else about it. Putting a row on screen with two columns saying "unknown" would be
    cheaper and impossible to explain to anybody looking at it.
  - **The loop does not live in the view model, and that was deliberate.** It exposes one tick
    and the window calls it on a timer. A loop inside would need a thread, a cancellation and a
    rule for the window closing mid-read - three things to get wrong - and a test would have to
    wait real seconds instead of calling a method.
  - **The mutation tool caught its own author.** Three entries from S6b went STALE and BROKEN
    the moment the view model was reshaped: two matched nothing, one no longer compiled after
    mutation. That is the point of having STALE as a verdict at all - "verified by breaking it"
    rots when the code moves, and it rots invisibly. They were rewritten against the new shape.
  - **Deliberately left out:** the second phase of `ADR-13`. At S6b this was written down as
    belonging to S6c, and that is **withdrawn**: it is separate machinery - background work,
    cancellation, columns filling in mid-read - and welding it to a refresh loop is how a slice
    grows while being built. **NOT MEASURED:** what the refresh costs on a two-processor
    machine, where the snapshot budget already has no margin.

- **Four mechanisms against races and tangle**, after the owner asked how to prevent them in
  code they do not read. 484 tests against 479, mutation 33 of 33, audit clean.
  - **The answer was not another document, and that is the finding.** The threading rules have
    been in `docs/06` since July and `docs/02` promised concurrency tests in the same month.
    Neither caught anything, because prose has nothing checking it - which is rule 3 of this
    project demonstrated on this project.
  - **Found by reading the code while answering:** `LoadAsync` had no guard against overlapping
    calls. Two presses of F5 sent two full readings and the one that **finished** later won
    rather than the one that **looked** later, so the list could settle on the older of two
    answers silently. None of the 479 tests could see it - none called anything twice at once.
  - **It is not a data race, and the distinction matters for whoever reads this next.** Every
    continuation comes back to the interface thread, so no two fields are ever written at once.
    What breaks is ordering and reentrancy. The real threading in this project is the core's
    second pass and it has its own guard.
  - **A test with its own ceiling, because the first version hung.** Removing the guard under
    mutation made the second call block on the same gate as the first and the run never ended -
    a hang reports nothing at all. Anything asserting that a call comes straight back has to
    time out and fail rather than await. This project paid for the same lesson once already,
    choosing a regular expression engine.
  - **An invariant over every ordered pair of thirteen operations** - typing, both switches, a
    tick, F5, the cursor arriving and leaving. Not a scenario: nobody can enumerate the orders
    in which those land, but what must be true after each of them can be stated.
  - **`BackgroundWorkGuards`:** `async void` only from a list with a reason beside each entry
    (one today, an overridden key handler the framework leaves no choice about), and work
    started and abandoned with **no list at all**, because there is no case for it here.
  - **Analyser rules named one at a time rather than a mode, and the measurement is why.**
    `AnalysisMode=Recommended` reports **496 warnings on this tree, 248 of them CA1707** -
    identifiers containing underscores, which is every test name in the project and a
    deliberate convention. A guard that screams at correct code is switched off within a week.
  - **The narrow set found one real defect on its first day:** `X509Certificate.CreateFromSignedFile`
    was never disposed, leaving one native certificate context per signed file to a finaliser -
    **544 of them in a single snapshot**. No test could have seen it: the answers were right
    and the run finished.
  - **And it would not have found the hole it was proposed for.** Analysers see patterns, and a
    missing reentrancy guard is a design decision. Worth writing down, because it deflates a
    mechanism I proposed myself.
  - **Deliberately not built:** a report on file and method length, offered as the fourth
    mechanism and not chosen. Backlog item 23, with the reason - I had named it the weakest of
    the four, since line count is a poor measure of tangle.

### Fixed

- **One malformed file could end a whole run** (`b9831a7`). `WindowsBinaryInspector` caught two
  exception types by name, so a third - a truncated certificate, a path the platform
  rejects, a handle that goes away mid-read - escaped to the top level and lost all 809
  other entries with exit code 1, on somebody's production machine. Now broad and reported
  as a refusal with its number and sentence. Second of exactly two broad catches in the
  project, and the comment in the other one saying it was the only one is corrected.
  - `HResult` rather than `GetLastWin32Error` in that path: by the time an exception has
    been built and thrown, the thread's last error is usually something else.

- **`--signatures` was absent from the usage text** (`b9831a7`), and `--timing` was shown only under
  `list` despite working on every verb. A switch has exactly one route to discovery, so an
  accepted and unmentioned one may as well not exist.
  - Guarded now by `UsageContractTests`: every accepted switch has to appear in the help,
    and every switch in the help has to be accepted. Written after making the mistake, and
    it immediately found a second one.

- **A switch missing its value reported itself as unknown** (`b9831a7`). `bws list --query` answered
  "Unknown option: --query", sending somebody to hunt for a typo in a word they had spelled
  correctly. Its own list and its own message now. Found by the guard above, which was not
  looking for it.

- **The binaries claimed to be version 1.0.0** (`38ae53f`). With no `Version` property anywhere the SDK
  stamps `1.0.0.0`, so every build declared a first release while both changelogs held
  everything under `[Unreleased]`. **Set to `0.1.0` by the owner on 2026-08-01** in
  `Directory.Build.props`, which is the only place any project takes it from. Rule 11 keeps
  this the owner's decision, so the number is theirs and the plumbing is mine.

- **The elevation check in `CLAUDE.md` answered `False` on an elevated session** (`38ae53f`), and had
  done so all along.** It asked `IsInRole('Administrators')`, which compares the *name* of
  the group. On this machine the group is `BUILTIN\Administratorzy`, so the answer was
  always no. Asking through `WindowsBuiltInRole.Administrator` compares the well-known SID
  `S-1-5-32-544` and answers correctly in any language.
  - **What this invalidated, and it is not small.** Every environment note recorded as
    "measured on a shell without administrator rights" was measured with rights. Two claims
    lose their evidence: that configuration is never refused on this machine, and that
    stopping and starting the four permitted services succeeded *without* elevation - the
    latter was the whole basis for "write permissions are per service, not per elevation".
    The run itself stands, because it compared against `sc.exe` after every step. The
    inference does not. Corrected in `docs/04` and `CLAUDE.md` rather than deleted.
  - **What survives:** `sc config` being refused on `GamingServicesNet` while the same
    process could stop it. That refusal was handed to an elevated caller, so it is a fact
    about that service's descriptor and it still means write rights are not one right.
  - Found by a contradiction, not by review: `whoami /priv` showed `SeDebugPrivilege`
    enabled in a session the documents called unelevated. Two readings of one fact
    disagreeing is the cheapest signal that one of them measures something other than its
    name.

- **`666 of 869` was never a count of refusals** (`38ae53f`) and it had been used in four documents and
  two code comments as the empirical justification for the `Denied` state. Walking the same
  keys: **666 of them have no security value at all.** Of the rest, an elevated session
  reads 203 and is refused none, a restricted token reads 157 and is refused 46.
  - Replaced with a measurement of the path the product actually uses. Over the manager's
    own 810 names, under `runas /trustlevel:0x20000`: opening for configuration is refused
    **3** times, opening with `READ_CONTROL` **8**, and every query on a handle that did
    open is refused **none**. With elevation all four numbers are zero.
  - **The four states keep their justification, but a different one.** It was "refusal is
    the default case". It is now "confusing refusal with emptiness makes a diff report
    changes that never happened", which holds at eight entries as well as at six hundred.
  - **First live specimens of a refused read on this machine**: `RoutePolicy`, `ZTDNS` and
    `ZTHELPER` refuse to open at all under a restricted token. `docs/05` records them.

- **A review of every source file and every document, at the owner's request** (`0305c79`). Nine things
  found, three of them contradictions inside a single document, two of them user-visible.
  - **`snapshotcreate` was going out to users.** The message telling somebody where a switch
    does work spelled the command from its enumeration value, so the answer to "then where
    do I use `--note`" was a word nobody can type. Guarded now by `UsageContractTests`, in
    both directions: every command appears in the help, and no message names a spelling the
    tool refuses.
  - **`bws snapshot` on its own answered "Unknown option: snapshot".** It is neither an
    option nor unknown - it is a command waiting for its verb. Two sentences now, because
    one wording has to lie about one of the two cases: half-typed and non-existent are
    different mistakes.
  - **The guard's own switch list was stale**, so `--memory` and `--note` had never been
    checked against the help at all. Both were in it, by luck rather than by guard.
  - **`docs/06` said the user-facing text guard "was deliberately not built"** while
    `UserFacingTextGuards.cs` exists and `docs/02` describes it working. A sentence that
    outlived its own invalidation, which is the failure mode the rule about prose with an
    expiry date names.
  - **The same document still called refusal "two thirds of entries"** one screen below the
    paragraph retracting that number.
  - **The acceptance command in `01` and `04` was `bws --query ...`**, without the verb that
    became mandatory at S3. It exits 2 today. Both fixed.
  - **`01` promised `AND`/`OR`/parentheses and a `~` operator** in the same document that
    records the decision to have none of them.
  - **The frozen switch table in `02` had neither `--memory`, `--note`, nor the
    `snapshot create` column**, and the list of listing fields stopped at signatures while
    seven more had shipped. A contract table that is out of date misleads better than no
    table at all.
  - **The snapshot is over its performance budget and nobody had said so.** Recorded now,
    against checklist point 7, which asks for exactly this to be reported rather than left
    for a reader to notice.
  - Three memory files carried resolved questions as open ones, a tool list two entries
    short, and an interop inventory missing two families.

- **A second pass over the comments, which have no guard at all** (`2395ba0`). Three claims in code
  were false and none of them could fail a build.
  - **"Around three seconds" for verifying signatures, in six places.** The real figure is
    4620-7656 ms over 810 entries and 544 files, measured over seven runs, and it has been
    in `docs/02` and `docs/04` since the day it was taken. The three-second estimate predates
    that measurement and outlived it in every comment that quoted it.
  - **"Exactly two broad catches in the project", when there are five.** The sentence was
    already corrected once, on the way from one to two, and then three more were added under
    it. Now it names where they are and what would make a sixth worth questioning, which is
    a claim that survives being right.
  - **Two different fields each called "the first whose ordinary state is not read".** They
    cannot both be. Triggers stopped being one the moment they measured cheap enough to read
    every time, and the sentence stayed. Nothing in a build notices a comment contradicting
    another comment.
  - Also corrected: the memory pass described itself as costing under a millisecond, which
    is what its calls cost and not what it costs.

- **A consistency review before moving to a new session**, of the documents, the memory and
  the comments. Three findings, and the shape of them is the argument for doing it at all.
  - **A number in a comment rotted for the third time.** The entry point said "exactly two
    broad catches in the project", was corrected to five, and had reached six. Instead of
    correcting it a third time, the count moved into a guard: every broad catch is now in a
    named list with its reason, and one appearing anywhere else fails a test. Proved by
    mutation.
  - **A summary contradicted its own body.** The delayed-flag reader still said it was asked
    "only where it can be true" while the code below had been changed that day to ask for
    every non-driver entry. The kind of thing only reading finds, since nothing compiles a
    summary against what it summarises.
  - **A false correction was nearly written into the documents.** A check of "five entries
    name a file that is not there" came back as one, which read as the machine having
    changed. It had not: `ConvertFrom-Json` in Windows PowerShell does not enumerate through
    a pipeline, so the count was of one object rather than five items. **The mistake pointed
    at "something stopped working"**, which is the direction a review is most willing to
    believe. Recorded with the other shell traps.

### Known gaps

Carried here rather than in a session's memory, because sessions end.
- ~~**The `unreadable` JSON shape has no guard.**~~ **Stale, and closed before it was even
  written down.** The listing and the snapshot share one shape - `ListingJson` renders
  through `EntryDocument`, the type the snapshot writes - and `SnapshotTests` holds that
  shape against a refused specimen. The gap was recorded when the listing had a mapping of
  its own, and moving that mapping into the core closed it without anybody noticing. Found
  2026-08-01 while acting on the entry, which is the argument for acting on them.

  *Original wording, kept because the way out it proposed is still the way to produce a
  refused entry on a machine that refuses nothing:* An elevated session is refused nothing, so
  an ordinary run produces no such entry. The `Reading<T>` type behind it is guarded.
  Closing this is cheaper than it used to look: under a restricted token the manager refuses
  to open `RoutePolicy`, `ZTDNS` and `ZTHELPER`, so a run under `runas /trustlevel:0x20000`
  produces the shape - no test project needed, only a way to run one from a test without
  the flakiness that costs.

- ~~**The snapshot is over its performance budget.**~~ **Closed at S5a1**, by the first of
  the three ways out. It measures 1723-2141 ms against the 5 s `8.1` promises. The other two
  - a snapshot without signatures, and raising the budget - stay unspent, and `01` records
  what would bring each of them back.
- ~~**A failed configuration read is always marked as a refusal.**~~ **Closed 2026-08-01 by
  correcting a name rather than adding a state.** The mistake was in the sentence, not the
  data: `ReadOutcome.Denied` claimed to mean "a fact about our permissions" while the number
  beside it had always told the truth - 1060 for a service that vanished, 5 for a refusal.
  A fifth state would make every consumer, the schema and the comparison learn a case the
  number already describes. A guard holds that the number is not flattened on the way to the
  file, since that is the only thing keeping the two apart.
- ~~**The delay flag is not read where it does nothing.**~~ **Closed 2026-08-01.** Read for
  every non-driver entry now. Eight entries on this machine carry it set while not being
  automatic, so a snapshot could not see any of them.
- **Nothing guards the descriptor staying on a handle of its own.** See the S4 permissions
  entry: the mistake is one word long and no test on an elevated machine sees it.
- **An entry invisible without elevation has no representation anywhere.** Not in the four
  states, not in the fake, not in the JSON. It is a missing row. Snapshots now carry the
  privilege level they were taken at, which is the half that was missing - the other half is
  the diff actually using it, and that is S5b.
- **Nothing guards that a snapshot is written atomically.** The window is microseconds wide.
  See the S5a entry: replacing the mechanism leaves every test green.
- **No date on a signature, and no countersignature timestamp.** A certificate that has
  expired since signing is a different fact from one that was expired when it signed.
- **The second pass cannot be interrupted.** Ctrl+C during those five seconds ends the
  process, which is harmless for a read but not the three-level handling a plan run gets.
- **The private working set is not read.** It is the number Task Manager shows under
  Memory, and the two ways to it are a mask seven processes refuse and performance counters
  whose set names are translated. Anybody comparing our column against Task Manager's
  default will see two different numbers, both correct.
- **`dwControlsAccepted`** likewise - a service's own declaration of whether it accepts
  being stopped, which would make a fifth preflight warning. Open question: whether it
  enters the public JSON contract or is read only when building a plan.
- **`ControlServiceEx` can carry a reason for stopping** into the system event log. We use
  `ControlService` and leave that empty, which is a poorer operation than the API allows,
  in a product whose heart is audit.
- **The fourth state of a field has no word in the query language.** `docs/07` promises
  four and gives three. Deferred by the owner until background loading exists, and pinned
  by a test so it is not rediscovered as a bug.
