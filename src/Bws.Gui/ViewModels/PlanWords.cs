using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Everything the plan panel SAYS about one value from the core, in one place.
///
/// <b>Its own file since 2026-08-19, and the size ratchet is what asked - for the fourth time in
/// this window and the fourth time pointing at a real seam.</b> <see cref="Planned"/> is about the
/// three states a person sees: nothing done, doing it, done. This is about turning one warning, one
/// refusal, one failed step or one verb into a sentence, and it grew by half when the panel learned
/// to report a run.
///
/// <b>The core carries no words at all and that is deliberate rather than an omission.</b> A warning
/// holds a kind and the facts, because the same warning reads differently in a window and in a
/// terminal - so the sentences live beside the interface that says them, and this is that place for
/// the window.
///
/// <b>EVERY KEY IS INSIDE ITS OWN Texts.Of CALL, and that is not a style rule.</b> A key travelling
/// as the value of an expression is invisible to TextKeyGuards, so a switch choosing a key and one
/// call around it produces strings that never reach a screen and nothing goes red. This project has
/// the lesson from ListState.Say and walked into it again on 2026-08-18.
/// </summary>
internal static class PlanWords
{
    /// <summary>
    /// The verb as a person reads it, with the key INSIDE each call.
    ///
    /// <b>Written first as one Texts.Of around a switch that chose the key, and TextKeyGuards
    /// reddened - correctly.</b> A key travelling as the value of an expression is invisible to it, so
    /// all three came back as strings that never reach a screen. This project has the lesson recorded
    /// already, from ListState.Say, and the recorded answer is this one rather than a wider pattern: a
    /// key visible where it is chosen is better for a reader too.
    /// </summary>
    internal static string Doing(ActionKind kind) => kind switch
    {
        ActionKind.Stop => Texts.Of("gui.plan.doing.stop"),
        ActionKind.Start => Texts.Of("gui.plan.doing.start"),
        _ => Texts.Of("gui.plan.doing.restart")
    };

    /// <summary>Why a step is there, with the key inside each call for the reason above.</summary>
    internal static string Reason(StepReason reason) => reason switch
    {
        StepReason.Requested => Texts.Of("gui.plan.reason.requested"),
        StepReason.Cascade => Texts.Of("gui.plan.reason.cascade"),
        _ => Texts.Of("gui.plan.reason.restore")
    };

    /// <summary>
    /// A warning in words. The kinds come from the core and the sentences belong here.
    ///
    /// <b>Written out rather than composed from the name of the value</b>, for the reason the command
    /// line gives about the same six: flattening a name to lower case works for as long as every one
    /// is a single word and then quietly asks for a key nobody wrote.
    /// </summary>
    internal static string Describe(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.Cascade => Texts.Of(
            "gui.plan.warning.cascade", warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.DependentsInTheWay => Texts.Of(
            "gui.plan.warning.inTheWay", warning.ServiceName, Listed(warning.Related)),

        PlanWarningKind.SharedProcess => Texts.Of(
            "gui.plan.warning.sharedProcess", warning.ServiceName, Listed(warning.Related)),

        PlanWarningKind.ReturnsAfterReboot => Texts.Of("gui.plan.warning.returnsAfterReboot", warning.ServiceName),

        PlanWarningKind.CascadeUnreadable => Texts.Of("gui.plan.warning.cascadeUnreadable", warning.ServiceName),

        _ => Texts.Of("gui.plan.warning.alreadyThere", warning.ServiceName)
    };

    /// <summary>A refusal in words, with the entry it belongs to named first.</summary>
    internal static string Describe(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.UnknownService => Texts.Of("gui.plan.problem.unknownService", problem.ServiceName),

        PlanProblemKind.NotOperable => Texts.Of("gui.plan.problem.notOperable", problem.ServiceName),

        PlanProblemKind.CascadeNotOperable => Texts.Of(
            "gui.plan.problem.cascadeNotOperable", problem.ServiceName, Listed(problem.Related)),

        _ => Texts.Of("gui.plan.problem.cannotComeBack", problem.ServiceName, Listed(problem.Related))
    };

    internal static string Listed(IReadOnlyList<string> names) => string.Join(", ", names);

    /// <summary>
    /// One failed step in words, with the manager's own message carried through rather than
    /// replaced.
    ///
    /// <b>Timing out is its own sentence and not a failure worded softly.</b> The manager took the
    /// request and the entry may well have arrived after we stopped looking, so telling somebody it
    /// failed would be a claim about something nobody saw - the same distinction StepOutcome makes,
    /// arriving on a screen.
    /// </summary>
    internal static string Describe(StepResult result) => result.Outcome == StepOutcome.TimedOut
        ? Texts.Of(
            "gui.plan.failure.timedOut",
            result.Step.ServiceName,
            result.Step.Operation == StepOperation.Stop
                ? Texts.Of("gui.plan.operation.stop")
                : Texts.Of("gui.plan.operation.start"))
        : Texts.Of(
            "gui.plan.failure.refused",
            result.Step.ServiceName,
            result.Step.Operation == StepOperation.Stop
                ? Texts.Of("gui.plan.operation.stop")
                : Texts.Of("gui.plan.operation.start"),
            result.Error ?? string.Empty);
}
