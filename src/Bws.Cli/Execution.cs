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

        ConsoleCancelEventHandler pressed = (_, key) =>
        {
            // Three presses, three different asks, and each message says what the next one
            // costs. The middle one was missing and it showed: pressing twice used to end the
            // process outright, leaving a half stopped cascade and printing nothing about it.
            // Somebody who wants a run to stop is not asking to be told nothing.
            //
            // Counted atomically, because this runs on a thread of the runtime's choosing and
            // nothing says two presses cannot arrive at once. With `++presses` both could read
            // zero, both could take the first branch, and the second level - the one added after
            // a real run on a virtual machine left a cascade half down - would never be reached
            // however many times somebody pressed.
            switch (Interlocked.Increment(ref presses))
            {
                case 1:
                    key.Cancel = true;
                    Stop(interruption);
                    Console.Error.WriteLine(Texts.Of("cli.run.interrupted"));
                    break;

                case 2:
                    key.Cancel = true;
                    Stop(abandonment);
                    Console.Error.WriteLine(Texts.Of("cli.run.abandoned"));
                    break;

                // The third is left alone. The runtime ends the process, there is no report and
                // the exit code is not one of ours - which is the honest meaning of pressing it
                // a third time after being told twice what would happen.
            }
        };

        Console.CancelKeyPress += pressed;

        try
        {
            return new PlanRunner(new WindowsScmControl(), new SystemClock()).Run(
                plan,
                timeout,
                interruption.Token,
                abandonment.Token,
                starting: (step, number) => Console.Error.WriteLine(
                    PlanText.Progress(step, number, plan.Steps.Count)));
        }
        finally
        {
            // TAKEN OFF BEFORE THE TWO SOURCES ABOVE ARE DISPOSED, and until 2026-08-03 it never
            // was. The handler outlived them: the plan finished, the `using` statements released
            // both, and the handler stayed subscribed while the report was rendered and written.
            // A press in that window called Cancel on a disposed source, which throws, on a
            // thread with nothing to catch it - so the one moment where somebody most wants the
            // report is the moment the process could end without printing it.
            //
            // The state (T) of rule 10, in the place this project has already paid for it once.
            Console.CancelKeyPress -= pressed;
        }
    }

    /// <summary>
    /// Cancels a source that may already have been let go.
    ///
    /// <b>What was left of the race above, closed 2026-08-26.</b> Taking the handler off before the
    /// two sources are released shut the wide window - the one where the report was being written
    /// with the handler still subscribed. It cannot shut the narrow one: a press can already be
    /// inside this handler, on a thread of the runtime's choosing, while the main thread is
    /// unsubscribing and leaving the method that owns both sources.
    ///
    /// Microseconds wide, and the cost of losing is the whole cost: an ObjectDisposedException on a
    /// thread with nothing to catch it ends the process, at the exact moment somebody is waiting to
    /// be told what the run did. The catch is narrow rather than broad - this is the one thing that
    /// can be wrong here, and anything else arriving is news.
    /// </summary>
    private static void Stop(CancellationTokenSource source)
    {
        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run is over and whatever this would have stopped has already stopped.
        }
    }

    /// <summary>
    /// Whether this run has to say out loud that it did not have administrator rights.
    /// </summary>
    /// <param name="elevated">
    /// The answer, handed in rather than fetched.
    ///
    /// <b>THAT IS THE WHOLE OF WHAT MAKES THIS CHECKABLE, and it is the shape
    /// <c>Mishaps.Told</c> already uses in the window for the same reason.</b> Reaching for
    /// <see cref="Session.IsElevated"/> inside here would put the decision behind a fact about the
    /// machine running the test - so the guard would be green on a developer's elevated session,
    /// green on an unelevated one, and green if the condition were inverted. Split off, it takes a
    /// boolean and a test can hand it both.
    /// </param>
    /// <param name="kind">
    /// What was asked for, because one command already says this better than this sentence can.
    ///
    /// <c>snapshot create</c> prints its own line about exactly this, and that one goes further:
    /// it names the consequence of comparing such a file later, which is where the harm from an
    /// unelevated reading actually lands. Two paragraphs about one fact is the shape that drifts
    /// apart at the first edit, so the more specific one wins and this stays quiet.
    /// </param>
    internal static bool AdmitsNotElevated(bool elevated, CommandKind kind) =>
        !elevated && kind != CommandKind.SnapshotCreate;

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
        // THE WIDEST ADMISSION FIRST, AND UNTIL 2026-09-09 IT WAS THE ONE THIS TOOL NEVER MADE.
        //
        // Everything below is about a FIELD that could not be read on an entry we were handed. This
        // is about entries we were never handed at all, and it is the only one of the two that
        // cannot be counted from here: an entry the manager did not enumerate leaves nothing behind
        // to notice. Session.IsElevated carries the measurement - 807 entries against 810 on one
        // machine, and five more whose descriptor was refused - together with the sentence that
        // matters, which is that a listing taken this way is a different document rather than a
        // shorter one.
        //
        // <b>The window has said this since it learned to plan, and the terminal did not.</b> Found
        // by the pre-release audit of 2026-09-09 asking why two interfaces answered the same
        // question differently. Rule 8 forbids silence in a result, and this was the largest piece
        // of silence left in the product.
        //
        // NOT ON `snapshot create`, WHICH ALREADY SAYS IT BETTER - the whole of that argument is at
        // AdmitsNotElevated, together with the reason the decision is a method rather than a line.
        if (AdmitsNotElevated(Session.IsElevated(), options.Kind))
        {
            Console.Error.WriteLine(Texts.Of("cli.warning.notElevated"));
        }

        var refused = entries.Count(entry => entry.StartType.Outcome == ReadOutcome.Denied);

        if (refused > 0)
        {
            // Never silent. A listing where part of the configuration could not be read looks
            // exactly like a complete one, and that is the worst failure this tool has.
            //
            // A PAIR SINCE 2026-09-01 - backlog 207. Only the pronoun changes here: "entries"
            // belongs to the SECOND number, which is the whole machine, so it stays plural and is
            // right. It was "read them" about one refusal.
            Console.Error.WriteLine(refused == 1
                ? Texts.Of("cli.warning.configurationRefused.one", refused, entries.Count)
                : Texts.Of("cli.warning.configurationRefused.many", refused, entries.Count));
        }

        var delayUnknown = entries.Count(entry => entry.DelayedAuto.Outcome == ReadOutcome.Denied);

        if (delayUnknown > 0)
        {
            // Its own line rather than folded into the one above. The configuration was read
            // for these entries and only the delay flag was not, which is a different fact.
            Console.Error.WriteLine(delayUnknown == 1
                ? Texts.Of("cli.warning.delayRefused.one", delayUnknown)
                : Texts.Of("cli.warning.delayRefused.many", delayUnknown));
        }

        if (result is not null && result.Unreadable > 0)
        {
            // The query asked about something that could not be read on some entries. They
            // were judged anyway, because a filter has to decide, so the result is an answer
            // built partly on what we failed to find out and has to say so.
            Console.Error.WriteLine(result.Unreadable == 1
                ? Texts.Of("cli.warning.queryIncomplete.one", result.Unreadable)
                : Texts.Of("cli.warning.queryIncomplete.many", result.Unreadable));
        }

        if (result is not null && result.TooCostly > 0)
        {
            // An expression that ran out of time never answered. Showing the shorter list
            // without a word would be the silent absence of results the language forbids.
            Console.Error.WriteLine(result.TooCostly == 1
                ? Texts.Of("cli.warning.queryTooCostly.one", result.TooCostly)
                : Texts.Of("cli.warning.queryTooCostly.many", result.TooCostly));
        }

        if (!options.Timing)
        {
            return;
        }

        // E4a asks this switch for the time spent reading, which is the part that belongs to
        // us. On a write command the rest of the clock is mostly the services taking their own
        // time, and reporting that as though it were ours would be a measurement of the wrong
        // thing wearing our label.
        // FOUR KEYS RATHER THAN TWO SINCE 2026-09-01 - backlog 207. The noun follows the count of
        // entries in both sentences, so a machine holding one read "Read 1 entries in 12 ms".
        var filtering = totalMilliseconds - readMilliseconds - inspected - measured;

        Console.Error.WriteLine((result is null, entries.Count == 1) switch
        {
            (true, true) => Texts.Of("cli.info.timingRead.one", entries.Count, readMilliseconds),
            (true, false) => Texts.Of("cli.info.timingRead.many", entries.Count, readMilliseconds),
            (false, true) => Texts.Of("cli.info.timing.one", entries.Count, readMilliseconds, filtering),
            (false, false) => Texts.Of("cli.info.timing.many", entries.Count, readMilliseconds, filtering)
        });

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
            // The noun follows the SECOND number here, which is the machine rather than the
            // match - "0 of 1 entry matched" is right and answering it from the count that
            // changed is the trap. The window says it the same way, and that is on purpose.
            Console.Error.WriteLine(entries.Count == 1
                ? Texts.Of("cli.info.matched.one", result.Entries.Count, entries.Count)
                : Texts.Of("cli.info.matched.many", result.Entries.Count, entries.Count));
        }
    }
}
