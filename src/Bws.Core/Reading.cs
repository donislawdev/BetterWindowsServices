namespace Bws.Core;

/// <summary>
/// What happened when we tried to read one piece of information.
///
/// Four states, not two. Measured on a real machine on 2026-07-31: reading the
/// security descriptor was refused for 666 of 869 entries on a shell without
/// administrator rights. Refusal is the normal case, not an edge case, and it is
/// a fact about our permissions rather than a fact about the service.
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
    private Reading(ReadOutcome outcome, T? value, string? reason)
    {
        Outcome = outcome;
        Value = value;
        Reason = reason;
    }

    public ReadOutcome Outcome { get; }

    /// <summary>Meaningful only when <see cref="Outcome"/> is <see cref="ReadOutcome.Present"/>.</summary>
    public T? Value { get; }

    /// <summary>Why the read failed. Set only for <see cref="ReadOutcome.Denied"/>.</summary>
    public string? Reason { get; }

    public bool IsPresent => Outcome == ReadOutcome.Present;

    public static Reading<T> NotRead() => new(ReadOutcome.NotRead, default, null);

    public static Reading<T> Present(T value) => new(ReadOutcome.Present, value, null);

    public static Reading<T> Absent() => new(ReadOutcome.Absent, default, null);

    public static Reading<T> Denied(string reason) => new(ReadOutcome.Denied, default, reason);

    /// <summary>
    /// The value, or the fallback. Deliberately makes the caller name what it wants shown,
    /// so that "no value" never silently prints as an empty cell that could mean either
    /// "there is nothing" or "I was not allowed to look".
    /// </summary>
    public T? ValueOr(T? fallback) => IsPresent ? Value : fallback;
}
