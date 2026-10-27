using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A request the manager took after which the entry could not be read, in the window's words - logic report
/// R02, 2026-10-26. Not "would not stop": nothing refused the stop, only the reading after it.
/// </summary>
public sealed class WatchedStepWordsGuards
{
    [Fact]
    public void A_request_taken_and_then_lost_from_sight_is_not_called_refused()
    {
        var said = PlanWords.Describe(new StepResult
        {
            Step = new PlanStep("Spooler", "Print Spooler", StepOperation.Stop, StepReason.Requested),
            Outcome = StepOutcome.Failed,
            SkippedBecause = null,
            Status = EntryStatus.Unknown,
            ProcessId = Reading<int>.NotRead(),
            ErrorCode = 5,
            Error = "Access is denied.",
            Milliseconds = 40,
            LostSight = true
        });

        Assert.Equal(
            Texts.Of("gui.plan.failure.lostSight", "Spooler", Texts.Of("gui.plan.operation.stop"), "Access is denied."),
            said);
    }
}
