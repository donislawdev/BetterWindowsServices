using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// A plan that ends a shared process, on a machine whose manager keeps some entries out of the listing - logic
/// report R04, the owner's decision of 2026-10-26 to detect what the manager hides.
///
/// <b>The shape a real machine produces without an error anywhere</b>: Microsoft's enumeration silently leaves
/// out every entry the caller may not query, and on the owner's machine three entries went missing that way
/// under a restricted token (backlog 493). Until this day the preview of an ending named the neighbours the
/// listing showed and nobody else.
///
/// <b>The machine is the measured chain</b> with Netlogon and SessionEnv sharing process 7777, so the plan
/// itself is one the tool already builds.
/// </summary>
public sealed class UnlistedNeighbourTests
{
    private const int Netlogon = 7777;

    [Fact]
    public void An_entry_the_manager_did_not_list_is_named_and_nothing_is_ended()
    {
        var plan = Forced(Shared(), new FakeEndingFacts().Unlisting("Withheld"));

        var problem = Assert.Single(plan.Problems);
        Assert.Equal(PlanProblemKind.NeighboursUnreadable, problem.Kind);
        Assert.Equal("Netlogon", problem.ServiceName);
        Assert.Equal(["Withheld"], problem.Related);
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void A_list_of_names_nobody_could_read_is_a_refusal_naming_nobody()
    {
        var plan = Forced(Shared(), new FakeEndingFacts().RefusingUnlisted());

        var problem = Assert.Single(plan.Problems);
        Assert.Equal(PlanProblemKind.NeighboursUnreadable, problem.Kind);
        Assert.Empty(problem.Related);
    }

    [Fact]
    public void The_question_carries_every_name_of_the_listing_the_plan_was_built_from()
    {
        var catalog = Shared();
        var facts = new FakeEndingFacts();

        var plan = Forced(catalog, facts);

        Assert.True(plan.IsRunnable);
        Assert.Equal(catalog.ReadAll().Select(entry => entry.ServiceName), Assert.Single(facts.AskedUnlisted));
    }

    [Fact]
    public void An_entry_with_a_process_of_its_own_has_no_neighbours_to_leave_out_and_is_not_asked()
    {
        var facts = new FakeEndingFacts().Unlisting("Withheld");

        var plan = Forced(Housed(("SessionEnv", Netlogon), ("Netlogon", Netlogon)), facts);

        Assert.Empty(plan.Problems);
        Assert.Empty(facts.AskedUnlisted);
    }

    [Fact]
    public void A_critical_process_is_refused_before_the_manager_is_asked_who_it_left_out()
    {
        var facts = new FakeEndingFacts().Critical(Netlogon).Unlisting("Withheld");

        var plan = Forced(Shared(), facts);

        Assert.Equal(PlanProblemKind.ProcessIsCritical, Assert.Single(plan.Problems).Kind);
        Assert.Empty(facts.AskedUnlisted);
    }

    [Fact]
    public void A_plan_with_nobody_to_ask_is_built_as_it_always_was()
    {
        var catalog = Shared();

        var plan = new PlanBuilder(catalog.ReadAll(), catalog).Build(new ServiceAction(ActionKind.ForceStop, "Netlogon"));

        Assert.True(plan.IsRunnable);
    }

    private static OperationPlan Forced(FakeScmCatalog catalog, FakeEndingFacts facts) =>
        new PlanBuilder(catalog.ReadAll(), catalog, facts).Build(new ServiceAction(ActionKind.ForceStop, "Netlogon"));

    /// <summary>The chain with Netlogon in a shared process - the one shape the question is asked of.</summary>
    private static FakeScmCatalog Shared() =>
        Rebuild(
            Housed(("SessionEnv", Netlogon), ("Netlogon", Netlogon)),
            "Netlogon",
            entry => entry with { EntryType = EntryType.SharedProcess });
}
