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
    /// The conditions under which the manager starts or stops this entry by itself.
    ///
    /// The first piece of expensive data, and the first field whose ordinary state is
    /// <see cref="ReadOutcome.NotRead"/>. That is what ADR-13 is about: a listing has to be
    /// useful before everything is known, so "nobody asked for this yet" has to be a state
    /// of its own and must never render like "there is nothing here". A stopped service
    /// with no triggers is broken. A stopped service waiting for a trigger is working.
    ///
    /// Absent, not empty, when the entry has none - measured on a real machine on
    /// 2026-08-01: 122 entries of 810 have at least one, so having none is the ordinary
    /// case and is a fact about the service rather than a gap in what we read.
    /// </summary>
    public required Reading<IReadOnlyList<ServiceTrigger>> Triggers { get; init; }

    /// <summary>
    /// The whole command the manager runs, executable and arguments together, exactly as
    /// it comes back.
    ///
    /// Kept verbatim rather than tidied, because this is the value a snapshot has to
    /// preserve and a diff has to compare. The arguments are part of what somebody changed
    /// when they changed it, and a normalised version would hide that.
    ///
    /// Free: it arrives in the same configuration structure the start type, the account and
    /// the dependencies come from, so reading it costs no extra call.
    /// </summary>
    public required Reading<string> BinaryPath { get; init; }

    /// <summary>
    /// The file that command actually runs, as an absolute path.
    ///
    /// Its own field rather than something a reader is left to work out, because working it
    /// out is the hard half: arguments have to come off, <c>\SystemRoot\</c> and relative
    /// paths have to be resolved, and an unquoted path with spaces is genuinely ambiguous.
    /// See <see cref="BinaryPathResolver"/> for the rules and the measurements behind them.
    ///
    /// Absent when the entry names no file at all and no default applies.
    /// </summary>
    public required Reading<string> BinaryFile { get; init; }

    /// <summary>
    /// Whether <see cref="BinaryFile"/> is on disk.
    ///
    /// An entry set to start automatically whose file is gone is an orphan in the glossary's
    /// sense, and C13 of the specification is about finding them. Measured on a real machine
    /// on 2026-08-01: five entries of 810 name a file that is not there, none of them
    /// automatic, which is why this is reported as its own fact rather than folded into a
    /// single "orphan" answer that would have been empty.
    ///
    /// Cheap enough to read on every listing: 34-39 ms across 810 entries, against a budget
    /// of a second, and against 45-75 ms for the triggers already read the same way.
    /// </summary>
    public required Reading<bool> BinaryOnDisk { get; init; }

    /// <summary>
    /// Who signed <see cref="BinaryFile"/> and whether the system trusts the signature.
    ///
    /// The first field whose ordinary state in a plain listing is
    /// <see cref="ReadOutcome.NotRead"/>, and the first one that earns it. Measured on a
    /// real machine on 2026-08-01: verifying 544 distinct files costs around three seconds
    /// against 322-329 ms for everything the listing does otherwise, so reading this every
    /// time would put the listing six times over its one second budget.
    ///
    /// That is what ADR-13 was written for. Triggers and launch paths both looked like they
    /// would need it and both turned out cheap enough not to - this is the family where the
    /// deferral is paid for by a measurement rather than by an expectation.
    /// </summary>
    public required Reading<BinarySignature> Signature { get; init; }

    /// <summary>
    /// How hard the system takes it when this entry fails during boot.
    ///
    /// Free: it arrives in the same configuration structure as the start type, and it has
    /// been sitting in that buffer unread since the first slice. `D1` names it among the
    /// things a snapshot holds, which is what finally gave it a reader.
    /// </summary>
    public required Reading<ErrorControl> ErrorControl { get; init; }

    /// <summary>
    /// The load order group this entry belongs to, if any.
    ///
    /// Also free and also from that buffer. It matters for the same reason a dependency
    /// does: an entry can declare a dependency on a whole group rather than on a service,
    /// and this is the other end of that relationship. Absent for most entries.
    /// </summary>
    public required Reading<string> LoadOrderGroup { get; init; }

    /// <summary>
    /// The version <see cref="BinaryFile"/> claims for itself.
    ///
    /// Cheap on its own - 0.27 s across the same 544 files - and read in the same pass
    /// anyway, because it comes from a file that has just been opened for the signature.
    /// Splitting it out would mean walking every binary on the machine twice to save a
    /// quarter of a second on a step that already costs three.
    ///
    /// Absent for a file with no version resource, which is ordinary rather than missing.
    /// </summary>
    public required Reading<string> FileVersion { get; init; }

    /// <summary>
    /// SHA-256 of <see cref="BinaryFile"/>, lower case hexadecimal.
    ///
    /// The thing a snapshot needs that a signature cannot give: a signature says who vouched
    /// for the file, and this says whether it is byte for byte the same file. A binary
    /// swapped for another one signed by the same publisher changes this and nothing else.
    ///
    /// Read alongside the signature rather than on its own, because both open the same file
    /// and walking every binary on the machine twice would cost more than the hash does.
    /// Measured on 2026-08-01: 0.52 s across 544 distinct files totalling 368 MB, against
    /// 4620-7656 ms for the signatures in the same pass.
    /// </summary>
    public required Reading<string> BinaryHash { get; init; }

    /// <summary>
    /// The privileges the entry asks the manager to leave in its token, by name.
    ///
    /// What it asks for, which is not what it gets and not what it could have. A service
    /// declaring none is not a service without privileges - the manager then leaves the
    /// account's whole set in place, so declaring nothing is the permissive case and
    /// declaring a short list is the careful one. Reading this the other way round would
    /// invert every finding built on it.
    ///
    /// Names are kept exactly as the manager returns them, including the casing, which
    /// varies between services on one machine: Schedule declares SeSystemTimePrivilege and
    /// Sense declares SeSystemtimePrivilege, and they are the same privilege. Comparison is
    /// therefore case-insensitive everywhere, and a diff that compared these as plain text
    /// would report a change between two machines that had none.
    ///
    /// Absent, not empty, for an entry declaring nothing - measured on a real machine on
    /// 2026-08-01: 215 of 339 services declare at least one, the longest list has 28, and
    /// all 471 drivers declare none because a driver has no token to trim.
    ///
    /// Cheap: 43-50 ms across 810 entries, on the configuration handle the listing already
    /// holds, against a budget of a second.
    /// </summary>
    public required Reading<IReadOnlyList<string>> RequiredPrivileges { get; init; }

    /// <summary>
    /// Whether the entry has an identity of its own. See <see cref="Core.ServiceSidType"/>
    /// for why "none" lives in the outcome rather than in the enumeration.
    ///
    /// Asked of every entry including drivers, although every driver measured answers none.
    /// Skipping them would save about ten milliseconds and would turn a measurement of one
    /// machine into a claim about all of them.
    /// </summary>
    public required Reading<ServiceSidType> SidType { get; init; }

    /// <summary>
    /// Who may do what to this entry, as the text form the system reads and writes.
    ///
    /// The text form rather than a decoded list, and that is a deliberate split the glossary
    /// makes in pitfall P9: the decoded list is what a person is shown, the text form is what
    /// a snapshot keeps, because only the text form is faithful and comparable. The window
    /// will decode it when there is a panel to decode it into.
    ///
    /// Owner, group and permissions. The audit list - the SACL - is not here: asking for it
    /// fails the whole read with error 5 unless SeSecurityPrivilege is enabled, which it is
    /// not even in an elevated session, so including it would cost the other three parts and
    /// buy nothing. This is a deliberate difference from <c>sc sdshow</c>, which shows the
    /// audit list and does not show owner or group.
    ///
    /// The one field here read through a handle of its own, because READ_CONTROL is a
    /// different right from the one the listing already has. Measured under a restricted
    /// token on 2026-08-01: five entries of 810 open for configuration and refuse when
    /// READ_CONTROL is added, so asking for both together would have cost them the fields
    /// they report today. 29-33 ms plus about 30 for the extra handle, across 810 entries.
    /// </summary>
    public required Reading<string> SecurityDescriptor { get; init; }

    /// <summary>
    /// What the process behind this entry is using, when one is running.
    ///
    /// The last of the four families S4 was cut into, and the only one that is a
    /// measurement rather than a description of how the machine is set up. Everything else
    /// on this record reads the same twice in a row - this does not, which is why it is not
    /// part of a snapshot and why a plain listing does not ask for it.
    ///
    /// Absent for the great majority: 691 entries of 810 are not running, and an entry with
    /// no process has no memory in the same way a stopped entry has no process id. Not read
    /// until somebody asks with <c>--memory</c> or with a query about it.
    ///
    /// Cheap, and deliberately not read anyway. Measured on 2026-08-01: asking all 110
    /// processes takes under a millisecond. See <see cref="MemoryPass"/> for why that is not
    /// the argument it looks like.
    /// </summary>
    public required Reading<ProcessMemory> Memory { get; init; }

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
    /// be running and is not.
    ///
    /// Unknown when the start type could not be read, because "I could not check"
    /// must never render as "everything is fine".
    /// </summary>
    public Reading<bool> RunsAgainstItsStartType =>
        StartType.Outcome switch
        {
            ReadOutcome.Present => Judge(),

            // Passes the refusal on whole, number and sentence together. A refusal always
            // carries both, so there is nothing here to invent - and inventing a sentence
            // was what the old fallback did, in English, in code.
            ReadOutcome.Denied => Reading<bool>.Denied(StartType.ErrorCode, StartType.Reason!),
            _ => Reading<bool>.NotRead()
        };

    /// <summary>
    /// Whether the entry not running is a failure, once the triggers are taken into account.
    ///
    /// An automatic entry that is stopped used to be the whole answer. It is not: an entry
    /// with a trigger that starts it is doing exactly what it was configured to do, and the
    /// system will bring it up when the condition arrives. Measured on a real machine on
    /// 2026-08-01, four of the ten entries this reported were waiting rather than broken -
    /// and a signal that is wrong four times out of ten is one people learn to ignore.
    ///
    /// The cost of that is here, in the last branch: the answer now depends on a field that
    /// can be unread, so an entry that looks stopped and whose triggers nobody read gets
    /// "I do not know" rather than an accusation. That only applies to entries that would
    /// otherwise be reported - everything else is answered without the triggers being needed
    /// at all, so an unread listing does not turn into 810 shrugs.
    /// </summary>
    private Reading<bool> Judge()
    {
        if (StartType.Value != Core.StartType.Automatic || Status == EntryStatus.Running)
        {
            return Reading<bool>.Present(false);
        }

        return Triggers.Outcome switch
        {
            // Something starts it by itself. Stopped is where it is supposed to sit.
            ReadOutcome.Present when Triggers.Value!.Any(trigger => trigger.Action == TriggerAction.Start) =>
                Reading<bool>.Present(false),

            ReadOutcome.Present or ReadOutcome.Absent => Reading<bool>.Present(true),
            ReadOutcome.Denied => Reading<bool>.Denied(Triggers.ErrorCode, Triggers.Reason!),
            _ => Reading<bool>.NotRead()
        };
    }
}
