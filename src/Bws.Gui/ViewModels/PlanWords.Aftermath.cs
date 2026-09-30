using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What a plan that ends a process says about what comes after it - three warnings and three refusals,
/// since 2026-09-30 (stability report W-3, package B2).
///
/// <b>Its own file, reached from the discard arm of the two switches beside it</b>, because the warning
/// switch stands one fork under the complexity ceiling and three more arms would have taken it two over.
/// The terminal's PlanText is cut the same way, so the two keep one shape. Each key is written out in full
/// inside the call, for the reason the head of the warning switch gives: a key built as an expression is
/// one TextKeyGuards cannot find.
/// </summary>
internal static partial class PlanWords
{
    private static string Aftermath(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.RecoveryRestarts => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.recoveryRestarts.one", warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of("gui.plan.warning.recoveryRestarts.many", warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.RecoveryRunsProgram => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.recoveryRunsProgram.one", warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of("gui.plan.warning.recoveryRunsProgram.many", warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.RecoveryUnnamed => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.recoveryUnnamed.one", warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of("gui.plan.warning.recoveryUnnamed.many", warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        _ => throw new ArgumentOutOfRangeException(
            nameof(warning), warning.Kind, EquivalentCommand.Unhandled)
    };

    /// <summary>
    /// The three refusals. An unreadable consequence with no names is the process itself - whether it is
    /// critical could not be read - so it has a sentence of its own.
    /// </summary>
    private static string Aftermath(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.ProcessIsCritical => Texts.Of("gui.plan.problem.processIsCritical", problem.ServiceName),

        PlanProblemKind.RecoveryRestartsComputer => problem.Related.Count == 1
            ? Texts.Of("gui.plan.problem.recoveryRestartsComputer.one", problem.ServiceName, Listed(problem.Related))
            : Texts.Of("gui.plan.problem.recoveryRestartsComputer.many", problem.ServiceName, Listed(problem.Related)),

        PlanProblemKind.AftermathUnreadable when problem.Related.Count == 0 =>
            Texts.Of("gui.plan.problem.aftermathUnreadable.process", problem.ServiceName),

        PlanProblemKind.AftermathUnreadable => problem.Related.Count == 1
            ? Texts.Of("gui.plan.problem.aftermathUnreadable.one", problem.ServiceName, Listed(problem.Related))
            : Texts.Of("gui.plan.problem.aftermathUnreadable.many", problem.ServiceName, Listed(problem.Related)),

        _ => throw new ArgumentOutOfRangeException(
            nameof(problem), problem.Kind, EquivalentCommand.Unhandled)
    };
}
