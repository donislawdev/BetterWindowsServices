using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What a plan says about the start in it - a start the manager is known to refuse, and a restart that is
/// only a start because the entry is not running.
///
/// <b>Its own file since 2026-09-30, reached from the discard arm of the warning switch beside it</b>
/// (stability report W-5, package D). That switch stood one fork under the complexity ceiling, and the
/// third warning about a start would have taken it there - so the two that were already in it moved here
/// with the new one, and the switch grew thinner rather than fatter. The terminal's PlanText is cut the
/// same way, so the two keep one shape. What this switch does not know goes on to
/// <see cref="Aftermath(PlanWarning)"/>, which keeps the refusal every kind without a sentence meets.
/// </summary>
internal static partial class PlanWords
{
    private static string Starting(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.DisabledCannotStart => Texts.Of("gui.plan.warning.disabledCannotStart", warning.ServiceName),

        PlanWarningKind.PausedCannotStart => Texts.Of("gui.plan.warning.pausedCannotStart", warning.ServiceName),

        PlanWarningKind.RestartOnlyStarts => Texts.Of("gui.plan.warning.restartOnlyStarts", warning.ServiceName),

        _ => Aftermath(warning)
    };
}
