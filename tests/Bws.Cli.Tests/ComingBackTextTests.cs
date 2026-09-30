using System.Text.Json;
using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// When Windows starts an ended entry again, in the terminal - backlog 501, the owner's decision of 2026-09-30:
/// the restart warning says it beside every name, and a run that ended the process says who comes back.
///
/// <b>Quoted as English here, on purpose, in two places.</b> The shape "Spooler (60 s or 120 s later)" is the
/// whole of the change as a person sees it, and a test holding only the keys would pass over a list joined
/// wrongly. The rest holds the keys.
/// </summary>
public sealed class ComingBackTextTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromSeconds(60);

    [Fact]
    public void The_restart_warning_says_when_beside_the_name()
    {
        var said = PlanText.Describe(Warned(new RecoveryRestart("Spooler", [Minute, TimeSpan.FromSeconds(120)])));

        Assert.Contains("Windows starts Spooler (60 s or 120 s later) again by itself", said, StringComparison.Ordinal);
        Assert.EndsWith("sc.exe qfailure Spooler", said, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_delay_is_named_and_a_fraction_of_a_second_stays_in_seconds()
    {
        var said = PlanText.Describe(Warned(
            new RecoveryRestart("WpnService", [.. new[] { 1, 2, 4, 8, 16 }.Select(seconds => TimeSpan.FromSeconds(seconds))]),
            new RecoveryRestart("Fax", [TimeSpan.FromMilliseconds(100)])));

        Assert.Contains(": WpnService (1 s, 2 s, 4 s, 8 s or 16 s later), Fax (0.1 s later).", said, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_that_ended_the_process_says_who_comes_back_before_the_warnings()
    {
        var report = PlanText.Render(Ran(StepOutcome.Succeeded));

        var line = Texts.Of(
            "cli.run.comesBack.one",
            "Spooler",
            Texts.Of("cli.plan.recovery.later", "Spooler", Texts.Of("cli.run.took.seconds", "60")));

        Assert.Contains(line, report, StringComparison.Ordinal);
        Assert.True(
            report.IndexOf(line, StringComparison.Ordinal) < report.IndexOf(Texts.Of("cli.plan.warnings"), StringComparison.Ordinal));
    }

    [Fact]
    public void A_run_that_never_ended_the_process_says_nothing_about_coming_back()
    {
        var report = PlanText.Render(Ran(StepOutcome.Failed));

        Assert.DoesNotContain("after the process behind Spooler was ended", report, StringComparison.Ordinal);
    }

    [Fact]
    public void The_machine_readable_warning_gains_no_field()
    {
        var warning = JsonDocument.Parse(PlanJson.Render(Ran(StepOutcome.Succeeded))).RootElement.GetProperty("warnings")[0];

        Assert.Equal(["kind", "serviceName", "related", "message"], warning.EnumerateObject().Select(field => field.Name));
        Assert.Contains("Spooler (60 s later)", warning.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    private static PlanWarning Warned(params RecoveryRestart[] restarts) =>
        new(PlanWarningKind.RecoveryRestarts, restarts[0].ServiceName, [.. restarts.Select(one => one.ServiceName)])
        {
            Restarts = restarts
        };

    private static PlanRun Ran(StepOutcome outcome)
    {
        var ending = new PlanStep(
            "Spooler", "Print Spooler", StepOperation.Terminate, StepReason.Requested, ProcessId: 4812, TakesWithIt: []);

        return new PlanRun
        {
            Plan = new OperationPlan
            {
                Action = new ServiceAction(ActionKind.ForceStop, "Spooler"),
                Steps = [ending],
                Warnings = [Warned(new RecoveryRestart("Spooler", [Minute]))],
                Problems = []
            },
            Results =
            [
                new StepResult
                {
                    Step = ending,
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
        };
    }
}
