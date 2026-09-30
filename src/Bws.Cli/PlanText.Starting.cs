using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// What a plan says about the start in it - a start the manager is known to refuse, and a restart that is
/// only a start because the entry is not running.
///
/// <b>Its own file since 2026-09-30, reached from the discard arm of the warning switch beside it</b>
/// (stability report W-5, package D). The window's twin of that switch stood one fork under the complexity
/// ceiling, and the third warning about a start would have taken it there - so the two that were already
/// in it moved here with the new one, and both switches grew thinner rather than one of them fatter. The
/// terminal is cut the same way so the two keep one shape. What this switch does not know goes on to
/// <see cref="Aftermath(PlanWarning)"/>, which keeps the refusal every kind without a sentence meets.
/// </summary>
internal static partial class PlanText
{
    private static string Starting(PlanWarning warning) => warning.Kind switch
    {
        // The line that makes the start possible, built the way the offer to stop a disabled entry builds
        // its own - the window has a startup type action for this and a terminal has the command.
        PlanWarningKind.DisabledCannotStart => Texts.Of(
            "cli.plan.warning.disabledCannotStart", warning.ServiceName,
            EquivalentCommand.For(new ServiceAction(
                ActionKind.SetStartType, warning.ServiceName, To: StartSetting.Manual))),

        PlanWarningKind.PausedCannotStart => Texts.Of("cli.plan.warning.pausedCannotStart", warning.ServiceName),

        PlanWarningKind.RestartOnlyStarts => Texts.Of("cli.plan.warning.restartOnlyStarts", warning.ServiceName),

        _ => Aftermath(warning)
    };
}
