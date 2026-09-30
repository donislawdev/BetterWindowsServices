namespace Bws.Core.Planning;

/// <summary>
/// Where a run left each entry against where it found it, and what it would take to put it back.
///
/// <b>ITS OWN TYPE BECAUSE TWO PLACES ASK IT AND THEY MUST NOT ANSWER DIFFERENTLY</b> - a single
/// <see cref="PlanRun"/> and a <see cref="BulkRun"/> over a whole selection. This is the same move
/// <see cref="DependentsFirst"/> made on 2026-08-18 and for the same reason: the order a cascade
/// comes down in is one question, so two implementations of it would be two things that have to
/// agree, and disagreement here means a failure rather than a curiosity.
///
/// <b>The bulk case is not the single case repeated, and that is the whole reason this could not
/// stay a loop inside PlanRun.</b> Asking each run separately and joining the answers reproduces,
/// one level up, exactly the mistake this arithmetic exists to avoid: the joined list comes out in
/// the order the PLANS ran, and a way back has to be in the reverse of the order things MOVED.
///
/// <b>THE WORKED EXAMPLE WAS WRITTEN THE WRONG WAY ROUND FIRST, AND A RUN ON A REAL MACHINE ON
/// 2026-08-19 SAID SO.</b> It claimed a bulk STOP was the dangerous case - that the joined way back
/// would say "start the dependant first" and the manager would refuse. Measured on Windows Server
/// 2025 with SessionEnv, which needs LanmanWorkstation: with both stopped, <c>sc start SessionEnv</c>
/// <b>succeeded</b> and brought LanmanWorkstation up with it. The manager starts what a service
/// needs, so a bad start order is rescued and the example proved nothing.
///
/// <b>The real case is the other direction, and it is the manager's own asymmetry - the same one
/// <see cref="BulkPlanBuilder"/> records.</b> After a bulk START, the way back is a list of STOPS,
/// and the manager rescues nothing there: measured on the same machine, <c>sc stop
/// LanmanWorkstation</c> while SessionEnv was running returned <b>error 1051, "a stop control has
/// been sent to a service that other running services are dependent on"</b>. A start selection is
/// not reordered, so joining run by run hands back the stops in the order the plans ran - depended
/// upon first - which is precisely the order that fails. Reversed over the whole run, the dependant
/// goes first and every line works.
///
/// A way out whose first line fails is worse than admitting there is none.
///
/// Everything else about the arithmetic is described at <see cref="Of"/>, which is where it was
/// before this file existed.
/// </summary>
public static class NetEffect
{
    /// <summary>
    /// What it would take to put every entry back where these results found it.
    ///
    /// <b>The cheapest honest form of the promise `ADR-11` makes about a plan being reversible, and
    /// deliberately not the machinery.</b> Nothing here undoes anything - it says what the commands
    /// would be, which is Nielsen's emergency exit for the price of a sentence. Backlog 58.
    ///
    /// <b>It is a net effect per entry, never a reversal of the steps, and that distinction is the
    /// whole reason this is arithmetic rather than a loop turning each step around.</b> Turning each
    /// step around one at a time would tell somebody who restarted a service to stop it - the two
    /// steps of a restart cancel out, and the entry ends exactly where it began. Reversing steps is
    /// the obvious implementation and it gives dangerous advice on the commonest write this tool
    /// performs.
    ///
    /// So each entry is asked two questions instead: where it was before the first step that moved
    /// it, and where it is after the last one. Equal means nothing to say.
    ///
    /// <b>A step that timed out counts as having moved the entry</b>, for the same reason
    /// <see cref="StepOutcome.TimedOut"/> is not <see cref="StepOutcome.Failed"/>: the manager
    /// accepted the request and the entry may well have arrived after we stopped watching. Leaving
    /// it out would be a claim about something nobody saw, and the direction of that error is the
    /// bad one - it would stay silent about an entry somebody was left holding.
    ///
    /// Skipped steps moved nothing by definition, and a refusal moved nothing either - <b>with one
    /// exception since 2026-09-29:</b> a neighbour in a process that was ended moved, whatever its own
    /// step said, and <see cref="WentWith"/> counts it.
    ///
    /// <b>The order is the reverse of the run</b>, and it has to be: a stop cascade takes the
    /// dependants down before the entry they depend on, so putting them back starts that entry
    /// first. Handing back the commands in the order they happened would hand back a sequence
    /// whose first line fails.
    ///
    /// <b>A WRITTEN START TYPE IS ASKED THE SAME TWO QUESTIONS AND ANSWERS THEM WITH A VALUE RATHER
    /// THAN A DIRECTION.</b> Where it was before the first step that wrote it, where it is after the
    /// last - equal means nothing to say, exactly as for an entry that ended where it began. What
    /// makes it a different case is that direction is not enough: undoing a stop is a start and
    /// undoing "set to disabled" is a value nobody can work out from the step itself, so it travels
    /// on <see cref="PlanStep.From"/> from the moment the plan was built.
    ///
    /// <b>An entry whose previous type is not known, or is one this tool has no word for, gets NO
    /// LINE.</b> That is the same decision <see cref="EquivalentCommand.For(BulkPlan)"/> makes about
    /// a refused entry, for the same reason: there is no command that would put it back, so writing
    /// one would hand somebody a line that fails. It is silence over a wrong way out, and a way out
    /// whose first line fails is worse than admitting there is none.
    /// </summary>
    /// <param name="results">
    /// Every step that was carried out, in the order it happened. One run's worth, or every run of
    /// a whole selection laid end to end - the arithmetic does not care which, and that is the
    /// property that makes one selection produce one answer rather than a joined list of answers.
    /// </param>
    public static IReadOnlyList<ReversalStep> Of(IEnumerable<StepResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var (moves, settings) = Tally(results);

        var back = moves
            .Where(move => Before(move.Value.First) != After(move.Value.Last))
            .Select(move => (
                Step: new ReversalStep(move.Key, Undoing(move.Value.Last)),
                move.Value.When,
                Setting: false))
            .Concat(settings
                .Where(written => Nameable(written.Value.From) && written.Value.From != written.Value.To)
                .Select(written => (
                    Step: new ReversalStep(written.Key, StepOperation.SetStartType, written.Value.From),
                    When: moves.TryGetValue(written.Key, out var moved)
                        ? Math.Max(written.Value.When, moved.When)
                        : written.Value.When,
                    Setting: true)));

        // Sorted once over both, rather than each list sorted and then joined. The order is the
        // reverse of the RUN, and two lists appended would put every setting after every move
        // whatever the machine actually did.
        //
        // WITH ONE EXCEPTION, AND IT ARRIVED WITH THE FIRST RUN THAT BUILDS BOTH (2026-09-24): an
        // entry that was set to disabled and then stopped. The reverse of that run starts it first -
        // and a disabled entry refuses to start, so the first line pasted would fail. The setting of
        // an entry goes back BEFORE that entry's own move, wherever it stood in the run: writing a
        // setting never depends on where the entry is, and starting it may depend on the setting.
        return [.. back
            .OrderByDescending(one => one.When)
            .ThenByDescending(one => one.Setting)
            .Select(one => one.Step)];
    }

    /// <summary>
    /// Whether these results took the entry down - asked by a step that puts the entry back, before it is
    /// tried.
    ///
    /// <b>HERE RATHER THAN IN THE RUNNER BECAUSE IT IS THE WAY BACK'S OWN QUESTION, and two answers to it
    /// would be two things that have to agree</b> - the argument this file was made for. On the owner's
    /// decision of 2026-09-30 (stability report W-5) a step putting something back gives back only what the
    /// run took: a stop that arrived or timed out, or an ending that took the entry with its process. Until
    /// then every such step ran, and an entry that was stopped all along was started by a restart nobody
    /// let begin.
    ///
    /// <b>One case counts here and not in the way back:</b> an ending after which Windows started the entry
    /// again at once (<see cref="StepResult.StartedAgain"/>). The process was ended, so the entry WAS taken
    /// down - the step putting it back reads it, finds it running and says so. For the way back the same
    /// entry ended where it began, which is why <see cref="Of"/> does not count it.
    /// </summary>
    internal static bool TookDown(IEnumerable<StepResult> results, string serviceName) =>
        Tally(results, endingCounts: true).Moves.TryGetValue(serviceName, out var moved)
        && moved.Last is StepOperation.Stop or StepOperation.Terminate;

    /// <summary>
    /// Where each entry was first and last moved, and what each startup setting was before and after -
    /// the two tallies <see cref="Of"/> turns into lines.
    ///
    /// <b>Out of Of on 2026-09-29</b>, when the neighbours of an ended process joined the count
    /// (stability report W-10) and Of went past the length the shape guard calls close to its ceiling.
    /// The seam is the one the method already had: counting what happened, then saying what undoes it.
    /// </summary>
    /// <param name="endingCounts">
    /// Count an ending that Windows answered by starting the entry again at once - <see cref="TookDown"/>
    /// asks with it, <see cref="Of"/> without.
    /// </param>
    private static (
        Dictionary<string, (StepOperation First, StepOperation Last, int When)> Moves,
        Dictionary<string, (StartSetting? From, StartSetting? To, int When)> Settings)
        Tally(IEnumerable<StepResult> results, bool endingCounts = false)
    {
        var moves = new Dictionary<string, (StepOperation First, StepOperation Last, int When)>(
            StringComparer.OrdinalIgnoreCase);

        // ITS OWN TALLY RATHER THAN A THIRD DIRECTION IN THE ONE ABOVE. A move is answered by
        // asking whether the entry ended up running, which is a question with two answers. A
        // setting is answered with a value, and folding the two into one dictionary would mean an
        // entry that was both moved and reconfigured had to pick which of the two it was.
        //
        // A run like that exists since 2026-09-24 - a startup setting of disabled carrying a stop,
        // spec C4 - and the entry gets both lines instead of quietly losing one, which is what this
        // arithmetic was written for before anything built one. The order of the two is decided
        // where the lines are sorted, in Of.
        var settings = new Dictionary<string, (StartSetting? From, StartSetting? To, int When)>(
            StringComparer.OrdinalIgnoreCase);

        // Entries a stop found already stopped - the one thing that tells a neighbour who was not in
        // the process when it ended from one who died with it.
        var foundStopped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var index = 0;

        foreach (var result in results)
        {
            var at = index++;

            if (result is { SkippedBecause: SkipReason.AlreadyThere, Step.Operation: StepOperation.Stop })
            {
                foundStopped.Add(result.Step.ServiceName);
            }

            if (result.Outcome != StepOutcome.Succeeded
                && result.Outcome != StepOutcome.TimedOut
                && !(endingCounts && result.StartedAgain))
            {
                continue;
            }

            if (result.Step.Operation == StepOperation.Terminate)
            {
                WentWith(result.Step, foundStopped, moves, at);
            }

            var name = result.Step.ServiceName;

            if (result.Step.Operation == StepOperation.SetStartType)
            {
                // The FIRST from and the LAST to, which is the move arithmetic said in values -
                // and it matters for the same reason. An entry set to manual and then to disabled
                // inside one run has one way back, and it goes to where the run found it rather
                // than to the halfway house it passed through.
                settings[name] = settings.TryGetValue(name, out var written)
                    ? (written.From, result.Step.To, at)
                    : (result.Step.From, result.Step.To, at);

                continue;
            }

            var operation = result.Step.Operation;

            moves[name] = moves.TryGetValue(name, out var seen)
                ? (seen.First, operation, at)
                : (operation, operation, at);
        }

        return (moves, settings);
    }
    /// <summary>
    /// The neighbours an ended process took down with it, counted as moved by the step that ended it.
    ///
    /// <b>THE EXTERNAL STABILITY REPORT FOUND THEM MISSING (W-10), and a live forced restart would have
    /// been worse than missing.</b> A neighbour whose polite stop was refused, or that never had one
    /// because the courtesy was skipped, has no step of its own that moved it - yet the process it
    /// lived in is gone. Left out, its way back was silence after a forced stop, and after a forced
    /// restart with <c>--force</c> it was "stop it", worked out from the one step that brought it
    /// back, about an entry that had been running all along.
    ///
    /// <b>Only neighbours with no move of their own and not found already stopped.</b> One whose own
    /// stop worked is counted already, and one its step found stopped was not in the process at all.
    /// </summary>
    private static void WentWith(
        PlanStep ending,
        HashSet<string> foundStopped,
        Dictionary<string, (StepOperation First, StepOperation Last, int When)> moves,
        int at)
    {
        foreach (var name in ending.TakesWithIt ?? [])
        {
            if (!moves.ContainsKey(name) && !foundStopped.Contains(name))
            {
                moves[name] = (StepOperation.Terminate, StepOperation.Terminate, at);
            }
        }
    }

    /// <summary>
    /// Whether a startup setting is one this tool could hand somebody a line for.
    ///
    /// <b>Asked through the word table rather than by listing the settings here</b>, because the
    /// question this is really asking is "could somebody type it". A list would be a second answer
    /// to that - and the day the command line learned its fourth word (2026-09-24, "delayed") is the
    /// day a list here would have gone on refusing it.
    /// </summary>
    private static bool Nameable(StartSetting? setting) => StartTypeWords.Of(setting) is not null;

    /// <summary>
    /// Whether an entry was running before the first step that moved it. A stop found it running,
    /// a start found it stopped.
    ///
    /// <b>This was written back to front and every test in PlanReversalTests said so at once</b> -
    /// the sentence above was right and the expression under it was its opposite, so a plain stop
    /// came out as having changed nothing and the whole property returned an empty list. Worth the
    /// line it takes to record: the comment was not what was wrong, and rereading it would not have
    /// found this.
    /// </summary>
    private static bool Before(StepOperation first) => first switch
    {
        StepOperation.Stop => true,
        // ENDING A PROCESS IS A STOP THAT ASKS NOBODY, so all three of these answer for it
        // exactly as they answer for a stop. Leaving it out would not have been silent: each
        // throws for an operation it was never taught, and the throw would land while the
        // report was being written - after the machine had already been changed.
        StepOperation.Terminate => true,
        StepOperation.Start => false,
        _ => throw new ArgumentOutOfRangeException(
            nameof(first), first, EquivalentCommand.Unhandled)
    };

    /// <summary>The step that puts back what one of these did.</summary>
    private static StepOperation Undoing(StepOperation last) => last switch
    {
        StepOperation.Stop => StepOperation.Start,
        StepOperation.Terminate => StepOperation.Start,
        StepOperation.Start => StepOperation.Stop,
        _ => throw new ArgumentOutOfRangeException(
            nameof(last), last, EquivalentCommand.Unhandled)
    };

    /// <summary>Whether a step that moved an entry left it running.</summary>
    private static bool After(StepOperation last) => last switch
    {
        StepOperation.Start => true,
        StepOperation.Stop => false,
        StepOperation.Terminate => false,
        _ => throw new ArgumentOutOfRangeException(
            nameof(last), last, EquivalentCommand.Unhandled)
    };
}
