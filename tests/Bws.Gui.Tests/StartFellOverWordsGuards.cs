using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The window's sentence for a start the manager took and the service did not survive.
///
/// <b>Written 2026-09-30, stability report W-7.</b> The shared sentence says the entry "would not
/// start", which is a refusal - and nothing refused this one. The service stopped again, and the number
/// beside it is its own exit code.
/// </summary>
public sealed class StartFellOverWordsGuards
{
    [Fact]
    public void A_service_that_stopped_while_starting_says_so_with_its_exit_code()
    {
        var said = PlanWords.Describe(Result(stoppedWhileStarting: true));

        Assert.Equal(Texts.Of("gui.plan.failure.stoppedWhileStarting", "Spooler", 1064, Words), said);
    }

    [Fact]
    public void A_start_the_manager_refused_still_reads_as_a_refusal()
    {
        var said = PlanWords.Describe(Result(stoppedWhileStarting: false));

        Assert.Equal(Texts.Of("gui.plan.failure.refused", "Spooler", Texts.Of("gui.plan.operation.start"), Words), said);
    }

    private const string Words = "An exception occurred in the service when handling the control request.";

    private static StepResult Result(bool stoppedWhileStarting) => new()
    {
        Step = new PlanStep("Spooler", "Print Spooler", StepOperation.Start, StepReason.Requested),
        Outcome = StepOutcome.Failed,
        SkippedBecause = null,
        Status = EntryStatus.Stopped,
        ProcessId = Reading<int>.Absent(),
        ErrorCode = 1064,
        Error = Words,
        Milliseconds = 240,
        StoppedWhileStarting = stoppedWhileStarting
    };
}
