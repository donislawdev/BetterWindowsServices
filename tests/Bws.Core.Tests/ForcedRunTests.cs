using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// How a plan that ends a process is carried out when something on the way does not go to plan -
/// the external stability report of 2026-09-29 (W-2) and the owner's decision on it.
///
/// <b>Three rules, each the opposite of what the run did before that day.</b> A dependant that will
/// not stop holds the ending, because it is still running on the process. A neighbour that will not
/// stop holds nothing, because it dies with the process either way and the preview says so. And a
/// neighbour is asked only on the way to an ending that is going to happen - an entry that stops
/// politely leaves its process, and its neighbours, where they are.
///
/// <b>And what the way back says about the neighbours afterwards (W-10)</b>, because the ending moves
/// them without a step of their own that did.
///
/// <b>Driven through plans written out by hand</b>, like EscalationRunTests, because these are about
/// what the RUNNER does with the steps it is handed. The builder's order has tests of its own.
/// </summary>
public sealed class ForcedRunTests
{
    private const int Held = 4812;

    [Fact]
    public void A_dependant_that_will_not_stop_holds_the_ending()
    {
        var control = new FakeScmControl().RefusingRequests("Dependant", 1052);

        var run = Run(control, Forced(
            Stop("Dependant", StepReason.Cascade),
            Stop("Spooler", StepReason.Requested),
            Stop("Housemate", StepReason.SharesTheProcess),
            Ending("Housemate")));

        Assert.Equal(StepOutcome.Failed, run.Results[0].Outcome);
        Assert.All(run.Results.Skip(1), result => Assert.Equal(SkipReason.EarlierStepFailed, result.SkippedBecause));

        // The assertion with the weight: nothing was ended under a service still running on it.
        Assert.Empty(control.Ended);
        Assert.Equal(["Dependant"], control.Requested);
    }

    [Fact]
    public void A_neighbour_that_will_not_stop_holds_nothing_back()
    {
        var control = new FakeScmControl()
            .Reaching("Spooler", new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, Held))
            .RefusingRequests("Housemate", 1052);

        var run = Run(control, Forced(
            Stop("Spooler", StepReason.Requested),
            Stop("Housemate", StepReason.SharesTheProcess),
            Ending("Housemate")));

        Assert.Equal(
            [StepOutcome.TimedOut, StepOutcome.Failed, StepOutcome.Succeeded],
            run.Results.Select(result => result.Outcome));

        Assert.Equal(Held, Assert.Single(control.Ended));
    }

    [Fact]
    public void Neighbours_stay_running_when_the_entry_stops_by_itself()
    {
        var control = new FakeScmControl();

        var run = Run(control, Forced(
            Stop("Spooler", StepReason.Requested),
            Stop("Housemate", StepReason.SharesTheProcess),
            Ending("Housemate")));

        Assert.Equal(SkipReason.ProcessStays, run.Results[1].SkippedBecause);
        Assert.Equal(SkipReason.AlreadyThere, run.Results[2].SkippedBecause);
        Assert.Equal(["Spooler"], control.Requested);
        Assert.Empty(control.Ended);

        // A neighbour the plan did not need is not "not where you asked" - exit code 3 over a machine
        // standing exactly where somebody wanted it would be a runbook line nobody trusts again.
        Assert.True(run.Completed);
    }

    /// <summary>
    /// Backlog 539: the same shape as a forced restart. The starts that would have put the neighbours back
    /// have nothing to give, because nothing took them down - and until 2026-10-07 that read as "not where
    /// you asked", exit code 3 over a machine standing exactly where it was asked to.
    /// </summary>
    [Fact]
    public void A_forced_restart_whose_polite_stop_works_is_complete()
    {
        var control = new FakeScmControl();

        var run = Run(control, Restarted(
            Stop("Spooler", StepReason.Requested),
            Stop("Housemate", StepReason.SharesTheProcess),
            Ending("Housemate"),
            Start("Spooler"),
            Start("Housemate")));

        Assert.Equal(SkipReason.ProcessStays, run.Results[1].SkippedBecause);
        Assert.Equal(StepOutcome.Succeeded, run.Results[3].Outcome);
        Assert.Equal(SkipReason.NothingToPutBack, run.Results[4].SkippedBecause);
        Assert.Equal(["Spooler", "Spooler"], control.Requested);
        Assert.Empty(control.Ended);
        Assert.True(run.Completed);
    }

    /// <summary>
    /// The other half of 539, and the reason the rule is narrow: an entry somebody stopped before the run is
    /// left stopped (package D, W-5 c) and the plan wanted it running. Its neighbour is out of the count -
    /// the entry itself is not.
    /// </summary>
    [Fact]
    public void A_forced_restart_of_an_entry_somebody_stopped_first_is_not_complete()
    {
        var control = new FakeScmControl().At("Spooler", EntryStatus.Stopped);

        var run = Run(control, Restarted(
            Stop("Spooler", StepReason.Requested),
            Stop("Housemate", StepReason.SharesTheProcess),
            Ending("Housemate"),
            Start("Spooler"),
            Start("Housemate")));

        Assert.Equal(SkipReason.AlreadyThere, run.Results[0].SkippedBecause);
        Assert.Equal(SkipReason.NothingToPutBack, run.Results[3].SkippedBecause);
        Assert.Empty(control.Requested);
        Assert.False(run.Completed);
    }

    [Fact]
    public void A_neighbour_is_left_alone_when_the_entry_cannot_be_read()
    {
        // The ending cannot happen - its own reading will be refused - so stopping a neighbour on the
        // way to it would take a service down for nothing.
        var control = new FakeScmControl().RefusingReads("Spooler", 5);

        var run = Run(control, Forced(
            Stop("Spooler", StepReason.Requested),
            Stop("Housemate", StepReason.SharesTheProcess),
            Ending("Housemate")));

        Assert.Equal(SkipReason.ProcessStays, run.Results[1].SkippedBecause);
        Assert.Empty(control.Requested);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void A_neighbour_is_left_alone_when_the_process_is_not_the_one_the_plan_named()
    {
        var control = new FakeScmControl()
            .Reaching("Spooler", new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, 9999));

        var run = Run(control, Forced(
            Stop("Spooler", StepReason.Requested),
            Stop("Housemate", StepReason.SharesTheProcess),
            Ending("Housemate")));

        Assert.Equal(SkipReason.ProcessStays, run.Results[1].SkippedBecause);
        Assert.Equal(StepOutcome.Failed, run.Results[2].Outcome);
        Assert.Equal(["Spooler"], control.Requested);
        Assert.Empty(control.Ended);
    }

    /// <summary>
    /// The way back names a neighbour the ending took down - stability report W-10. Its own step was
    /// refused, so nothing but the ending moved it, and until 2026-09-29 it had no line at all.
    /// </summary>
    [Fact]
    public void A_neighbour_that_died_with_the_process_is_handed_back()
    {
        var control = new FakeScmControl()
            .Reaching("Spooler", new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, Held))
            .RefusingRequests("Housemate", 1052);

        var run = Run(control, Forced(
            Stop("Spooler", StepReason.Requested),
            Stop("Housemate", StepReason.SharesTheProcess),
            Ending("Housemate")));

        Assert.Equal(
            ["Housemate", "Spooler"],
            run.Reversal.Where(back => back.Operation == StepOperation.Start).Select(back => back.ServiceName).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A forced restart that skipped the courtesy brings a neighbour back that nobody asked to stop -
    /// and the way back used to say "stop it", worked out from the one step it could see, about a
    /// service that had been running all along.
    /// </summary>
    [Fact]
    public void A_forced_restart_without_the_courtesy_hands_back_nothing_for_a_neighbour_it_brought_back()
    {
        // Named to the double before the ending, so that it lives in the process being ended - the
        // double only takes down entries it has already heard of.
        var control = new FakeScmControl().At("Housemate", EntryStatus.Running);

        var run = Run(control, Forced(
            new PlanStep("Spooler", "Spooler", StepOperation.Terminate, StepReason.Requested, ProcessId: Held, TakesWithIt: ["Housemate"]),
            Start("Spooler"),
            Start("Housemate")));

        Assert.Equal(Held, Assert.Single(control.Ended));
        Assert.Empty(run.Reversal);
    }

    [Fact]
    public void A_neighbour_found_already_stopped_is_not_handed_back()
    {
        // Its own step found it stopped, so it was not in the process when the process ended.
        var control = new FakeScmControl()
            .Reaching("Spooler", new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, Held))
            .At("Housemate", EntryStatus.Stopped);

        var run = Run(control, Forced(
            Stop("Spooler", StepReason.Requested),
            Stop("Housemate", StepReason.SharesTheProcess),
            Ending("Housemate")));

        Assert.Equal("Spooler", Assert.Single(run.Reversal).ServiceName);
    }

    private static PlanStep Stop(string name, StepReason reason) => new(name, name, StepOperation.Stop, reason);

    private static PlanStep Start(string name) => new(name, name, StepOperation.Start, StepReason.Restore);

    private static PlanStep Ending(params string[] housemates) =>
        new("Spooler", "Spooler", StepOperation.Terminate, StepReason.Escalation, ProcessId: Held, TakesWithIt: housemates);

    private static OperationPlan Forced(params PlanStep[] steps) => new()
    {
        Action = new ServiceAction(ActionKind.ForceStop, "Spooler"),
        Steps = steps,
        Warnings = [],
        Problems = []
    };

    private static OperationPlan Restarted(params PlanStep[] steps) =>
        Forced(steps) with { Action = new ServiceAction(ActionKind.ForceRestart, "Spooler") };

    private static PlanRun Run(FakeScmControl control, OperationPlan plan) =>
        new PlanRunner(control, new FakeClock()).Run(plan, TimeSpan.FromSeconds(30));
}
