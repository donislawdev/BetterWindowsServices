using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The last look at a process before it is ended, and what the step says when the manager brings the
/// entry straight back - stability report W-3 and W-6, package B2, 2026-09-30.
///
/// <b>Two things a plan cannot know when it is built</b>, because both happen after the preview: a service
/// starting inside the process, or a dependant starting outside it. Until that day the ending went ahead
/// over both. And a step that ended a process watched only for Stopped, which a recovery restart at 0 ms
/// showed for 42-58 ms or not at all on the throwaway machine - so it waited the whole limit and reported
/// running out of time over a service already running again.
///
/// <b>Plans written out by hand</b>, like ForcedRunTests, because these are about what the RUNNER does.
/// </summary>
public sealed class EndingCompanyRunTests
{
    private const int Held = 4812;

    [Fact]
    public void A_service_that_moved_into_the_process_after_the_preview_holds_the_ending()
    {
        // Named to the double, so it lives in the process - every entry here is in 4812 unless told.
        var control = new FakeScmControl().At("Newcomer", EntryStatus.Running);

        var result = Assert.Single(Run(control, Ending()).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Contains("Newcomer", result.Error, StringComparison.Ordinal);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void A_neighbour_the_plan_named_is_not_a_stranger()
    {
        var control = new FakeScmControl().At("Housemate", EntryStatus.Running);

        Run(control, Ending("Housemate"));

        Assert.Equal(Held, Assert.Single(control.Ended));
    }

    [Fact]
    public void A_dependant_that_started_outside_the_process_after_the_preview_holds_the_ending()
    {
        var control = new FakeScmControl()
            .DependedOnBy("Spooler", "Watcher")
            .At("Watcher", EntryStatus.Running)
            .RunningIn("Watcher", 5555);

        var result = Assert.Single(Run(control, Ending()).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Contains("Watcher", result.Error, StringComparison.Ordinal);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void A_stopped_dependant_and_one_inside_the_process_hold_nothing_back()
    {
        // The first needs nothing from a process. The second dies with it and the plan named it.
        var control = new FakeScmControl()
            .DependedOnBy("Spooler", "Sleeper", "Housemate")
            .At("Sleeper", EntryStatus.Stopped)
            .At("Housemate", EntryStatus.Running);

        Run(control, Ending("Housemate"));

        Assert.Equal(Held, Assert.Single(control.Ended));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_process_whose_company_cannot_be_read_is_not_ended(bool statuses)
    {
        var control = statuses
            ? new FakeScmControl().RefusingStatuses(5)
            : new FakeScmControl().RefusingDependents("Spooler", 5);

        var result = Assert.Single(Run(control, Ending()).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(5, result.ErrorCode);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void An_entry_back_in_a_new_process_is_said_at_once_rather_than_after_the_limit()
    {
        var control = new FakeScmControl()
            .ComingBack("Spooler", new ServiceProgress(EntryStatus.Running, 0, TimeSpan.Zero, 5555));

        var result = Assert.Single(Run(control, Ending()).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.True(result.StartedAgain);
        Assert.Equal(0, result.ErrorCode);
        Assert.Equal(EntryStatus.Running, result.Status);
        Assert.Equal(5555, result.ProcessId.Value);

        // The first look already saw it - the fake clock has not moved, where the old watch ran the limit.
        Assert.Equal(0, result.Milliseconds);
    }

    [Fact]
    public void An_entry_being_started_with_no_process_yet_is_watched_on()
    {
        // The manager marks it start pending before the new process exists - measured, for 31 and 45 ms.
        var control = new FakeScmControl().ComingBack(
            "Spooler",
            new ServiceProgress(EntryStatus.StartPending, 0, TimeSpan.FromSeconds(2), 0),
            new ServiceProgress(EntryStatus.Running, 0, TimeSpan.Zero, 5555));

        var result = Assert.Single(Run(control, Ending()).Results);

        Assert.True(result.StartedAgain);
        Assert.Equal(5555, result.ProcessId.Value);
    }

    [Fact]
    public void An_ending_that_is_seen_stopped_is_a_success_as_it_always_was()
    {
        var control = new FakeScmControl();

        var result = Assert.Single(Run(control, Ending()).Results);

        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.False(result.StartedAgain);
    }

    private static OperationPlan Ending(params string[] housemates) => new()
    {
        Action = new ServiceAction(ActionKind.ForceStop, "Spooler", Immediate: true),
        Steps = [new PlanStep("Spooler", "Spooler", StepOperation.Terminate, StepReason.Requested, ProcessId: Held, TakesWithIt: housemates)],
        Warnings = [],
        Problems = []
    };

    private static PlanRun Run(FakeScmControl control, OperationPlan plan) =>
        new PlanRunner(control, new FakeClock()).Run(plan, TimeSpan.FromSeconds(30));
}
