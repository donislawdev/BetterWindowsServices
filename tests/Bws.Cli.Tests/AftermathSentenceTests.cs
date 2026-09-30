using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// The terminal's lines for what ending a process sets off - stability report W-3, package B2, 2026-09-30.
///
/// <b>Each of these was a kind with no sentence at all until that day</b>, and both switches it arrives
/// through end in a refusal rather than a borrowed sentence - so a missing arm here is an exception in
/// front of somebody about to end a process. These pin which key each shape reaches.
/// </summary>
public sealed class AftermathSentenceTests
{
    [Theory]
    [InlineData(PlanWarningKind.RecoveryRestarts, "cli.plan.warning.recoveryRestarts")]
    [InlineData(PlanWarningKind.RecoveryRunsProgram, "cli.plan.warning.recoveryRunsProgram")]
    [InlineData(PlanWarningKind.RecoveryUnnamed, "cli.plan.warning.recoveryUnnamed")]
    public void Each_warning_has_its_own_sentence_in_both_numbers(PlanWarningKind kind, string key)
    {
        Assert.Equal(
            Texts.Of($"{key}.one", "Spooler", 1, "Spooler"),
            PlanText.Describe(new PlanWarning(kind, "Spooler", ["Spooler"])));

        Assert.Equal(
            Texts.Of($"{key}.many", "Spooler", 2, "Spooler, Fax"),
            PlanText.Describe(new PlanWarning(kind, "Spooler", ["Spooler", "Fax"])));
    }

    [Fact]
    public void A_critical_process_says_the_machine_stops()
    {
        Assert.Equal(
            Texts.Of("cli.plan.problem.processIsCritical", "RpcSs"),
            PlanText.Describe(new PlanProblem(PlanProblemKind.ProcessIsCritical, "RpcSs", [])));
    }

    [Fact]
    public void An_unreadable_consequence_with_no_names_is_about_the_process_itself()
    {
        Assert.Equal(
            Texts.Of("cli.plan.problem.aftermathUnreadable.process", "Spooler", string.Empty),
            PlanText.Describe(new PlanProblem(PlanProblemKind.AftermathUnreadable, "Spooler", [])));

        Assert.Equal(
            Texts.Of("cli.plan.problem.aftermathUnreadable.one", "Spooler", "Fax"),
            PlanText.Describe(new PlanProblem(PlanProblemKind.AftermathUnreadable, "Spooler", ["Fax"])));
    }

    [Fact]
    public void A_computer_restart_names_who_carries_it()
    {
        Assert.Equal(
            Texts.Of("cli.plan.problem.recoveryRestartsComputer.many", "DcomLaunch", "Power, SystemEventsBroker"),
            PlanText.Describe(new PlanProblem(
                PlanProblemKind.RecoveryRestartsComputer, "DcomLaunch", ["Power", "SystemEventsBroker"])));
    }

    [Fact]
    public void An_ending_the_manager_undid_at_once_is_not_called_refused()
    {
        var said = PlanText.Describe(new StepResult
        {
            Step = new PlanStep("Spooler", "Print Spooler", StepOperation.Terminate, StepReason.Requested, ProcessId: 4812),
            Outcome = StepOutcome.Failed,
            SkippedBecause = null,
            Status = EntryStatus.Running,
            ProcessId = Reading<int>.Present(5555),
            ErrorCode = 0,
            Error = "ours",
            Milliseconds = 16,
            StartedAgain = true
        });

        Assert.Equal(Texts.Of("cli.run.outcome.startedAgain", 5555, "16 ms"), said);
    }
}
