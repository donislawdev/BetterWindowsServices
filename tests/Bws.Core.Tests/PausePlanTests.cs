using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// Pausing and resuming - spec C1, backlog 555 part (b), the owner's decisions of 2026-11-25 - the plan and
/// the run, against what the manager answered on the throwaway machine that day
/// (tools/vm-service/pause-probe.ps1): 1062 for a stopped entry, 1052 for one that takes no pauses, a turned
/// down pause going from PausePending back to Running, and a stop sent during PausePending lost.
/// </summary>
public sealed class PausePlanTests
{
    private const uint Held = FakeScmControl.FakeProcess;

    [Fact]
    public void A_running_entry_that_takes_pauses_is_paused_in_one_step_and_what_depends_on_it_is_named_as_staying()
    {
        var plan = Plan(ActionKind.Pause, "LanmanWorkstation", Pausable());

        Assert.Empty(plan.Problems);
        Assert.Equal(StepOperation.Pause, Assert.Single(plan.Steps).Operation);
        Assert.Equal(["SessionEnv", "Netlogon"], Warning(plan, PlanWarningKind.DependentsKeepRunning).Related);
        Assert.Equal("bws pause LanmanWorkstation", EquivalentCommand.For(plan.Action));
    }

    [Fact]
    public void A_stopped_dependant_is_not_named_and_an_entry_nothing_depends_on_gets_no_sentence()
    {
        var catalog = Rebuild(Pausable(), "Netlogon", entry => entry with { Status = EntryStatus.Stopped });

        Assert.Equal(["SessionEnv"], Warning(Plan(ActionKind.Pause, "LanmanWorkstation", catalog), PlanWarningKind.DependentsKeepRunning).Related);

        var alone = Rebuild(Pausable(), "SessionEnv", entry => entry with { AcceptsPause = Reading<bool>.Present(true) });

        Assert.Empty(Plan(ActionKind.Pause, "SessionEnv", alone).Warnings);
    }

    [Theory]
    [InlineData(ActionKind.Pause, EntryStatus.Stopped, PlanProblemKind.NotRunning)]
    [InlineData(ActionKind.Resume, EntryStatus.Stopped, PlanProblemKind.StoppedNotPaused)]
    [InlineData(ActionKind.Pause, EntryStatus.StopPending, PlanProblemKind.NotRunning)]
    [InlineData(ActionKind.Resume, EntryStatus.StopPending, PlanProblemKind.StoppedNotPaused)]
    public void A_stopped_or_stopping_entry_is_refused_and_never_started(ActionKind kind, EntryStatus status, PlanProblemKind why)
    {
        var plan = Plan(kind, "LanmanWorkstation", Pausable(status));

        Assert.Empty(plan.Steps);
        Assert.Equal(why, Assert.Single(plan.Problems).Kind);
    }

    [Fact]
    public void A_running_entry_that_takes_no_pauses_is_refused_before_anything_is_sent()
    {
        var plan = Plan(ActionKind.Pause, "LanmanWorkstation", Pausable(accepts: false));

        Assert.Empty(plan.Steps);
        Assert.Equal(PlanProblemKind.PauseNotAccepted, Assert.Single(plan.Problems).Kind);
    }

    [Fact]
    public void An_entry_still_starting_is_planned_whatever_its_mask_says_and_a_driver_is_not()
    {
        // Its mask is not believed until it runs - the run waits for it, and the manager answers then.
        var starting = Plan(ActionKind.Pause, "LanmanWorkstation", Pausable(EntryStatus.StartPending, accepts: false));

        Assert.Equal(StepOperation.Pause, Assert.Single(starting.Steps).Operation);

        var driver = Rebuild(Pausable(), "LanmanWorkstation", entry => entry with { EntryType = EntryType.KernelDriver });

        Assert.Equal(PlanProblemKind.NotOperable, Assert.Single(Plan(ActionKind.Pause, "LanmanWorkstation", driver).Problems).Kind);
    }

    [Theory]
    [InlineData(ActionKind.Pause, EntryStatus.Paused)]
    [InlineData(ActionKind.Resume, EntryStatus.Running)]
    public void An_entry_already_there_keeps_its_step_and_is_said_to_be_there(ActionKind kind, EntryStatus status)
    {
        var plan = Plan(kind, "LanmanWorkstation", Pausable(status));

        Assert.Single(plan.Steps);
        Assert.Equal(PlanWarningKind.AlreadyThere, Assert.Single(plan.Warnings).Kind);
    }

    [Fact]
    public void A_paused_entry_is_resumed_in_one_step_with_nothing_said_about_what_depends_on_it()
    {
        var plan = Plan(ActionKind.Resume, "LanmanWorkstation", Pausable(EntryStatus.Paused));

        Assert.Equal(StepOperation.Resume, Assert.Single(plan.Steps).Operation);
        Assert.Empty(plan.Warnings);
        Assert.Equal("bws resume LanmanWorkstation", EquivalentCommand.For(plan.Action));
    }

    [Theory]
    [InlineData(ActionKind.Pause)]
    [InlineData(ActionKind.Resume)]
    public void A_move_that_arrives_succeeds_and_its_way_back_is_the_other_one(ActionKind kind)
    {
        var pausing = kind == ActionKind.Pause;
        var control = new FakeScmControl().At("Spooler", pausing ? EntryStatus.Running : EntryStatus.Paused);

        var run = Ran(control, Asked(kind));

        var result = Assert.Single(run.Results);
        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.Equal(pausing ? EntryStatus.Paused : EntryStatus.Running, result.Status);
        Assert.Equal(pausing ? "bws resume Spooler" : "bws pause Spooler", EquivalentCommand.For(Assert.Single(run.Reversal)));
    }

    [Fact]
    public void A_pause_the_service_turns_down_is_a_failure_in_our_words_with_no_way_back()
    {
        // Measured 2026-11-25: a pause whose handler threw went PausePending, then Running ~100 ms later, exit 0.
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .Reaching("Spooler", Progress(EntryStatus.PausePending, 1), Progress(EntryStatus.Running, 0));

        var run = Ran(control, Asked(ActionKind.Pause));

        var result = Assert.Single(run.Results);
        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(0, result.ErrorCode);
        Assert.Equal(
            "It began to pause and went back to running, so the service turned the pause down.", result.Error);
        Assert.Empty(run.Reversal);
    }

    [Fact]
    public void Running_straight_after_the_request_is_a_pause_not_yet_begun_rather_than_one_turned_down()
    {
        // A service may take the control and report PausePending a moment later.
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .Reaching(
                "Spooler",
                Progress(EntryStatus.Running, 0),
                Progress(EntryStatus.Running, 0),
                Progress(EntryStatus.PausePending, 1),
                Progress(EntryStatus.Paused, 0));

        Assert.Equal(StepOutcome.Succeeded, Assert.Single(Ran(control, Asked(ActionKind.Pause)).Results).Outcome);
    }

    [Fact]
    public void A_pause_that_ends_stopped_fails_saying_it_stopped_rather_than_completed()
    {
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .Reaching("Spooler", Progress(EntryStatus.PausePending, 1), Progress(EntryStatus.Stopped, 0));

        var result = Assert.Single(Ran(control, Asked(ActionKind.Pause)).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("It stopped instead of pausing.", result.Error);
    }

    [Fact]
    public void A_stop_meeting_an_entry_still_pausing_waits_for_the_pause_to_finish_and_then_stops_it()
    {
        // Measured 2026-11-25: a stop sent during PausePending was taken and lost. The double refuses one sent
        // while the entry is on its way, so only a stop sent AFTER the pause finished can succeed here.
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .OnItsWay("Spooler", TimeSpan.FromSeconds(3), Progress(EntryStatus.PausePending, 1));

        var run = Ran(control, Stopping(), clock);

        var result = Assert.Single(run.Results);
        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.Equal(EntryStatus.Stopped, result.Status);
        Assert.False(result.ArrivedUnasked);
        Assert.Equal("Spooler", Assert.Single(control.Requested));
    }

    [Fact]
    public void A_stop_meeting_a_pause_that_never_finishes_is_never_sent_and_says_so()
    {
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .OnItsWay("Spooler", TimeSpan.FromMinutes(10), Progress(EntryStatus.PausePending, 1));

        var result = Assert.Single(Ran(control, Stopping(), clock).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(
            "It was still pausing when the waiting ran out, so it was never asked to stop. Ask again once it has paused.",
            result.Error);
        Assert.Empty(control.Requested);
    }

    [Fact]
    public void A_pause_meeting_an_entry_already_pausing_only_watches_it_arrive_and_hands_nothing_back()
    {
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .OnItsWay("Spooler", TimeSpan.FromSeconds(2), Progress(EntryStatus.PausePending, 1));

        var run = Ran(control, Asked(ActionKind.Pause), clock);

        Assert.True(Assert.Single(run.Results).ArrivedUnasked);
        Assert.Empty(control.Requested);
        Assert.Empty(run.Reversal);
    }

    [Fact]
    public void A_resume_the_service_turns_down_is_a_failure_in_our_words()
    {
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Paused)
            .Reaching("Spooler", Progress(EntryStatus.ContinuePending, 1), Progress(EntryStatus.Paused, 0));

        var result = Assert.Single(Ran(control, Asked(ActionKind.Resume)).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("It began to resume and went back to being paused, so the service turned the resume down.", result.Error);
    }

    [Theory]
    [InlineData(StepOperation.Resume, EntryStatus.PausePending,
        "It was still pausing when the waiting ran out, so it was never asked to resume. Ask again once it has paused.")]
    [InlineData(StepOperation.Stop, EntryStatus.ContinuePending,
        "It was still resuming when the waiting ran out, so it was never asked to stop. Ask again once it has resumed.")]
    [InlineData(StepOperation.Pause, EntryStatus.StartPending,
        "It was still starting when the waiting ran out, so it was never asked to pause. Ask again once it has started.")]
    public void A_step_meeting_an_entry_heading_elsewhere_that_never_gets_there_is_never_sent(
        StepOperation operation, EntryStatus pending, string said)
    {
        var clock = new FakeClock();
        var control = new FakeScmControl(clock).OnItsWay("Spooler", TimeSpan.FromMinutes(10), Progress(pending, 1));

        var result = Assert.Single(Ran(control, Doing(operation), clock).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(said, result.Error);
        Assert.Empty(control.Requested);
    }

    [Theory]
    [InlineData(StepOperation.Pause, EntryStatus.PausePending,
        "It was already pausing when its turn came, and the waiting ran out before it paused. It was never asked to pause, so nothing here resumes it.")]
    [InlineData(StepOperation.Resume, EntryStatus.ContinuePending,
        "It was already resuming when its turn came, and the waiting ran out before it was running. It was never asked to resume, so nothing here pauses it again.")]
    public void A_step_that_only_watched_an_entry_that_never_arrived_says_nothing_was_asked(
        StepOperation operation, EntryStatus pending, string said)
    {
        var clock = new FakeClock();
        var control = new FakeScmControl(clock).OnItsWay("Spooler", TimeSpan.FromMinutes(10), Progress(pending, 1));

        var run = Ran(control, Doing(operation), clock);

        Assert.Equal(said, Assert.Single(run.Results).Error);
        Assert.Empty(control.Requested);
        Assert.Empty(run.Reversal);
    }

    private static FakeScmCatalog Pausable(EntryStatus status = EntryStatus.Running, bool accepts = true) =>
        Rebuild(Chain(), "LanmanWorkstation", entry => entry with
        {
            Status = status,
            AcceptsPause = status == EntryStatus.Stopped ? Reading<bool>.Absent() : Reading<bool>.Present(accepts)
        });

    private static ServiceProgress Progress(EntryStatus status, uint checkPoint) =>
        new(status, checkPoint, checkPoint == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(4), Held);

    private static OperationPlan Asked(ActionKind kind) => new()
    {
        Action = new ServiceAction(kind, "Spooler"),
        Steps = [new PlanStep("Spooler", "Spooler", kind == ActionKind.Pause ? StepOperation.Pause : StepOperation.Resume, StepReason.Requested)],
        Warnings = [],
        Problems = []
    };

    private static OperationPlan Stopping() => Doing(StepOperation.Stop);

    private static OperationPlan Doing(StepOperation operation) => new()
    {
        Action = new ServiceAction(
            operation switch
            {
                StepOperation.Pause => ActionKind.Pause,
                StepOperation.Resume => ActionKind.Resume,
                _ => ActionKind.Stop
            },
            "Spooler"),
        Steps = [new PlanStep("Spooler", "Spooler", operation, StepReason.Requested)],
        Warnings = [],
        Problems = []
    };

    private static PlanRun Ran(FakeScmControl control, OperationPlan plan, FakeClock? clock = null) =>
        new PlanRunner(control, clock ?? new FakeClock()).Run(plan, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
}
