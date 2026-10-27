using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// What the two ways of stopping a run do to a step that is WAITING.
///
/// <b>Written 2026-09-30, stability report W-11, on the owner's decision of 2026-09-29.</b> Until that
/// day both were checked only between steps, so the terminal's second Ctrl+C - "stop altogether" -
/// waited out the step in flight, which from that day can be as long as the entry keeps moving. The
/// rule now: before anything is asked, a waiting step stops exactly as a step not yet reached would,
/// and after the manager was asked only abandonment ends the watching.
/// </summary>
public sealed class PlanRunHaltTests
{
    private const string Name = "Spooler";

    [Fact]
    public void Abandoning_during_the_watch_ends_it_rather_than_waiting_out_the_limit()
    {
        using var abandonment = new CancellationTokenSource();
        var clock = new StoppingClock(TimeSpan.FromSeconds(2), abandonment);
        var control = new FakeScmControl(clock)
            .At(Name, EntryStatus.Running)
            .Reaching(Name, PlanRunProgressTests.Pending(0, TimeSpan.Zero));

        var run = new PlanRunner(control, clock)
            .Run(PlanRunProgressTests.Stopping(), TimeSpan.FromSeconds(60), abandonment: abandonment.Token, cancellation: TestContext.Current.CancellationToken);

        // The watching ended, the entry may still arrive - which is what the outcome says, on the
        // owner's decision of 2026-09-30, rather than a new value of it.
        Assert.Equal(StepOutcome.TimedOut, Assert.Single(run.Results).Outcome);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"Watched for {clock.Elapsed}.");
        Assert.True(run.Cancelled);
    }

    [Fact]
    public void Abandoning_before_the_request_asks_nothing()
    {
        using var abandonment = new CancellationTokenSource();
        var clock = new StoppingClock(TimeSpan.FromSeconds(2), abandonment);
        var control = new FakeScmControl(clock)
            .OnItsWay(Name, TimeSpan.FromSeconds(90), PlanRunProgressTests.Pending(1, TimeSpan.Zero));

        var run = new PlanRunner(control, clock)
            .Run(PlanRunProgressTests.Starting(), TimeSpan.FromSeconds(60), abandonment: abandonment.Token, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(SkipReason.Cancelled, Assert.Single(run.Results).SkippedBecause);
        Assert.Empty(control.Requested);
    }

    [Fact]
    public void Abandoning_while_an_entry_already_on_its_way_is_watched_reports_a_step_never_attempted()
    {
        // Nothing was asked of an entry that was stopping by itself, so the step was never attempted -
        // a timeout here would have the way back count a move this tool never made.
        using var abandonment = new CancellationTokenSource();
        var clock = new StoppingClock(TimeSpan.FromSeconds(2), abandonment);
        var control = new FakeScmControl(clock)
            .OnItsWay(Name, TimeSpan.FromSeconds(90), PlanRunProgressTests.Pending(1, TimeSpan.Zero));

        var run = new PlanRunner(control, clock)
            .Run(PlanRunProgressTests.Stopping(), TimeSpan.FromSeconds(60), abandonment: abandonment.Token, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(SkipReason.Cancelled, Assert.Single(run.Results).SkippedBecause);
        Assert.Empty(control.Requested);
    }

    [Fact]
    public void An_interruption_skips_a_step_going_forward_that_is_still_waiting_to_be_asked()
    {
        using var interruption = new CancellationTokenSource();
        var clock = new StoppingClock(TimeSpan.FromSeconds(2), interruption);
        var control = new FakeScmControl(clock)
            .OnItsWay(Name, TimeSpan.FromSeconds(90), PlanRunProgressTests.Pending(1, TimeSpan.Zero));

        var run = new PlanRunner(control, clock)
            .Run(PlanRunProgressTests.Starting(), TimeSpan.FromSeconds(60), interruption.Token);

        Assert.Equal(SkipReason.Cancelled, Assert.Single(run.Results).SkippedBecause);
        Assert.Empty(control.Requested);
    }

    [Fact]
    public void An_interruption_does_not_stop_a_step_putting_something_back_from_waiting_to_be_asked()
    {
        // The first level promises the steps that give back what earlier ones took, and a start waiting
        // for the entry to finish stopping is one of them - the restart of the package's own reason,
        // interrupted while the entry was still stopping.
        using var interruption = new CancellationTokenSource();
        var clock = new StoppingClock(TimeSpan.FromSeconds(70), interruption);
        var control = new FakeScmControl(clock)
            .At(Name, EntryStatus.Running)
            .Arriving(Name, TimeSpan.FromSeconds(90), PlanRunProgressTests.Pending(1, TimeSpan.Zero));

        var run = new PlanRunner(control, clock)
            .Run(PlanRunProgressTests.Restarting(), TimeSpan.FromSeconds(60), interruption.Token);

        Assert.Equal(StepOutcome.Succeeded, run.Results[1].Outcome);
        Assert.True(run.Cancelled);
    }

    [Fact]
    public void Stop_pressed_while_a_step_gets_ready_sends_nothing_and_a_restart_then_puts_nothing_back()
    {
        // Logic report R09, 2026-10-26. The loop asks between steps, and a step then announces itself and
        // reads the entry - Stop pressed in between used to let the request go after the reading. Nothing
        // of the step had changed the machine, so it is held exactly as a step not yet reached.
        using var interruption = new CancellationTokenSource();
        var control = new FakeScmControl().At(Name, EntryStatus.Running);

        var run = new PlanRunner(control, new FakeClock()).Run(
            PlanRunProgressTests.Restarting(),
            TimeSpan.FromSeconds(60),
            interruption.Token,
            starting: (_, _) => interruption.Cancel());

        Assert.Equal(SkipReason.Cancelled, run.Results[0].SkippedBecause);
        Assert.Equal(SkipReason.NothingToPutBack, run.Results[1].SkippedBecause);
        Assert.Empty(control.Requested);
        Assert.True(run.Cancelled);
    }

    [Fact]
    public void Stop_pressed_while_an_ending_reads_the_process_ends_nothing()
    {
        // The widest of the gaps R09 named: before a process dies, the step reads the whole listing and the
        // dependants of everything in it, and Stop pressed during that used to be answered by the ending.
        using var interruption = new CancellationTokenSource();
        var control = new FakeScmControl();

        var run = new PlanRunner(new Interrupting(control, interruption, whileReadingStatuses: true), new FakeClock())
            .Run(Ending(), TimeSpan.FromSeconds(60), interruption.Token);

        Assert.Equal(SkipReason.Cancelled, Assert.Single(run.Results).SkippedBecause);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void Stop_pressed_just_before_a_setting_is_written_writes_nothing()
    {
        using var interruption = new CancellationTokenSource();
        var control = new FakeScmControl().At(Name, EntryStatus.Running);

        var run = new PlanRunner(control, new FakeClock()).Run(
            Setting(),
            TimeSpan.FromSeconds(60),
            interruption.Token,
            starting: (_, _) => interruption.Cancel());

        Assert.Equal(SkipReason.Cancelled, Assert.Single(run.Results).SkippedBecause);
        Assert.Empty(control.Configured);
    }

    [Fact]
    public void Abandoning_just_before_a_step_putting_something_back_starts_nothing()
    {
        // The first level promises the steps that give back what earlier ones took - the second promises
        // nothing, and is now heard after the reading too, not only between steps.
        using var abandonment = new CancellationTokenSource();
        var control = new FakeScmControl().At(Name, EntryStatus.Running);

        var run = new PlanRunner(control, new FakeClock()).Run(
            PlanRunProgressTests.Restarting(),
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken,
            abandonment.Token,
            starting: (_, number) =>
            {
                if (number == 2)
                {
                    abandonment.Cancel();
                }
            });

        Assert.Equal(StepOutcome.Succeeded, run.Results[0].Outcome);
        Assert.Equal(SkipReason.Cancelled, run.Results[1].SkippedBecause);
        Assert.Equal([Name], control.Requested);
    }

    private static OperationPlan Ending() => new()
    {
        Action = new ServiceAction(ActionKind.ForceStop, Name, Immediate: true),
        Steps = [new PlanStep(Name, Name, StepOperation.Terminate, StepReason.Requested, ProcessId: (int)FakeScmControl.FakeProcess)],
        Warnings = [],
        Problems = []
    };

    private static OperationPlan Setting() => new()
    {
        Action = new ServiceAction(ActionKind.SetStartType, Name, To: StartSetting.Disabled),
        Steps = [new PlanStep(Name, Name, StepOperation.SetStartType, StepReason.Requested, To: StartSetting.Disabled)],
        Warnings = [],
        Problems = []
    };

    /// <summary>
    /// Time that only moves when somebody waits, and that pulls one of the run's two levers once it has
    /// moved far enough - the only way to reach a runner in the middle of a step without a second thread.
    /// </summary>
    private sealed class StoppingClock(TimeSpan at, CancellationTokenSource lever) : IClock
    {
        private readonly FakeClock _time = new();

        public DateTimeOffset Now => _time.Now;

        public TimeSpan Elapsed => _time.Elapsed;

        public void Wait(TimeSpan duration)
        {
            _time.Wait(duration);

            if (_time.Elapsed >= at)
            {
                lever.Cancel();
            }
        }
    }
}
