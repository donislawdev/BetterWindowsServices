using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the sheet says after a forced restart whose polite stop was enough - backlog 539.
///
/// <b>Until 2026-10-07 it said "The entry is not where you asked. What did not is below." over an empty list.</b>
/// The starts that would have put the neighbours back had nothing to give, because nothing took them down,
/// and the run counted them as not arrived - the same verdict that gave <c>bws kill X --restart</c> exit code 3,
/// found by reading the code before the rehearsal on the throwaway machine ran it. The list under the notice
/// holds only steps that failed or ran out of time, so there was nothing to point at. The results here are the
/// ones the rehearsal recorded, written out by hand, because this is about the sheet and not about the runner.
/// </summary>
public sealed class ForcedRestartNoticeGuards
{
    private static readonly PlanStep[] Steps =
    [
        new("ShareA", "Share A", StepOperation.Stop, StepReason.Requested),
        new("ShareB", "Share B", StepOperation.Stop, StepReason.SharesTheProcess),
        new("ShareA", "Share A", StepOperation.Terminate, StepReason.Escalation, ProcessId: 4812, TakesWithIt: ["ShareB"]),
        new("ShareA", "Share A", StepOperation.Start, StepReason.Restore),
        new("ShareB", "Share B", StepOperation.Start, StepReason.Restore)
    ];

    [Fact]
    public void A_forced_restart_whose_polite_stop_worked_reads_done()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Restarting());
        panel.Finished(Ran(panel));

        Assert.Equal(Texts.Of("gui.plan.notice.done.one"), panel.Notice);
        Assert.Empty(panel.Failures);
    }

    private static BulkPlan Restarting() => new()
    {
        Action = new BulkAction(ActionKind.ForceRestart, ["ShareA"]),
        Plans =
        [
            new OperationPlan
            {
                Action = new ServiceAction(ActionKind.ForceRestart, "ShareA"),
                Steps = Steps,
                Warnings = [],
                Problems = []
            }
        ],
        Problems = []
    };

    private static BulkRun Ran(Planned panel) => new()
    {
        Plan = panel.Plan!,
        Runs =
        [
            new PlanRun
            {
                Plan = panel.Plan!.Plans[0],
                Results =
                [
                    Done(Steps[0]),
                    Skipped(Steps[1], SkipReason.ProcessStays),
                    Skipped(Steps[2], SkipReason.AlreadyThere),
                    Done(Steps[3]),
                    Skipped(Steps[4], SkipReason.NothingToPutBack)
                ],
                Cancelled = false,
                Ceiling = TimeSpan.FromSeconds(60)
            }
        ]
    };

    private static StepResult Done(PlanStep step) => Result(step) with { Outcome = StepOutcome.Succeeded };

    private static StepResult Skipped(PlanStep step, SkipReason reason) =>
        Result(step) with { Outcome = StepOutcome.Skipped, SkippedBecause = reason };

    private static StepResult Result(PlanStep step) => new()
    {
        Step = step,
        Outcome = StepOutcome.Succeeded,
        SkippedBecause = null,
        Status = EntryStatus.Unknown,
        ProcessId = Reading<int>.NotRead(),
        ErrorCode = 0,
        Error = null,
        Milliseconds = 0
    };
}
