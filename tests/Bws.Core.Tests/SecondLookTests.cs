using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// The looks a run takes at the machine before the first thing it cannot undo - whether what a restart takes
/// down can come back (logic report R03), and what ending a process sets off (R05). Both on the owner's
/// decisions of 2026-10-26.
///
/// <b>Every test here was a plan carried out on an answer the preview had and the machine no longer gave.</b>
/// A restart of an entry set to Disabled after the window's last full reading stopped it and could not start
/// it, and an ending whose recovery list became "restart the computer" after the preview went ahead.
/// </summary>
public sealed class SecondLookTests
{
    private const string Name = "Spooler";

    private const int Netlogon = 7777;

    [Fact]
    public void A_restart_of_an_entry_set_to_Disabled_after_the_preview_stops_nothing()
    {
        var control = new FakeScmControl().At(Name, EntryStatus.Running).StartingAs(Name, StartType.Disabled);

        var run = Run(control, PlanRunProgressTests.Restarting());

        Assert.Equal(StepOutcome.Failed, run.Results[0].Outcome);
        Assert.Equal(0, run.Results[0].ErrorCode);
        Assert.Contains(Name, run.Results[0].Error, StringComparison.Ordinal);
        Assert.Contains("Disabled", run.Results[0].Error, StringComparison.Ordinal);
        Assert.Equal(SkipReason.NothingToPutBack, run.Results[1].SkippedBecause);
        Assert.Empty(control.Requested);
    }

    [Fact]
    public void A_dependant_set_to_Disabled_holds_the_whole_restart()
    {
        // The dependant is the first thing a restart takes down, so it is what the refusal lands on - and
        // the entry asked about is never stopped either, because nothing of the plan may run without the way
        // back it promised.
        var control = new FakeScmControl()
            .At("Netlogon", EntryStatus.Running)
            .At(Name, EntryStatus.Running)
            .StartingAs("Netlogon", StartType.Disabled);

        var run = Run(control, Restart(Name, "Netlogon"));

        Assert.Contains("Netlogon", run.Results[0].Error, StringComparison.Ordinal);
        Assert.Equal(SkipReason.EarlierStepFailed, run.Results[1].SkippedBecause);
        Assert.All(run.Results.Skip(2), result => Assert.Equal(SkipReason.NothingToPutBack, result.SkippedBecause));
        Assert.Empty(control.Requested);
    }

    [Fact]
    public void A_forced_restart_refused_for_a_disabled_neighbour_ends_no_process()
    {
        // The steps standing behind a failure - the neighbours and the ending - run after an ordinary one,
        // because they exist for the stop that did not work. A refusal of the whole plan is not that, and
        // the first draft of this check let them go on and end the process.
        var control = new FakeScmControl()
            .At(Name, EntryStatus.Running)
            .At("Housemate", EntryStatus.Running)
            .StartingAs("Housemate", StartType.Disabled);

        var run = Run(control, ForcedRestart("Housemate"));

        Assert.Contains("Housemate", run.Results[0].Error, StringComparison.Ordinal);
        Assert.Equal(SkipReason.EarlierStepFailed, run.Results[1].SkippedBecause);
        Assert.Equal(SkipReason.EarlierStepFailed, run.Results[2].SkippedBecause);
        Assert.All(run.Results.Skip(3), result => Assert.Equal(SkipReason.NothingToPutBack, result.SkippedBecause));
        Assert.Empty(control.Requested);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void A_start_type_nobody_could_read_holds_nothing_back()
    {
        // Missing information is not a reason to refuse - the builder's line, held here too.
        var control = new FakeScmControl().At(Name, EntryStatus.Running).RefusingStartType(Name, 5);

        var run = Run(control, PlanRunProgressTests.Restarting());

        Assert.All(run.Results, result => Assert.Equal(StepOutcome.Succeeded, result.Outcome));
        Assert.Equal([Name, Name], control.Requested);
    }

    [Fact]
    public void A_plan_that_puts_nothing_back_asks_no_start_type()
    {
        var control = new FakeScmControl().At(Name, EntryStatus.Running);

        Run(control, PlanRunProgressTests.Stopping());

        Assert.Equal(0, control.StartTypeReads);
    }

    [Fact]
    public void An_ending_whose_recovery_list_is_what_the_preview_read_goes_ahead()
    {
        // Two lists with the same items are two objects - the comparison is by value, not by reference.
        var facts = new FakeEndingFacts().Recovering(Name, RecoveryAction.RestartService, RecoveryAction.Nothing);
        var control = new FakeScmControl();

        var run = Run(control, Ending(Frozen(Name, RecoveryAction.RestartService, RecoveryAction.Nothing)), facts);

        Assert.Equal(StepOutcome.Succeeded, Assert.Single(run.Results).Outcome);
        Assert.Single(control.Ended);
    }

    [Theory]
    [InlineData(RecoveryAction.RestartComputer)]
    [InlineData(RecoveryAction.RunProgram)]
    [InlineData(RecoveryAction.Nothing)]
    public void An_ending_whose_recovery_list_changed_after_the_preview_ends_nothing(RecoveryAction now)
    {
        // A restart of the computer is the case the report was about, and it is not the only one: a list
        // that gained a program, or lost its restart, makes the preview's sentences untrue as well.
        var facts = new FakeEndingFacts().Recovering(Name, now);
        var control = new FakeScmControl();

        var result = Assert.Single(Run(control, Ending(Frozen(Name, RecoveryAction.RestartService)), facts).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Contains(Name, result.Error, StringComparison.Ordinal);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void A_neighbour_whose_recovery_list_changed_holds_the_ending_and_is_named()
    {
        var facts = new FakeEndingFacts().Recovering("Housemate", RecoveryAction.RestartComputer);
        var control = new FakeScmControl().At("Housemate", EntryStatus.Running);
        var step = Ending([Frozen(Name), Frozen("Housemate")], "Housemate");

        var result = Assert.Single(Run(control, step, facts).Results);

        Assert.Contains("Housemate", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(Name + ",", result.Error, StringComparison.Ordinal);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void A_process_that_became_critical_after_the_preview_is_not_ended()
    {
        var facts = new FakeEndingFacts().Critical((int)FakeScmControl.FakeProcess);
        var control = new FakeScmControl();

        var result = Assert.Single(Run(control, Ending(Frozen(Name)), facts).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void A_process_whose_criticality_cannot_be_read_just_before_is_not_ended()
    {
        var facts = new FakeEndingFacts().CriticalUnreadable((int)FakeScmControl.FakeProcess);
        var control = new FakeScmControl();

        var result = Assert.Single(Run(control, Ending(Frozen(Name)), facts).Results);

        Assert.Equal(5, result.ErrorCode);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void A_runner_with_nobody_to_ask_ends_on_what_the_earlier_checks_found()
    {
        // The builder's rule, nothing read is nothing said: the same ending, given no reader, goes ahead.
        var control = new FakeScmControl();

        new PlanRunner(control, new FakeClock()).Run(Ending(Frozen(Name, RecoveryAction.RestartService)), TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Single(control.Ended);
    }

    [Fact]
    public void The_builder_freezes_what_it_read_and_the_same_machine_lets_the_run_through()
    {
        // The two halves together, over the measured chain: what the preview read travels on the ending
        // step, and a machine that did not change between the two moments ends the process.
        var facts = new FakeEndingFacts().Recovering("Netlogon", RecoveryAction.RestartService);
        var catalog = Housed(("MRxSmb20", 4444), ("LanmanWorkstation", 5555), ("SessionEnv", 6666), ("Netlogon", Netlogon));
        var plan = new PlanBuilder(catalog.ReadAll(), catalog, facts)
            .Build(new ServiceAction(ActionKind.ForceStop, "Netlogon", Immediate: true));

        var ending = Assert.Single(plan.Steps);
        Assert.Equal("Netlogon", Assert.Single(ending.Recovery!).ServiceName);

        var control = new FakeScmControl().RunningIn("Netlogon", Netlogon);
        var run = new PlanRunner(control, new FakeClock(), facts).Run(plan, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(StepOutcome.Succeeded, Assert.Single(run.Results).Outcome);
        Assert.Equal([Netlogon], control.Ended);
    }

    private static RecoveryReading Frozen(string serviceName, params RecoveryAction[] actions) =>
        new(serviceName, Reading<RecoveryList>.Present(new RecoveryList(
            [.. actions.Select(action => new RecoveryItem(action, TimeSpan.Zero))], FakeEndingFacts.ADay)));

    private static OperationPlan Ending(RecoveryReading frozen) => Ending([frozen]);

    private static OperationPlan Ending(IReadOnlyList<RecoveryReading> frozen, params string[] housemates) => new()
    {
        Action = new ServiceAction(ActionKind.ForceStop, Name, Immediate: true),
        Steps =
        [
            new PlanStep(
                Name,
                Name,
                StepOperation.Terminate,
                StepReason.Requested,
                ProcessId: (int)FakeScmControl.FakeProcess,
                TakesWithIt: housemates,
                Recovery: frozen)
        ],
        Warnings = [],
        Problems = []
    };

    /// <summary>A forced restart with its polite stop, the neighbours, the ending and the way back, by hand.</summary>
    private static OperationPlan ForcedRestart(string housemate) => new()
    {
        Action = new ServiceAction(ActionKind.ForceRestart, Name),
        Steps =
        [
            new PlanStep(Name, Name, StepOperation.Stop, StepReason.Requested),
            new PlanStep(housemate, housemate, StepOperation.Stop, StepReason.SharesTheProcess),
            new PlanStep(
                Name,
                Name,
                StepOperation.Terminate,
                StepReason.Escalation,
                ProcessId: (int)FakeScmControl.FakeProcess,
                TakesWithIt: [housemate],
                Recovery: [Frozen(Name), Frozen(housemate)]),
            new PlanStep(Name, Name, StepOperation.Start, StepReason.Restore),
            new PlanStep(housemate, housemate, StepOperation.Start, StepReason.Restore)
        ],
        Warnings = [],
        Problems = []
    };

    /// <summary>A restart taking the dependants down first and putting them back last, written out by hand.</summary>
    private static OperationPlan Restart(string serviceName, params string[] dependants) => new()
    {
        Action = new ServiceAction(ActionKind.Restart, serviceName, IncludeDependents: true),
        Steps =
        [
            .. dependants.Select(name => new PlanStep(name, name, StepOperation.Stop, StepReason.Cascade)),
            new PlanStep(serviceName, serviceName, StepOperation.Stop, StepReason.Requested),
            new PlanStep(serviceName, serviceName, StepOperation.Start, StepReason.Restore),
            .. Enumerable.Reverse(dependants).Select(name => new PlanStep(name, name, StepOperation.Start, StepReason.Restore))
        ],
        Warnings = [],
        Problems = []
    };

    private static PlanRun Run(FakeScmControl control, OperationPlan plan, FakeEndingFacts? facts = null) =>
        new PlanRunner(control, new FakeClock(), facts).Run(plan, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
}
