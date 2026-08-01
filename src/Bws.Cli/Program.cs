using System.Diagnostics;
using Bws.Cli;
using Bws.Core;
using Bws.Core.Querying;

// Slices S1 and S2: read every entry the service control manager knows about, narrow the
// listing with a query, and print what is left. Still no writing and no expensive data.

var options = CommandLine.Read(args);

if (options.Rejected.Count > 0)
{
    // Diagnostics go to the error channel even when the run fails. The data channel stays
    // clean so a failed run never drops a stray line into somebody's pipe.
    Console.Error.WriteLine(Texts.Of("cli.unknownOption", string.Join(", ", options.Rejected)));
    Console.Error.WriteLine(Texts.Of("cli.usage"));
    return ExitCode.Usage;
}

// Read the query before touching the system. A typo costs nothing this way, and the
// alternative is enumerating hundreds of entries in order to throw them away.
var parsed = QueryParser.Parse(options.Query);

if (!parsed.IsValid)
{
    foreach (var problem in parsed.Problems)
    {
        Console.Error.WriteLine(QueryMessages.Of(problem));
    }

    // A query with a mistake in it filters nothing and says what is wrong. In a window
    // that means the listing stays as it was, and here it means no listing at all: a
    // terminal writes into pipes, and answering a typo with the unfiltered listing would
    // hand every entry to whatever comes next in the pipeline.
    return ExitCode.Usage;
}

try
{
    var stopwatch = Stopwatch.StartNew();
    var entries = new WindowsScmCatalog().ReadAll();
    var read = stopwatch.ElapsedMilliseconds;

    var result = parsed.Query!.Filter(entries);
    stopwatch.Stop();

    Console.Out.WriteLine(options.Json
        ? ListingJson.Render(result.Entries)
        : ListingTable.Render(result.Entries));

    Report(entries, result, options, read, stopwatch.ElapsedMilliseconds);

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
    // The whole chain, not just the top message. A wrapper such as
    // TypeInitializationException says only "something threw", and the sentence that
    // actually explains the failure sits underneath it. Printing one line and dropping
    // the rest is the quiet kind of silence rule 8 forbids.
    for (Exception? level = failure; level is not null; level = level.InnerException)
    {
        Console.Error.WriteLine(level.Message);
    }

    return ExitCode.Runtime;
}
#pragma warning restore CA1031

/// <summary>
/// Everything the run has to admit to, on the error channel, with the exit code left
/// alone. A partial answer is not a failure, and reporting it as one would make scripts
/// treat an ordinary lack of permissions as a broken tool. Staying quiet about it is the
/// other way to get this wrong, and the worse one.
/// </summary>
static void Report(
    IReadOnlyList<ScmEntry> entries,
    QueryResult result,
    CommandLine options,
    long readMilliseconds,
    long totalMilliseconds)
{
    var refused = entries.Count(entry => entry.StartType.Outcome == ReadOutcome.Denied);

    if (refused > 0)
    {
        // Never silent. A listing where part of the configuration could not be read looks
        // exactly like a complete one, and that is the worst failure this tool has.
        Console.Error.WriteLine(Texts.Of("cli.warning.configurationRefused", refused, entries.Count));
    }

    var delayUnknown = entries.Count(entry => entry.DelayedAuto.Outcome == ReadOutcome.Denied);

    if (delayUnknown > 0)
    {
        // Its own line rather than folded into the one above. The configuration was read
        // for these entries and only the delay flag was not, which is a different fact.
        Console.Error.WriteLine(Texts.Of("cli.warning.delayRefused", delayUnknown));
    }

    if (result.Unreadable > 0)
    {
        // The query asked about something that could not be read on some entries. They
        // were judged anyway, because a filter has to decide, so the result is an answer
        // built partly on what we failed to find out and has to say so.
        Console.Error.WriteLine(Texts.Of("cli.warning.queryIncomplete", result.Unreadable));
    }

    if (result.TooCostly > 0)
    {
        // An expression that ran out of time never answered. Showing the shorter list
        // without a word would be the silent absence of results the language forbids.
        Console.Error.WriteLine(Texts.Of("cli.warning.queryTooCostly", result.TooCostly));
    }

    if (options.Timing)
    {
        Console.Error.WriteLine(Texts.Of(
            "cli.info.timing", entries.Count, readMilliseconds, totalMilliseconds - readMilliseconds));

        Console.Error.WriteLine(Texts.Of("cli.info.matched", result.Entries.Count, entries.Count));
    }
}

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
