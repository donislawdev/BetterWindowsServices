using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// What a plan that ends a process refuses, and whom it asks about, since the external stability
/// report of 2026-09-29 (W-4 and W-6).
///
/// <b>Every test here is a preview that used to promise less than the run would do.</b> A forced
/// stop ended the process under running dependants, named no critical entry arriving with the
/// cascade, and never asked about the neighbours at all - whether anything needed them, whether they
/// would take a stop, whether a restart could bring them back. The owner's decision was a refusal
/// wherever something running would be left standing on a process that is gone.
///
/// <b>The machine is the measured chain</b> - MRxSmb20 needed by LanmanWorkstation, which is needed by
/// SessionEnv and Netlogon - with the processes moved around per test. Netlogon sits at the end of it,
/// so nothing depends on it.
/// </summary>
public sealed class ForcedStopRefusalTests
{
    [Fact]
    public void A_forced_stop_refuses_while_entries_depending_on_it_run_outside_the_plan()
    {
        // The manager refuses an ordinary stop here with 1051 and nothing is harmed. The last step of
        // this plan asks no manager, so the same situation was a process ended under three services.
        var plan = Plan(ActionKind.ForceStop, "MRxSmb20", includeDependents: false, Separate());

        var problem = Assert.Single(plan.Problems);

        Assert.Equal(PlanProblemKind.DependentsInTheWay, problem.Kind);
        Assert.Equal(["LanmanWorkstation", "Netlogon", "SessionEnv"], problem.Related.Order(StringComparer.Ordinal));
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void With_its_dependants_asked_for_they_are_stopped_first_and_the_warning_names_exactly_those()
    {
        var plan = Plan(ActionKind.ForceStop, "MRxSmb20", includeDependents: true, Separate());

        string[] stopped = [.. plan.Steps.Where(step => step.Reason == StepReason.Cascade).Select(step => step.ServiceName)];

        Assert.Equal(3, stopped.Length);

        // THE WARNING AND THE STEPS ARE ONE FACT. Until 2026-09-29 the warning was raised from the
        // cascade and the steps were left out under --force, so the preview said three entries would
        // stop and the run stopped none.
        Assert.Equal(stopped, Warning(plan, PlanWarningKind.Cascade).Related);
        Assert.Equal(StepOperation.Terminate, plan.Steps[^1].Operation);
    }

    [Fact]
    public void Skipping_the_courtesy_cannot_carry_the_dependants()
    {
        // Loud rather than a refusal a person can meet: the command line turns the pair back before
        // anything is read, and the window never asks for dependants.
        var catalog = Separate();
        var builder = new PlanBuilder(catalog.ReadAll(), catalog);

        Assert.Throws<ArgumentException>(() => builder.Build(
            new ServiceAction(ActionKind.ForceStop, "MRxSmb20", IncludeDependents: true, Immediate: true)));
    }

    [Fact]
    public void A_running_entry_that_needs_a_neighbour_is_a_refusal()
    {
        // Netlogon shares its process with LanmanWorkstation here, and SessionEnv - running, and in no
        // step of this plan - needs LanmanWorkstation. Ending the process pulls it out from under
        // SessionEnv, and --dependents would not help: SessionEnv does not depend on Netlogon.
        var plan = Plan(ActionKind.ForceStop, "Netlogon", includeDependents: false, Neighboured());

        var problem = Assert.Single(plan.Problems);

        Assert.Equal(PlanProblemKind.NeighbourNeeded, problem.Kind);
        Assert.Equal("SessionEnv", Assert.Single(problem.Related));
    }

    [Fact]
    public void A_neighbour_whose_dependants_cannot_be_read_is_a_casualty_list_known_to_be_short()
    {
        var catalog = Neighboured();
        catalog.RefuseDependentsFor.Add("LanmanWorkstation");

        var plan = Plan(ActionKind.ForceStop, "Netlogon", includeDependents: false, catalog);

        Assert.Equal(PlanProblemKind.CascadeUnreadable, Assert.Single(plan.Problems).Kind);
    }

    [Fact]
    public void A_critical_entry_arriving_with_the_cascade_is_named_on_a_forced_stop()
    {
        // The ordinary stop names it through CriticalEntries, which does not run for this kind, and
        // the forcing kind asked only the neighbours and the target until 2026-09-29.
        var catalog = new FakeScmCatalog(
            [
                Running("Target", "Target") with { ProcessId = Reading<int>.Present(4444) },
                Running("RpcSs", "Remote Procedure Call") with { ProcessId = Reading<int>.Present(5555) }
            ])
            .DependedOnBy("Target", "RpcSs");

        var plan = Plan(ActionKind.ForceStop, "Target", includeDependents: true, catalog);

        Assert.Contains("RpcSs", Warning(plan, PlanWarningKind.CriticalService).Related);
    }

    [Fact]
    public void A_disabled_neighbour_of_a_forced_restart_could_not_come_back()
    {
        var catalog = Rebuild(
            Sharing(),
            "SessionEnv",
            entry => entry with { StartType = Reading<Core.StartType>.Present(Core.StartType.Disabled) });

        var problem = Assert.Single(
            new PlanBuilder(catalog.ReadAll(), catalog).Build(new ServiceAction(ActionKind.ForceRestart, "Netlogon")).Problems);

        Assert.Equal(PlanProblemKind.CannotComeBack, problem.Kind);
        Assert.Equal("SessionEnv", Assert.Single(problem.Related));
    }

    [Fact]
    public void A_neighbour_that_will_not_take_a_stop_is_named_before_anything_runs()
    {
        var catalog = Rebuild(Sharing(), "SessionEnv", entry => entry with { AcceptsStop = Reading<bool>.Present(false) });

        var plan = Plan(ActionKind.ForceStop, "Netlogon", includeDependents: false, catalog);

        Assert.Contains("SessionEnv", Warning(plan, PlanWarningKind.DoesNotAcceptStop).Related);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_ending_names_the_neighbours_it_takes_with_it(bool immediate)
    {
        // With --force the neighbours have no steps of their own, and this list is the only thing a
        // way back can learn them from.
        var catalog = Sharing();

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.ForceStop, "Netlogon", Immediate: immediate));

        var ending = Assert.Single(plan.Steps, step => step.Operation == StepOperation.Terminate);

        Assert.Equal(["SessionEnv"], ending.TakesWithIt!);
    }

    [Fact]
    public void The_entry_is_asked_before_its_neighbours_and_the_neighbours_before_the_ending()
    {
        // Until 2026-09-29 the neighbours came first, and a neighbour refusing its stop skipped the
        // polite stop of the entry itself - the process was ended without the entry ever being asked.
        var plan = Plan(ActionKind.ForceStop, "Netlogon", includeDependents: false, Sharing());

        Assert.Equal(
            [StepReason.Requested, StepReason.SharesTheProcess, StepReason.Escalation],
            plan.Steps.Select(step => step.Reason));
    }

    /// <summary>Every entry in a process of its own.</summary>
    private static FakeScmCatalog Separate() =>
        Housed(("MRxSmb20", 4444), ("LanmanWorkstation", 5555), ("SessionEnv", 6666), ("Netlogon", 7777));

    /// <summary>Netlogon and SessionEnv in one process, and neither needs the other.</summary>
    private static FakeScmCatalog Sharing() =>
        Housed(("MRxSmb20", 4444), ("LanmanWorkstation", 5555), ("SessionEnv", 7777), ("Netlogon", 7777));

    /// <summary>Netlogon and LanmanWorkstation in one process, with SessionEnv needing the second.</summary>
    private static FakeScmCatalog Neighboured() =>
        Housed(("MRxSmb20", 4444), ("LanmanWorkstation", 7777), ("SessionEnv", 6666), ("Netlogon", 7777));
}
