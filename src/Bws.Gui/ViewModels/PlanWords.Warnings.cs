using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The half of <see cref="PlanWords"/> that words what a plan WARNS about.
///
/// <b>Its own file since 2026-09-30, and the size ratchet is what asked</b> - the two warnings of the
/// stability report's package C took PlanWords.cs to 204 lines of code, over the line where a file
/// counts as close to the ceiling. The seam is the same one the terminal's PlanText was cut along the
/// same day: every kind of warning arrives here, one arm each, and nothing else grows when one does.
/// </summary>
internal static partial class PlanWords
{
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

        // THE LITERAL SITS INSIDE Texts.Of RATHER THAN IN A TERNARY HANDED TO IT, and the first
        // attempt did the second - TextKeyGuards found both halves and was right to. Its patterns
        // read the argument of a call, so a key chosen one line earlier is a key nobody can find by
        // searching for it, which is the same failure that file records against a key assembled at
        // run time. The five arms above are all written this way and now so is this one.
        PlanWarningKind.DoesNotAcceptStop => warning.Related.Count == 1
            ? Texts.Of(
                "gui.plan.warning.doesNotAcceptStop.one",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of(
                "gui.plan.warning.doesNotAcceptStop.many",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.TerminationTakesWithIt => warning.Related.Count == 1
            ? Texts.Of(
                "gui.plan.warning.takesWithIt.one",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of(
                "gui.plan.warning.takesWithIt.many",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.CriticalService => warning.Related.Count == 1
            ? Texts.Of(
                "gui.plan.warning.critical.one",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of(
                "gui.plan.warning.critical.many",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        // SAME NAMES, DIFFERENT WHEN - see the terminal's own arm for the whole of the argument.
        PlanWarningKind.CriticalStartType => warning.Related.Count == 1
            ? Texts.Of(
                "gui.plan.warning.criticalStartType.one",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of(
                "gui.plan.warning.criticalStartType.many",
                warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.AlreadyThere => Texts.Of("gui.plan.warning.alreadyThere", warning.ServiceName),

        // The offer to stop it too is not in this sentence - it stands under it as a button, which
        // PlanWarningLine decides. The terminal's sentence names --stop instead, having no button.
        PlanWarningKind.KeepsRunning => Texts.Of("gui.plan.warning.keepsRunning", warning.ServiceName),

        PlanWarningKind.StartsAtNextBoot => Texts.Of("gui.plan.warning.startsAtNextBoot", warning.ServiceName),

        // NAMED ARMS AND A REFUSAL, SINCE 2026-09-06, AND THE WILDCARD THAT WAS HERE IS WHY. Every
        // kind but one used to fall through to "is already in that state, so nothing would change" -
        // a warning added without a sentence would have said something confident and wrong about a
        // machine rather than nothing at all. The terminal's own switch had the same shape and was
        // changed the same day. Since 2026-09-30 the refusal stands at the end of a chain of switches
        // along - the start in the plan first, then what an ending sets off - and this one gave its
        // two arms about a start to the first of them rather than growing to the ceiling.
        _ => Starting(warning)
    };
}
