using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// The terminal's line for a start the manager took and the service did not survive.
///
/// <b>Written 2026-09-30, stability report W-7.</b> Every failed step used to read "refused: ...", and
/// this one was not refused by anybody - the service stopped again, and the number is its own exit code.
/// </summary>
public sealed class StartFellOverSentenceTests
{
    [Fact]
    public void A_service_that_stopped_while_starting_is_not_called_refused()
    {
        var said = PlanText.Describe(Result(stoppedWhileStarting: true));

        Assert.Equal(Texts.Of("cli.run.outcome.stoppedWhileStarting", Words, 1064, "240 ms"), said);
    }

    [Fact]
    public void A_start_the_manager_refused_still_reads_as_a_refusal()
    {
        var said = PlanText.Describe(Result(stoppedWhileStarting: false));

        Assert.Equal(Texts.Of("cli.run.outcome.failed", Words, 1064), said);
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
