namespace Bws.Core.Planning;

/// <summary>
/// The last look at a process before it is ended: who lives in it now, and who outside it needs them.
///
/// <b>Its own file since 2026-09-30</b> (stability report W-6, package B2, the owner's decision of 2026-09-29
/// at package B), and the size ceiling asked for the seam as well - the file beside it holds 198 lines of
/// code against a line of 202.
/// </summary>
public sealed partial class PlanRunner
{
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

    /// <summary>
    /// Said when the entry was seen in another process before it was seen stopped - the manager started it
    /// again at once. The ending happened, and the words say that before they say it did not last.
    /// </summary>
    private const string CameBack =
        "The process was ended, and the service was already running again in a new process before it was "
        + "seen stopped - Windows starts a service again by itself when its recovery actions say so.";
}
