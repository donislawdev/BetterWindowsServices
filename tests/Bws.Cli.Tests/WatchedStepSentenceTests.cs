using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// The two step outcomes that got words of their own on 2026-10-26 (logic report R01 and R02): an entry that
/// got where the step wanted it with nothing asked, and a request the manager took after which the entry
/// could not be read. The first is not "done", because nothing here did it - a restart then says, a line
/// further down, that it never stopped the entry. The second is not "refused", because nothing was refused
/// but the reading.
/// </summary>
public sealed class WatchedStepSentenceTests
{
    [Fact]
    public void An_arrival_nothing_asked_for_says_so_rather_than_done()
    {
        var said = PlanText.Describe(Result(StepOutcome.Succeeded) with { ArrivedUnasked = true });

        Assert.Equal(Texts.Of("cli.run.outcome.arrivedUnasked", "40 ms"), said);
    }

    [Fact]
    public void A_request_taken_and_then_lost_from_sight_is_not_called_refused()
    {
        var said = PlanText.Describe(Result(StepOutcome.Failed) with
        {
            LostSight = true,
            ErrorCode = 5,
            Error = "Access is denied."
        });

        Assert.Equal(Texts.Of("cli.run.outcome.lostSight", "Access is denied.", 5), said);
    }

    private static StepResult Result(StepOutcome outcome) => new()
    {
        Step = new PlanStep("Spooler", "Print Spooler", StepOperation.Stop, StepReason.Requested),
        Outcome = outcome,
        SkippedBecause = null,
        Status = EntryStatus.Stopped,
        ProcessId = Reading<int>.Absent(),
        ErrorCode = 0,
        Error = null,
        Milliseconds = 40
    };
}
