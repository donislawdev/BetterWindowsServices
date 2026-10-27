namespace Bws.Core.Planning;

/// <summary>
/// The look a run takes before its first step at whether what it is about to take down can come back.
///
/// <b>Its own file since 2026-10-26</b> (logic report R03, the owner's decision "a check before the run").
/// The rest of the class carries steps out, and this asks one question of the whole plan before any of them -
/// the same seam <see cref="Crowded"/> has for the one step that ends a process.
/// </summary>
public sealed partial class PlanRunner
{
    /// <summary>
    /// A refusal for the first step when anything this plan takes down and puts back is set to Disabled now -
    /// or nothing, when all of it can be started again.
    ///
    /// <b>THE BUILDER ALREADY REFUSES THIS, AND IT REFUSES ON WHAT IT WAS HANDED.</b> A restart of a disabled
    /// entry is an outage dressed as a round trip, so <see cref="PlanBuilder"/> will not plan one - but the
    /// window builds plans from its listing, whose start types are as old as the last full reading, because the
    /// reading every second asks only where each entry is. An entry set to Disabled by somebody else after that
    /// reading was restarted by the window: stopped, refused when it was to be started, and left stopped. And a
    /// preview can sit open for as long as somebody reads it, in either interface.
    ///
    /// <b>So the run asks once more, immediately before the first thing it does</b>, and declines the whole plan
    /// rather than carrying out the half of it that cannot be undone. It works nothing out afresh - it reads one
    /// value for each entry a step will start back, and either the plan that was shown runs or nothing does.
    ///
    /// <b>Only a start type that was READ refuses</b>, the line the builder draws: an unreadable one is missing
    /// information, and turning that into a refusal would be the opposite of what the four read outcomes are for.
    /// A plan putting nothing back - a stop, a start, a setting - asks nothing.
    /// </summary>
    private ControlAnswer? CannotComeBack(OperationPlan plan)
    {
        List<string> disabled =
        [
            .. plan.Steps
                .Where(step => step is { Reason: StepReason.Restore, Operation: StepOperation.Start })
                .Select(step => step.ServiceName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => control.ReadStartType(name) is { IsPresent: true, Value: StartType.Disabled })
        ];

        return disabled.Count > 0
            ? ControlAnswer.Refused(0, string.Concat(Listed(disabled), SetToDisabled))
            : null;
    }

    /// <summary>Said in our own words, like <see cref="ProcessMoved"/>, with the names in front.</summary>
    private const string SetToDisabled =
        " - set to Disabled, so it could not be started again, and nothing was stopped. Ask again to build a plan "
        + "against the machine as it is now.";
}
