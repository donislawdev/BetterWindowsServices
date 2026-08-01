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

    /// <summary>Options nobody knows, and options given without the value they need.</summary>
    internal IReadOnlyList<string> Rejected { get; private init; } = [];

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
        string? query = null;
        string? badTimeout = null;
        var timeout = TimeSpan.FromSeconds(60);
        var rejected = new List<string>();

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

            if (Matches(argument, "--json")) { json = true; continue; }
            if (Matches(argument, "--timing")) { timing = true; continue; }
            if (Matches(argument, "--dry-run")) { dryRun = true; continue; }
            if (Matches(argument, "--dependents")) { dependents = true; continue; }

            // Both spellings, because both are what people's fingers do.
            if (argument.StartsWith("--query=", StringComparison.OrdinalIgnoreCase))
            {
                query = argument["--query=".Length..];
                continue;
            }

            if (Matches(argument, "--query"))
            {
                if (index + 1 >= arguments.Length)
                {
                    // An option that needs a value and did not get one is a mistake, not an
                    // empty query. Treating it as empty would quietly list everything.
                    rejected.Add(argument);
                    continue;
                }

                query = arguments[++index];
                continue;
            }

            if (argument.StartsWith("--timeout=", StringComparison.OrdinalIgnoreCase))
            {
                badTimeout = Seconds(argument["--timeout=".Length..], ref timeout);
                continue;
            }

            if (Matches(argument, "--timeout"))
            {
                if (index + 1 >= arguments.Length)
                {
                    rejected.Add(argument);
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
            Query = query,
            Timeout = timeout,
            BadTimeout = badTimeout,
            Rejected = rejected
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
