using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// What a plan that ends a process says about what comes after it - stability report W-3, the owner's
/// decisions of 2026-09-29 and 2026-09-30.
///
/// <b>Measured before a line changed</b> (the throwaway machine, 2026-09-30): the manager restarted a
/// service in ten endings of ten when its recovery list said so, and a program in the list ran - while the
/// preview of <c>bws kill</c> said nothing about either. Every test here is a preview that used to be
/// silent about something the ending sets off.
///
/// <b>The machine is the measured chain</b> with Netlogon at the end of it, so nothing depends on the
/// entry being ended - alone in process 7777, or sharing it with SessionEnv.
/// </summary>
public sealed class AftermathTests
{
    private const int Netlogon = 7777;

    [Fact]
    public void A_restart_in_the_recovery_list_is_said_before_anything_is_ended()
    {
        var facts = new FakeEndingFacts().Recovering("Netlogon", RecoveryAction.RestartService, RecoveryAction.Nothing);

        var plan = Forced(Separate(), facts);

        Assert.Empty(plan.Problems);
        Assert.Equal("Netlogon", Assert.Single(Warning(plan, PlanWarningKind.RecoveryRestarts).Related));
    }

    [Fact]
    public void The_restart_warning_carries_every_delay_once_in_the_order_of_the_list()
    {
        // Backlog 501. 60 s twice and 120 s once is two delays, a program is not a restart, and a neighbour
        // with no restart at all is not named - the names and the delays come from one reading.
        var facts = new FakeEndingFacts()
            .Recovering(
                "Netlogon",
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromSeconds(60)),
                new RecoveryItem(RecoveryAction.RunProgram, TimeSpan.FromSeconds(5)),
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromSeconds(120)),
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromSeconds(60)),
                new RecoveryItem(RecoveryAction.Nothing, TimeSpan.Zero))
            .Recovering("SessionEnv", new RecoveryItem(RecoveryAction.Nothing, TimeSpan.FromSeconds(30)));

        var warning = Warning(Forced(Sharing(), facts), PlanWarningKind.RecoveryRestarts);

        var restart = Assert.Single(warning.Restarts);
        Assert.Equal("Netlogon", restart.ServiceName);
        Assert.Equal([TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120)], restart.After);
        Assert.Equal(["Netlogon"], warning.Related);
    }

    [Fact]
    public void A_neighbour_that_sets_a_program_off_is_named_and_the_entry_is_asked_first()
    {
        var facts = new FakeEndingFacts().Recovering("SessionEnv", RecoveryAction.Nothing, RecoveryAction.RunProgram);

        var plan = Forced(Sharing(), facts);

        Assert.Equal("SessionEnv", Assert.Single(Warning(plan, PlanWarningKind.RecoveryRunsProgram).Related));
        Assert.Equal(["Netlogon", "SessionEnv"], facts.AskedRecovery);
    }

    [Fact]
    public void An_item_this_tool_has_no_name_for_is_said_rather_than_guessed_at()
    {
        var facts = new FakeEndingFacts().Recovering("Netlogon", RecoveryAction.Unnamed, RecoveryAction.RestartService);

        var plan = Forced(Separate(), facts);

        Assert.Equal("Netlogon", Assert.Single(Warning(plan, PlanWarningKind.RecoveryUnnamed).Related));
        Assert.Equal("Netlogon", Assert.Single(Warning(plan, PlanWarningKind.RecoveryRestarts).Related));
    }

    [Fact]
    public void A_computer_restart_anywhere_in_a_neighbours_list_is_a_refusal()
    {
        // Third, not first: which item runs depends on a failure count nobody can read.
        var facts = new FakeEndingFacts().Recovering(
            "SessionEnv", RecoveryAction.RestartService, RecoveryAction.RestartService, RecoveryAction.RestartComputer);

        var plan = Forced(Sharing(), facts);

        var problem = Assert.Single(plan.Problems);
        Assert.Equal(PlanProblemKind.RecoveryRestartsComputer, problem.Kind);
        Assert.Equal("SessionEnv", Assert.Single(problem.Related));
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void A_recovery_list_the_manager_will_not_show_is_a_refusal()
    {
        var plan = Forced(Separate(), new FakeEndingFacts().RefusingRecovery("Netlogon"));

        var problem = Assert.Single(plan.Problems);
        Assert.Equal(PlanProblemKind.AftermathUnreadable, problem.Kind);
        Assert.Equal("Netlogon", Assert.Single(problem.Related));
    }

    [Fact]
    public void A_critical_process_is_refused_before_anything_else_is_asked()
    {
        var facts = new FakeEndingFacts().Critical(Netlogon);

        var plan = Forced(Sharing(), facts);

        Assert.Equal(PlanProblemKind.ProcessIsCritical, Assert.Single(plan.Problems).Kind);

        // Asked from the same reading as the right to end it, so nothing further was worked out.
        Assert.Empty(facts.AskedRecovery);
    }

    [Fact]
    public void Not_knowing_whether_the_process_is_critical_is_a_refusal()
    {
        var plan = Forced(Separate(), new FakeEndingFacts().CriticalUnreadable(Netlogon));

        var problem = Assert.Single(plan.Problems);
        Assert.Equal(PlanProblemKind.AftermathUnreadable, problem.Kind);
        Assert.Empty(problem.Related);
    }

    [Fact]
    public void An_entry_gone_from_the_manager_since_the_listing_sets_nothing_off()
    {
        var plan = Forced(Separate(), new FakeEndingFacts().GoneFromTheManager("Netlogon"));

        Assert.Empty(plan.Problems);
        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind is PlanWarningKind.RecoveryRestarts
            or PlanWarningKind.RecoveryRunsProgram or PlanWarningKind.RecoveryUnnamed);
    }

    [Fact]
    public void The_cascade_is_never_asked_because_nothing_in_it_dies_with_the_process()
    {
        var facts = new FakeEndingFacts();
        var catalog = Separate();

        var plan = new PlanBuilder(catalog.ReadAll(), catalog, facts)
            .Build(new ServiceAction(ActionKind.ForceStop, "MRxSmb20", IncludeDependents: true));

        Assert.Empty(plan.Problems);
        Assert.Contains("MRxSmb20", facts.AskedRecovery);
        Assert.DoesNotContain(facts.AskedRecovery, name => name is "LanmanWorkstation" or "SessionEnv" or "Netlogon");
    }

    private static OperationPlan Forced(FakeScmCatalog catalog, FakeEndingFacts facts) =>
        new PlanBuilder(catalog.ReadAll(), catalog, facts).Build(new ServiceAction(ActionKind.ForceStop, "Netlogon"));

    private static FakeScmCatalog Separate() =>
        Housed(("MRxSmb20", 4444), ("LanmanWorkstation", 5555), ("SessionEnv", 6666), ("Netlogon", Netlogon));

    private static FakeScmCatalog Sharing() =>
        Housed(("MRxSmb20", 4444), ("LanmanWorkstation", 5555), ("SessionEnv", Netlogon), ("Netlogon", Netlogon));
}
