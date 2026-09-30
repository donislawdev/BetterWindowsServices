using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// The half of <see cref="PlanText"/> that words what a plan WARNS about, as against what it does and
/// what came of it.
///
/// <b>Its own file since 2026-09-30, and the size ratchet is what asked</b> - the two warnings of the
/// stability report's package C took PlanText.cs to 203 lines of code, one over the line where a file
/// counts as close to the ceiling. The seam is a subject rather than a count: every kind of warning
/// arrives here, one arm each, and nothing else in the class grows when one does.
/// </summary>
internal static partial class PlanText
{
    /// <summary>
    /// Turns a warning into words. The core reports a kind and the entries involved and
    /// never a sentence, so the wording is decided here where it can be reviewed.
    /// </summary>
    internal static string Describe(PlanWarning warning) => warning.Kind switch
    {
        // Two keys apiece rather than "entry(s)". Text is a feature here, and a warning
        // reading "1 other entries" spends trust that the warning itself needs.
        PlanWarningKind.Cascade => Texts.Of(
            Count("cli.plan.warning.cascade", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        PlanWarningKind.DependentsInTheWay => Texts.Of(
            Count("cli.plan.warning.inTheWay", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        // A PAIR SINCE 2026-09-01, AND NO NUMBER APPEARS IN EITHER SENTENCE - backlog 207. The
        // window got both halves on 2026-08-19 and this one did not, so the two interfaces said
        // different things about the same fact. "Those keep running" about a single entry is a
        // plural with nothing to count it, which is the shape a scan for placeholders cannot see.
        PlanWarningKind.SharedProcess => Texts.Of(
            Count("cli.plan.warning.sharedProcess", warning), warning.ServiceName, Join(warning.Related)),

        PlanWarningKind.ReturnsAfterReboot => Texts.Of(
            "cli.plan.warning.returnsAfterReboot", warning.ServiceName),

        PlanWarningKind.CascadeUnreadable => Texts.Of(
            "cli.plan.warning.cascadeUnreadable", warning.ServiceName),

        PlanWarningKind.DoesNotAcceptStop => Texts.Of(
            Count("cli.plan.warning.doesNotAcceptStop", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        // NOT THE SHARED PROCESS SENTENCE, WHICH SAYS THE OPPOSITE. That one tells somebody
        // the neighbours keep running, which is true of an ordinary stop and exactly wrong
        // here - so the builder does not raise it for a forcing ask at all.
        PlanWarningKind.TerminationTakesWithIt => Texts.Of(
            Count("cli.plan.warning.takesWithIt", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        PlanWarningKind.CriticalService => Texts.Of(
            Count("cli.plan.warning.critical", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        // SAME NAMES, DIFFERENT WHEN. The sentence above is about a machine going down while
        // somebody watches, this one about a machine that comes up wrong weeks later. Sharing a
        // sentence would have meant dropping the timing, which is the half that decides what an
        // administrator does next.
        PlanWarningKind.CriticalStartType => Texts.Of(
            Count("cli.plan.warning.criticalStartType", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        PlanWarningKind.AlreadyThere => Texts.Of("cli.plan.warning.alreadyThere", warning.ServiceName),

        // THE TERMINAL'S HALF OF THE OFFER. The window puts a button under this sentence and a
        // terminal has none, so the sentence ends with the line that takes the offer - the same
        // command with --stop, which is a whole plan of its own rather than a second command after.
        PlanWarningKind.KeepsRunning => Texts.Of(
            "cli.plan.warning.keepsRunning", warning.ServiceName,
            EquivalentCommand.For(new ServiceAction(
                ActionKind.SetStartType, warning.ServiceName, To: StartSetting.Disabled, AlsoStop: true))),

        PlanWarningKind.StartsAtNextBoot => Texts.Of("cli.plan.warning.startsAtNextBoot", warning.ServiceName),

        // The line that makes the start possible, built the way the offer above builds its own - the
        // window has a startup type action for this and a terminal has the command.
        PlanWarningKind.DisabledCannotStart => Texts.Of(
            "cli.plan.warning.disabledCannotStart", warning.ServiceName,
            EquivalentCommand.For(new ServiceAction(
                ActionKind.SetStartType, warning.ServiceName, To: StartSetting.Manual))),

        PlanWarningKind.PausedCannotStart => Texts.Of("cli.plan.warning.pausedCannotStart", warning.ServiceName),

        // NAMED ARMS AND A REFUSAL, SINCE 2026-09-06, AND THE WILDCARD THAT WAS HERE IS WHY. Every
        // kind but one used to fall through to "is already in that state, so nothing would change" -
        // so a warning added without a sentence would not have been silent, which is survivable, but
        // would have said something confident and wrong about a machine, which is not. The window's
        // own switch had the same shape and was changed the same day. Since 2026-09-30 the refusal
        // stands at the end of the next switch along, which names what an ending sets off.
        _ => Aftermath(warning)
    };
}
