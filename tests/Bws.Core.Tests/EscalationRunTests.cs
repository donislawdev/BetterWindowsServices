using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The half of a forced stop that happens while it happens.
///
/// <b>Three properties, and each of them is a decision that could have gone the other way.</b> A
/// step that ends a process runs after an earlier one failed to arrive, which is the opposite of
/// how every other forward step behaves. It does NOT run after somebody asked to stop, which is the
/// opposite of how a restoring step behaves. And it refuses itself when the process it was told to
/// end is no longer the one the plan named.
///
/// <b>Driven through a plan rather than a step</b>, because the runner takes the steps as they were
/// shown and the interesting question is what it does with the ones it was given.
/// </summary>
public sealed class EscalationRunTests
{
    private const int Held = 4812;

    [Fact]
    public void The_process_is_ended_when_the_stop_before_it_ran_out_of_time()
    {
        // THE WHOLE FEATURE IN ONE ASSERTION. The ordinary rule is that nothing goes forward once a
        // step has failed to arrive - and under that rule the only step that was ever going to help
        // would be skipped, on the exact plan built for this situation.
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .RunningIn("Spooler", Held)
            .Reaching("Spooler", new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, Held));

        var run = new PlanRunner(control, new FakeClock()).Run(Forced(), TimeSpan.FromSeconds(30));

        Assert.Equal(StepOutcome.TimedOut, run.Results[0].Outcome);
        Assert.Equal(StepOutcome.Succeeded, run.Results[1].Outcome);
        Assert.Equal(Held, Assert.Single(control.Ended));
    }

    [Fact]
    public void Nothing_is_ended_when_the_stop_before_it_worked()
    {
        // What keeps this safe is not the reason on the step but the reading before it: a step whose
        // entry has already reached the state it wanted is skipped, so an escalation standing behind
        // a stop that worked does nothing at all.
        var control = new FakeScmControl().At("Spooler", EntryStatus.Running).RunningIn("Spooler", Held);

        var run = new PlanRunner(control, new FakeClock()).Run(Forced(), TimeSpan.FromSeconds(30));

        Assert.Equal(StepOutcome.Succeeded, run.Results[0].Outcome);
        Assert.Equal(SkipReason.AlreadyThere, run.Results[1].SkippedBecause);
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void Nothing_is_ended_after_somebody_asks_to_stop()
    {
        // The one case that separates this from a restoring step, which runs whatever happened. An
        // interruption is somebody withdrawing the instruction, and ending a process afterwards
        // would be acting on it anyway.
        using var interrupted = new CancellationTokenSource();
        interrupted.Cancel();

        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .RunningIn("Spooler", Held);

        var run = new PlanRunner(control, new FakeClock())
            .Run(Forced(), TimeSpan.FromSeconds(30), interrupted.Token);

        Assert.All(run.Results, result => Assert.Equal(SkipReason.Cancelled, result.SkippedBecause));
        Assert.Empty(control.Ended);
    }

    [Fact]
    public void A_process_that_is_no_longer_the_one_the_plan_named_is_left_alone()
    {
        // Windows hands out process numbers again once a process is gone, and a plan is built one
        // moment and carried out the next. Refusing on a mismatch delivers exactly the plan that was
        // shown or nothing at all - ending a DIFFERENT process would be the runner inventing a step.
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .RunningIn("Spooler", 9999)
            .Reaching("Spooler", new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, 9999));

        var run = new PlanRunner(control, new FakeClock()).Run(Forced(), TimeSpan.FromSeconds(30));

        Assert.Equal(StepOutcome.Failed, run.Results[1].Outcome);
        Assert.Empty(control.Ended);
    }

    /// <summary>
    /// A run whose escalation arrived is a run that worked - the entry is where every step wanted it.
    ///
    /// <b>Found by reading on 2026-09-16 and measured on the throwaway machine before a line changed:
    /// exit code 3 and "the entry is not where you asked" over an entry standing in Stopped.</b>
    /// <c>Completed</c> counted STEPS, and the polite stop in front of the kill had timed out - which
    /// is the exact situation the plan carries the kill for. Until that day the product had only
    /// ever met the one step shape, where the entry was already in StopPending and the polite step
    /// was skipped, so the wrong answer had never been on any screen.
    ///
    /// <b>The polite step still reads as timed out</b>, because it did, and the sentence about it is
    /// what tells somebody why the process was ended. What changes is the verdict over the run.
    /// </summary>
    [Fact]
    public void A_run_whose_escalation_arrived_is_a_run_that_worked()
    {
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .RunningIn("Spooler", Held)
            .Reaching("Spooler", new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, Held));

        var run = new PlanRunner(control, new FakeClock()).Run(Forced(), TimeSpan.FromSeconds(30));

        Assert.Equal(StepOutcome.TimedOut, run.Results[0].Outcome);
        Assert.Equal(StepOutcome.Succeeded, run.Results[1].Outcome);

        Assert.True(
            run.Completed,
            "The process was ended and the entry reached Stopped, which is where both steps wanted it "
            + "- a run counted incomplete here is the verdict reading the polite step and not the entry.");
    }

    [Fact]
    public void A_process_that_survives_being_ended_is_reported_as_a_step_that_ran_out_of_time()
    {
        // Win32 documents the call as asynchronous and says a process with pending driver work
        // cannot exit until that work finishes. So a successful call and an entry that never reaches
        // Stopped is a real pair - and calling it a success because the call returned would be the
        // tool reporting on itself rather than on the machine.
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .RunningIn("Spooler", Held)
            .SurvivingTermination(Held)
            .Reaching("Spooler", new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, Held));

        var run = new PlanRunner(control, new FakeClock()).Run(Forced(), TimeSpan.FromSeconds(30));

        Assert.Equal(Held, Assert.Single(control.Ended));
        Assert.Equal(StepOutcome.TimedOut, run.Results[1].Outcome);

        // The even claim beside the test above: with the last step for the entry not arrived, the
        // verdict is what it always was. Counting by entry is not counting more generously.
        Assert.False(run.Completed);
    }

    /// <summary>
    /// The two step shape a forced stop takes: ask, then end what is left.
    ///
    /// Written here rather than built through PlanBuilder, because these tests are about what the
    /// RUNNER does with steps it was handed. The builder has tests of its own for the shape.
    /// </summary>
    private static OperationPlan Forced() => new()
    {
        Action = new ServiceAction(ActionKind.ForceStop, "Spooler"),
        Steps =
        [
            new PlanStep("Spooler", "Print Spooler", StepOperation.Stop, StepReason.Requested),
            new PlanStep(
                "Spooler", "Print Spooler", StepOperation.Terminate, StepReason.Escalation, ProcessId: Held)
        ],
        Warnings = [],
        Problems = []
    };
}
