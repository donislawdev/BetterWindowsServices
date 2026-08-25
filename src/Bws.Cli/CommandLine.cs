using System.Globalization;
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
internal sealed record CommandLine
{
    internal CommandKind Kind { get; private init; }

    /// <summary>The entry a write command is about. Empty for <see cref="CommandKind.List"/>.</summary>
    internal string ServiceName { get; private init; } = string.Empty;

    /// <summary>
    /// The start type somebody named, as they wrote it. Empty when they named none.
    ///
    /// <b>The word rather than the value, and it is kept that way all the way to the refusal.</b>
    /// A word that names no start type has to appear in the sentence that says so - "manuel is not
    /// a start type" is an answer, and "that is not a start type" sends somebody back to look at a
    /// line they have already read twice. Reading it into a value here would throw away the only
    /// half of it worth saying.
    /// </summary>
    internal string StartTypeWord { get; private init; } = string.Empty;

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
    internal TimeSpan Timeout { get; private init; } = TimeSpan.FromSeconds(60);

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

    /// <summary>Options nobody knows, and bare words where none belongs.</summary>
    internal IReadOnlyList<string> Rejected { get; private init; } = [];

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

    /// <summary>Which ask this is, when it is one. See <see cref="WriteCommands"/>.</summary>
    internal ActionKind Action => WriteCommands.AskedFor(Kind);

    /// <summary>Whether this command changes anything at all.</summary>
    internal bool IsWrite => WriteCommands.Writes(Kind);

    /// <summary>The options where giving one twice means one of the two was thrown away.</summary>
    private static readonly string[] CarriesAValue = ["--query", "--note", "--timeout"];

    // Suppressed rather than defended: at 199 lines this one really is too long, and the
    // analyser is right. Splitting it is a change to working code that no slice asked for, so
    // it is written down as backlog item 24 instead of being done here on the way past - and
    // this comment is the reason the suppression is not a way of forgetting about it.
#pragma warning disable MA0051
    internal static CommandLine Read(string[] arguments)
    {
        var kind = CommandKind.None;
        var serviceName = string.Empty;
        var startTypeWord = string.Empty;
        var json = false;
        var timing = false;
        var dryRun = false;
        var dependents = false;
        var signatures = false;
        var memory = false;
        var followNetwork = false;
        var force = false;
        string? query = null;
        var path = string.Empty;
        var against = string.Empty;
        var exitCode = false;
        var live = false;
        string? note = null;
        string? badSubcommand = null;
        string? badTimeout = null;
        var timeout = TimeSpan.FromSeconds(60);
        var rejected = new List<string>();
        var incomplete = new List<string>();
        var given = new List<string>();

        var help = arguments.Length == 0;
        var version = false;
        string? badVerb = null;

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];

            // Asked before anything else and outside the option OptionSurface.Surface, because these two are
            // questions about the tool rather than options belonging to a verb. Putting them in
            // the table of what each verb accepts would make "bws --help" require a verb, which
            // is the opposite of what somebody typing it wants.
            if (Arguments.Matches(argument, "--help") || Arguments.Matches(argument, "-h")) { help = true; continue; }
            if (Arguments.Matches(argument, "--version")) { version = true; continue; }

            if (!argument.StartsWith('-'))
            {
                if (kind == CommandKind.None)
                {
                    // "snapshot" is a noun, not a verb, so it needs the word after it. E1
                    // puts create, diff and restore under it, and only the first is built.
                    if (Arguments.Matches(argument, "snapshot"))
                    {
                        var next = index + 1 < arguments.Length ? arguments[index + 1] : string.Empty;

                        if (Arguments.Matches(next, "create"))
                        {
                            kind = CommandKind.SnapshotCreate;
                            index++;
                            continue;
                        }

                        if (Arguments.Matches(next, "diff"))
                        {
                            kind = CommandKind.SnapshotDiff;
                            index++;
                            continue;
                        }

                        // Its own answer rather than "unknown option". Snapshot is not an
                        // option and it is not unknown - it is a noun waiting for its verb,
                        // and calling it an option sends somebody to check their spelling of
                        // a word they spelled correctly. The same mistake this tool already
                        // made once, with a switch given without its value.
                        badSubcommand = next;

                        if (next.Length > 0)
                        {
                            index++;
                        }

                        continue;
                    }

                    if (Arguments.TryVerb(argument, out var verb))
                    {
                        kind = verb;
                        continue;
                    }

                    // A first word that is not a verb is somebody reaching for a command, not
                    // an unknown option. Recorded as its own thing so the answer can say which
                    // command they probably meant.
                    //
                    // Not when a subcommand has already gone wrong, and a guard caught that:
                    // `bws snapshot restore baseline.json` leaves the verb unset, so the file
                    // name walked into this branch and overwrote "there is no snapshot restore"
                    // with "there is no command baseline.json". The first sentence is the true
                    // one - the second answers a question nobody asked.
                    badVerb ??= badSubcommand is null ? argument : null;
                    continue;
                }

                // Where a snapshot goes. Optional: E1's own example writes it without one,
                // and then a name is worked out from the machine and the time.
                if (kind == CommandKind.SnapshotCreate && path.Length == 0)
                {
                    path = argument;
                    continue;
                }

                // Two files, in the order typed. A third is a mistake rather than a third
                // side to compare, and it falls through to the rejected words below.
                if (kind == CommandKind.SnapshotDiff && path.Length == 0)
                {
                    path = argument;
                    continue;
                }

                if (kind == CommandKind.SnapshotDiff && against.Length == 0)
                {
                    against = argument;
                    continue;
                }

                // The first bare word after a write verb is the entry it is about. A second
                // one is a mistake, not a second target: bulk operations take --query.
                //
                // After "list" there is no such word at all. Taking one and ignoring it
                // would mean "bws list Spooler" quietly printed the whole machine, which is
                // the silent kind of wrong this project spends most of its rules on.
                if (WriteCommands.Writes(kind) && serviceName.Length == 0)
                {
                    serviceName = argument;
                    continue;
                }

                // AND THE SECOND ONE IS THE START TYPE, ON THE ONE VERB THAT TAKES A VALUE. Kept as
                // a bare word rather than made a switch, because it is not optional - "bws
                // start-type Spooler" is not a shorter way of asking the same thing, and a switch
                // is a shape people read as one that can be left off.
                //
                // Still a mistake on every other write verb, where it falls through to the rejected
                // words below exactly as it did before this verb existed.
                if (WriteCommands.NeedsAStartType(kind) && startTypeWord.Length == 0)
                {
                    startTypeWord = argument;
                    continue;
                }

                rejected.Add(argument);
                continue;
            }

            if (Arguments.Matches(argument, "--json")) { json = true; given.Add("--json"); continue; }
            if (Arguments.Matches(argument, "--timing")) { timing = true; given.Add("--timing"); continue; }
            if (Arguments.Matches(argument, "--dry-run")) { dryRun = true; given.Add("--dry-run"); continue; }
            if (Arguments.Matches(argument, "--dependents")) { dependents = true; given.Add("--dependents"); continue; }
            if (Arguments.Matches(argument, "--signatures")) { signatures = true; given.Add("--signatures"); continue; }
            if (Arguments.Matches(argument, "--memory")) { memory = true; given.Add("--memory"); continue; }
            if (Arguments.Matches(argument, "--follow-network")) { followNetwork = true; given.Add("--follow-network"); continue; }
            if (Arguments.Matches(argument, "--force")) { force = true; given.Add("--force"); continue; }
            if (Arguments.Matches(argument, "--exit-code")) { exitCode = true; given.Add("--exit-code"); continue; }
            if (Arguments.Matches(argument, "--live")) { live = true; given.Add("--live"); continue; }

            // Both spellings, because both are what people's fingers do.
            if (argument.StartsWith("--query=", StringComparison.OrdinalIgnoreCase))
            {
                query = argument["--query=".Length..];
                given.Add("--query");
                continue;
            }

            if (Arguments.Matches(argument, "--query"))
            {
                given.Add("--query");

                if (Arguments.NeedsValue(arguments, index))
                {
                    // An option that needs a value and did not get one is a mistake, not an
                    // empty query. Treating it as empty would quietly list everything.
                    incomplete.Add("--query");
                    continue;
                }

                query = arguments[++index];
                continue;
            }

            if (argument.StartsWith("--note=", StringComparison.OrdinalIgnoreCase))
            {
                note = argument["--note=".Length..];
                given.Add("--note");
                continue;
            }

            if (Arguments.Matches(argument, "--note"))
            {
                given.Add("--note");

                if (Arguments.NeedsValue(arguments, index))
                {
                    // A note that was asked for and not given is a mistake, not an empty
                    // note. Writing the snapshot anyway would lose the one thing the person
                    // was in the middle of saying about it.
                    incomplete.Add("--note");
                    continue;
                }

                note = arguments[++index];
                continue;
            }

            if (argument.StartsWith("--timeout=", StringComparison.OrdinalIgnoreCase))
            {
                given.Add("--timeout");
                badTimeout = Arguments.Seconds(argument["--timeout=".Length..], ref timeout);
                continue;
            }

            if (Arguments.Matches(argument, "--timeout"))
            {
                given.Add("--timeout");

                if (Arguments.NeedsValue(arguments, index))
                {
                    incomplete.Add("--timeout");
                    continue;
                }

                badTimeout = Arguments.Seconds(arguments[++index], ref timeout);
                continue;
            }

            rejected.Add(argument);
        }

        return new CommandLine
        {
            Kind = kind,
            ServiceName = serviceName,
            StartTypeWord = startTypeWord,
            Json = json,
            Timing = timing,
            DryRun = dryRun,
            Dependents = dependents,
            Signatures = signatures,
            Memory = memory,
            FollowNetwork = followNetwork,
            Force = force,
            Query = query,
            Path = path,
            Against = against,
            ExitCodeOnDifference = exitCode,
            Live = live,
            Note = note,
            BadSubcommand = badSubcommand,
            BadVerb = badVerb,
            Help = help,
            Version = version,
            Timeout = timeout,
            BadTimeout = badTimeout,
            Rejected = rejected,
            Incomplete = incomplete,

            // In the order they were typed, each named once however many times it appeared.
            Repeated =
            [
                .. given
                    .Where(option => CarriesAValue.Contains(option, StringComparer.Ordinal))
                    .GroupBy(option => option, StringComparer.Ordinal)
                    .Where(twice => twice.Count() > 1)
                    .Select(twice => twice.Key)
            ],

            // In the order they were typed, each named once however many times it appeared.
            Misplaced =
            [
                .. given
                    .Distinct(StringComparer.Ordinal)
                    .Where(option => !OptionSurface.Surface.Single(entry => entry.Option == option).Verbs.Contains(kind))
            ]
        };
    }

#pragma warning restore MA0051

}
