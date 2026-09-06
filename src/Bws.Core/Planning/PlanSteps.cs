namespace Bws.Core.Planning;

/// <summary>
/// One entry plus one operation, turned into a step of a plan.
///
/// <b>Its own class since 2026-09-06, because the size ratchet asked and the seam was already there.</b>
/// <see cref="PlanBuilder"/> decides WHAT will happen - what is in the way, in what order, and what
/// cannot be done at all. This is the smaller question of how one of those becomes a value: which
/// name is the identity and which is only a label, and what a step that writes a setting has to
/// carry so there is a way back from it.
///
/// <b>Shared rather than duplicated</b>, because <see cref="ForcedStop"/> builds steps too and two
/// answers to "which name goes on a step" would be two things obliged to agree about identity.
/// </summary>
internal static class PlanSteps
{
    /// <summary>
    /// The three argument shape of <see cref="Step"/>, for handing to somebody who builds
    /// steps of their own.
    ///
    /// <b>A method rather than a method group, because Step carries an optional fourth
    /// argument and a group with one of those converts to nothing.</b> Named once here rather
    /// than written as a lambda at each of the three call sites.
    /// </summary>
    internal static PlanStep Moving(ScmEntry entry, StepOperation operation, StepReason reason) =>
        Made(entry, operation, reason);
    /// <summary>
    /// The start type a way back would have to write, or nothing where there is no honest answer.
    ///
    /// <b>THE ONE PLACE THAT CAN ANSWER THIS, WHICH IS WHY IT IS ASKED WHILE THE PLAN IS BEING
    /// BUILT.</b> A step knows what it set and a result knows what came of it - the entry as it was
    /// found is only in scope here, and reading it later would be reading a machine this tool has
    /// since changed.
    ///
    /// <b>Nothing at all rather than a guess, in three cases, and each is a different silence.</b>
    /// The manager can refuse a configuration read. The type can be one this tool has no word for.
    /// And an automatic entry can carry the delay flag, which is the case worth spelling out:
    ///
    /// <b>An automatic entry may also be marked to start late, and nothing this tool writes can say
    /// "automatic, and late".</b> The delay is a separate field on the entry rather than a sixth
    /// start type, and 13 of 78 automatic services carry it on a real machine - measured
    /// 2026-08-01.
    ///
    /// <b>MEASURED ON A REAL MACHINE 2026-08-25, THREE WAYS, AND IT REFUTED THE PREDICTION WRITTEN
    /// BEFORE THE RUN AND THEN REFUTED THE SENTENCE THAT REPLACED IT.</b> On a service made delayed
    /// automatic and read with sc.exe after every step:
    ///
    /// <list type="bullet">
    /// <item>our write to manual, then sc.exe to auto - plain automatic. The flag is gone.</item>
    /// <item>sc.exe to demand, then our write to automatic - plain automatic. Gone again.</item>
    /// <item>our write to manual, then our write to automatic - AUTOMATIC (DELAYED), twice.</item>
    /// </list>
    ///
    /// So sc.exe clears the flag on every start type it writes and this tool never touches it -
    /// which is exactly what WindowsScmControl.Configure promises in as many words, and it means a
    /// round trip made entirely with this tool DOES land back on delayed automatic.
    ///
    /// <b>The way back is refused here anyway, and that is a decision rather than the measurement.</b>
    /// It would be correct today and it would rest on a property of our own write that no guard
    /// holds - one line changed in Configure and every one of those lines becomes a claim that
    /// quietly stopped being true. A way back is the most dangerous sentence this tool prints, and
    /// "correct as long as nobody changes the writer" is not the footing for it. Silence costs
    /// somebody one manual step. The alternative costs them a machine that comes up differently
    /// from the way they left it.
    ///
    /// <b>Said out loud because it is the owner's to decide, not mine:</b> the measurement says
    /// this could be offered, and beside it sits a larger question - our Automatic and the Automatic
    /// in services.msc are not the same write. Backlog 231 and 232.
    ///
    /// A reading that is present and false is the only one that clears it. Denied and never read
    /// both mean nobody knows, and this is not the place to decide that nobody knows means no.
    /// </summary>
    internal static StartType? WayBackTo(ScmEntry entry)
    {
        if (!entry.StartType.IsPresent)
        {
            return null;
        }

        var was = entry.StartType.Value;

        return was == StartType.Automatic && entry.DelayedAuto is not { IsPresent: true, Value: false }
            ? null
            : was;
    }
    internal static PlanStep Made(
        ScmEntry entry, StepOperation operation, StepReason reason, StartType? to = null) =>
        new(
            entry.ServiceName,

            // A step carries the internal name as its identity and this only as its label, so the
            // rule applies here for the same reason it applies in a listing - see
            // ServiceDisplayName. Nothing keys, matches or compares on this text.
            ServiceDisplayName.Of(entry.DisplayName, entry.ServiceName),
            operation,
            reason,
            to,
            operation == StepOperation.SetStartType ? WayBackTo(entry) : null);
}
