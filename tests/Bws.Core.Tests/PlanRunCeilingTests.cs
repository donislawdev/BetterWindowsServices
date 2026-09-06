using Bws.Core.Planning;

namespace Bws.Core.Tests;

/// <summary>
/// Which steps ran past the ceiling they were given, and why that is worth saying out loud.
///
/// <b>Written 2026-08-04, after Windows Server 2025 showed that the ceiling is not what it
/// looks like.</b> <c>bws start X --timeout 1</c> against a service that never reports itself
/// to the manager took <b>30 375-30 450 ms across three runs</b> and then reported the truth -
/// refused, error 1053, the entry left stopped. Correct in every particular, and to whoever
/// typed it, indistinguishable from a switch that does nothing.
///
/// The reason is not a defect that can be removed. <c>StartService</c> is the only way in and
/// it returns when the service process connects to the manager or dies, so the manager's own
/// answer takes as long as it takes. There is no asynchronous start in that API. What can be
/// fixed is the silence, and that is what this decides: which steps get a sentence saying the
/// time went somewhere the ceiling does not reach.
///
/// These build a <see cref="PlanRun"/> by hand rather than running one. The judgement is
/// arithmetic over results, and getting a real step to outrun a real ceiling needs a service
/// that cannot start - which is not something to install on somebody's machine to run a test.
/// </summary>
public sealed class PlanRunCeilingTests
{
    [Fact]
    public void A_step_that_ran_past_the_ceiling_is_named()
    {
        // The measured case, in the numbers it actually had.
        var run = RunWith(Ceiling(1), Step(StepOutcome.Failed, milliseconds: 30_450));

        var outran = Assert.Single(run.OutranTheCeiling);
        Assert.Equal(30_450, outran.Milliseconds);
    }

    [Fact]
    public void A_step_that_gave_up_at_the_ceiling_is_not_named()
    {
        // This is the whole reason the test has no threshold in it. A step that timed out
        // reached the ceiling BECAUSE the ceiling worked, and its own line already reads
        // "gave up after 1 s, still StartPending". Naming it here as well would turn the
        // sentence into noise on exactly the runs where the switch did its job.
        var run = RunWith(Ceiling(1), Step(StepOutcome.TimedOut, milliseconds: 1_020));

        Assert.Empty(run.OutranTheCeiling);
    }

    [Fact]
    public void A_step_that_finished_inside_the_ceiling_is_not_named()
    {
        var run = RunWith(Ceiling(60), Step(StepOutcome.Succeeded, milliseconds: 1_300));

        Assert.Empty(run.OutranTheCeiling);
    }

    [Fact]
    public void A_step_that_succeeded_late_is_named_too()
    {
        // Not only refusals. If the manager held a stop past the ceiling and it then worked,
        // the command still outran what was asked for, and the person waiting deserves the
        // same sentence. Built rather than captured - no machine has shown this yet.
        var run = RunWith(Ceiling(1), Step(StepOutcome.Succeeded, milliseconds: 4_000));

        Assert.Single(run.OutranTheCeiling);
    }

    [Fact]
    public void Every_step_that_outran_it_is_named_rather_than_the_first()
    {
        // A cascade can hold several of these, and a report naming one of four would send
        // somebody looking for a single slow entry that is not the whole story.
        var run = RunWith(
            Ceiling(1),
            Step(StepOutcome.Failed, milliseconds: 30_450),
            Step(StepOutcome.Succeeded, milliseconds: 200),
            Step(StepOutcome.Failed, milliseconds: 30_100));

        Assert.Equal(2, run.OutranTheCeiling.Count);
    }

    private static TimeSpan Ceiling(int seconds) => TimeSpan.FromSeconds(seconds);

    private static StepResult Step(StepOutcome outcome, long milliseconds) => new()
    {
        Step = new PlanStep("Any", "Any", StepOperation.Start, StepReason.Requested),
        Outcome = outcome,
        SkippedBecause = null,
        Status = EntryStatus.Stopped,
        ProcessId = Reading<int>.Absent(),
        ErrorCode = outcome == StepOutcome.Failed ? 1053 : 0,
        Error = outcome == StepOutcome.Failed ? "The service did not respond in a timely fashion." : null,
        Milliseconds = milliseconds
    };

    private static PlanRun RunWith(TimeSpan ceiling, params StepResult[] results) => new()
    {
        Plan = new OperationPlan
        {
            Action = new ServiceAction(ActionKind.Start, "Any"),
            Steps = [.. results.Select(result => result.Step)],
            Warnings = [],
            Problems = []
        },
        Results = results,
        Cancelled = false,
        Ceiling = ceiling
    };
}
