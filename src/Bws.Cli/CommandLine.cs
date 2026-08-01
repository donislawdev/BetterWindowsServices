using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>What the person asked the tool to do.</summary>
internal enum CommandKind
{
    /// <summary>Nothing recognisable. Print how to use it.</summary>
    None,
    List,
    Stop,
    Start,
    Restart
}

/// <summary>
/// What the person asked for on the command line.
///
/// The verbs come from E1 of the product specification rather than from taste. That
/// section is a frozen contract: people put these lines in runbooks, so inventing a
/// shorter spelling later costs somebody a broken document.
///
/// Still hand rolled rather than done with a framework. The surface is four verbs, and
/// it stays in this one place so that swapping it later is mechanical.
/// </summary>
internal sealed record CommandLine
{
    internal CommandKind Kind { get; private init; }

    /// <summary>The entry a write command is about. Empty for <see cref="CommandKind.List"/>.</summary>
    internal string ServiceName { get; private init; } = string.Empty;

    internal bool Json { get; private init; }

    internal bool Timing { get; private init; }

    /// <summary>Show what would happen and change nothing.</summary>
    internal bool DryRun { get; private init; }

    /// <summary>Allow the entries that break to be taken down as well.</summary>
    internal bool Dependents { get; private init; }

    /// <summary>
    /// Read who signed each binary, which the listing does not do by default.
    ///
    /// Measured at roughly three seconds against a third of a second for the rest of a
    /// listing, so it is asked for rather than assumed. A query about signatures turns it
    /// on by itself - somebody who wrote signed:no has already asked.
    /// </summary>
    internal bool Signatures { get; private init; }

    /// <summary>Null when no query was given, which selects everything.</summary>
    internal string? Query { get; private init; }

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

    /// <summary>Options nobody knows, and bare words where none belongs.</summary>
    internal IReadOnlyList<string> Rejected { get; private init; } = [];

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
    /// Options that exist but do not belong to this verb.
    ///
    /// Their own list rather than folded into <see cref="Rejected"/>, because the answer a
    /// person needs is different: not "there is no such option" but "not with this command,
    /// and here is where it works". Reported rather than ignored, because an option quietly
    /// doing nothing is a runbook line that looks like it works and does something else.
    /// </summary>
    internal IReadOnlyList<string> Misplaced { get; private init; } = [];

    /// <summary>Which verbs each option belongs to. The whole surface, in one readable place.</summary>
    private static readonly (string Option, CommandKind[] Verbs)[] Surface =
    [
        ("--query", [CommandKind.List]),

        // Listing only. A plan never asks who signed anything, so accepting it on a write
        // verb would be a switch that does nothing - the silence this table was built to
        // end. Measured cost is around three seconds, which is why it is asked for rather
        // than assumed.
        ("--signatures", [CommandKind.List]),
        ("--json", [CommandKind.List, CommandKind.Stop, CommandKind.Start, CommandKind.Restart]),

        // Diagnostic, and every command reads the manager before doing anything, so it
        // applies to every command. It used to be accepted everywhere and only honoured for
        // the listing, which is the same silence from the other side.
        ("--timing", [CommandKind.List, CommandKind.Stop, CommandKind.Start, CommandKind.Restart]),

        ("--dry-run", [CommandKind.Stop, CommandKind.Start, CommandKind.Restart]),

        // Not on start, and this one was missed the first time round. Starting is not the
        // mirror of stopping: the manager brings up whatever the entry needs by itself, and
        // nothing that merely depends on it has to move. The plan says so and ignores the
        // word - so accepting it here would be the same silence one level further down,
        // where somebody writing "start it and everything under it" gets one step and no
        // hint that the rest of their sentence was dropped.
        ("--dependents", [CommandKind.Stop, CommandKind.Restart]),

        ("--timeout", [CommandKind.Stop, CommandKind.Start, CommandKind.Restart])
    ];

    /// <summary>Where an option does work, for the message that says it does not work here.</summary>
    internal static IReadOnlyList<string> Accepts(string option) =>
    [
        .. Surface.Single(entry => entry.Option == option).Verbs
            .Select(verb => verb.ToString().ToLowerInvariant())
    ];

    internal ActionKind Action => Kind switch
    {
        CommandKind.Stop => ActionKind.Stop,
        CommandKind.Start => ActionKind.Start,
        _ => ActionKind.Restart
    };

    internal bool IsWrite => Kind is CommandKind.Stop or CommandKind.Start or CommandKind.Restart;

    internal static CommandLine Read(string[] arguments)
    {
        var kind = CommandKind.None;
        var serviceName = string.Empty;
        var json = false;
        var timing = false;
        var dryRun = false;
        var dependents = false;
        var signatures = false;
        string? query = null;
        string? badTimeout = null;
        var timeout = TimeSpan.FromSeconds(60);
        var rejected = new List<string>();
        var incomplete = new List<string>();
        var given = new List<string>();

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];

            if (!argument.StartsWith('-'))
            {
                if (kind == CommandKind.None && TryVerb(argument, out var verb))
                {
                    kind = verb;
                    continue;
                }

                // The first bare word after a write verb is the entry it is about. A second
                // one is a mistake, not a second target: bulk operations take --query.
                //
                // After "list" there is no such word at all. Taking one and ignoring it
                // would mean "bws list Spooler" quietly printed the whole machine, which is
                // the silent kind of wrong this project spends most of its rules on.
                if (kind is not (CommandKind.None or CommandKind.List) && serviceName.Length == 0)
                {
                    serviceName = argument;
                    continue;
                }

                rejected.Add(argument);
                continue;
            }

            if (Matches(argument, "--json")) { json = true; given.Add("--json"); continue; }
            if (Matches(argument, "--timing")) { timing = true; given.Add("--timing"); continue; }
            if (Matches(argument, "--dry-run")) { dryRun = true; given.Add("--dry-run"); continue; }
            if (Matches(argument, "--dependents")) { dependents = true; given.Add("--dependents"); continue; }
            if (Matches(argument, "--signatures")) { signatures = true; given.Add("--signatures"); continue; }

            // Both spellings, because both are what people's fingers do.
            if (argument.StartsWith("--query=", StringComparison.OrdinalIgnoreCase))
            {
                query = argument["--query=".Length..];
                given.Add("--query");
                continue;
            }

            if (Matches(argument, "--query"))
            {
                given.Add("--query");

                if (index + 1 >= arguments.Length)
                {
                    // An option that needs a value and did not get one is a mistake, not an
                    // empty query. Treating it as empty would quietly list everything.
                    incomplete.Add("--query");
                    continue;
                }

                query = arguments[++index];
                continue;
            }

            if (argument.StartsWith("--timeout=", StringComparison.OrdinalIgnoreCase))
            {
                given.Add("--timeout");
                badTimeout = Seconds(argument["--timeout=".Length..], ref timeout);
                continue;
            }

            if (Matches(argument, "--timeout"))
            {
                given.Add("--timeout");

                if (index + 1 >= arguments.Length)
                {
                    incomplete.Add("--timeout");
                    continue;
                }

                badTimeout = Seconds(arguments[++index], ref timeout);
                continue;
            }

            rejected.Add(argument);
        }

        return new CommandLine
        {
            Kind = kind,
            ServiceName = serviceName,
            Json = json,
            Timing = timing,
            DryRun = dryRun,
            Dependents = dependents,
            Signatures = signatures,
            Query = query,
            Timeout = timeout,
            BadTimeout = badTimeout,
            Rejected = rejected,
            Incomplete = incomplete,

            // In the order they were typed, each named once however many times it appeared.
            Misplaced =
            [
                .. given
                    .Distinct(StringComparer.Ordinal)
                    .Where(option => !Surface.Single(entry => entry.Option == option).Verbs.Contains(kind))
            ]
        };
    }

    /// <summary>
    /// Reads a number of seconds, or says what it got instead.
    ///
    /// Nothing below a second, and nothing at all rather than a default quietly standing in.
    /// Somebody who writes --timeout 30s meant thirty seconds, and giving them sixty because
    /// their spelling was not understood is the kind of quiet substitution that turns up in
    /// a runbook months later.
    /// </summary>
    private static string? Seconds(string value, ref TimeSpan timeout)
    {
        if (!int.TryParse(value, out var seconds) || seconds < 1)
        {
            return value;
        }

        timeout = TimeSpan.FromSeconds(seconds);
        return null;
    }

    private static bool TryVerb(string argument, out CommandKind kind)
    {
        kind = argument.ToLowerInvariant() switch
        {
            "list" => CommandKind.List,
            "stop" => CommandKind.Stop,
            "start" => CommandKind.Start,
            "restart" => CommandKind.Restart,
            _ => CommandKind.None
        };

        return kind != CommandKind.None;
    }

    private static bool Matches(string argument, string option) =>
        argument.Equals(option, StringComparison.OrdinalIgnoreCase);
}
