using System.Diagnostics;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Core.Querying;

namespace Bws.Cli;

/// <summary>
/// Carrying a plan out, and saying afterwards what a run cost.
///
/// <b>Moved out of Program.cs on 2026-08-02 because the size ratchet said so, for the fourth
/// time that day.</b> Refusing to overwrite an existing snapshot pushed that file twenty lines
/// past a ceiling that may only ever go down.
///
/// The seam is between deciding and doing. What stays behind reads the command line, works out
/// which verb was asked for and produces the text. What moved here is the part that waits: a
/// plan being carried out step by step with three levels of Ctrl+C behind it, and the timing
/// report that says where the seconds went.
/// </summary>
internal static class Execution
{
    /// <summary>
    /// Carries the plan out, with the waiting made visible.
    ///
    /// Progress goes to the error channel while it happens, because a terminal that shows
    /// nothing for half a minute looks broken, and the data channel carries the finished
    /// document and nothing else. That split is the contract in 02-DECYZJE-TECHNICZNE, not a
    /// preference: progress belongs with warnings, not with the JSON somebody is piping.
    /// </summary>
    internal static PlanRun Carry(OperationPlan plan, TimeSpan timeout)
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
    internal static void Report(
        IReadOnlyList<ScmEntry> entries,
        QueryResult? result,
        CommandLine options,
        long readMilliseconds,
        long totalMilliseconds,
        long inspected,
        long measured)
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
            : Texts.Of(
                "cli.info.timing",
                entries.Count,
                readMilliseconds,
                totalMilliseconds - readMilliseconds - inspected - measured));

        if (inspected > 0)
        {
            // Only when it happened. A line reporting zero milliseconds spent on signatures
            // would invite the reading that they were checked and found instantly, which is
            // the opposite of what a run without them means.
            Console.Error.WriteLine(Texts.Of("cli.info.timingInspected", inspected));
        }

        if (measured > 0)
        {
            Console.Error.WriteLine(Texts.Of("cli.info.timingMeasured", measured));
        }

        if (result is not null)
        {
            Console.Error.WriteLine(Texts.Of("cli.info.matched", result.Entries.Count, entries.Count));
        }
    }
}
