namespace Bws.Core.Planning;

/// <summary>
/// The entries this machine does not work without, and what a plan touching one has to say.
///
/// <b>ITS OWN FILE SINCE 2026-09-09, AND BEFORE THAT THE CONCEPT WAS SPLIT ACROSS TWO CLASSES
/// THAT BOTH HAD IT AS A SIDELINE.</b> The list lived in <see cref="ProcessNeighbours"/>, whose
/// subject is who shares a process - a fact about how Windows packed the machine, unrelated to
/// whether the machine needs anything. The sentence-raising lived in <see cref="PlanBuilder"/>,
/// whose subject is what a plan will do. Neither owned this, so the question "what else reaches
/// these names" had nowhere to be asked - which is how the ordinary stop went three days without
/// the warning that already existed for the forcing one.
///
/// <b>The size ratchet asked for the split and this is where it pointed</b>, both files having
/// grown past the ceiling in one change. That the two pieces the ratchet wanted out were halves
/// of one subject is the argument for putting them together rather than in two new files.
///
/// <b>ITS PROPER HOME IS THE GUARDED LIST OF SECTION H - the specification's own word for a set
/// an administrator maintains - and this is not that.</b> Backlog 324 carries the gap. What this
/// file buys towards it is a place for that mechanism to arrive: today the names are a constant,
/// and the day they come from configuration, one class changes rather than three.
/// </summary>
internal static class CriticalEntries
{
    /// <summary>
    /// Entries the machine does not work without, by name.
    ///
    /// <b>A STARTER LIST AND IT SAYS SO, because a list that pretends to be complete is worse than
    /// one that admits it is not.</b> These are the entries whose stopping stops a Windows machine
    /// rather than inconveniencing it: the two halves of RPC, which nearly everything else calls
    /// into, the account manager and key isolation, which live in the process Windows treats as
    /// critical, the session manager, and plug and play. There will be others on somebody else's
    /// machine.
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
    /// process, and a cascade, are both places where somebody ends up taking down a critical entry
    /// they never named.
    /// </summary>
    internal static IReadOnlyList<string> Named(IEnumerable<ScmEntry> affected) =>
        [.. affected.Where(entry => WithoutTheseTheMachineStops.Contains(entry.ServiceName))
            .Select(entry => entry.ServiceName)];

    /// <summary>
    /// Says so on an ordinary plan - the one somebody reaches first.
    ///
    /// <b>UNTIL 2026-09-09 NOTHING SAID THIS EXCEPT A PLAN THAT ENDS A PROCESS.</b> The list, the
    /// sentence and the reason for all of it existed since 2026-09-06 and <see cref="ForcedStop"/>
    /// was the only caller - so <c>stop PlugPlay</c> built a plan whose one warning was about a
    /// shared process. Found by an audit reading four such plans rather than by a test, because
    /// nothing was asserting an absence nobody had noticed.
    ///
    /// <b>NOT CALLED FOR THE FORCING KINDS, and that is not tidiness.</b>
    /// <see cref="ForcedStop.AddWarnings"/> raises <see cref="PlanWarningKind.CriticalService"/>
    /// for those, so including them here would put the sentence on such a plan twice. The split is
    /// by kind rather than by a flag because the two callers ask different questions: this one asks
    /// who the plan STOPS, that one asks who dies along with a process.
    ///
    /// <b>A warning rather than a refusal, on the owner's decision of 2026-09-06.</b> An
    /// administrator has the right to manage their own machine, which is the line `R2` of the
    /// specification already draws. The window makes its own decision about how heavy a
    /// confirmation to ask for - see <c>Planned.NeedsTyping</c> - and that is the window's, not
    /// this.
    /// </summary>
    internal static void AddWarnings(
        List<PlanWarning> warnings,
        ScmEntry target,
        ServiceAction action,
        IReadOnlyList<ScmEntry> cascade)
    {
        if (PlanBuilder.StopsPolitely(action, target))
        {
            var stopping = Named([.. cascade, target]);

            if (stopping.Count > 0)
            {
                warnings.Add(new PlanWarning(
                    PlanWarningKind.CriticalService, target.ServiceName, stopping));
            }
        }

        // DISABLING ONE IS THE SAME HARM ARRIVING LATER, AND LATER IS WHY IT IS A SEPARATE KIND.
        // Owner's decision of 2026-09-09, taken with the wiring above. Nothing moves today, the
        // manager stops starting the entry at all - including on demand for something else that
        // needs it - and the machine finds out at the next boot, which is the reboot nobody is
        // watching.
        //
        // The target alone, because a start type has no cascade: the blocking list is empty for
        // this kind by construction in PlanBuilder unless a stop rides on the setting, and even then
        // what is DISABLED is the target and nothing else. Building this from the cascade would read
        // as though there were one to consider. Manual and automatic are deliberately not warned about
        // - `docs/03` pitfall `P1` keeps "you should not" for the thing that stops the entry
        // starting at all, and a warning on every start type change is one nobody finishes reading.
        if (action.Kind == ActionKind.SetStartType && action.To == StartSetting.Disabled)
        {
            var disabling = Named([target]);

            if (disabling.Count > 0)
            {
                warnings.Add(new PlanWarning(
                    PlanWarningKind.CriticalStartType, target.ServiceName, disabling));
            }
        }
    }
}
