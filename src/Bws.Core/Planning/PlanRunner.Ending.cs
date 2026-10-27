namespace Bws.Core.Planning;

/// <summary>
/// The last look at a process before it is ended: who lives in it now, who outside it needs them, and - since 2026-10-26 - what its death sets off.
///
/// <b>Its own file since 2026-09-30</b> (stability report W-6, package B2, the owner's decision of 2026-09-29
/// at package B), and the size ceiling asked for the seam as well - the file beside it holds 198 lines of
/// code against a line of 202. <see cref="Unfit"/>, the check that calls the other two, came here on
/// 2026-10-27, when the file beside it stood one line of code into the crowd near the ceiling.
/// </summary>
public sealed partial class PlanRunner
{
    /// <summary>
    /// Why the process the step names must not be ended now - or nothing, when it may.
    ///
    /// <b>THE PLACE THIS CLASS LOOKS AT MOST AGAIN, AND IT IS NOT THE CLASS WORKING ANYTHING OUT
    /// AFRESH.</b> Windows hands out process numbers again once a process is gone, and
    /// a plan built one moment is carried out the next - so the number in the preview can, by the
    /// time this runs, belong to something nobody has ever heard of. Refusing on a mismatch
    /// delivers exactly the plan that was shown or nothing at all, which is the promise. Deciding
    /// to end a DIFFERENT process would be the class inventing a step, and it does not do that.
    ///
    /// <b>The window between this reading and the call is narrow and is not closed.</b> Holding the
    /// process open would close it, at the cost of a handle outliving the step, and the honest
    /// answer is that a process identity Windows hands out in one piece is what this really wants
    /// and there is not one.
    /// </summary>
    private ControlAnswer? Unfit(PlanStep step, ControlAnswer before)
    {
        var holding = Holding(before);

        // THE CREATION TIME GOES DOWN WITH THE NUMBER AND THE OTHER SIDE CHECKS IT ON THE HANDLE IT
        // KILLS WITH. This class checks that the ENTRY still names the same process, which is a
        // different question from whether the NUMBER still names the same process - Windows gives
        // numbers out again, and only something holding a handle can rule that out. So this half of
        // the identity is carried rather than compared here. Backlog 323. And the process is read for
        // who lives in it NOW before anything dies, since 2026-09-30 - Crowded says why - and for
        // what its death sets off NOW, since 2026-10-26 - Afterwards says why.
        return holding.IsPresent && holding.Value == step.ProcessId
            ? Crowded(step) ?? Afterwards(step)
            : ControlAnswer.Refused(0, ProcessMoved);
    }

    /// <summary>
    /// Said in the plainest words available, because it is the one refusal here that is ours rather
    /// than the system's - the same shape as the one <see cref="Configure"/> gives for a start type
    /// it has no number for.
    /// </summary>
    private const string ProcessMoved =
        "The process behind this entry is not the one the plan named, so nothing was ended. "
        + "Ask again to build a plan against the machine as it is now.";

    /// <summary>
    /// A refusal when the process is no longer what the plan said would die with it - or nothing, when it is.
    ///
    /// <b>THE PLAN FROZE ITS CASUALTY LIST, AND A MACHINE KEEPS MOVING BETWEEN THE PREVIEW AND THE PRESS.</b>
    /// A service can start inside a shared process after the preview, and a dependant can start outside it -
    /// until this existed the first died without a word and the second lost what it depends on, because the
    /// ending asks nobody. The polite stop in front of it did not help with the second either: the manager
    /// refuses it with 1051 while a dependant runs, and a refused stop is exactly what the ending stands behind.
    ///
    /// <b>It declines rather than works anything out afresh</b>, the way <see cref="ProcessMoved"/> does: the
    /// plan that was shown or nothing at all. Neighbours its own earlier steps stopped hold no process any more,
    /// so they are not "in" it and never look like strangers.
    ///
    /// <b>One enumeration and one question per entry the process holds</b> - 105 of 110 processes on a measured
    /// machine hold one service - asked once, immediately before the one call that cannot be undone.
    /// </summary>
    private ControlAnswer? Crowded(PlanStep step)
    {
        var statuses = control.ReadStatuses();

        if (!statuses.IsPresent)
        {
            return ControlAnswer.Refused(statuses.ErrorCode, Unchecked);
        }

        List<string> held =
        [
            .. statuses.Value!
                .Where(entry => entry.ProcessId.IsPresent && entry.ProcessId.Value == step.ProcessId)
                .Select(entry => entry.ServiceName)
        ];

        var named = new HashSet<string>(step.TakesWithIt ?? [], StringComparer.OrdinalIgnoreCase) { step.ServiceName };
        List<string> newcomers = [.. held.Where(name => !named.Contains(name))];

        return newcomers.Count > 0
            ? ControlAnswer.Refused(0, string.Concat(Listed(newcomers), MovedIn))
            : Needing(held, statuses.Value!);
    }

    /// <summary>
    /// Running entries OUTSIDE the process that depend on anything inside it. Dependants inside it die with it
    /// and are the question above, and stopped ones need nothing from a process.
    /// </summary>
    private ControlAnswer? Needing(List<string> held, IReadOnlyList<ScmStatus> statuses)
    {
        var inside = new HashSet<string>(held, StringComparer.OrdinalIgnoreCase);
        var running = new HashSet<string>(
            statuses.Where(entry => entry.Status != EntryStatus.Stopped).Select(entry => entry.ServiceName),
            StringComparer.OrdinalIgnoreCase);
        var needing = new List<string>();

        foreach (var name in held)
        {
            var dependents = control.ReadDependents(name);

            if (dependents.Outcome == ReadOutcome.Denied)
            {
                return ControlAnswer.Refused(dependents.ErrorCode, Unchecked);
            }

            needing.AddRange(dependents.IsPresent
                ? dependents.Value!.Where(dependent => running.Contains(dependent) && !inside.Contains(dependent))
                : []);
        }

        return needing.Count == 0
            ? null
            : ControlAnswer.Refused(0, string.Concat(Listed([.. needing.Distinct(StringComparer.OrdinalIgnoreCase)]), NowNeeded));
    }

    /// <summary>
    /// A refusal when what the process's death sets off is no longer what the preview said - or nothing, when it is.
    ///
    /// <b>THE PREVIEW ASKED TWO QUESTIONS OF THE PROCESS BEYOND WHO LIVES IN IT, AND UNTIL 2026-10-26 THE RUN ASKED
    /// NEITHER AGAIN</b> (logic report R05, the owner's decision "everything as in the preview"): whether Windows
    /// survives losing it, and what the manager does to each entry once it is gone. The builder refuses a critical
    /// process and a recovery list that restarts the computer, and a list changed between the preview and the
    /// press went ahead on the preview's answer - so an ending the preview called safe could restart the machine.
    ///
    /// <b>Any difference refuses, not only one that would have refused the plan.</b> A list that gained "run a
    /// program" or lost its restart makes the preview's sentences untrue, and the preview is the promise.
    ///
    /// <b>Nothing to ask is nothing said</b>, the builder's own rule: a runner built with nobody to ask - a test of
    /// something else - ends the process on what the earlier checks found, as it did before this existed.
    /// </summary>
    private ControlAnswer? Afterwards(PlanStep step)
    {
        if (processes is null)
        {
            return null;
        }

        var critical = processes.Read(step.ProcessId!.Value).Critical;

        if (critical.Outcome == ReadOutcome.Denied)
        {
            return ControlAnswer.Refused(critical.ErrorCode, CriticalUnchecked);
        }

        if (critical is { IsPresent: true, Value: true })
        {
            return ControlAnswer.Refused(0, BecameCritical);
        }

        List<string> changed =
        [
            .. (step.Recovery ?? [])
                .Where(frozen => !RecoveryList.Same(frozen.Actions, processes.ReadRecovery(frozen.ServiceName)))
                .Select(frozen => frozen.ServiceName)
        ];

        return changed.Count > 0 ? ControlAnswer.Refused(0, string.Concat(Listed(changed), RecoveryChanged)) : null;
    }

    private static string Listed(List<string> names) => string.Join(", ", names);

    /// <summary>
    /// Said in our own words, like <see cref="ProcessMoved"/>, because nothing refused it but this class. The
    /// names go in front of each sentence, as they would in a person's report of it.
    /// </summary>
    private const string MovedIn =
        " started running in this process after the plan was built, and the plan does not name them, so "
        + "nothing was ended. Ask again to build a plan against the machine as it is now.";

    private const string NowNeeded =
        " started running after the plan was built and depend on an entry living in this process, so nothing "
        + "was ended. Stop them first, or ask again.";

    private const string Unchecked =
        "Who lives in this process could not be read just before ending it, so nothing was ended. Ask again.";

    private const string CriticalUnchecked =
        "Whether Windows can survive losing this process could not be read just before ending it, so nothing was "
        + "ended. Ask again.";

    private const string BecameCritical =
        "Windows now marks this process as one it cannot run without, so nothing was ended - ending it would stop "
        + "the machine.";

    private const string RecoveryChanged =
        " - what Windows does when their process dies changed after the plan was built, so nothing was ended. Ask "
        + "again to see what ending it would set off now.";

    /// <summary>
    /// Said when the entry was seen in another process before it was seen stopped - the manager started it
    /// again at once. The ending happened, and the words say that before they say it did not last.
    /// </summary>
    private const string CameBack =
        "The process was ended, and the service was already running again in a new process before it was "
        + "seen stopped - Windows starts a service again by itself when its recovery actions say so.";
}
