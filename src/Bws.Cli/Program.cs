using System.Diagnostics;
using Bws.Cli;
using Bws.Core;

// Slice S1: read every entry the service control manager knows about and print it.
// No filtering, no writing, no expensive data. Those belong to later slices.
//
// Argument handling is deliberately minimal. The command surface is one command and
// two flags today, and the shape of the real surface depends on the query language,
// which is not designed yet. Picking a command line framework now would mean choosing
// for a surface nobody has seen. It stays in this one place so the swap is mechanical
// when the time comes.

var wantsJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
var wantsTiming = args.Contains("--timing", StringComparer.OrdinalIgnoreCase);

var unknown = args
    .Where(argument => argument.StartsWith('-'))
    .Where(argument => !argument.Equals("--json", StringComparison.OrdinalIgnoreCase))
    .Where(argument => !argument.Equals("--timing", StringComparison.OrdinalIgnoreCase))
    .ToArray();

if (unknown.Length > 0)
{
    // Diagnostics go to the error channel even when the run fails. The data channel
    // stays clean so a failed run never drops a stray line into someone's pipe.
    Console.Error.WriteLine($"Unknown option: {string.Join(", ", unknown)}");
    Console.Error.WriteLine("Usage: bws [--json] [--timing]");
    return ExitCode.Usage;
}

try
{
    var stopwatch = Stopwatch.StartNew();
    var entries = new WindowsScmCatalog().ReadAll();
    stopwatch.Stop();

    Console.Out.WriteLine(wantsJson
        ? ListingJson.Render(entries)
        : ListingTable.Render(entries));

    var refused = entries.Count(entry => entry.StartType.Outcome == ReadOutcome.Denied);

    if (refused > 0)
    {
        // Never silent. A listing where part of the configuration could not be read looks
        // exactly like a complete one, and that is the worst failure this tool has.
        Console.Error.WriteLine(
            $"Configuration was refused for {refused} of {entries.Count} entries. " +
            "Run elevated to read them.");
    }

    if (wantsTiming)
    {
        Console.Error.WriteLine($"Read {entries.Count} entries in {stopwatch.ElapsedMilliseconds} ms.");
    }

    return ExitCode.Ok;
}
#pragma warning disable CA1031
// The entry point of a command line tool is the one place a broad catch is right.
// It does not swallow anything: the message goes to the error channel and the process
// ends with a failing code. The alternative is a stack trace in the user's face, which
// tells them less and looks like a crash.
//
// This is the only suppression of this rule in the project. Anywhere else, a broad
// catch would be the silence that rule 8 forbids.
catch (Exception failure)
{
    Console.Error.WriteLine(failure.Message);
    return ExitCode.Runtime;
}
#pragma warning restore CA1031

/// <summary>
/// Exit codes are a public contract: monitoring and scripts depend on them, so a new
/// way of ending means a new constant here, never reusing a near-enough one.
/// </summary>
internal static class ExitCode
{
    internal const int Ok = 0;
    internal const int Runtime = 1;
    internal const int Usage = 2;
}
