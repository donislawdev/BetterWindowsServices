namespace Bws.Core.Planning;

/// <summary>
/// Turns what somebody asked for over a selection into everything that would happen. `C2`.
///
/// <b>It builds ordinary plans and decides nothing a single plan already decides.</b> Cascade,
/// warnings, refusals and the order INSIDE one plan all stay with <see cref="PlanBuilder"/>, which
/// is where they were already tested without a machine to break. What this adds is the two answers
/// that only exist once there is more than one entry: which plan goes first, and which entries get
/// no plan at all.
///
/// <b>Reads and reasons, never writes</b> - the same property that makes the interesting half of
/// `ADR-11` testable, and the reason a selection of twenty can be checked without touching a
/// machine.
///
/// The listing is handed in rather than fetched, for the reason <see cref="PlanBuilder"/> gives: the
/// caller usually has one already and reading it again would cost a fifth of a second to learn
/// nothing new.
/// </summary>
/// <param name="processes">
/// Handed straight to <see cref="PlanBuilder"/> and used for nothing here - see that class for what
/// it is and what its absence means. Threaded through rather than left out because a selection is
/// exactly where a refusal discovered late is most expensive: the entries before it in the order
/// have already been dealt with by then.
/// </param>
public sealed class BulkPlanBuilder(
    IReadOnlyList<ScmEntry> entries,
    IScmCatalog catalog,
    IEndingFactsReader? processes = null)
{
    public BulkPlan Build(BulkAction action)
    {
        // A name given twice is the same ask twice rather than two asks, so it is built once. This
        // is not the dedup the type above refuses to do: that one would drop a STEP the runner is
        // going to take, and this drops a repeated REQUEST before any plan exists. The record of
        // what somebody asked keeps its repeats, because it is a record.
        var asked = action.ServiceNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var builder = new PlanBuilder(entries, catalog, processes);
        var plans = new List<OperationPlan>();
        var problems = new List<PlanProblem>();

        foreach (var name in InTheOrderTheyMustHappen(action.Kind, asked))
        {
            var plan = builder.Build(new ServiceAction(action.Kind, name, action.IncludeDependents, action.To));

            if (plan.Problems.Count > 0)
            {
                // Owner's decision, 2026-08-18: the refusal belongs to its own entry and the rest of
                // the selection carries on. Visible in the preview, before anything runs, so the
                // choice to go ahead without that entry is made by a person rather than by us.
                problems.AddRange(plan.Problems);

                continue;
            }

            plans.Add(plan);
        }

        return new BulkPlan
        {
            Action = action,
            Plans = plans,
            Problems = problems
        };
    }

    /// <summary>
    /// Which entry is dealt with first.
    ///
    /// <b>Taking something down is ordered and starting something is not, and the asymmetry is the
    /// manager's rather than ours.</b> An entry cannot stop while something running needs it, so a
    /// selection holding both sides of a dependency has exactly one order that works and the shared
    /// rule in <see cref="DependentsFirst"/> is the same one a cascade is put in. Starting is the
    /// other way round: the manager starts what a service needs before starting the service, so an
    /// order invented here would be a claim about something we do not do.
    ///
    /// <b>A restart is ordered like a stop, and it is worth saying why rather than leaving it to be
    /// worked out.</b> Its first half takes things down, so the same constraint applies to it. Its
    /// second half can leave an entry restarted twice when one selected entry is the cascade of
    /// another - the machine still ends where the plan said it would, the preview shows both, and
    /// <see cref="BulkPlan.Overlapping"/> names it. A cleverer order would trade a visible repeat
    /// for an invisible rule.
    ///
    /// <b>Setting a start type joined the unordered side on 2026-08-26, and it had been on the
    /// wrong one since this was written.</b> Nothing is taken down - <see cref="PlanBuilder"/> says
    /// so in the arm that builds the step, which is one line under a switch whose other arms are
    /// four - so there is no constraint for an order to satisfy. The cost of getting it wrong was
    /// not the order, which was harmless, but the question asked to work it out:
    /// <see cref="DependentsFirst.Order"/> spends one <c>ReadDependents</c> per name, and each of
    /// those opens the manager and then the service. Selecting three hundred entries and asking to
    /// disable them paid three hundred round trips to the manager, on the thread drawing the
    /// window, to sort a list whose order did not matter.
    /// </summary>
    private List<string> InTheOrderTheyMustHappen(ActionKind kind, List<string> asked) =>
        kind is ActionKind.Start or ActionKind.SetStartType
            ? asked
            : DependentsFirst.Order(catalog, asked);
}
