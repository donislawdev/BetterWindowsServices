namespace Bws.Core.Planning;

/// <summary>
/// What a person asked for over many entries at once. Their words, before any analysis.
///
/// Kept apart from the plan for the same reason <see cref="ServiceAction"/> is: the action is what
/// was wanted, the plan is what would happen, and the whole value of the pattern is that those are
/// two objects somebody can hold side by side.
///
/// <b>The names are kept exactly as they were handed over, repeats and all.</b> A name given twice
/// is the same ask twice rather than two asks, so the builder works on the distinct set - but it
/// does not rewrite this record on the way, because this is the record of what somebody asked and
/// not of what was made of it.
/// </summary>
/// <param name="IncludeDependents">
/// Whether the entries that break may be taken down as well. One answer for the whole selection,
/// because it is one question somebody was asked once - and off by default, which is the same
/// safety property <see cref="ServiceAction"/> carries for the same reason.
/// </param>
public sealed record BulkAction(
    ActionKind Kind,
    IReadOnlyList<string> ServiceNames,
    bool IncludeDependents = false,
    StartType? To = null);

/// <summary>
/// Everything that would happen to a selection, worked out and frozen. `C2` in one type.
///
/// <b>IT HOLDS PLANS RATHER THAN REPLACING THEM, AND THAT IS THE WHOLE DESIGN.</b> Each plan inside
/// still concerns one entry and its cascade, exactly as <see cref="OperationPlan"/> always did, and
/// each will be carried out by the existing runner step for step. What lives here is only what
/// cannot be seen from inside a single plan: the order the plans go in, which entries could not get
/// one at all, how many entries come along that nobody asked for, and which entry is named twice.
///
/// <b>Nothing here removes a step, and the temptation to is the trap this type was nearly built
/// around.</b> Two selected entries can produce plans that both stop the same third one. Hiding the
/// repeat would read better and would break the one property `ADR-11` exists for - the runner takes
/// the steps exactly as the preview showed them and never works anything out again, so a preview
/// shorter than the run is a preview that lies. The repeat is harmless where it happens: the runner
/// reads an entry before acting and reports "already there", which is the truth. So it is NAMED,
/// through <see cref="Overlapping"/>, rather than tidied away - rule 8 of the project's untouchable
/// rules, applied to a preview.
///
/// Frozen because it is evidence, like the plans it holds.
/// </summary>
public sealed record BulkPlan
{
    public required BulkAction Action { get; init; }

    /// <summary>
    /// One per entry that got a plan, in the order they will be carried out.
    ///
    /// Every plan here is runnable. An entry the builder had to refuse is in
    /// <see cref="Problems"/> instead and has no plan at all, which is the owner's decision of
    /// 2026-08-18: one driver in a selection does not hold up nineteen services, and the refusal is
    /// visible beside its own entry BEFORE anything runs.
    /// </summary>
    public required IReadOnlyList<OperationPlan> Plans { get; init; }

    /// <summary>
    /// The entries that could not get a plan, and why. Never silent, and never fatal to the rest.
    ///
    /// <b>The preview therefore carries two lists - what will happen and what will not - and that is
    /// the price of the decision above, said out loud.</b> The alternative the specification also
    /// offers, quietly skipping them, would make a selection of twenty with three dropped look
    /// exactly like a selection of twenty.
    /// </summary>
    public required IReadOnlyList<PlanProblem> Problems { get; init; }

    /// <summary>There is something to do. An empty selection is not runnable and is not an error.</summary>
    public bool IsRunnable => Plans.Count > 0;

    /// <summary>Every step of every plan, in the order they happen.</summary>
    public IEnumerable<PlanStep> Steps => Plans.SelectMany(plan => plan.Steps);

    /// <summary>Everything worth saying before somebody presses the button, from every plan.</summary>
    public IEnumerable<PlanWarning> Warnings => Plans.SelectMany(plan => plan.Warnings);

    /// <summary>
    /// Entries the selection will touch that nobody in it asked about - the second number in `C2`'s
    /// own sentence, "stopping these 3 will drag another 7".
    ///
    /// <b>Counted across the whole selection rather than per plan, which is the only way it can be
    /// right.</b> An entry can be dragged in by two different plans, and adding up per-plan cascades
    /// would count it twice and promise a bigger consequence than there is. An entry that is BOTH
    /// selected and dragged in is not extra at all - somebody asked for it - so the asked-for set
    /// comes out.
    /// </summary>
    public IReadOnlyList<string> Extra
    {
        get
        {
            var asked = Action.ServiceNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

            return
            [
                .. Steps
                    .Select(step => step.ServiceName)
                    .Where(name => !asked.Contains(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            ];
        }
    }

    /// <summary>
    /// Entries named by more than one of the plans above.
    ///
    /// <b>Said rather than resolved, and the reason is at the head of this type.</b> A reader of the
    /// preview who sees a service twice and is told nothing will read it as a fault in the tool. One
    /// who is told it appears in two plans, and that the second attempt will find it already where
    /// it was put, has been told the truth about a machine.
    ///
    /// Counted by plans rather than by steps on purpose: a single plan can legitimately name an entry
    /// twice - a restart stops it and starts it again - and that is not an overlap between two asks.
    /// </summary>
    public IReadOnlyList<string> Overlapping =>
        [
            .. Plans
                .SelectMany(plan => plan.Steps
                    .Select(step => step.ServiceName)
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
        ];
}
