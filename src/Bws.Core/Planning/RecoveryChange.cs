namespace Bws.Core.Planning;

/// <summary>
/// The parts of an entry's recovery that one write can change while leaving the others alone - the Recovery
/// tab of services.msc, cut where the manager's own call cuts it.
///
/// <b>Five parts and not four, although the manager writes the list and its reset period as one.</b> A person
/// can ask for either alone - "restart it after a minute" says nothing about the period, and "forget the
/// failures after a day" says nothing about the list - and the owner's decision of 2026-11-24 keeps every part
/// nobody named as it is. So the ASK names the two apart, and the step a plan builds from it names both
/// whenever either is touched, with the other half read off the entry (<see cref="RecoveryPlan"/>).
///
/// <b>The API is what makes the parts real rather than a convenience.</b> Microsoft's page on
/// <c>SERVICE_FAILURE_ACTIONSW</c> says a null command or message is left as it is and an empty one removed,
/// and a null list leaves the list and the period alone - measured on the throwaway machine on 2026-11-24
/// (<c>tools/vm-service/recovery-write-probe.ps1</c>), each one landed without touching the others. The flag
/// is another information level, so another call.
/// </summary>
[Flags]
public enum RecoveryParts
{
    /// <summary>Nothing - an ask that changes no part is refused before it is planned.</summary>
    None = 0,

    /// <summary>The items of the list, in order: what each failure does and after how long.</summary>
    Actions = 1,

    /// <summary>How long without a failure before the manager counts from 1 again.</summary>
    ResetPeriod = 2,

    /// <summary>The program an item that runs a program runs.</summary>
    Command = 4,

    /// <summary>The message broadcast before an item restarts the computer.</summary>
    RebootMessage = 8,

    /// <summary>Whether the list also answers a stop with an error rather than only a crash.</summary>
    OnNonCrashFailures = 16
}

/// <summary>
/// What a recovery write puts on the machine: which parts, and the value of each.
///
/// <b>The values travel as a whole <see cref="RecoverySettings"/>, and only the parts named are read from
/// it.</b> That shape is what makes the way back cost nothing to say: undoing a change of these parts is a
/// change of the SAME parts to the values the entry had, which is the same type with the reading taken before
/// the plan in place of the values asked for. A command or a message of null, in a part that is named,
/// means "remove it" - the reading's own word for none, which is what the manager hands back after removal.
/// </summary>
/// <param name="Parts">Which parts this write changes. Never <see cref="RecoveryParts.None"/> on a plan.</param>
/// <param name="To">The values. Those of parts not named are not read by anything.</param>
public sealed record RecoveryChange(RecoveryParts Parts, RecoverySettings To)
{
    /// <summary>Whether this write changes that part - every bit of it, for a combination.</summary>
    public bool Writes(RecoveryParts part) => part != RecoveryParts.None && (Parts & part) == part;

    /// <summary>
    /// The settings an entry has once this is written over what it had: the named parts from this, the rest
    /// from <paramref name="before"/>.
    /// </summary>
    public RecoverySettings Over(RecoverySettings before)
    {
        ArgumentNullException.ThrowIfNull(before);

        var list = new RecoveryList(
            Writes(RecoveryParts.Actions) ? To.List.Items : before.List.Items,
            Writes(RecoveryParts.ResetPeriod) ? To.List.ResetPeriod : before.List.ResetPeriod)
        {
            Command = Writes(RecoveryParts.Command) ? To.List.Command : before.List.Command,
            RebootMessage = Writes(RecoveryParts.RebootMessage) ? To.List.RebootMessage : before.List.RebootMessage
        };

        return new RecoverySettings(
            list, Writes(RecoveryParts.OnNonCrashFailures) ? To.OnNonCrashFailures : before.OnNonCrashFailures);
    }

    /// <summary>
    /// Whether two settings agree on every part this write names - "already there" before a plan, and "nobody
    /// changed it since" just before the write (the logic report's R05, applied to a write).
    /// </summary>
    public bool Agrees(RecoverySettings first, RecoverySettings second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return (!Writes(RecoveryParts.Actions) || first.List.Items.SequenceEqual(second.List.Items))
            && (!Writes(RecoveryParts.ResetPeriod) || first.List.ResetPeriod == second.List.ResetPeriod)
            && (!Writes(RecoveryParts.Command) || string.Equals(first.List.Command, second.List.Command, StringComparison.Ordinal))
            && (!Writes(RecoveryParts.RebootMessage)
                || string.Equals(first.List.RebootMessage, second.List.RebootMessage, StringComparison.Ordinal))
            && (!Writes(RecoveryParts.OnNonCrashFailures) || first.OnNonCrashFailures == second.OnNonCrashFailures);
    }

    /// <summary>
    /// The change that puts the named parts back to what <paramref name="before"/> holds - the way back, or
    /// nothing when this write changed none of them.
    /// </summary>
    public RecoveryChange? Undoing(RecoverySettings before)
    {
        ArgumentNullException.ThrowIfNull(before);

        return Agrees(before, To) ? null : this with { To = before };
    }

    /// <summary>The parts named, one by one, in the order of the enumeration - for a document and a sentence.</summary>
    public IReadOnlyList<RecoveryParts> Named() =>
        [.. Enum.GetValues<RecoveryParts>().Where(Writes)];

    /// <summary>
    /// Settings with nothing in them - no item, no program, no message, the flag off. What a list becomes when
    /// every part is removed, and the starting point a form fills in.
    /// </summary>
    public static RecoverySettings Nothing { get; } = new(new RecoveryList([], TimeSpan.Zero), false);
}

/// <summary>
/// One step's write, and what the entry had when the plan was built.
///
/// <b>The second half is what <see cref="PlanStep.From"/> is for a start type</b>: a step knows what it writes
/// and a result knows what came of it, and neither knows what was replaced - so it is read while the plan is
/// built and frozen here. It is also what the run compares against immediately before writing, so a list
/// somebody changed between the preview and the press is not written over unseen.
/// </summary>
/// <param name="Change">What the step writes - the parts resolved, both halves of the list when either is.</param>
/// <param name="From">
/// The whole recovery the entry had, flag included, or nothing when it could not be read - and then there is
/// no way back and nothing to compare, which the plan's sentences say.
/// </param>
public sealed record RecoveryWrite(RecoveryChange Change, RecoverySettings? From);
