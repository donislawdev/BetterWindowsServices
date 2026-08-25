namespace Bws.Core;

// The vocabulary an SCM entry is described in: what it is, what it is doing, how it starts,
// how hard the system takes it failing, and whose session it belongs to.
//
// Split out of ScmEntry.cs on 2026-08-25, forced by the size ratchet rather than chosen.
// That file stood at exactly 500 lines, which is one line below the point where the guard
// counts a file as long, and two of the two allowed long files were already spent - so the
// per-user role below could not be added there without reddening the build first.
//
// The seam it landed on was already here: everything above the record is vocabulary, and the
// record is a row of data. The same split has been made before in this tree for the same
// reason, and the note in SizeRatchetGuards lists the earlier ones.

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
/// How hard the system takes it when this entry fails to start during boot.
///
/// About the boot sequence and nothing else. A service that fails at three in the
/// afternoon is not covered by any of this - which is the confusion the glossary warns
/// about, because the word "critical" invites reading it as "this service is important".
/// It means "if this does not start at boot, stop booting".
/// </summary>
public enum ErrorControl
{
    Unknown = 0,

    /// <summary>Carry on and say nothing.</summary>
    Ignore,

    /// <summary>Carry on and log it. What almost everything uses.</summary>
    Normal,

    /// <summary>Restart with the last known good configuration, or carry on if already using it.</summary>
    Severe,

    /// <summary>Restart with the last known good configuration, or fail the boot.</summary>
    Critical
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
/// Whether the entry gets an identity of its own, so that rights can be given to the
/// service rather than to the account it happens to run as.
///
/// Three values where Windows has four, and the missing one is the point. The manager
/// reports NONE for an entry with no identity of its own, which is not a kind of identity -
/// it is the absence of one. That is <see cref="ReadOutcome.Absent"/>, so it is expressed
/// there rather than here, and <c>sidtype:none</c> in the query language then means what
/// <c>none</c> means for every other field instead of being one enumeration's private word.
///
/// Measured on a real machine on 2026-08-01, agreeing with <c>sc qsidtype</c> on all four
/// entries it was checked against by hand: 250 services unrestricted, 11 restricted, 78
/// with none, and all 471 drivers with none - drivers have no token for a SID to go into.
/// </summary>
public enum ServiceSidType
{
    Unknown = 0,

    /// <summary>The service has a SID and it is in the token like any other group.</summary>
    Unrestricted,

    /// <summary>
    /// The service has a SID and the token is write-restricted to it, which is the stronger
    /// of the two: the process can only write where that SID is allowed.
    /// </summary>
    Restricted
}

/// <summary>
/// Whether the entry belongs to the per-user family, and on which side of it.
///
/// Windows 10 and later create a copy of certain services per logged-on session. What the
/// manager holds is two different things wearing almost the same name: a <b>template</b> that
/// never runs and exists to be copied, and one <b>instance</b> per session, which is the one
/// doing the work. Glossary term `perUserService`, spec item `A11`.
///
/// <b>Why this is a field and not a name test.</b> Instances are named parent plus an
/// underscore and the session in hex, which makes matching on the name look sufficient and it
/// is not. Measured on a real machine 2026-08-25 over all 798 entries with sc.exe as the
/// oracle: counting the family by name suffix gives 24 and 24, counting it by these bits gives
/// 23 and 23. The two the name test invents are <c>Power</c>, which is the machine's power
/// service - a plain 0x20 share process, running, automatic - and <c>Power_a17007</c>, a plain
/// 0x10 own process that simply has a hex-looking tail. <b>A window that collapsed by name
/// would file the power service away as session noise.</b>
///
/// <b>Zero here means a measured no, not an absent answer</b>, which is why it is None rather
/// than the Unknown the enums above open with. Those describe values the manager may decline
/// to give or may name something we have no word for. This one is read from the type bits that
/// come back with every enumerated entry, so every entry has a real answer to it.
/// </summary>
public enum PerUserRole
{
    /// <summary>Not part of the per-user family at all, which is most of the machine.</summary>
    None = 0,

    /// <summary>
    /// The pattern a session's copy is made from. Never runs, so it sits Stopped while its
    /// instances run - and that is the shape that inflates any count of "automatic and not
    /// running" taken without this field.
    /// </summary>
    Template,

    /// <summary>The copy made for one logged-on session. This is the one with a process.</summary>
    Instance
}

