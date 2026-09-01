using System.Globalization;

namespace Bws.Cli;

/// <summary>
/// Turning the words somebody typed into the record beside this file.
///
/// Split out of CommandLine.cs on 2026-08-25, forced by the size ratchet rather than chosen:
/// that file stood on the ceiling of the longest shipped file in the product, so the reading
/// verb `show` could not add a property, a switch and a parse arm anywhere in it.
///
/// The seam is the same one ScmEntry took the same day: everything beside this is the SHAPE of
/// what was asked for, and everything here is the reading that produces it. Backlog 24 asks for
/// a further split - Read is 199 lines and the analyser is right about it - and that is still
/// deferred, because this cut moved a method without touching a line of it.
/// </summary>
internal sealed partial record CommandLine
{
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
        var full = false;
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
        var extra = new List<string>();
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

                // The first bare word after a verb that takes a name is the entry it is about.
                // A second one is a mistake rather than a second target - this tool acts on one
                // named entry at a time, and --query belongs to `list` alone. That is what the
                // switch table says, and until 2026-09-01 this comment said "bulk operations take
                // --query", which is an offer no write verb here can honour.
                //
                // After "list" there is no such word at all. Taking one and ignoring it
                // would mean "bws list Spooler" quietly printed the whole machine, which is
                // the silent kind of wrong this project spends most of its rules on.
                //
                // ASKED THROUGH TakesAName RATHER THAN Writes SINCE 2026-08-25, when a reading
                // verb joined the family. The predicate moved rather than gaining an "or show"
                // here, because the parser is one of two places that need the same line - the
                // other is the exit code for a name nobody can find.
                if (OptionSurface.TakesAName(kind) && serviceName.Length == 0)
                {
                    serviceName = argument;
                    continue;
                }

                // AND THE SECOND ONE IS THE START TYPE, ON THE ONE VERB THAT TAKES A VALUE. Kept as
                // a bare word rather than made a switch, because it is not optional - "bws
                // start-type Spooler" is not a shorter way of asking the same thing, and a switch
                // is a shape people read as one that can be left off.
                //
                // Still a mistake on every other write verb, where it falls through to the words
                // with nowhere to go exactly as it did before this verb existed.
                if (WriteCommands.NeedsAStartType(kind) && startTypeWord.Length == 0)
                {
                    startTypeWord = argument;
                    continue;
                }

                // NOT `rejected`, SINCE 2026-09-01. A word that reaches here is spelled perfectly
                // and is not an option at all - it is one more name than the verb has room for -
                // and calling it an unknown option sent somebody looking for a typo they had not
                // made. Backlog 233.
                extra.Add(argument);
                continue;
            }

            if (Arguments.Matches(argument, "--full")) { full = true; given.Add("--full"); continue; }
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
                given.Add("--query");

                if (!Empty(incomplete, "--query", argument["--query=".Length..]))
                {
                    query = argument["--query=".Length..];
                }

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

                if (!Empty(incomplete, "--query", arguments[++index]))
                {
                    query = arguments[index];
                }

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
            Full = full,
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
            Extra = extra,
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

    /// <summary>
    /// Whether an option was given a value that says nothing, and notes it as missing if so.
    ///
    /// <b>THE FOURTH SPELLING OF ONE MISTAKE, CLOSED 2026-08-26 - owner's decision.</b> The query
    /// language already turns back <c>!!!</c>, <c>name:""</c> and a field with nothing after it,
    /// every time with the same sentence: a script with a typo in its query must not receive the
    /// whole machine and a green exit code. <c>--query=</c> and <c>--query ""</c> were the same
    /// mistake one layer out, where the empty text never reaches the parser as a term at all - it
    /// arrives as an empty query, and an empty query legitimately means everything.
    ///
    /// <b>The distinction this draws is the whole of it: NO --query means everything, an EMPTY
    /// --query means somebody typed something that came to nothing.</b> A shell that expands a
    /// variable to nothing produces the second, which is exactly the case worth catching, and
    /// nothing about the first changes.
    ///
    /// Whitespace counts as empty for the same reason the parser treats it that way - a query of
    /// three spaces selects everything, so accepting it would leave the same hole with a wider
    /// door.
    /// </summary>
    private static bool Empty(List<string> incomplete, string option, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        incomplete.Add(option);

        return true;
    }
}
