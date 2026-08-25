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

        var index = 0;

        foreach (var result in results)
        {
            var at = index++;

            if (result.Outcome != StepOutcome.Succeeded && result.Outcome != StepOutcome.TimedOut)
            {
                continue;
            }

            // A WRITTEN START TYPE HAS NO WAY BACK HERE, AND THAT IS A GAP RATHER THAN A DECISION
            // ABOUT ITS VALUE - said out loud because the section this feeds is called "to put this
            // back". Undoing one needs the type the entry had BEFORE, and nothing in a result
            // carries it: a step knows what it set, not what it replaced. Everything below is about
            // an entry that moved, and this kind moves nothing.
            //
            // What it costs, exactly: after setting a start type the panel offers no way back and
            // says nothing about one, rather than offering a wrong one. Backlog 229.
            if (result.Step.Operation == StepOperation.SetStartType)
            {
                continue;
            }

            var name = result.Step.ServiceName;
            var operation = result.Step.Operation;

            moves[name] = moves.TryGetValue(name, out var seen)
                ? (seen.First, operation, at)
                : (operation, operation, at);
        }

        return
        [
            .. moves
                .Where(move => Before(move.Value.First) != After(move.Value.Last))
                .OrderByDescending(move => move.Value.When)
                .Select(move => new ReversalStep(move.Key, Undoing(move.Value.Last)))
        ];
    }

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
