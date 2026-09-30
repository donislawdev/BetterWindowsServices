using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// What the limit of a step counts, and what a step does with an entry already on its way somewhere.
///
/// <b>Written 2026-09-30 for the stability report's W-1 and W-7, on the owner's decision of
/// 2026-09-29.</b> The limit - <c>--timeout</c>, the window's "Wait up to" box - counts time WITHOUT
/// PROGRESS, so a service stopping honestly for longer than the limit is watched to its end. And a step
/// meeting an entry still moving waits for it instead of asking, which the manager would refuse. Every
/// test here was red on the code before that day. The restart one is the reason for the package: it
/// left the service stopped.
/// </summary>
public sealed class PlanRunProgressTests
{
    private const string Name = "Spooler";

    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private static readonly ServiceProgress Stopped = new(EntryStatus.Stopped, 0, TimeSpan.Zero, ProcessId: 0);

    [Fact]
    public void A_stop_that_keeps_making_progress_past_the_limit_is_watched_to_its_end()
    {
        // About a hundred seconds of a check point rising, under a limit of sixty. Until 2026-09-30 the
        // limit was a wall across the whole step and this was given up on at sixty.
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .At(Name, EntryStatus.Running)
            .Reaching(Name, [.. Rising(400), Stopped]);

        var result = Assert.Single(Run(control, clock, Stopping()).Results);

        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.True(clock.Waited > Limit, $"Waited only {clock.Waited}.");
    }

    [Fact]
    public void The_limit_is_counted_from_the_last_progress_rather_than_from_the_start_of_the_step()
    {
        // A hundred and twenty rises - the last at about 29 s on the runner's pace of looking - and then
        // nothing. Given up on a limit after the LAST rise, so at about 89 s, never at 60.
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .At(Name, EntryStatus.Running)
            .Reaching(Name, [.. Rising(120), Pending(120, TimeSpan.Zero)]);

        var result = Assert.Single(Run(control, clock, Stopping()).Results);

        Assert.Equal(StepOutcome.TimedOut, result.Outcome);
        Assert.InRange(clock.Waited, TimeSpan.FromSeconds(85), TimeSpan.FromSeconds(95));
    }

    [Fact]
    public void A_restart_whose_stop_ran_out_starts_the_entry_once_it_has_finished_stopping()
    {
        // THE REASON FOR THE PACKAGE. The stop is given up on at the limit - the entry promised nothing
        // and sat still - and the entry finishes stopping at ninety seconds, after the run has moved on
        // to putting it back. Until 2026-09-30 the start went straight to an entry still stopping, the
        // manager refused it, and the service stayed stopped.
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .At(Name, EntryStatus.Running)
            .Arriving(Name, TimeSpan.FromSeconds(90), Pending(1, TimeSpan.Zero));

        var run = Run(control, clock, Restarting());

        Assert.Equal(StepOutcome.TimedOut, run.Results[0].Outcome);
        Assert.Equal(StepOutcome.Succeeded, run.Results[1].Outcome);
        Assert.Equal(EntryStatus.Running, run.Results[1].Status);

        // Asked twice - the stop, then the start once it could be taken.
        Assert.Equal([Name, Name], control.Requested);
    }

    [Fact]
    public void A_stop_that_meets_an_entry_already_stopping_asks_nothing_and_waits_for_it()
    {
        // The manager refuses a stop to an entry already stopping, and until 2026-09-30 that refusal was
        // the step's result - a failure over an entry on its way to exactly where the step wanted it.
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .OnItsWay(Name, TimeSpan.FromSeconds(5), Pending(1, TimeSpan.FromSeconds(10)));

        var result = Assert.Single(Run(control, clock, Stopping()).Results);

        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.Empty(control.Requested);
    }

    [Fact]
    public void A_start_that_meets_an_entry_still_stopping_waits_for_it_to_stop_and_then_asks()
    {
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .OnItsWay(Name, TimeSpan.FromSeconds(5), Pending(1, TimeSpan.FromSeconds(10)));

        var result = Assert.Single(Run(control, clock, Starting()).Results);

        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.Equal([Name], control.Requested);
    }

    [Fact]
    public void An_entry_that_never_leaves_the_other_way_is_never_asked()
    {
        // Nothing was sent, so the way back must not count it as a move - a failure in our own words
        // with no number, not a timeout, which the way back reads as "may have moved".
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .OnItsWay(Name, TimeSpan.FromMinutes(10), Pending(1, TimeSpan.Zero));

        var result = Assert.Single(Run(control, clock, Starting()).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(0, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(EntryStatus.StopPending, result.Status);
        Assert.Empty(control.Requested);
    }

    [Fact]
    public void The_time_the_manager_sits_on_a_request_is_what_outruns_the_limit()
    {
        // Half a minute inside the start call, as measured on Windows Server 2025, under a limit of one
        // second. That is named - and the watching after it would not have been.
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .At(Name, EntryStatus.Stopped)
            .SlowToAnswer(Name, TimeSpan.FromSeconds(30));

        var run = new PlanRunner(control, clock).Run(Starting(), TimeSpan.FromSeconds(1));

        Assert.Equal(StepOutcome.Succeeded, run.Results[0].Outcome);
        Assert.Equal(30_000, Assert.Single(run.OutranTheCeiling).Answered);
    }

    internal static OperationPlan Stopping() =>
        Planned(ActionKind.Stop, Step(StepOperation.Stop, StepReason.Requested));

    internal static OperationPlan Starting() =>
        Planned(ActionKind.Start, Step(StepOperation.Start, StepReason.Requested));

    internal static OperationPlan Restarting() => Planned(
        ActionKind.Restart,
        Step(StepOperation.Stop, StepReason.Requested),
        Step(StepOperation.Start, StepReason.Restore));

    /// <summary>An entry stopping, with its check point where it is and a wait hint of its own.</summary>
    internal static ServiceProgress Pending(uint checkPoint, TimeSpan hint) =>
        new(EntryStatus.StopPending, checkPoint, hint, ProcessId: FakeScmControl.FakeProcess);

    private static PlanRun Run(FakeScmControl control, FakeClock clock, OperationPlan plan) =>
        new PlanRunner(control, clock).Run(plan, Limit);

    /// <summary>
    /// A check point rising once per look and no wait hint - so nothing but the limit can end the watch,
    /// and every look is progress.
    /// </summary>
    private static IEnumerable<ServiceProgress> Rising(int count) =>
        Enumerable.Range(1, count).Select(point => Pending((uint)point, TimeSpan.Zero));

    private static OperationPlan Planned(ActionKind kind, params PlanStep[] steps) => new()
    {
        Action = new ServiceAction(kind, Name),
        Steps = steps,
        Warnings = [],
        Problems = []
    };

    private static PlanStep Step(StepOperation operation, StepReason reason) => new(Name, Name, operation, reason);
}
