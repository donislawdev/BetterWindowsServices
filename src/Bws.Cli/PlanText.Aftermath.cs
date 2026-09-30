using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// What a plan that ends a process says about what comes after it - three warnings and three refusals,
/// since 2026-09-30 (stability report W-3, package B2).
///
/// <b>Its own file, reached from the discard arm of the two switches beside it</b>, so that neither grows:
/// the window's twin of the warning switch stands one fork under the complexity ceiling, and this side is
/// cut the same way so the two stay one shape. The named arms and the refusal at the end are the rule
/// those switches have kept since 2026-09-06 - a kind with no sentence throws rather than borrowing one.
/// </summary>
internal static partial class PlanText
{
    private static string Aftermath(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.RecoveryRestarts => Texts.Of(
            Count("cli.plan.warning.recoveryRestarts", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        PlanWarningKind.RecoveryRunsProgram => Texts.Of(
            Count("cli.plan.warning.recoveryRunsProgram", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        PlanWarningKind.RecoveryUnnamed => Texts.Of(
            Count("cli.plan.warning.recoveryUnnamed", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        _ => throw new ArgumentOutOfRangeException(
            nameof(warning), warning.Kind, EquivalentCommand.Unhandled)
    };

    /// <summary>
    /// The three refusals. <b>An unreadable consequence with no names is the process itself</b> - whether it
    /// is critical could not be read - so it gets a sentence of its own rather than an empty list.
    /// </summary>
    private static string Aftermath(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.ProcessIsCritical => Texts.Of("cli.plan.problem.processIsCritical", problem.ServiceName),

        PlanProblemKind.RecoveryRestartsComputer => Texts.Of(
            problem.Related.Count == 1
                ? "cli.plan.problem.recoveryRestartsComputer.one"
                : "cli.plan.problem.recoveryRestartsComputer.many",
            problem.ServiceName, Join(problem.Related)),

        PlanProblemKind.AftermathUnreadable => Texts.Of(
            problem.Related.Count == 0
                ? "cli.plan.problem.aftermathUnreadable.process"
                : problem.Related.Count == 1
                    ? "cli.plan.problem.aftermathUnreadable.one"
                    : "cli.plan.problem.aftermathUnreadable.many",
            problem.ServiceName, Join(problem.Related)),

        _ => throw new ArgumentOutOfRangeException(
            nameof(problem), problem.Kind, EquivalentCommand.Unhandled)
    };
}
