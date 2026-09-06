namespace Bws.Core.Planning;

/// <summary>
/// Who else lives in one process, and whether that process is one this tool may end.
///
/// <b>Its own class rather than more of <see cref="PlanBuilder"/>, and the seam is a subject.</b>
/// Everything in that class is about what DEPENDS on what - the manager's own answer, an order
/// worked out from it, and who is in the way. This answers a different question with a different
/// source: two entries are neighbours because they report the same process number, which has
/// nothing to do with either one needing the other. <see cref="DependentsFirst"/> came out of the
/// same file on the same argument.
///
/// <b>It matters only when something is going to be ENDED.</b> Stopping a service leaves its
/// neighbours running, which is what the shared-process warning has said all along. Ending the
/// process they share is the one operation where the neighbours are not a curiosity but a casualty
/// list, and a plan that did not name them would be a preview of something smaller than what
/// happens.
/// </summary>
internal static class ProcessNeighbours
{
    /// <summary>
    /// The kernel's own process. Drivers run in it and no service entry has any business reporting
    /// it, but a reading that says so has to be refused rather than acted on.
    /// </summary>
    private const int TheKernel = 4;

    /// <summary>
    /// Every other entry reporting the same process number as this one, in the order the listing
    /// gave them.
    ///
    /// <b>Compared by number rather than by anything about the entries</b>, because that is what
    /// sharing a process IS. Two services in one svchost have nothing else in common - not a
    /// dependency, not a group, not an account.
    ///
    /// <b>Only entries whose process could be read.</b> One whose number is absent is not a
    /// neighbour and is not evidence of anything: it means the entry is not running, or that
    /// nobody got an answer, and neither is a reason to name somebody in a casualty list.
    /// </summary>
    internal static IReadOnlyList<ScmEntry> Of(IReadOnlyList<ScmEntry> entries, ScmEntry target) =>
        !target.ProcessId.IsPresent
            ? []
            : [.. entries.Where(entry =>
                entry.ProcessId.IsPresent
                && entry.ProcessId.Value == target.ProcessId.Value
                && !string.Equals(entry.ServiceName, target.ServiceName, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// The process this entry runs in, when it is one we may end, and nothing at all otherwise.
    ///
    /// <b>Four refusals, arriving as one answer, because what they share is the only thing that
    /// matters: there is no process to name in a preview.</b>
    ///
    /// <b>Absent or unread</b> - the manager did not say, or said there is none, which is ordinary
    /// for an entry already stopped. Neither is a number.
    ///
    /// <b>Zero</b> is what the manager reports for an entry that is not running. It is also a real
    /// process number belonging to the system idle process, so passing it on would turn "nothing is
    /// running this" into a request to end something that has always been there.
    ///
    /// <b>Four</b> is the kernel. No service entry reports it, and one that did would be describing
    /// a machine this tool has no business writing to.
    ///
    /// <b>Our own</b> costs one call to ask and closes a class of mistake nobody would ever see
    /// coming: a tool that ended itself half way through a plan would report nothing at all about
    /// what it had already done.
    /// </summary>
    internal static int? Endable(ScmEntry target)
    {
        if (!target.ProcessId.IsPresent)
        {
            return null;
        }

        var processId = target.ProcessId.Value;

        return processId is 0 or TheKernel || processId == Environment.ProcessId ? null : processId;
    }

    /// <summary>
    /// Entries the machine does not work without, by name.
    ///
    /// <b>A STARTER LIST AND IT SAYS SO, because a list that pretends to be complete is worse than
    /// one that admits it is not.</b> These are the entries whose process ending stops a Windows
    /// machine rather than inconveniencing it: the two halves of RPC, which nearly everything else
    /// calls into, the account manager and key isolation, which live in the process Windows treats
    /// as critical, and the session manager. There will be others on somebody else's machine.
    ///
    /// <b>Its proper home is the guarded list of section H</b> - the specification's own word for a
    /// set an administrator maintains - and this is not that. This is what can be said today
    /// without a configuration mechanism to hold it, and the glossary's pitfall `P1` is why it is
    /// worded as "you should not" rather than "you cannot": the owner decided on 2026-09-06 that
    /// these are warned about and not refused, because an administrator has the right to manage
    /// their own machine.
    ///
    /// <b>Compared without case, like every service name in this project</b> - `ADR-14`.
    /// </summary>
    private static readonly HashSet<string> WithoutTheseTheMachineStops = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs",
        "RpcEptMapper",
        "DcomLaunch",
        "SamSs",
        "KeyIso",
        "LSM",
        "PlugPlay"
    };

    /// <summary>
    /// The entries in this list that the machine does not work without, named in the order given.
    ///
    /// <b>Asked of the whole casualty list rather than of the target alone</b>, because a shared
    /// process is exactly where somebody ends up taking down a critical entry they never named.
    /// </summary>
    internal static IReadOnlyList<string> Critical(IEnumerable<ScmEntry> dying) =>
        [.. dying.Where(entry => WithoutTheseTheMachineStops.Contains(entry.ServiceName))
            .Select(entry => entry.ServiceName)];

    /// <summary>
    /// Whether this entry has already been asked to stop.
    ///
    /// <b>The plan is built after a stop that did not work, which is the only way into it from the
    /// window</b> - so the entry is sitting in a pending state, having been asked once already.
    /// A polite stop step for it would send the same request a second time and then wait the whole
    /// ceiling again, which is a minute of somebody's life spent on a question already answered.
    ///
    /// <b>Any pending state, not just the stopping one.</b> An entry on its way somewhere has a
    /// request in flight, and adding another is how a person ends up watching two.
    /// </summary>
    internal static bool AlreadyAsked(ScmEntry target) =>
        target.Status is EntryStatus.StopPending
            or EntryStatus.StartPending
            or EntryStatus.PausePending
            or EntryStatus.ContinuePending;
}
