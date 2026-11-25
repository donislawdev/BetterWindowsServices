using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// What a plan says about a pause or a resume - its two refusals of a stopped entry, the refusal of an entry
/// that takes no pauses, and the sentence naming what keeps running or saying that it could not be read. Since
/// 2026-11-25 (backlog 555 part b).
///
/// <b>The last link of the chain of switches</b>, reached from the recovery ones: what this one does not know
/// throws, so a kind with no sentence never borrows one.
/// </summary>
internal static partial class PlanText
{
    private static string Pausing(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.DependentsKeepRunning => Texts.Of(
            Count("cli.plan.warning.dependentsKeepRunning", warning), warning.ServiceName, Join(warning.Related)),

        PlanWarningKind.DependentsUnreadable => Texts.Of("cli.plan.warning.dependentsUnreadable", warning.ServiceName),

        _ => throw new ArgumentOutOfRangeException(nameof(warning), warning.Kind, EquivalentCommand.Unhandled)
    };

    private static string Pausing(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.NotRunning => Texts.Of("cli.plan.problem.notRunning", problem.ServiceName),

        // The start is named, not done - the owner's decision of 2026-11-25. The line is built the way the
        // offer to start a disabled entry builds its own, so the word and the command cannot drift apart.
        PlanProblemKind.StoppedNotPaused => Texts.Of(
            "cli.plan.problem.stoppedNotPaused", problem.ServiceName,
            EquivalentCommand.For(new ServiceAction(ActionKind.Start, problem.ServiceName))),

        PlanProblemKind.PauseNotAccepted => Texts.Of("cli.plan.problem.pauseNotAccepted", problem.ServiceName),

        _ => throw new ArgumentOutOfRangeException(nameof(problem), problem.Kind, EquivalentCommand.Unhandled)
    };
}
