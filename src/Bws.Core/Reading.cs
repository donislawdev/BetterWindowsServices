namespace Bws.Core;

/// <summary>
/// What happened when we tried to read one piece of information.
///
/// Four states, not two. Measured on a real machine on 2026-07-31: reading the
/// security descriptor from the registry was refused for 666 of the 869 keys a script
/// walked, on a shell without administrator rights. Refusal is a normal case rather
/// than an edge case, and it is a fact about our permissions, not about the service.
///
/// That 869 is a count of registry keys and not of entries. The manager reports around
/// 810, and the two are counted by different means over different things - said here
/// because the number sitting next to the word "entries" is exactly how the two get
/// confused. Configuration read through the manager, which is what this tool does, was
/// refused zero times on that same machine.
///
/// Collapsing <see cref="Denied"/> into <see cref="Absent"/> would make a snapshot
/// taken without elevation look like a snapshot of a machine where those values do
/// not exist. Comparing it against an elevated one would then report hundreds of
/// changes that never happened.
/// </summary>
public enum ReadOutcome
{
    /// <summary>Nobody has asked for it yet. Expensive data arrives in a second pass (ADR-13).</summary>
    NotRead = 0,

    /// <summary>Read succeeded and there is a value.</summary>
    Present = 1,

    /// <summary>Read succeeded and there is genuinely nothing. A fact about the service.</summary>
    Absent = 2,

    /// <summary>Read failed. A fact about our permissions, not about the service.</summary>
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
