namespace Bws.Core.Planning;

/// <summary>
/// Carries out a plan over a selection, one plan at a time, with the existing runner.
///
/// <b>IT DECIDES NOTHING AND ADDS NO STEP, which is the property that keeps `ADR-11` true one level
/// up.</b> <see cref="PlanRunner"/> takes the steps of one plan exactly as the preview showed them
/// and never works anything out again - so this hands it those plans, in the order the preview
/// showed, and does not touch what is inside them. Every question about cascade, order within a
/// plan, warnings and refusals was answered by <see cref="BulkPlanBuilder"/> before anything ran.
///
/// <b>Takes a runner rather than a manager, and that is not a style choice - it is
/// PlanOnlyGuards.</b> The third of those guards asserts that every construction of the writer
/// stands in the same STATEMENT as a <c>new PlanRunner</c>. A runner built inside here, from an
/// <c>IScmControl</c> handed in, would put the writer's construction in a caller's statement with no
/// plan runner beside it, and the guard would redden - correctly, because that shape is the first
/// step towards a write that skips a plan. Written before this class existed, it constrained it.
/// </summary>
public sealed class BulkRunner(PlanRunner runner)
{
    /// <summary>
    /// Runs every plan, in the order the preview showed, and reports what came of each.
    /// </summary>
    /// <param name="timeout">The longest any ONE step will be watched for. Handed to each plan
    /// unchanged, because it is a property of a step rather than of a selection.</param>
    /// <param name="cancellation">
    /// Stop going forward. The steps that put things back are still carried out - inside the plan
    /// that was running when it arrived, and inside every plan after it.
    /// </param>
    /// <param name="abandonment">Stop altogether, putting nothing back. The expensive ask.</param>
    /// <param name="starting">
    /// Called before each step that is actually attempted, with its position ACROSS THE WHOLE
    /// SELECTION.
    ///
    /// Offset rather than restarted at each plan, because a person watching is reading it against
    /// the numbered list the panel showed them, and that list is numbered across the whole
    /// selection too. Restarting at one for every plan would show "1 of 32" four times.
    /// </param>
    public BulkRun Run(
        BulkPlan plan,
        TimeSpan timeout,
        CancellationToken cancellation = default,
        CancellationToken abandonment = default,
        Action<PlanStep, int>? starting = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // CHECKED FOR ALL OF THEM BEFORE ANY OF THEM RUNS, rather than letting the runner refuse the
        // one it reaches. PlanRunner throws on a plan with problems, and reaching that on the
        // fourteenth plan would mean a machine already moved by thirteen and an exception instead of
        // a report - the one outcome a write path must not have. Loud rather than quiet, for the
        // reason PlanRunner gives: a plan with problems carried out anyway is the worst bug here.
        if (!plan.IsRunnable || plan.Plans.Any(one => !one.IsRunnable))
        {
            throw new InvalidOperationException(
                "A selection with nothing to do, or holding a plan with problems, must never be " +
                "run. Check IsRunnable first.");
        }

        var runs = new List<PlanRun>(plan.Plans.Count);
        var before = 0;

        foreach (var one in plan.Plans)
        {
            // Copied into the loop rather than captured, because the closure below outlives this
            // iteration and `before` keeps moving. Captured directly, every plan would announce its
            // steps against the total at the end of the run.
            var offset = before;

            Action<PlanStep, int>? announce = starting is null
                ? null
                : (step, number) => starting(step, offset + number);

            // EVERY PLAN IS RUN, INCLUDING AFTER AN INTERRUPTION, and the reason is evidence rather
            // than restores. A cancelled runner still produces a result for every step of the plan
            // it is given, saying in as many words that it was cancelled - so going through them all
            // is what makes `Runs` as long as `Plans` and lets somebody see what was left alone.
            // Breaking out here would be cheaper and would hand back a report that says nothing at
            // all about the entries after the interruption.
            //
            // THE OTHER ARGUMENT DOES NOT HOLD AND IS WRITTEN DOWN SO NOBODY REBUILDS IT: a later
            // plan's Restore steps cannot be giving back what an earlier plan took, because a bulk
            // action carries ONE kind for the whole selection and DependentsFirst puts the dependants
            // before the entries they depend on - so the plan that could restore something always
            // ran earlier. The restores of a cancelled plan find their work done and cost a read.
            runs.Add(runner.Run(one, timeout, cancellation, abandonment, announce));

            before += one.Steps.Count;
        }

        return new BulkRun { Plan = plan, Runs = runs };
    }
}
