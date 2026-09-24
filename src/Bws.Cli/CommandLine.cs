using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// What the person asked for on the command line.
///
/// The verbs come from E1 of the product specification rather than from taste. That
/// section is a frozen contract: people put these lines in runbooks, so inventing a
/// shorter spelling later costs somebody a broken document.
///
/// Still hand rolled rather than done with a framework. The OptionSurface.Surface is four verbs, and
/// it stays in this one place so that swapping it later is mechanical.
/// </summary>
internal sealed partial record CommandLine
{
    internal CommandKind Kind { get; private init; }

    /// <summary>The entry a write command is about. Empty for <see cref="CommandKind.List"/>.</summary>
    internal string ServiceName { get; private init; } = string.Empty;

    /// <summary>
    /// What the start-type verb was asked for: the word, as it was written, and whether a stop
    /// rides on it. <see cref="StartTypeAsk.None"/> for every other verb.
    /// </summary>
    internal StartTypeAsk Setting { get; private init; } = StartTypeAsk.None;

    internal bool Json { get; private init; }

    internal bool Timing { get; private init; }

    /// <summary>Show what would happen and change nothing.</summary>
    internal bool DryRun { get; private init; }

    /// <summary>Allow the entries that break to be taken down as well.</summary>
    internal bool Dependents { get; private init; }

    /// <summary>
    /// Read who signed each binary, which the listing does not do by default.
    ///
    /// Measured at 4620-7656 ms over 810 entries and 544 distinct files, against 476-551 ms
    /// for the rest of a listing, so it is asked for rather than assumed. A query about
    /// signatures turns it on by itself - somebody who wrote signed:no has already asked.
    ///
    /// The spread is wider than most whole operations here, which is itself the point: one
    /// run of this tells nobody anything.
    /// </summary>
    internal bool Signatures { get; private init; }

    /// <summary>
    /// Allow the tool to look at a launch path that lives on another machine.
    ///
    /// Off by default, and unlike every other switch here that default is not about cost. A
    /// service can register its image on a share, and asking the disk about it is an ordinary
    /// existence check that <b>blocks for 21 053 ms on an unreachable host</b> - measured
    /// 2026-08-02 against 1.23 ms for a local path - and authenticates as whoever ran this.
    ///
    /// With it off, the disk question for such an entry comes back <b>not read</b> rather
    /// than "missing". The path is still reported: which file a command names is worked out
    /// from text, and only the disk question needs the disk.
    /// </summary>
    internal bool FollowNetwork { get; private init; }

    /// <summary>
    /// Write the snapshot even though a file is already there.
    ///
    /// Off by default, and that default is the point. A snapshot is kept for months and
    /// compared later, so the file somebody is about to lose is exactly the kind of file that
    /// hurts to lose - and until 2026-08-02 this command replaced whatever was at the path
    /// without a word and ended with code 0, whether or not what it replaced was a snapshot
    /// at all.
    /// </summary>
    internal bool Force { get; private init; }

    /// <summary>
    /// Whether the forcing verb was asked to bring the entry back once the process is gone.
    ///
    /// <b>A switch rather than a second verb, and the reason is discoverability rather than
    /// tidiness.</b> An administrator whose service will not restart types the verb they know
    /// and looks for a flag. What they must NOT be given is --force on restart: on Windows that
    /// spelling already means "even if something depends on it", so it would be read as
    /// something else entirely by everybody who has used Restart-Service.
    /// </summary>
    internal bool Restart { get; private init; }

    /// <summary>
    /// Whether a report should also print the fields that are genuinely empty.
    ///
    /// It never governs a field nobody could read. Those print either way - rule 8 of CLAUDE.md,
    /// and a switch able to hide one would be that rule broken and spelled as an option.
    /// </summary>
    internal bool Full { get; private init; }

    /// <summary>
    /// Read what each running entry's process is using, which the listing does not do by
    /// default.
    ///
    /// Not for the reason <see cref="Signatures"/> is asked for. This one is measured at
    /// 5-8 ms over 810 entries and 110 processes, so the switch buys no time worth
    /// mentioning - it exists because memory is the one thing here that is a measurement
    /// rather than a description of how the machine is set up, and a plain listing stays the
    /// second of those. A query about memory turns it on by itself.
    /// </summary>
    internal bool Memory { get; private init; }

    /// <summary>
    /// Whether the listing should go and read who depends on each entry.
    ///
    /// Asked for rather than always read: a call per entry, measured at 236-259 ms over 313
    /// services on 2026-09-05, against 423-500 ms for the whole listing. A query naming the
    /// field turns it on by itself, exactly as one about signatures or memory does.
    /// </summary>
    internal bool RequiredBy { get; private init; }

    /// <summary>Null when no query was given, which selects everything.</summary>
    internal string? Query { get; private init; }

    /// <summary>
    /// Where the snapshot goes. Empty when nobody said, and then a name is worked out.
    /// </summary>
    internal string Path { get; private init; } = string.Empty;

    /// <summary>
    /// The second file a comparison reads. Empty when nobody gave one.
    ///
    /// The order is the order they were typed, and it is the order of the sentence a diff
    /// answers: what changed going from the first to the second. Swapping them swaps every
    /// before and after, which is why neither is worked out for the person.
    /// </summary>
    internal string Against { get; private init; } = string.Empty;

    /// <summary>
    /// Report differences through the exit code as well as on screen.
    ///
    /// Off by default, and that is a decision rather than caution. The table of exit codes in
    /// `docs/02` says what it says on purpose: an empty result is 0 and an incomplete result
    /// is 0, because a code answers whether the tool worked rather than what it found. A diff
    /// that failed by default would be the only command breaking that rule, and would trip
    /// every script that only wanted to print the differences. Asking for it is one word.
    /// </summary>
    internal bool ExitCodeOnDifference { get; private init; }

    /// <summary>
    /// Compare the file against this machine as it is right now.
    ///
    /// A word rather than an inference from "only one file was given". `E1` writes it this
    /// way, and it is the better of the two: reading the machine takes a couple of seconds
    /// and needs rights over its services, so it is not something a command should start
    /// doing because an argument was left out.
    /// </summary>
    internal bool Live { get; private init; }

    /// <summary>
    /// What the person wants their future self to know about this snapshot. Null when they
    /// said nothing, which is not the same as an empty note.
    /// </summary>
    internal string? Note { get; private init; }

    /// <summary>
    /// The longest to watch any one step.
    ///
    /// A minute by default, which is the number E1 of the specification already uses in its
    /// own example. It is a cap and not the deadline: an entry that keeps reporting progress
    /// is given the time it asks for, and this only stops a plan sitting on a terminal
    /// forever when the entry never finishes what it keeps saying it is doing.
    /// </summary>
    internal TimeSpan Timeout { get; private init; } = StepCeiling.Default;

    /// <summary>What was given to --timeout that could not be read as seconds. Null when fine.</summary>
    internal string? BadTimeout { get; private init; }

    /// <summary>
    /// What followed <c>snapshot</c> when it was not a verb we know. Empty string when
    /// nothing followed it at all, null when the question never came up.
    ///
    /// Its own field rather than a rejected word, because the two need different answers.
    /// A rejected word is something nobody has heard of. This is a command that exists and
    /// is half typed.
    /// </summary>
    internal string? BadSubcommand { get; private init; }

    /// <summary>
    /// Options nobody knows.
    ///
    /// <b>Bare words left here until 2026-09-01 and the sentence they got was wrong about them</b> -
    /// backlog 233. <c>bws start type Spooler manual</c> answered "Unknown option: Spooler, manual",
    /// which calls two words somebody spelled correctly options, and sends them hunting for a typo
    /// in a word that has none. See <see cref="Extra"/>.
    /// </summary>
    internal IReadOnlyList<string> Rejected { get; private init; } = [];

    /// <summary>
    /// Bare words a verb has nowhere to put.
    ///
    /// <b>Their own list since 2026-09-01, for the reason <see cref="BadSubcommand"/> and
    /// <see cref="Incomplete"/> already have theirs: the honest sentence differs.</b> An option
    /// nobody knows is a spelling to check. This is a word spelled perfectly that the verb has no
    /// room for, and the useful answer is how many names the verb takes - which is what
    /// <c>OptionSurface.TakesWhat</c> exists to say.
    ///
    /// <b>The fault is OLDER than the verb that made it easy to meet.</b> <c>bws stop A B</c> has
    /// answered "unknown option" since the day stop was built. <c>start-type</c> only raised how
    /// often somebody lands on it, because it stands one character from <c>start</c>.
    /// </summary>
    internal IReadOnlyList<string> Extra { get; private init; } = [];

    /// <summary>
    /// Somebody asked how to use this.
    ///
    /// Its own answer rather than a rejected option, and that is the whole point of it: until
    /// 2026-08-02 <c>bws --help</c> replied "Unknown option: --help", printed the usage on the
    /// ERROR channel and ended with code 2 - the code this tool reserves for what somebody typed
    /// wrongly. Asking for help was reported as a mistake. clig.dev puts help under -h and
    /// --help and expects it on the data channel with a code of zero.
    ///
    /// Read before the verb, because it is a question about the tool and not about a command.
    /// </summary>
    internal bool Help { get; private init; }

    /// <summary>
    /// Somebody asked what this is.
    ///
    /// Did not exist before 2026-08-02, while the number sat in Directory.Build.props and in
    /// every binary. clig.dev lists --version among the flags whose meaning is settled, and a
    /// tool that goes on production servers with no way to say what it is makes an
    /// administrator read file properties.
    /// </summary>
    internal bool Version { get; private init; }

    /// <summary>
    /// Somebody asked the licence question at its full depth.
    ///
    /// Without it the answer is the notice: what this program is under, that it comes with no
    /// warranty, and the names of what it carries. With it, every component the release ships
    /// with its version, its licence and where it came from - the same set the SPDX document
    /// published beside the archive carries, because both are rendered from one register.
    /// </summary>
    internal bool Components { get; private init; }

    /// <summary>
    /// A first word that is not a verb.
    ///
    /// Kept apart from <see cref="Rejected"/> for the reason <see cref="BadSubcommand"/> is:
    /// the honest sentence differs. A rejected word is an option nobody knows. This is somebody
    /// reaching for a command, and the answer can offer the one they probably meant.
    /// </summary>
    internal string? BadVerb { get; private init; }

    /// <summary>
    /// Options that exist and were given without the value they need.
    ///
    /// Their own list, because "there is no such option" sends somebody looking for a typo
    /// in a word they spelled correctly. These two used to share a message and the wrong
    /// one was shown - found by a guard checking that every switch in the help is a switch
    /// the tool accepts, which is not what it was written to look for.
    /// </summary>
    internal IReadOnlyList<string> Incomplete { get; private init; } = [];

    /// <summary>
    /// Options carrying a value that were given more than once.
    ///
    /// Refused rather than resolved, and the last one used to win in silence - so
    /// <c>bws list --query "a" --query "b"</c> searched for b and said nothing about a. That is
    /// the same fault as a switch swallowed as another one's value, from a third direction: the
    /// tool accepted something a person wrote and did nothing with it.
    ///
    /// The convention elsewhere is that the last wins, and it is a reasonable convention for a
    /// tool where a wrapper script sets a default somebody overrides. This one has no wrappers
    /// and a rule against silent acceptance, so it says so instead. Owner's decision, 2026-08-03.
    ///
    /// Only the three that carry a value. A flag given twice means exactly what it meant once.
    /// </summary>
    internal IReadOnlyList<string> Repeated { get; private init; } = [];

    /// <summary>
    /// Options that exist but do not belong to this verb.
    ///
    /// Their own list rather than folded into <see cref="Rejected"/>, because the answer a
    /// person needs is different: not "there is no such option" but "not with this command,
    /// and here is where it works". Reported rather than ignored, because an option quietly
    /// doing nothing is a runbook line that looks like it works and does something else.
    /// </summary>
    internal IReadOnlyList<string> Misplaced { get; private init; } = [];

    /// <summary>
    /// Whether the reading produced no complaint of any kind.
    ///
    /// <b>It exists for one caller and the reason is an ordering trap, not tidiness.</b>
    /// <see cref="Immediate"/> answers before <c>Refusals</c> does, because help and version are
    /// questions about the tool rather than about a command - somebody typing <c>--help</c> after
    /// a line that went wrong wants the help. <c>license</c> is answered in the same place and
    /// must NOT inherit that: <c>bws license --json</c> would print the notice and swallow the
    /// switch, which is exactly the silence the belonging table in <see cref="OptionSurface"/>
    /// was built to end. So it answers only when there is nothing to refuse, and everything else
    /// falls through to the sentence Refusals already writes.
    ///
    /// <b>Every list of complaints on this record, named rather than counted.</b> A list added
    /// later and forgotten here would make this say yes to a line that has something wrong with
    /// it - so <c>LicenceCommandTests</c> walks the whole option surface and proves that no
    /// option belonging to another verb can be swallowed by this one.
    /// </summary>
    internal bool NothingWrong =>
        Rejected.Count == 0
        && Extra.Count == 0
        && Misplaced.Count == 0
        && Incomplete.Count == 0
        && Repeated.Count == 0
        && BadVerb is null
        && BadSubcommand is null
        && BadTimeout is null;

    /// <summary>Which ask this is, when it is one. See <see cref="WriteCommands"/>.</summary>
    internal ActionKind Action => WriteCommands.AskedFor(Kind, Restart);

    /// <summary>Whether this command changes anything at all.</summary>
    internal bool IsWrite => WriteCommands.Writes(Kind);

}
