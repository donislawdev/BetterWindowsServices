using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// When Windows starts an ended entry again, in the window - backlog 501, the owner's decision of 2026-09-30:
/// the restart warning says it beside every name, and the notice after a run that ended the process says who
/// comes back.
///
/// <b>The notice rather than a section of its own</b>, because the second sentence qualifies the first -
/// "Done. The entry is where you asked." is true when it is read and stops being true a minute later.
/// </summary>
public sealed class ComingBackWordsGuards
{
    private static readonly TimeSpan Minute = TimeSpan.FromSeconds(60);

    [Fact]
    public void The_restart_warning_says_when_beside_every_name()
    {
        var said = PlanWords.Describe(Warned(
            new RecoveryRestart("WpnService", [.. new[] { 1, 2, 4, 8, 16 }.Select(seconds => TimeSpan.FromSeconds(seconds))]),
            new RecoveryRestart("Fax", [TimeSpan.FromMilliseconds(100)])));

        Assert.Contains(": WpnService (1 s, 2 s, 4 s, 8 s or 16 s later), Fax (0.1 s later).", said, StringComparison.Ordinal);
    }

    [Fact]
    public void The_notice_after_a_run_that_ended_the_process_says_who_comes_back()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing());
        panel.Finished(Ran(panel, StepOutcome.Succeeded));

        Assert.Equal(
            Texts.Of(
                "gui.plan.notice.andAfter",
                Texts.Of("gui.plan.notice.done.one"),
                Texts.Of(
                    "gui.plan.notice.comesBack.one",
                    "Spooler",
                    Texts.Of("gui.plan.recovery.later", "Spooler", Texts.Of("gui.plan.recovery.seconds", "60")))),
            panel.Notice);
    }

    [Fact]
    public void The_notice_after_a_run_that_never_ended_the_process_is_only_the_report()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing());
        panel.Finished(Ran(panel, StepOutcome.Failed));

        Assert.Equal(Texts.Of("gui.plan.notice.partly.one"), panel.Notice);
    }

    private static PlanWarning Warned(params RecoveryRestart[] restarts) =>
        new(PlanWarningKind.RecoveryRestarts, restarts[0].ServiceName, [.. restarts.Select(one => one.ServiceName)])
        {
            Restarts = restarts
        };

    private static BulkPlan Forcing() => new()
    {
        Action = new BulkAction(ActionKind.ForceStop, ["Spooler"]),
        Plans =
        [
            new OperationPlan
            {
                Action = new ServiceAction(ActionKind.ForceStop, "Spooler"),
                Steps =
                [
                    new PlanStep(
                        "Spooler", "Print Spooler", StepOperation.Terminate, StepReason.Requested, ProcessId: 4812, TakesWithIt: [])
                ],
                Warnings = [Warned(new RecoveryRestart("Spooler", [Minute]))],
                Problems = []
            }
        ],
        Problems = []
    };

    private static BulkRun Ran(Planned panel, StepOutcome outcome)
    {
        var plan = panel.Plan!;
        var one = plan.Plans[0];

        return new BulkRun
        {
            Plan = plan,
            Runs =
            [
                new PlanRun
                {
                    Plan = one,
                    Results =
                    [
                        new StepResult
                        {
                            Step = one.Steps[0],
                            Outcome = outcome,
                            SkippedBecause = null,
                            Status = outcome == StepOutcome.Succeeded ? EntryStatus.Stopped : EntryStatus.Running,
                            ProcessId = Reading<int>.NotRead(),
                            ErrorCode = outcome == StepOutcome.Succeeded ? 0 : 5,
                            Error = outcome == StepOutcome.Succeeded ? null : "Access is denied.",
                            Milliseconds = 18
                        }
                    ],
                    Cancelled = false,
                    Ceiling = Minute
                }
            ]
        };
    }
}
