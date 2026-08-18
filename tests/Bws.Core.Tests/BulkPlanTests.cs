using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The plan over a selection, checked without anything happening. `C2`.
///
/// <b><see cref="PlanBuilderTests"/> holds what one plan decides. This holds only what cannot be
/// seen from inside one:</b> which plan goes first, which entries get no plan at all, how many
/// entries come along that nobody asked for, and which entry is named by two plans at once. Anything
/// asserted here that a single plan already answers would be a second copy of a rule, in the one
/// place where two copies disagreeing means somebody's machine goes down in the wrong order.
///
/// The chain is the one measured on a real machine on 2026-08-01, the same one that class uses:
/// MRxSmb20 is needed by LanmanWorkstation, which is needed by SessionEnv and Netlogon.
/// </summary>
public sealed class BulkPlanTests
{
    [Fact]
    public void Two_entries_that_need_nothing_from_each_other_keep_the_order_they_came_in()
    {
        var plan = Bulk(ActionKind.Stop, ["Spooler", "MRxSmb20"], includeDependents: false);

        Assert.True(plan.IsRunnable);
        Assert.Equal(["Spooler", "MRxSmb20"], plan.Plans.Select(one => one.Action.ServiceName));
        Assert.Empty(plan.Problems);
    }

    /// <summary>
    /// THE ONE ANSWER A SINGLE PLAN CANNOT GIVE, and the reason this type exists at all.
    ///
    /// Both of these were selected, and one of them is needed by the other. The manager refuses to
    /// stop MRxSmb20 while LanmanWorkstation runs, so a bulk that took the selection in the order it
    /// was handed over would fail its first step and then succeed at the second - reporting a
    /// refusal for something the person had in fact arranged correctly.
    /// </summary>
    [Fact]
    public void An_entry_something_else_in_the_selection_needs_is_dealt_with_last()
    {
        // Handed over the wrong way round on purpose, so passing means it was reordered rather
        // than that the input happened to be right.
        var plan = Bulk(ActionKind.Stop, ["MRxSmb20", "LanmanWorkstation"], includeDependents: false);

        Assert.Equal(
            ["LanmanWorkstation", "MRxSmb20"],
            plan.Plans.Select(one => one.Action.ServiceName));
    }

    /// <summary>
    /// Starting is NOT reordered, and that is the manager's asymmetry rather than an omission.
    ///
    /// The manager starts what a service needs before starting the service, so an order worked out
    /// here would be a claim about something we do not do.
    /// </summary>
    [Fact]
    public void Starting_a_selection_is_not_reordered()
    {
        var plan = Bulk(ActionKind.Start, ["MRxSmb20", "LanmanWorkstation"], includeDependents: false);

        Assert.Equal(
            ["MRxSmb20", "LanmanWorkstation"],
            plan.Plans.Select(one => one.Action.ServiceName));
    }

    /// <summary>
    /// Owner's decision, 2026-08-18: a refusal belongs to its own entry and the rest carries on.
    ///
    /// One driver picked up by a rubber-band selection does not hold up nineteen services, and the
    /// refusal is in the preview before anything runs, so going ahead without that entry is a
    /// person's choice rather than ours.
    /// </summary>
    [Fact]
    public void An_entry_the_plan_refuses_does_not_stop_the_rest_of_the_selection()
    {
        // THE REFUSED ONE IS FIRST ON PURPOSE. With it last this test would pass even if a
        // refusal abandoned everything after it, which is the behaviour being asserted.
        var plan = Bulk(ActionKind.Stop, ["amduw23g-202073-df09ebb6", "Spooler"], includeDependents: false);

        Assert.True(plan.IsRunnable);
        Assert.Equal("Spooler", Assert.Single(plan.Plans).Action.ServiceName);
        Assert.Equal(PlanProblemKind.NotOperable, Assert.Single(plan.Problems).Kind);
    }

    [Fact]
    public void A_name_nobody_has_is_a_problem_beside_its_own_entry()
    {
        // First, for the reason given in the test above: last, it would prove nothing.
        var plan = Bulk(ActionKind.Stop, ["NoSuchServiceAnywhere", "Spooler"], includeDependents: false);

        Assert.True(plan.IsRunnable);
        Assert.Single(plan.Plans);

        var problem = Assert.Single(plan.Problems);

        Assert.Equal(PlanProblemKind.UnknownService, problem.Kind);
        Assert.Equal("NoSuchServiceAnywhere", problem.ServiceName);
    }

    /// <summary>
    /// AN ENTRY IN TWO PLANS IS NAMED RATHER THAN TIDIED AWAY - rule 8 applied to a preview.
    ///
    /// Both selected entries drag the same two along, and LanmanWorkstation is both selected and
    /// inside the cascade of the other one. Removing the repeat would read better and would make the
    /// preview shorter than the run, which is the one thing `ADR-11` exists to prevent. The repeat is
    /// harmless where it lands: the runner reads an entry before acting and reports "already there".
    /// </summary>
    [Fact]
    public void An_entry_named_by_two_plans_is_said_out_loud()
    {
        var plan = Bulk(ActionKind.Stop, ["MRxSmb20", "LanmanWorkstation"], includeDependents: true);

        Assert.Equal(
            ["LanmanWorkstation", "Netlogon", "SessionEnv"],
            plan.Overlapping.OrderBy(name => name, StringComparer.Ordinal));
    }

    /// <summary>
    /// The second number in `C2`'s own sentence - "stopping these 3 will drag another 7".
    ///
    /// Counted across the selection rather than per plan. Both plans drag SessionEnv and Netlogon in,
    /// and adding up per-plan cascades would count them twice and promise a bigger consequence than
    /// there is. LanmanWorkstation is dragged in by the other plan AND was asked for, so it is not
    /// extra - somebody asked for it.
    /// </summary>
    [Fact]
    public void What_comes_along_is_counted_once_and_excludes_what_was_asked_for()
    {
        var plan = Bulk(ActionKind.Stop, ["MRxSmb20", "LanmanWorkstation"], includeDependents: true);

        Assert.Equal(
            ["Netlogon", "SessionEnv"],
            plan.Extra.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void The_same_name_twice_is_the_same_ask_twice()
    {
        var plan = Bulk(ActionKind.Stop, ["Spooler", "spooler"], includeDependents: false);

        Assert.Single(plan.Plans);

        // The record of what somebody asked keeps its repeats, because it is a record.
        Assert.Equal(2, plan.Action.ServiceNames.Count);
    }

    /// <summary>
    /// Degenerate and legal: nothing selected. Not runnable, and not an error either - nobody asked
    /// for anything, so there is nothing to refuse.
    /// </summary>
    [Fact]
    public void An_empty_selection_is_not_runnable_and_is_not_a_problem()
    {
        var plan = Bulk(ActionKind.Stop, [], includeDependents: false);

        Assert.False(plan.IsRunnable);
        Assert.Empty(plan.Plans);
        Assert.Empty(plan.Problems);
        Assert.Empty(plan.Steps);
        Assert.Empty(plan.Extra);
        Assert.Empty(plan.Overlapping);
    }

    /// <summary>
    /// A selection of one goes through without any ceremony for many, which is the path the window
    /// will take most often.
    /// </summary>
    [Fact]
    public void A_selection_of_one_is_one_ordinary_plan()
    {
        var plan = Bulk(ActionKind.Stop, ["Spooler"], includeDependents: false);

        var only = Assert.Single(plan.Plans);

        Assert.Equal("Spooler", Assert.Single(only.Steps).ServiceName);
        Assert.Empty(plan.Extra);
        Assert.Empty(plan.Overlapping);
    }

    /// <summary>
    /// The listing is read once however many entries were selected. The double counts, so this is
    /// checked rather than hoped - twenty entries costing twenty readings would be a fifth of a
    /// second each to learn nothing new.
    /// </summary>
    [Fact]
    public void The_listing_is_read_once_however_many_entries_were_selected()
    {
        var catalog = Chain();

        _ = new BulkPlanBuilder(catalog.ReadAll(), catalog)
            .Build(new BulkAction(ActionKind.Stop, ["MRxSmb20", "LanmanWorkstation", "Spooler"]));

        Assert.Equal(1, catalog.Reads);
    }

    // -- fixtures --------------------------------------------------------------------------

    private static BulkPlan Bulk(ActionKind kind, IReadOnlyList<string> names, bool includeDependents)
    {
        var catalog = Chain();

        return new BulkPlanBuilder(catalog.ReadAll(), catalog)
            .Build(new BulkAction(kind, names, includeDependents));
    }

    /// <summary>The chain as the manager describes it, transitive sets included.</summary>
    private static FakeScmCatalog Chain()
    {
        var entries = new List<ScmEntry>(Specimens.All)
        {
            Running("MRxSmb20", "SMB 2.0 Redirector"),
            Running("LanmanWorkstation", "Stacja robocza"),
            Running("SessionEnv", "Konfiguracja pulpitu zdalnego"),
            Running("Netlogon", "Logowanie do sieci")
        };

        return new FakeScmCatalog(entries)
            .DependedOnBy("MRxSmb20", "SessionEnv", "Netlogon", "LanmanWorkstation")
            .DependedOnBy("LanmanWorkstation", "SessionEnv", "Netlogon");
    }

    private static ScmEntry Running(string serviceName, string displayName) =>
        Entries.Named(serviceName, displayName) with
        {
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            ProcessId = Reading<int>.Present(4444)
        };
}
