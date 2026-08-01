using System.Diagnostics;
using Bws.Cli;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Core.Querying;

// Slices S1 to S3: read every entry the manager knows about, narrow the listing with a
// query, and work out what a stop, a start or a restart would do - then carry it out and
// say what came of every step.

var options = CommandLine.Read(args);

if (options.Rejected.Count > 0)
{
    // Diagnostics go to the error channel even when the run fails. The data channel stays
    // clean so a failed run never drops a stray line into somebody's pipe.
    Console.Error.WriteLine(Texts.Of("cli.unknownOption", string.Join(", ", options.Rejected)));
    Console.Error.WriteLine(Texts.Of("cli.usage"));
    return ExitCode.Usage;
}

if (options.Incomplete.Count > 0)
{
    // A different mistake from an unknown option, and it used to be reported as one -
    // sending somebody to hunt for a typo in a word they had spelled correctly.
    Console.Error.WriteLine(Texts.Of("cli.optionNeedsValue", string.Join(", ", options.Incomplete)));
    Console.Error.WriteLine(Texts.Of("cli.usage"));
    return ExitCode.Usage;
}

if (options.Kind == CommandKind.None)
{
    Console.Error.WriteLine(Texts.Of("cli.usage"));
    return ExitCode.Usage;
}

if (options.Misplaced.Count > 0)
{
    // An option that exists but not here. Refused rather than ignored: a switch that
    // quietly does nothing turns a runbook line into something that looks right and behaves
    // differently, and nobody finds out until it matters.
    foreach (var option in options.Misplaced)
    {
        Console.Error.WriteLine(Texts.Of(
            "cli.optionNotForCommand",
            option,
            options.Kind.ToString().ToLowerInvariant(),
            string.Join(", ", CommandLine.Accepts(option))));
    }

    return ExitCode.Usage;
}

if (options.IsWrite && options.ServiceName.Length == 0)
{
    Console.Error.WriteLine(Texts.Of("cli.missingServiceName", options.Action.ToString().ToLowerInvariant()));
    return ExitCode.Usage;
}

if (options.BadTimeout is not null)
{
    Console.Error.WriteLine(Texts.Of("cli.badTimeout", options.BadTimeout));
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
    var catalog = new WindowsScmCatalog();
    var entries = catalog.ReadAll();
    var read = stopwatch.ElapsedMilliseconds;

    // Its own number, and not folded into the time spent filtering. The second pass is by
    // far the most expensive thing this tool does - measured at around five seconds against
    // a third of one for the read - and reporting it under the word "filtered" would put a
    // true number next to a sentence about something else.
    long inspected = 0;

    // Every command produces its text, and exactly one place puts text on the data channel.
    // Not tidiness: it is what makes "could anything else have reached standard output"
    // answerable by looking, and a guard in the architecture tests holds it to one.
    string data;
    var exit = ExitCode.Ok;

    if (options.IsWrite)
    {
        var plan = new PlanBuilder(entries, catalog)
            .Build(new ServiceAction(options.Action, options.ServiceName, options.Dependents));

        if (!plan.IsRunnable)
        {
            stopwatch.Stop();

            foreach (var problem in plan.Problems)
            {
                Console.Error.WriteLine(PlanText.Describe(problem));
            }

            return ExitCode.Usage;
        }

        if (options.DryRun)
        {
            stopwatch.Stop();
            data = options.Json ? PlanJson.Render(plan) : PlanText.Render(plan);
        }
        else
        {
            var run = Carry(plan, options.Timeout);
            stopwatch.Stop();

            data = options.Json ? PlanJson.Render(run) : PlanText.Render(run);

            // A plan that did not finish is neither a broken tool nor a mistyped command,
            // so it is neither of the codes those two already have. Monitoring needs to
            // tell "the stop was refused" from "bws itself fell over".
            exit = run.Cancelled
                ? ExitCode.Interrupted
                : run.Completed ? ExitCode.Ok : ExitCode.Incomplete;
        }
    }
    else
    {
        // The second pass, and the first thing in this tool that is asked for rather than
        // simply done. Measured at roughly three seconds against 322-329 ms for everything
        // above, so a listing does not verify signatures unless somebody wants them.
        //
        // A query about them counts as wanting them. Answering "signed:no" with an empty
        // list because nobody had looked would be a correct query returning what reads
        // exactly like "there are none" - which is the one failure this whole language is
        // arranged to avoid.
        if (options.Signatures || parsed.Query!.NeedsSecondPass)
        {
            var before = stopwatch.ElapsedMilliseconds;
            entries = SecondPass.Fill(entries, new WindowsBinaryInspector());
            inspected = stopwatch.ElapsedMilliseconds - before;
        }

        var result = parsed.Query!.Filter(entries);
        stopwatch.Stop();

        data = options.Json
            ? ListingJson.Render(result.Entries)
            : ListingTable.Render(result.Entries);

        Report(entries, result, options, read, stopwatch.ElapsedMilliseconds, inspected);
    }

    if (options.IsWrite)
    {
        // Every command reads the manager first, so everything the read has to admit to is
        // owed on every command. It used to be said only when listing, which meant a plan
        // built on entries whose configuration was refused looked exactly like one built on
        // a complete reading.
        Report(entries, result: null, options, read, stopwatch.ElapsedMilliseconds, inspected: 0);
    }

    Console.Out.WriteLine(data);

    return exit;
}
#pragma warning disable CA1031
// The entry point of a command line tool is the one place a broad catch is right.
// It does not swallow anything: the message goes to the error channel and the process
// ends with a failing code. The alternative is a stack trace in the user's face, which
// tells them less and looks like a crash.
//
// One of exactly two suppressions of this rule in the project. The other is in
// WindowsBinaryInspector, where one malformed file out of several hundred must cost its
// own answer rather than the whole run. Anywhere else, a broad catch would be the silence
// that rule 8 forbids - and the test of that is whether the failure still reaches the
// person. In both places it does.
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
/// Carries the plan out, with the waiting made visible.
///
/// Progress goes to the error channel while it happens, because a terminal that shows
/// nothing for half a minute looks broken, and the data channel carries the finished
/// document and nothing else. That split is the contract in 02-DECYZJE-TECHNICZNE, not a
/// preference: progress belongs with warnings, not with the JSON somebody is piping.
/// </summary>
static PlanRun Carry(OperationPlan plan, TimeSpan timeout)
{
    using var interruption = new CancellationTokenSource();
    using var abandonment = new CancellationTokenSource();
    var presses = 0;

    Console.CancelKeyPress += (_, key) =>
    {
        // Three presses, three different asks, and each message says what the next one
        // costs. The middle one was missing and it showed: pressing twice used to end the
        // process outright, leaving a half stopped cascade and printing nothing about it.
        // Somebody who wants a run to stop is not asking to be told nothing.
        switch (++presses)
        {
            case 1:
                key.Cancel = true;
                interruption.Cancel();
                Console.Error.WriteLine(Texts.Of("cli.run.interrupted"));
                break;

            case 2:
                key.Cancel = true;
                abandonment.Cancel();
                Console.Error.WriteLine(Texts.Of("cli.run.abandoned"));
                break;

            // The third is left alone. The runtime ends the process, there is no report and
            // the exit code is not one of ours - which is the honest meaning of pressing it
            // a third time after being told twice what would happen.
        }
    };

    return new PlanRunner(new WindowsScmControl(), new SystemClock()).Run(
        plan,
        timeout,
        interruption.Token,
        abandonment.Token,
        starting: (step, number) => Console.Error.WriteLine(
            PlanText.Progress(step, number, plan.Steps.Count)));
}

/// <summary>
/// Everything the run has to admit to, on the error channel, with the exit code left
/// alone. A partial answer is not a failure, and reporting it as one would make scripts
/// treat an ordinary lack of permissions as a broken tool. Staying quiet about it is the
/// other way to get this wrong, and the worse one.
/// </summary>
static void Report(
    IReadOnlyList<ScmEntry> entries,
    QueryResult? result,
    CommandLine options,
    long readMilliseconds,
    long totalMilliseconds,
    long inspected)
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

    if (result is not null && result.Unreadable > 0)
    {
        // The query asked about something that could not be read on some entries. They
        // were judged anyway, because a filter has to decide, so the result is an answer
        // built partly on what we failed to find out and has to say so.
        Console.Error.WriteLine(Texts.Of("cli.warning.queryIncomplete", result.Unreadable));
    }

    if (result is not null && result.TooCostly > 0)
    {
        // An expression that ran out of time never answered. Showing the shorter list
        // without a word would be the silent absence of results the language forbids.
        Console.Error.WriteLine(Texts.Of("cli.warning.queryTooCostly", result.TooCostly));
    }

    if (!options.Timing)
    {
        return;
    }

    // E4a asks this switch for the time spent reading, which is the part that belongs to
    // us. On a write command the rest of the clock is mostly the services taking their own
    // time, and reporting that as though it were ours would be a measurement of the wrong
    // thing wearing our label.
    Console.Error.WriteLine(result is null
        ? Texts.Of("cli.info.timingRead", entries.Count, readMilliseconds)
        : Texts.Of("cli.info.timing", entries.Count, readMilliseconds, totalMilliseconds - readMilliseconds - inspected));

    if (inspected > 0)
    {
        // Only when it happened. A line reporting zero milliseconds spent on signatures
        // would invite the reading that they were checked and found instantly, which is
        // the opposite of what a run without them means.
        Console.Error.WriteLine(Texts.Of("cli.info.timingInspected", inspected));
    }

    if (result is not null)
    {
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

    /// <summary>
    /// The plan was good, it ran, and something in it did not get where it was going.
    ///
    /// Its own code rather than the runtime one. A monitor that cannot tell "the manager
    /// refused to stop that service" from "bws could not start at all" has to treat both
    /// as the same night-time page, and only one of them is about the tool.
    /// </summary>
    internal const int Incomplete = 3;

    /// <summary>
    /// Somebody stopped the run by hand.
    ///
    /// Takes precedence over <see cref="Incomplete"/> when both apply, because it is the
    /// cause and the other is the effect - a person reading one number wants to know that
    /// the run was stopped, not that stopping it left work undone.
    ///
    /// Non-zero even when every step still arrived, which happens often now that the steps
    /// putting things back are carried out anyway. A wrapper script must not treat a run
    /// somebody stopped as a clean success. Ansible reserves a code for this too and the
    /// reasoning is the same.
    ///
    /// Deliberately not 130, the shell convention of 128 plus the signal number. That
    /// convention belongs to POSIX shells, means nothing on Windows, and mixing it into a
    /// table of small numbers would make the table harder to read rather than easier.
    /// </summary>
    internal const int Interrupted = 4;
}
