using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// What a pause plan says about the entries that depend on the paused one: only those that really keep
/// running, out loud when they could not be read (rule 8), and - over a selection - not those the same
/// selection pauses. The chain: SessionEnv and Netlogon depend on LanmanWorkstation.
/// </summary>
public sealed class PauseDependantsTests
{
    [Fact]
    public void A_list_of_dependants_that_cannot_be_read_is_said_and_the_pause_is_still_planned()
    {
        var catalog = Pausable();
        catalog.RefuseDependentsFor.Add("LanmanWorkstation");

        var plan = Plan(ActionKind.Pause, "LanmanWorkstation", catalog);

        Assert.Equal(StepOperation.Pause, Assert.Single(plan.Steps).Operation);
        Assert.Equal("LanmanWorkstation", Warning(plan, PlanWarningKind.DependentsUnreadable).ServiceName);
        Assert.Single(plan.Warnings);
    }

    [Theory]
    [InlineData(EntryStatus.Paused)]
    [InlineData(EntryStatus.PausePending)]
    [InlineData(EntryStatus.StopPending)]
    public void A_dependant_that_is_not_running_is_not_named_as_keeping_running(EntryStatus status)
    {
        var catalog = Rebuild(Pausable(), "Netlogon", entry => entry with { Status = status });

        Assert.Equal(
            ["SessionEnv"],
            Warning(Plan(ActionKind.Pause, "LanmanWorkstation", catalog), PlanWarningKind.DependentsKeepRunning).Related);
    }

    [Fact]
    public void A_dependant_the_same_selection_pauses_is_not_named_as_keeping_running()
    {
        var plan = Bulk(Rebuild(Pausable(), "SessionEnv", Takes), "LanmanWorkstation", "SessionEnv");

        Assert.Empty(plan.Problems);
        Assert.Equal(["Netlogon"], Warning(Of(plan, "LanmanWorkstation"), PlanWarningKind.DependentsKeepRunning).Related);
        Assert.Equal(StepOperation.Pause, Assert.Single(Of(plan, "SessionEnv").Steps).Operation);
    }

    [Fact]
    public void A_selection_that_pauses_every_dependant_says_nothing_about_them()
    {
        var catalog = Rebuild(Rebuild(Pausable(), "SessionEnv", Takes), "Netlogon", Takes);

        var plan = Bulk(catalog, "LanmanWorkstation", "SessionEnv", "Netlogon");

        Assert.Empty(Of(plan, "LanmanWorkstation").Warnings);
        Assert.Equal(3, plan.Plans.Count);
    }

    [Fact]
    public void A_dependant_whose_own_pause_is_refused_keeps_running_and_stays_named()
    {
        var catalog = Rebuild(Pausable(), "SessionEnv", entry => entry with { AcceptsPause = Reading<bool>.Present(false) });

        var plan = Bulk(catalog, "LanmanWorkstation", "SessionEnv");

        Assert.Equal(PlanProblemKind.PauseNotAccepted, Assert.Single(plan.Problems).Kind);
        Assert.Equal(
            ["SessionEnv", "Netlogon"],
            Warning(Of(plan, "LanmanWorkstation"), PlanWarningKind.DependentsKeepRunning).Related);
    }

    private static FakeScmCatalog Pausable() => Rebuild(Chain(), "LanmanWorkstation", Takes);

    private static ScmEntry Takes(ScmEntry entry) => entry with { AcceptsPause = Reading<bool>.Present(true) };

    private static BulkPlan Bulk(FakeScmCatalog catalog, params string[] names) =>
        new BulkPlanBuilder(catalog.ReadAll(), catalog).Build(new BulkAction(ActionKind.Pause, names, false));

    private static OperationPlan Of(BulkPlan plan, string serviceName) =>
        Assert.Single(plan.Plans, one => one.Action.ServiceName == serviceName);
}
