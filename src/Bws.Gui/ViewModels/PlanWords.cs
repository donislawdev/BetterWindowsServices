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
    /// <b>THE DISCARD USED TO MEAN RESTARTING, AND A PROBE CAUGHT IT ON A LIVE WINDOW ON
    /// 2026-08-25.</b> The first run of the start type menu put "What restarting Adobe Acrobat
    /// Update Service would do" over a step reading "set AdobeARMservice to Disabled" - the title of
    /// the panel, which is the line somebody reads before changing a machine, describing an
    /// operation nobody asked for. Every named kind has its own arm now and an unnamed one refuses.
    internal static string Doing(ActionKind kind) => kind switch
    {
        ActionKind.Stop => Texts.Of("gui.plan.doing.stop"),
        ActionKind.Start => Texts.Of("gui.plan.doing.start"),
        ActionKind.Restart => Texts.Of("gui.plan.doing.restart"),
        ActionKind.SetStartType => Texts.Of("gui.plan.doing.setStartType"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind, Bws.Core.Planning.EquivalentCommand.Unhandled)
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
    ///
    /// <b>TWO KEYS APIECE WHEREVER A SENTENCE COUNTS SOMETHING OR POINTS AT A GROUP, ADDED
    /// 2026-08-19.</b> The command line has had exactly this pair since it learned to warn, and its
    /// own comment says why - a warning reading "1 other entries" spends the trust the warning
    /// needs. These sentences were written later, from the same facts, and arrived with the plural
    /// half only. Nothing went red, because no guard in this project reads prose.
    ///
    /// <b>The pair is spelled out per branch rather than through a helper that appends ".one" or
    /// ".many", which is what the command line does.</b> That helper cannot live here: a key built
    /// as an expression is invisible to TextKeyGuards, so both halves would be reported as text no
    /// screen ever shows, and the missing one would render on screen as its own key. The head of
    /// this file carries the same lesson from Doing, one switch earlier.
    /// </summary>
    internal static string Describe(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.Cascade => warning.Related.Count == 1
            ? Texts.Of(
                "gui.plan.warning.cascade.one", warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of(
                "gui.plan.warning.cascade.many", warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.DependentsInTheWay => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.inTheWay.one", warning.ServiceName, Listed(warning.Related))
            : Texts.Of("gui.plan.warning.inTheWay.many", warning.ServiceName, Listed(warning.Related)),

        PlanWarningKind.SharedProcess => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.sharedProcess.one", warning.ServiceName, Listed(warning.Related))
            : Texts.Of("gui.plan.warning.sharedProcess.many", warning.ServiceName, Listed(warning.Related)),

        PlanWarningKind.ReturnsAfterReboot => Texts.Of("gui.plan.warning.returnsAfterReboot", warning.ServiceName),

        PlanWarningKind.CascadeUnreadable => Texts.Of("gui.plan.warning.cascadeUnreadable", warning.ServiceName),

        _ => Texts.Of("gui.plan.warning.alreadyThere", warning.ServiceName)
    };

    /// <summary>
    /// A refusal in words, with the entry it belongs to named first.
    ///
    /// <b>The same singular and plural pair as the warnings above, and for the same reason.</b> A
    /// refusal is read by somebody deciding whether to press, so a sentence pointing at "these
    /// drivers" when there is one of them is a sentence they have to check against the list.
    /// </summary>
    internal static string Describe(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.UnknownService => Texts.Of("gui.plan.problem.unknownService", problem.ServiceName),

        PlanProblemKind.NotOperable => Texts.Of("gui.plan.problem.notOperable", problem.ServiceName),

        PlanProblemKind.CascadeNotOperable => problem.Related.Count == 1
            ? Texts.Of("gui.plan.problem.cascadeNotOperable.one", problem.ServiceName, Listed(problem.Related))
            : Texts.Of("gui.plan.problem.cascadeNotOperable.many", problem.ServiceName, Listed(problem.Related)),

        _ => problem.Related.Count == 1
            ? Texts.Of("gui.plan.problem.cannotComeBack.one", problem.ServiceName, Listed(problem.Related))
            : Texts.Of("gui.plan.problem.cannotComeBack.many", problem.ServiceName, Listed(problem.Related))
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
    internal static string Describe(StepResult result) =>
        result.Step.Operation == StepOperation.SetStartType

            // A SENTENCE OF ITS OWN, because the shared one does not survive this verb: "Spooler
            // would not set the start type of" is what the template above would produce. A timeout
            // cannot arrive here at all - writing a setting is done when it returns, so there is
            // nothing to watch and nothing to give up on.
            ? Texts.Of(
                "gui.plan.failure.refusedStartType",
                result.Step.ServiceName,
                result.Error ?? string.Empty)
            : Ordinary(result);

    private static string Ordinary(StepResult result) => result.Outcome == StepOutcome.TimedOut
        ? Texts.Of(
            "gui.plan.failure.timedOut",
            result.Step.ServiceName,
            Word(result.Step.Operation))
        : Texts.Of(
            "gui.plan.failure.refused",
            result.Step.ServiceName,
            Word(result.Step.Operation),
            result.Error ?? string.Empty);

    /// <summary>
    /// What one step DOES, in one word, said in one place.
    ///
    /// <b>Four copies of this stood in this window until 2026-08-25, and every one of them was a
    /// ternary</b> - "a stop, otherwise a start". A third kind of step would have been called a
    /// start on four screens at once, in a panel whose whole purpose is telling somebody what is
    /// about to happen to their machine. The core has the same change at the same time, including
    /// in the one place that asks the manager to move something.
    ///
    /// <b>It refuses rather than guessing, and a compile error was not available:</b> a switch
    /// covering every named member of an enum still needs a discard - CS8524 - because the variable
    /// can hold a number nobody named.
    /// </summary>
    internal static string Word(StepOperation operation) => operation switch
    {
        StepOperation.Stop => Texts.Of("gui.plan.operation.stop"),
        StepOperation.Start => Texts.Of("gui.plan.operation.start"),
        StepOperation.SetStartType => Texts.Of("gui.plan.operation.setStartType"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(operation), operation, Bws.Core.Planning.EquivalentCommand.Unhandled)
    };
}
