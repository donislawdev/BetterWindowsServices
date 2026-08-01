namespace Bws.Cli;

/// <summary>
/// What the person asked for on the command line.
///
/// Still hand rolled rather than done with a framework. The surface is three options, and
/// the shape of the real one depends on the write operations, which do not exist yet.
/// It stays in this one place so that swapping it later is mechanical.
/// </summary>
internal sealed record CommandLine
{
    internal bool Json { get; private init; }

    internal bool Timing { get; private init; }

    /// <summary>Null when no query was given, which selects everything.</summary>
    internal string? Query { get; private init; }

    /// <summary>Options nobody knows, and options given without the value they need.</summary>
    internal IReadOnlyList<string> Rejected { get; private init; } = [];

    internal static CommandLine Read(string[] arguments)
    {
        var json = false;
        var timing = false;
        string? query = null;
        var rejected = new List<string>();

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];

            if (Matches(argument, "--json"))
            {
                json = true;
                continue;
            }

            if (Matches(argument, "--timing"))
            {
                timing = true;
                continue;
            }

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

            rejected.Add(argument);
        }

        return new CommandLine
        {
            Json = json,
            Timing = timing,
            Query = query,
            Rejected = rejected
        };
    }

    private static bool Matches(string argument, string option) =>
        argument.Equals(option, StringComparison.OrdinalIgnoreCase);
}
