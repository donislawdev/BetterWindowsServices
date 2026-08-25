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
    /// Skipped steps moved nothing by definition, and a refusal moved nothing either.
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

        var moves = new Dictionary<string, (StepOperation First, StepOperation Last, int When)>(
            StringComparer.OrdinalIgnoreCase);

        // ITS OWN TALLY RATHER THAN A THIRD DIRECTION IN THE ONE ABOVE. A move is answered by
        // asking whether the entry ended up running, which is a question with two answers. A
        // setting is answered with a value, and folding the two into one dictionary would mean an
        // entry that was both moved and reconfigured had to pick which of the two it was.
        //
        // Nothing builds such a run today - one ask travels over a whole selection, so every step
        // in a run is the same kind - and the arithmetic is written so that if one ever does, the
        // entry gets both lines instead of quietly losing one.
        var settings = new Dictionary<string, (StartType? From, StartType? To, int When)>(
            StringComparer.OrdinalIgnoreCase);

        var index = 0;

        foreach (var result in results)
        {
            var at = index++;

            if (result.Outcome != StepOutcome.Succeeded && result.Outcome != StepOutcome.TimedOut)
            {
                continue;
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

        var back = moves
            .Where(move => Before(move.Value.First) != After(move.Value.Last))
            .Select(move => (
                Step: new ReversalStep(move.Key, Undoing(move.Value.Last)),
                move.Value.When))
            .Concat(settings
                .Where(written => Nameable(written.Value.From) && written.Value.From != written.Value.To)
                .Select(written => (
                    Step: new ReversalStep(written.Key, StepOperation.SetStartType, written.Value.From),
                    written.Value.When)));

        // Sorted once over both, rather than each list sorted and then joined. The order is the
        // reverse of the RUN, and two lists appended would put every setting after every move
        // whatever the machine actually did.
        return [.. back.OrderByDescending(one => one.When).Select(one => one.Step)];
    }

    /// <summary>
    /// Whether a start type is one this tool could hand somebody a line for.
    ///
    /// <b>Asked through the word table rather than by listing the three types here</b>, because the
    /// question this is really asking is "could somebody type it". A list of types would be a
    /// second answer to that, and the day the command line learns a fourth word this would still be
    /// refusing it.
    /// </summary>
    private static bool Nameable(StartType? type) => StartTypeWords.Of(type) is not null;

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
        StepOperation.Start => false,
        _ => throw new ArgumentOutOfRangeException(
            nameof(first), first, EquivalentCommand.Unhandled)
    };

    /// <summary>The step that puts back what one of these did.</summary>
    private static StepOperation Undoing(StepOperation last) => last switch
    {
        StepOperation.Stop => StepOperation.Start,
        StepOperation.Start => StepOperation.Stop,
        _ => throw new ArgumentOutOfRangeException(
            nameof(last), last, EquivalentCommand.Unhandled)
    };

    /// <summary>Whether a step that moved an entry left it running.</summary>
    private static bool After(StepOperation last) => last switch
    {
        StepOperation.Start => true,
        StepOperation.Stop => false,
        _ => throw new ArgumentOutOfRangeException(
            nameof(last), last, EquivalentCommand.Unhandled)
    };
}
