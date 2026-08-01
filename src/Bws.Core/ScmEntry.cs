namespace Bws.Core;

/// <summary>
/// What an entry is technically. Windows keeps services and drivers in the same
/// place and returns them from the same call, so "service" alone is never a safe
/// word for the whole set. See the glossary, pitfall P6.
/// </summary>
public enum EntryType
{
    Unknown = 0,
    KernelDriver,
    FileSystemDriver,

    /// <summary>Runs in a process of its own.</summary>
    OwnProcess,

    /// <summary>Shares a process with other services, usually svchost.</summary>
    SharedProcess
}

/// <summary>
/// What the entry is doing right now. Not to be confused with <see cref="StartType"/>,
/// which is about the next boot. Glossary pitfall P3: an entry can be Automatic and
/// Stopped at the same time, and that combination is the silent failure we exist to
/// surface.
/// </summary>
public enum EntryStatus
{
    Unknown = 0,
    Stopped,
    StartPending,
    StopPending,
    Running,
    ContinuePending,
    PausePending,
    Paused
}

/// <summary>
/// What the system will do with the entry at the next boot. Not the current state.
/// </summary>
public enum StartType
{
    Unknown = 0,
    Boot,
    System,
    Automatic,
    Manual,
    Disabled
}

/// <summary>
/// One entry in the service control manager: a service or a driver.
///
/// Field names follow the binding "w kodzie" column of the glossary. They are a
/// public contract because they surface in the CLI JSON output and later in the
/// snapshot format, so renaming one is a breaking change, not a tidy-up.
///
/// Everything here is cheap to read: it comes from enumerating the manager plus one
/// configuration query. Expensive data (signatures, permissions, triggers, memory)
/// belongs to a second pass and is not modelled yet.
/// </summary>
public sealed record ScmEntry
{
    /// <summary>The immutable identity. Case-insensitive to compare, case-preserving to show.</summary>
    public required string ServiceName { get; init; }

    /// <summary>Label for a human. Translated to the system language, never an identity.</summary>
    public required string DisplayName { get; init; }

    public required EntryType EntryType { get; init; }

    public required EntryStatus Status { get; init; }

    /// <summary>
    /// Process holding the entry, when one is running. Absent rather than zero when
    /// the entry is stopped, because zero is a value and "not running" is not.
    /// </summary>
    public required Reading<int> ProcessId { get; init; }

    /// <summary>Comes from the configuration query, which can be refused.</summary>
    public required Reading<StartType> StartType { get; init; }

    /// <summary>
    /// Whether an automatic entry starts late, after the boot rush.
    ///
    /// A separate field rather than a sixth <see cref="Core.StartType"/> value, and the
    /// reason is the refusal case. The manager answers the start type and the delay flag
    /// through two different calls, so "automatic, and I could not find out whether it is
    /// delayed" is a state that happens. One enumeration holds one value and would have to
    /// report that as plain automatic, which is a confident answer about something nobody
    /// read. Measured on a real machine on 2026-08-01: 13 of 78 automatic services carry
    /// the flag, and sc.exe reports both kinds as start type 2.
    ///
    /// Absent on anything that is not automatic, where the idea does not apply. Absent is
    /// not false: false would claim the question was asked and answered.
    /// </summary>
    public required Reading<bool> DelayedAuto { get; init; }

    /// <summary>
    /// Account the entry runs as, as text. Translated on non-English systems, so it is
    /// a label and never an identity (ADR-14). The account SID belongs to the expensive
    /// pass and is not read here.
    /// </summary>
    public required Reading<string> Account { get; init; }

    /// <summary>
    /// What this entry declares it needs before it can run.
    ///
    /// Free: it arrives in the same configuration structure the start type and the account
    /// come from, so reading it costs no extra call. Measured on a real machine on
    /// 2026-08-01: 210 of 339 services declare at least one, the longest list has five.
    ///
    /// Names are kept exactly as the manager returns them, including the leading plus that
    /// marks a load order group rather than a service. RemoteAccess on that machine
    /// declares "+NetBIOSGroup" alongside four ordinary services. Stripping the marker
    /// would turn a group into a service that does not exist.
    ///
    /// This is what the entry declares, which is not the same question as what would break
    /// if it stopped. The manager answers that one itself, and the cascade will ask it
    /// rather than inverting this list, because the manager also knows which services are
    /// in which group and this list does not.
    /// </summary>
    public required Reading<IReadOnlyList<string>> DependsOn { get; init; }

    /// <summary>
    /// True for a name in <see cref="DependsOn"/> that names a load order group rather
    /// than a service. Stopping one member of a group does not necessarily break anything
    /// that depends on the group, so the two cannot be treated alike when planning.
    /// </summary>
    public static bool IsGroup(string dependency) =>
        dependency.StartsWith('+');

    public bool IsDriver => EntryType is EntryType.KernelDriver or EntryType.FileSystemDriver;

    /// <summary>
    /// The single most useful derived fact in the whole tool: the entry is supposed to
    /// be running and is not. Measured on a real machine, ten entries answered this on
    /// a normal workstation.
    ///
    /// Unknown when the start type could not be read, because "I could not check"
    /// must never render as "everything is fine".
    /// </summary>
    public Reading<bool> RunsAgainstItsStartType =>
        StartType.Outcome switch
        {
            ReadOutcome.Present => Reading<bool>.Present(
                StartType.Value == Core.StartType.Automatic && Status != EntryStatus.Running),
            // Passes the refusal on whole, number and sentence together. A refusal always
            // carries both, so there is nothing here to invent - and inventing a sentence
            // was what the old fallback did, in English, in code.
            ReadOutcome.Denied => Reading<bool>.Denied(StartType.ErrorCode, StartType.Reason!),
            _ => Reading<bool>.NotRead()
        };
}
