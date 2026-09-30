namespace Bws.Core.Planning;

/// <summary>
/// What ending a process sets off after it is gone - the machine stopping, or the manager doing what each
/// dead entry's recovery list tells it to - and whether a plan may be built over that.
///
/// <b>Its own class since 2026-09-30</b> (stability report W-3, package B2), and the seam is a subject rather
/// than the size ceiling alone. <see cref="ForcedStop"/> works out what the ending takes DOWN. This answers
/// what comes back up, or goes further down, once nothing of the process is left - questions the first
/// half never had to ask, because until that day this tool believed a dead service stayed dead. Measured on
/// the throwaway machine the same day: the manager restarted it in ten endings of ten.
///
/// <b>Asked of the entry and every neighbour, never of the cascade.</b> A cascade entry gets a polite stop,
/// and one that does not stop holds the ending back altogether (the owner's decision of 2026-09-29) - so
/// nothing in the cascade ever dies with the process, and its recovery list is never set off.
/// </summary>
internal static class Aftermath
{
    /// <summary>One entry that dies with the process, and its recovery list as the manager answered.</summary>
    internal readonly record struct Recovered(string ServiceName, Reading<IReadOnlyList<RecoveryAction>> Actions);

    /// <summary>
    /// The last question before a plan that ends a process exists - what the manager does to the dead once
    /// the process is gone, which can still refuse it - and the ending it describes when it does not.
    ///
    /// <b>Here rather than at the end of <see cref="ForcedStop.Decide"/></b>, whose file stood 14 lines of
    /// code under the size ceiling's line when the question arrived on 2026-09-30. The ending is built here
    /// for the same reason: it is the first thing that can only exist once this question said yes.
    /// </summary>
    internal static (PlanProblem? Refusal, ForcedStop.Ending? Ending) Weigh(
        IEndingFactsReader processes,
        EndingFacts facts,
        ScmEntry target,
        IReadOnlyList<ScmEntry> sharing,
        ServiceAction action,
        int processId)
    {
        var recovery = Read(processes, target, sharing);

        if (Refusal(target, recovery) is { } refused)
        {
            return (refused, null);
        }

        return (null, new ForcedStop.Ending(
            processId,
            sharing,
            action.Immediate,
            facts.Created.IsPresent ? facts.Created.Value : null,
            recovery));
    }

    /// <summary>The entry first and its neighbours in the order the listing gave them - the order every sentence names them in.</summary>
    private static IReadOnlyList<Recovered> Read(
        IEndingFactsReader processes, ScmEntry target, IReadOnlyList<ScmEntry> sharing) =>
    [
        .. sharing.Prepend(target)
            .Select(entry => new Recovered(entry.ServiceName, processes.ReadRecovery(entry.ServiceName)))
    ];

    /// <summary>
    /// Whether the process is one Windows will not survive losing - asked from the same reading as the right
    /// to end it, so it is refused as early as that refusal is.
    ///
    /// <b>Unreadable is a refusal too, and nothing read is nothing said.</b> The first is the owner's rule of
    /// 2026-09-30 for this step: no preview known to be missing a consequence. The second is the plan built
    /// with nobody to ask, which behaves exactly as it did before this question existed.
    /// </summary>
    internal static PlanProblem? Critical(EndingFacts facts, ScmEntry target) => facts.Critical.Outcome switch
    {
        ReadOutcome.Present when facts.Critical.Value => new PlanProblem(PlanProblemKind.ProcessIsCritical, target.ServiceName, []),
        ReadOutcome.Denied => new PlanProblem(PlanProblemKind.AftermathUnreadable, target.ServiceName, []),
        _ => null
    };

    /// <summary>
    /// A computer restart anywhere in a list, then a list that could not be read - in that order, because the
    /// first is a fact about the machine and the second is only the absence of one.
    /// </summary>
    private static PlanProblem? Refusal(ScmEntry target, IReadOnlyList<Recovered> recovery)
    {
        var restarting = Having(recovery, RecoveryAction.RestartComputer);

        if (restarting.Count > 0)
        {
            return new PlanProblem(PlanProblemKind.RecoveryRestartsComputer, target.ServiceName, restarting);
        }

        List<string> unread = [.. recovery.Where(one => one.Actions.Outcome == ReadOutcome.Denied).Select(one => one.ServiceName)];

        return unread.Count > 0 ? new PlanProblem(PlanProblemKind.AftermathUnreadable, target.ServiceName, unread) : null;
    }

    /// <summary>
    /// The three sentences a plan that is allowed can still owe somebody: who comes back, who sets a program
    /// off, and who carries an item this tool has no name for.
    /// </summary>
    internal static void AddWarnings(List<PlanWarning> warnings, ScmEntry target, IReadOnlyList<Recovered> recovery)
    {
        Warn(warnings, target, Having(recovery, RecoveryAction.RestartService), PlanWarningKind.RecoveryRestarts);
        Warn(warnings, target, Having(recovery, RecoveryAction.RunProgram), PlanWarningKind.RecoveryRunsProgram);
        Warn(warnings, target, Having(recovery, RecoveryAction.Unnamed), PlanWarningKind.RecoveryUnnamed);
    }

    private static void Warn(List<PlanWarning> warnings, ScmEntry target, List<string> named, PlanWarningKind kind)
    {
        if (named.Count > 0)
        {
            warnings.Add(new PlanWarning(kind, target.ServiceName, named));
        }
    }

    /// <summary>
    /// Every entry whose list holds this item ANYWHERE - the manager runs item N for failure N since the
    /// machine started, and no call hands out N. Absent and unread lists hold nothing.
    /// </summary>
    private static List<string> Having(IReadOnlyList<Recovered> recovery, RecoveryAction action) =>
        [.. recovery.Where(one => one.Actions.IsPresent && one.Actions.Value!.Contains(action)).Select(one => one.ServiceName)];
}
