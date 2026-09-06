using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// The sentence a person reads after a write, telling them how to get back.
///
/// The core works out <b>whether</b> there is a way back and what it consists of, and
/// <c>PlanReversalTests</c> next door holds that half. This holds the other one: that it reaches
/// the screen, in the order the core put it in, spelled as something somebody can paste.
///
/// <b>Worth its own file rather than a line in the core tests, because the two halves fail
/// separately and this project has paid for that distinction more than once.</b> A correct answer
/// that never reaches a screen is the shape of the elevation sentence that sat in the language file
/// from S6a onwards, and of every binding that compiled and drew nothing.
/// </summary>
public sealed class PlanTextReversalTests
{
    [Fact]
    public void A_run_that_moved_something_ends_with_the_command_that_puts_it_back()
    {
        var text = PlanText.Render(RunOf(Moved("Spooler", StepOperation.Stop)));

        Assert.Contains("bws start Spooler", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_command_word_is_the_one_a_shell_accepts()
    {
        // Not a text key, and this is the assertion that says so. The same word exists in the
        // resource file as prose, under cli.plan.operation.stop, where it is free to be reworded -
        // and a reworded line must not be able to turn this into a command that does not exist.
        var text = PlanText.Render(RunOf(Moved("Spooler", StepOperation.Start)));

        Assert.Contains("bws stop Spooler", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_restart_that_ended_where_it_began_says_nothing_about_a_way_back()
    {
        // The dangerous case, asserted here as well as in the core: the reader must not be told to
        // stop a service they just asked to have running.
        var text = PlanText.Render(RunOf(
            Moved("Spooler", StepOperation.Stop),
            Moved("Spooler", StepOperation.Start)));

        Assert.DoesNotContain("bws stop Spooler", text, StringComparison.Ordinal);
        Assert.DoesNotContain("bws start Spooler", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_preview_offers_no_way_back_because_it_moved_nothing()
    {
        // Rendered from the plan alone, which is what --dry-run does. Printing the commands here
        // would read as though something had happened.
        var run = RunOf(Moved("Spooler", StepOperation.Stop));

        var text = PlanText.Render(run.Plan);

        Assert.DoesNotContain("bws start Spooler", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cascade_reaches_the_screen_in_the_order_the_core_put_it_in()
    {
        // The order is the whole value of the list: a stop cascade has to be put back starting with
        // the entry the others depend on. A report that shuffled them would hand over a sequence
        // whose first line the manager refuses.
        var text = PlanText.Render(RunOf(
            Moved("DependantOne", StepOperation.Stop),
            Moved("TheTarget", StepOperation.Stop)));

        Assert.True(
            text.IndexOf("bws start TheTarget", StringComparison.Ordinal)
            < text.IndexOf("bws start DependantOne", StringComparison.Ordinal),
            "The entry the others depend on has to be started first:" + Environment.NewLine + text);
    }

    private static StepResult Moved(string name, StepOperation operation) => new()
    {
        Step = new PlanStep(name, name, operation, StepReason.Requested),
        Outcome = StepOutcome.Succeeded,
        SkippedBecause = null,
        Status = operation == StepOperation.Stop ? EntryStatus.Stopped : EntryStatus.Running,
        ProcessId = operation == StepOperation.Stop
            ? Reading<int>.Absent()
            : Reading<int>.Present(4812),
        ErrorCode = 0,
        Error = null,
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
