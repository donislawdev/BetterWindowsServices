namespace Bws.Core;

/// <summary>
/// What happened when we tried to read one piece of information.
///
/// Four states, not two. Refusal is real and it is a fact about our permissions rather
/// than about the service: measured on a real machine on 2026-08-01, under a genuinely
/// restricted token, the manager refuses to open 3 of 810 entries for configuration and
/// 8 for the security descriptor. With elevation both numbers are zero.
///
/// This comment used to say 666 of 869, and that number was wrong twice over. It counted
/// registry keys rather than entries, and what it counted was not refusals: 666 of those
/// keys simply have no security value to read. The shell it was measured on was also
/// elevated, contrary to what was recorded, because the check asked whether the user was
/// in a group called "Administrators" on a machine where that group is called
/// "Administratorzy". Kept here rather than quietly replaced, because the wrong number
/// was persuasive for two months and the shape of the mistake is worth more than the
/// number that replaced it.
///
/// The reason for four states does not rest on refusal being common. Collapsing
/// <see cref="Denied"/> into <see cref="Absent"/> would make a snapshot taken without
/// elevation look like a snapshot of a machine where those values do not exist, and
/// comparing the two would report changes that never happened. That holds at eight
/// entries exactly as it held at the six hundred that were never there.
/// </summary>
public enum ReadOutcome
{
    /// <summary>Nobody has asked for it yet. Expensive data arrives in a second pass (ADR-13).</summary>
    NotRead = 0,

    /// <summary>Read succeeded and there is a value.</summary>
    Present = 1,

    /// <summary>Read succeeded and there is genuinely nothing. A fact about the service.</summary>
    Absent = 2,

    /// <summary>
    /// We asked and did not get an answer. Never a fact about the value itself.
    ///
    /// <b>The number beside it says why, and it is not always permission.</b> That sentence
    /// used to read "a fact about our permissions", which is true of nearly every case and
    /// false in one that matters: a service can be enumerated and then be gone by the time
    /// its configuration is asked for, and the manager answers 1060 rather than 5. Both
    /// arrive here, and only the number tells them apart - which is why it travels with the
    /// state instead of being flattened into a sentence.
    ///
    /// Deliberately not a fifth state. The name of this one was overclaiming, and correcting
    /// a name is cheaper than a new state that every consumer, the schema and the diff would
    /// all have to learn - especially when the thing that distinguishes the two cases is
    /// already carried and already reaches the JSON as <c>unreadable.errorCode</c>.
    /// </summary>
    Denied = 3
}

/// <summary>
/// A value together with the story of how we came by it.
/// See <see cref="ReadOutcome"/> for why the story matters as much as the value.
/// </summary>
public readonly record struct Reading<T>
{
    private Reading(ReadOutcome outcome, T? value, int errorCode, string? reason)
    {
        Outcome = outcome;
        Value = value;
        ErrorCode = errorCode;
        Reason = reason;
    }

    public ReadOutcome Outcome { get; }

    /// <summary>Meaningful only when <see cref="Outcome"/> is <see cref="ReadOutcome.Present"/>.</summary>
    public T? Value { get; }

    /// <summary>
    /// The system's own number for the refusal, or zero. Set only for
    /// <see cref="ReadOutcome.Denied"/>.
    ///
    /// Carried next to the sentence because the two are for different readers, and only one
    /// of them keeps its meaning across machines. Measured on 2026-08-01: the same refusal
    /// reads "The service cannot be started..." on one machine and "Nie można uruchomić
    /// określonej usługi..." on another, because Windows answers in the language of the
    /// machine. A script keying on the sentence works until it meets a different install.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Why the read failed, in the system's own words. Set only for
    /// <see cref="ReadOutcome.Denied"/>. For a person - see <see cref="ErrorCode"/> for the
    /// half a script should read.
    /// </summary>
    public string? Reason { get; }

    public bool IsPresent => Outcome == ReadOutcome.Present;

    public static Reading<T> NotRead() => new(ReadOutcome.NotRead, default, 0, null);

    public static Reading<T> Present(T value) => new(ReadOutcome.Present, value, 0, null);

    public static Reading<T> Absent() => new(ReadOutcome.Absent, default, 0, null);

    public static Reading<T> Denied(int errorCode, string reason) =>
        new(ReadOutcome.Denied, default, errorCode, reason);

    /// <summary>
    /// The value, or the fallback. Deliberately makes the caller name what it wants shown,
    /// so that "no value" never silently prints as an empty cell that could mean either
    /// "there is nothing" or "I was not allowed to look".
    /// </summary>
    public T? ValueOr(T? fallback) => IsPresent ? Value : fallback;
}
