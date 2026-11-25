using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What a plan says about a pause or a resume - its three refusals and the sentence naming what keeps
/// running. Since 2026-11-25 (backlog 555 part b) - the terminal's PlanText.Pausing says the same.
///
/// <b>The last link of the chain of switches</b>, reached from the aftermath ones: what this one does not
/// know throws, so a kind with no sentence never borrows one. Each key is written out inside the call, for
/// the reason the head of the warning switch gives.
/// </summary>
internal static partial class PlanWords
{
    private static string Pausing(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.DependentsKeepRunning => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.dependentsKeepRunning.one", warning.ServiceName, Listed(warning.Related))
            : Texts.Of("gui.plan.warning.dependentsKeepRunning.many", warning.ServiceName, Listed(warning.Related)),

        _ => throw new ArgumentOutOfRangeException(nameof(warning), warning.Kind, EquivalentCommand.Unhandled)
    };

    private static string Pausing(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.NotRunning => Texts.Of("gui.plan.problem.notRunning.one", problem.ServiceName),
        PlanProblemKind.StoppedNotPaused => Texts.Of("gui.plan.problem.stoppedNotPaused.one", problem.ServiceName),
        PlanProblemKind.PauseNotAccepted => Texts.Of("gui.plan.problem.pauseNotAccepted.one", problem.ServiceName),

        _ => throw new ArgumentOutOfRangeException(nameof(problem), problem.Kind, EquivalentCommand.Unhandled)
    };
}
