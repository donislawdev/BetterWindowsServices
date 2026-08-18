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
/// one level up, exactly the mistake this arithmetic exists to avoid. Worked example, from the
/// chain measured on a real machine on 2026-08-01: stop a selection of LanmanWorkstation and
/// MRxSmb20, where the first needs the second. <see cref="DependentsFirst"/> deals with
/// LanmanWorkstation first, so run one leaves "start LanmanWorkstation" and run two leaves "start
/// MRxSmb20". Joined run by run, the first line handed to somebody is the one the manager refuses -
/// LanmanWorkstation cannot start while what it needs is stopped. A way out that does not work is
/// worse than admitting there is none.
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
                .Select(move => new ReversalStep(
                    move.Key,
                    move.Value.Last == StepOperation.Stop ? StepOperation.Start : StepOperation.Stop))
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
    private static bool Before(StepOperation first) => first == StepOperation.Stop;

    /// <summary>Whether a step that moved an entry left it running.</summary>
    private static bool After(StepOperation last) => last == StepOperation.Start;
}
