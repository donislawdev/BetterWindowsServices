namespace Bws.Core.Planning;

/// <summary>
/// What a startup setting does NOT do, said on the plan that changes one.
///
/// <b>UX-GUI-006, and spec C4 calls it the critical detail:</b> a startup setting changes what
/// happens at the next boot and nothing now. Somebody who disables a running service has left it
/// running, and somebody who makes a stopped one automatic has not started it. Until 2026-09-24 the
/// plan said neither, while the plan to stop an automatic service has always said the mirror of the
/// first ("it will be back after the next restart").
///
/// <b>A class of its own because the builder's warning method stands at the complexity ceiling</b>,
/// the same seam <see cref="CriticalEntries.AddWarnings"/> already is. The subject is also its own:
/// everything else there is about what the plan does, and this is about what it leaves alone.
///
/// <b>Only where the state was READ.</b> An entry whose state the manager did not give is neither
/// running nor stopped as far as anybody knows, and a sentence about it would be a claim - the same
/// line the builder draws for an entry that does not accept a stop.
/// </summary>
internal static class StartTypeWarnings
{
    internal static void Add(List<PlanWarning> warnings, ScmEntry target, ServiceAction action)
    {
        if (action is not { Kind: ActionKind.SetStartType, To: { } setting }
            || target.Status == EntryStatus.Unknown)
        {
            return;
        }

        // Disabled only, not Manual. Spec C4 and glossary pitfall P7 are both about disabling, and
        // "a manual service keeps running" is the obvious reading that needs no sentence. A plan
        // that already stops the entry has nothing to warn about - that is what the offer was for.
        if (setting == StartSetting.Disabled
            && target.Status != EntryStatus.Stopped
            && !PlanBuilder.StopsAlong(action, target))
        {
            warnings.Add(new PlanWarning(PlanWarningKind.KeepsRunning, target.ServiceName));
        }

        // Also where the entry is already set that way. The sentence describes the machine after the
        // plan, and it is true whether or not the plan changes the setting - a stopped automatic
        // service is exactly the symptom this tool's listing exists to find.
        if (StartSettings.StartsAtBoot(setting) && target.Status == EntryStatus.Stopped)
        {
            warnings.Add(new PlanWarning(PlanWarningKind.StartsAtNextBoot, target.ServiceName));
        }
    }
}
