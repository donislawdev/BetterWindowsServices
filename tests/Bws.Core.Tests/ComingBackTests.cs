using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Who Windows starts again after a run that ended a process, and when - backlog 501, the owner's decision
/// of 2026-09-30.
///
/// <b>Measured the day before, on the throwaway machine:</b> a forced stop with a restart after 3000 ms
/// reported the step succeeded, the run complete and exit code 0, and three seconds later the service was
/// running. These pin who the line after such a run names. The rule underneath is Microsoft's own - a
/// service fails when its process ends without it reporting Stopped - so what each test varies is whether
/// an entry reported Stopped before the ending, and whether the run started it again afterwards.
/// </summary>
public sealed class ComingBackTests
{
    private const int Held = 4812;

    private static readonly RecoveryRestart SpoolerBack =
        new("Spooler", [TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120)]);

    private static readonly RecoveryRestart HousemateBack = new("Housemate", [TimeSpan.Zero]);

    [Fact]
    public void A_forced_stop_names_everybody_who_died_with_the_process_and_when()
    {
        var control = new FakeScmControl().At("Spooler", EntryStatus.Running).At("Housemate", EntryStatus.Running);

        var run = Run(control, Forced(ActionKind.ForceStop, Ending() with { Reason = StepReason.Requested }));

        Assert.Equal(Held, Assert.Single(control.Ended));
        Assert.Equal([SpoolerBack, HousemateBack], run.ComingBack);
    }

    [Fact]
    public void A_neighbour_whose_own_stop_arrived_sets_nothing_off()
    {
        var control = new FakeScmControl()
            .Reaching("Spooler", Stopping())
            .At("Housemate", EntryStatus.Running);

        var run = Run(control, Forced(
            ActionKind.ForceStop,
            Step("Spooler", StepOperation.Stop, StepReason.Requested),
            Step("Housemate", StepOperation.Stop, StepReason.SharesTheProcess),
            Ending()));

        Assert.Equal(StepOutcome.Succeeded, run.Results[1].Outcome);
        Assert.Equal(["Spooler"], Names(run));
    }

    [Fact]
    public void A_neighbour_whose_own_stop_timed_out_dies_with_the_process()
    {
        // Still in StopPending when the process went, so it never reported Stopped - the one case where the
        // way back and this line disagree, and why this is not NetEffect's count of who the ending took.
        var control = new FakeScmControl()
            .Reaching("Spooler", Stopping())
            .Reaching("Housemate", Stopping());

        var run = Run(control, Forced(
            ActionKind.ForceStop,
            Step("Spooler", StepOperation.Stop, StepReason.Requested),
            Step("Housemate", StepOperation.Stop, StepReason.SharesTheProcess),
            Ending()));

        Assert.Equal(StepOutcome.TimedOut, run.Results[1].Outcome);
        Assert.Equal(["Spooler", "Housemate"], Names(run));
    }

    [Fact]
    public void An_entry_Windows_started_again_at_once_is_left_to_its_own_line()
    {
        var control = new FakeScmControl()
            .ComingBack("Spooler", new ServiceProgress(EntryStatus.Running, 0, TimeSpan.Zero, 5555))
            .At("Housemate", EntryStatus.Running);

        var run = Run(control, Forced(ActionKind.ForceStop, Ending() with { Reason = StepReason.Requested }));

        Assert.True(run.Results[0].StartedAgain);
        Assert.Equal(["Housemate"], Names(run));
    }

    [Fact]
    public void A_forced_restart_that_put_everything_back_announces_nothing()
    {
        var control = new FakeScmControl().At("Spooler", EntryStatus.Running).At("Housemate", EntryStatus.Running);

        var run = Run(control, Forced(
            ActionKind.ForceRestart,
            Ending() with { Reason = StepReason.Requested },
            Step("Spooler", StepOperation.Start, StepReason.Restore),
            Step("Housemate", StepOperation.Start, StepReason.Restore)));

        Assert.True(run.Completed);
        Assert.Empty(run.ComingBack);
    }

    [Fact]
    public void An_entry_whose_start_back_was_refused_is_left_to_its_recovery_list()
    {
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .RefusingRequests("Spooler", 1058)
            .At("Housemate", EntryStatus.Running);

        var run = Run(control, Forced(
            ActionKind.ForceRestart,
            Ending() with { Reason = StepReason.Requested },
            Step("Spooler", StepOperation.Start, StepReason.Restore),
            Step("Housemate", StepOperation.Start, StepReason.Restore)));

        Assert.Equal(StepOutcome.Failed, run.Results[1].Outcome);
        Assert.Equal(["Spooler"], Names(run));
    }

    [Fact]
    public void A_run_left_undone_after_the_ending_names_what_it_did_not_start()
    {
        // Leave the rest undone, pressed while the process was being ended: the steps putting things back are
        // never tried, so both entries are left to their lists - the case this line matters most in.
        var plan = Forced(
            ActionKind.ForceRestart,
            Ending() with { Reason = StepReason.Requested },
            Step("Spooler", StepOperation.Start, StepReason.Restore));

        var run = Carried(plan, [Came(plan.Steps[0], StepOutcome.Succeeded), Came(plan.Steps[1], StepOutcome.Skipped)]);

        Assert.Equal(["Spooler", "Housemate"], Names(run));
    }

    [Fact]
    public void Nothing_comes_back_when_the_process_was_never_ended()
    {
        using var interruption = new CancellationTokenSource();
        interruption.Cancel();

        var control = new FakeScmControl().At("Spooler", EntryStatus.Running).At("Housemate", EntryStatus.Running);

        var run = new PlanRunner(control, new FakeClock()).Run(
            Forced(ActionKind.ForceStop, Ending() with { Reason = StepReason.Requested }),
            TimeSpan.FromSeconds(30),
            interruption.Token);

        Assert.Empty(control.Ended);
        Assert.Empty(run.ComingBack);
    }

    [Fact]
    public void A_plan_that_warned_about_no_restart_announces_none()
    {
        var control = new FakeScmControl().At("Spooler", EntryStatus.Running).At("Housemate", EntryStatus.Running);

        var run = Run(control, Forced(ActionKind.ForceStop, Ending() with { Reason = StepReason.Requested }) with
        {
            Warnings = []
        });

        Assert.Equal(Held, Assert.Single(control.Ended));
        Assert.Empty(run.ComingBack);
    }

    private static ServiceProgress Stopping() => new(EntryStatus.StopPending, 0, TimeSpan.Zero, Held);

    private static PlanStep Step(string name, StepOperation operation, StepReason reason) =>
        new(name, name, operation, reason);

    private static PlanStep Ending() =>
        new("Spooler", "Spooler", StepOperation.Terminate, StepReason.Escalation, ProcessId: Held, TakesWithIt: ["Housemate"]);

    private static OperationPlan Forced(ActionKind kind, params PlanStep[] steps) => new()
    {
        Action = new ServiceAction(kind, "Spooler"),
        Steps = steps,
        Warnings =
        [
            new PlanWarning(PlanWarningKind.RecoveryRestarts, "Spooler", ["Spooler", "Housemate"])
            {
                Restarts = [SpoolerBack, HousemateBack]
            }
        ],
        Problems = []
    };

    private static PlanRun Run(FakeScmControl control, OperationPlan plan) =>
        new PlanRunner(control, new FakeClock()).Run(plan, TimeSpan.FromSeconds(30));

    private static PlanRun Carried(OperationPlan plan, IReadOnlyList<StepResult> results) => new()
    {
        Plan = plan,
        Results = results,
        Cancelled = true,
        Ceiling = TimeSpan.FromSeconds(30)
    };

    private static StepResult Came(PlanStep step, StepOutcome outcome) => new()
    {
        Step = step,
        Outcome = outcome,
        SkippedBecause = outcome == StepOutcome.Skipped ? SkipReason.Cancelled : null,
        Status = EntryStatus.Stopped,
        ProcessId = Reading<int>.NotRead(),
        ErrorCode = 0,
        Error = null,
        Milliseconds = 0
    };

    private static IEnumerable<string> Names(PlanRun run) => run.ComingBack.Select(one => one.ServiceName);
}
