namespace Bws.Core.Planning;

/// <summary>
/// What a plan that starts an entry says when what was read already names the manager's refusal.
///
/// <b>Added 2026-09-30, stability report W-7 and W-8, on the owner's decision of that day.</b> A start
/// of a disabled entry and a start of a paused one are both refused by the manager every time, and
/// until that day both plans were one step with nothing under it - the refusal arrived as a number
/// after the button was pressed.
///
/// <b>A class of its own for the same reason as <see cref="StartTypeWarnings"/></b>: the builder's
/// warning method stands near its ceilings, and the subject is its own - everything there is about
/// what a plan does, and this is about a start the machine is known to turn down.
///
/// <b>Only a start - which since 2026-09-30 includes a restart of an entry read as stopped</b>, planned as
/// a start (<c>PlanBuilder.AsPlanned</c>). A restart of a running disabled entry is refused outright before
/// any warning is worked out (<c>PlanProblemKind.CannotComeBack</c>), and a restart of a paused one stops
/// it first, which a paused service accepts.
/// </summary>
internal static class RefusedStartWarnings
{
    internal static void Add(List<PlanWarning> warnings, ScmEntry target, ServiceAction action)
    {
        if (action.Kind != ActionKind.Start)
        {
            return;
        }

        // Stopped or on its way there - an entry already starting is waited for rather than asked, and
        // one that is running needs no start at all. Only where the start type was READ, because an
        // unreadable one is not "disabled".
        if (target.StartType is { IsPresent: true, Value: StartType.Disabled }
            && target.Status is EntryStatus.Stopped or EntryStatus.StopPending)
        {
            warnings.Add(new PlanWarning(PlanWarningKind.DisabledCannotStart, target.ServiceName));
        }

        if (target.Status is EntryStatus.Paused or EntryStatus.PausePending)
        {
            warnings.Add(new PlanWarning(PlanWarningKind.PausedCannotStart, target.ServiceName));
        }
    }
}
