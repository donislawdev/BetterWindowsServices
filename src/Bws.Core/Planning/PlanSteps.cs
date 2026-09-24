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
    /// The three argument shape of <see cref="Made"/>, for handing to somebody who builds
    /// steps of their own.
    ///
    /// <b>A method rather than a method group, because Made carries an optional fourth
    /// argument and a group with one of those converts to nothing.</b> Named once here rather
    /// than written as a lambda at each of the three call sites.
    /// </summary>
    internal static PlanStep Moving(ScmEntry entry, StepOperation operation, StepReason reason) =>
        Made(entry, operation, reason);
    /// <summary>
    /// The startup setting a way back would have to write, or nothing where there is no honest answer.
    ///
    /// <b>THE ONE PLACE THAT CAN ANSWER THIS, WHICH IS WHY IT IS ASKED WHILE THE PLAN IS BEING
    /// BUILT.</b> A step knows what it set and a result knows what came of it - the entry as it was
    /// found is only in scope here, and reading it later would be reading a machine this tool has
    /// since changed.
    ///
    /// <b>A DELAYED AUTOMATIC ENTRY GETS ITS WAY BACK SINCE 2026-09-24, and the reason it did not
    /// before is the reason it may now.</b> Until then this tool wrote the start type and left the
    /// late start flag as it found it, so a round trip made with it happened to land back on delayed
    /// automatic (three runs on a real machine, 2026-08-25) - correct, and resting on a property of
    /// the writer that no guard held. A way back is the most dangerous sentence this tool prints, and
    /// "correct as long as nobody changes the writer" was not the footing for it. Backlog 232 made a
    /// guard on both sides of the write its condition. Now the writer writes both halves of an exact
    /// <see cref="StartSetting"/> and this reads the entry through the SAME table the writer uses,
    /// <see cref="StartSettings"/> - so the way back names a value that is written, not a flag that
    /// is hoped to be left alone.
    ///
    /// <b>Nothing at all rather than a guess, in three cases, and each is a different silence.</b>
    /// The manager can refuse a configuration read, of the type or of the flag. The type can be Boot
    /// or System, which no setting writes. And the entry can carry the late flag while not being
    /// automatic - nine entries on the owner's machine, WinRM and BITS among them - which none of the
    /// four settings reproduces: every one of them writes the flag false there. The owner's decision
    /// of 2026-09-24 was no line for those rather than a line that puts back how the entry behaves
    /// and quietly drops a flag a snapshot will still see.
    /// </summary>
    internal static StartSetting? WayBackTo(ScmEntry entry) => StartSettings.Of(entry);

    internal static PlanStep Made(
        ScmEntry entry, StepOperation operation, StepReason reason, StartSetting? to = null) =>
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
