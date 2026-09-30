using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The window's sentences for what ending a process sets off - stability report W-3, package B2, 2026-09-30.
///
/// <b>Both switches these arrive through end in a refusal</b>, so a kind without an arm is an exception on the
/// sheet somebody is about to press. These pin the key each shape reaches, in both numbers.
/// </summary>
public sealed class AftermathWordsGuards
{
    [Theory]
    [InlineData(PlanWarningKind.RecoveryRestarts, "gui.plan.warning.recoveryRestarts")]
    [InlineData(PlanWarningKind.RecoveryRunsProgram, "gui.plan.warning.recoveryRunsProgram")]
    [InlineData(PlanWarningKind.RecoveryUnnamed, "gui.plan.warning.recoveryUnnamed")]
    public void Each_warning_has_its_own_sentence_in_both_numbers(PlanWarningKind kind, string key)
    {
        Assert.Equal(
            Texts.Of($"{key}.one", "Spooler", 1, "Spooler"),
            PlanWords.Describe(new PlanWarning(kind, "Spooler", ["Spooler"])));

        Assert.Equal(
            Texts.Of($"{key}.many", "Spooler", 2, "Spooler, Fax"),
            PlanWords.Describe(new PlanWarning(kind, "Spooler", ["Spooler", "Fax"])));
    }

    [Fact]
    public void The_three_refusals_each_have_a_sentence()
    {
        Assert.Equal(
            Texts.Of("gui.plan.problem.processIsCritical", "RpcSs"),
            PlanWords.Describe(new PlanProblem(PlanProblemKind.ProcessIsCritical, "RpcSs", [])));

        Assert.Equal(
            Texts.Of("gui.plan.problem.recoveryRestartsComputer.one", "RpcSs", "RpcSs"),
            PlanWords.Describe(new PlanProblem(PlanProblemKind.RecoveryRestartsComputer, "RpcSs", ["RpcSs"])));

        Assert.Equal(
            Texts.Of("gui.plan.problem.aftermathUnreadable.process", "Spooler"),
            PlanWords.Describe(new PlanProblem(PlanProblemKind.AftermathUnreadable, "Spooler", [])));

        Assert.Equal(
            Texts.Of("gui.plan.problem.aftermathUnreadable.many", "Spooler", "Fax, Spooler"),
            PlanWords.Describe(new PlanProblem(PlanProblemKind.AftermathUnreadable, "Spooler", ["Fax", "Spooler"])));
    }

    [Fact]
    public void An_ending_the_manager_undid_at_once_names_the_new_process()
    {
        var said = PlanWords.Describe(new StepResult
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

        Assert.Equal(Texts.Of("gui.plan.failure.startedAgain", "Spooler", 5555), said);
    }
}
