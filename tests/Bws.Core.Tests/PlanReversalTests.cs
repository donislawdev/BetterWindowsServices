using Bws.Core.Planning;

namespace Bws.Core.Tests;

/// <summary>
/// What it would take to put the machine back, after a run has moved it.
///
/// <b>The cheapest honest form of the promise `ADR-11` makes about a plan being reversible</b> -
/// backlog 58, where it sat as the weakest of Nielsen's heuristics in this tool: until now the
/// only way out of a write in progress was Ctrl+C. Nothing here undoes anything. It says what the
/// commands would be.
///
/// <b>The case that decides the shape of the whole thing is the restart.</b> Turning each step
/// around one at a time is the obvious implementation, and on the commonest write this tool
/// performs it tells somebody who restarted a service to stop it - because a restart is a stop and
/// a start, and the entry ends exactly where it began. So the question asked of each entry is not
/// "what were its steps" but "where was it before, and where is it now".
///
/// These build a <see cref="PlanRun"/> by hand, like the ceiling tests next door and for the same
/// reason: the judgement is arithmetic over results, and arranging a real interrupted cascade on a
/// real machine to assert one sentence is not a trade worth making.
/// </summary>
public sealed class PlanReversalTests
{
    [Fact]
    public void A_stop_that_worked_says_how_to_start_it_back()
    {
        var run = RunOf(Moved("Spooler", StepOperation.Stop));

        var back = Assert.Single(run.Reversal);
        Assert.Equal("Spooler", back.ServiceName);
        Assert.Equal(StepOperation.Start, back.Operation);
    }

    [Fact]
    public void A_start_that_worked_says_how_to_stop_it_back()
    {
        var run = RunOf(Moved("Spooler", StepOperation.Start));

        var back = Assert.Single(run.Reversal);
        Assert.Equal(StepOperation.Stop, back.Operation);
    }

    [Fact]
    public void A_restart_that_ended_where_it_began_says_nothing()
    {
        // THE ONE THAT DECIDES THE IMPLEMENTATION. Reversing the two steps of a restart hands
        // back "bws stop Spooler", which takes down a service somebody had just asked to keep
        // running - dangerous advice on the commonest write in this tool, printed in the calmest
        // possible voice.
        var run = RunOf(
            Moved("Spooler", StepOperation.Stop),
            Moved("Spooler", StepOperation.Start));

        Assert.Empty(run.Reversal);
    }

    [Fact]
    public void A_restart_interrupted_after_the_stop_says_how_to_start_it()
    {
        // The other half of the same case, and the one where this is worth the most: the entry is
        // down, nobody asked for it to be down, and the run has already ended.
        var run = RunOf(
            Moved("Spooler", StepOperation.Stop),
            Skipped("Spooler", StepOperation.Start, SkipReason.Cancelled));

        var back = Assert.Single(run.Reversal);
        Assert.Equal(StepOperation.Start, back.Operation);
    }

    [Fact]
    public void A_cascade_hands_the_commands_back_in_the_reverse_order()
    {
        // A stop cascade takes the dependants down first and the entry they depend on last, so
        // putting it back starts that entry first. Handing these back in the order they happened
        // would hand back a sequence whose first line the manager refuses.
        var run = RunOf(
            Moved("DependantOne", StepOperation.Stop),
            Moved("DependantTwo", StepOperation.Stop),
            Moved("TheTarget", StepOperation.Stop));

        Assert.Equal(
            ["TheTarget", "DependantTwo", "DependantOne"],
            run.Reversal.Select(step => step.ServiceName));
    }

    [Fact]
    public void A_step_the_manager_refused_moved_nothing_so_nothing_is_offered()
    {
        var run = RunOf(Refused("Spooler", StepOperation.Stop));

        Assert.Empty(run.Reversal);
    }

    [Fact]
    public void An_entry_that_was_already_there_moved_nothing_so_nothing_is_offered()
    {
        // Running the same plan twice must not start offering a way back out of a run that did
        // nothing at all.
        var run = RunOf(Skipped("Spooler", StepOperation.Stop, SkipReason.AlreadyThere));

        Assert.Empty(run.Reversal);
    }

    [Fact]
    public void A_step_that_timed_out_counts_as_having_moved_the_entry()
    {
        // Same reasoning as the outcome itself: the manager accepted the request and the entry may
        // well have arrived after we stopped watching. Staying silent here would be a claim about
        // something nobody saw, and it would stay silent about an entry somebody was left holding.
        var run = RunOf(TimedOut("Spooler", StepOperation.Stop));

        var back = Assert.Single(run.Reversal);
        Assert.Equal(StepOperation.Start, back.Operation);
    }

    [Fact]
    public void An_entry_moved_twice_is_named_once()
    {
        // Two lines about one service, one of them wrong, is worse than no lines at all.
        var run = RunOf(
            Moved("Spooler", StepOperation.Stop),
            Moved("Other", StepOperation.Stop),
            Moved("Spooler", StepOperation.Stop));

        Assert.Equal(2, run.Reversal.Count);
        Assert.Single(run.Reversal, step => step.ServiceName == "Spooler");
    }

    private static StepResult Moved(string name, StepOperation operation) =>
        Result(name, operation, StepOutcome.Succeeded, skipped: null);

    private static StepResult Refused(string name, StepOperation operation) =>
        Result(name, operation, StepOutcome.Failed, skipped: null);

    private static StepResult TimedOut(string name, StepOperation operation) =>
        Result(name, operation, StepOutcome.TimedOut, skipped: null);

    private static StepResult Skipped(string name, StepOperation operation, SkipReason why) =>
        Result(name, operation, StepOutcome.Skipped, why);

    private static StepResult Result(
        string name,
        StepOperation operation,
        StepOutcome outcome,
        SkipReason? skipped) => new()
        {
            Step = new PlanStep(name, name, operation, StepReason.Requested),
            Outcome = outcome,
            SkippedBecause = skipped,
            Status = operation == StepOperation.Stop ? EntryStatus.Stopped : EntryStatus.Running,
            ProcessId = operation == StepOperation.Stop
                ? Reading<int>.Absent()
                : Reading<int>.Present(4812),
            ErrorCode = outcome == StepOutcome.Failed ? 5 : 0,
            Error = outcome == StepOutcome.Failed ? "Access is denied." : null,
            Milliseconds = 10
        };

    private static PlanRun RunOf(params StepResult[] results) => new()
    {
        Plan = new OperationPlan
        {
            Action = new ServiceAction(ActionKind.Stop, results[0].Step.ServiceName),
            Steps = [.. results.Select(result => result.Step)],
            Warnings = [],
            Problems = []
        },
        Results = results,
        Cancelled = false,
        Ceiling = TimeSpan.FromSeconds(60)
    };
}
