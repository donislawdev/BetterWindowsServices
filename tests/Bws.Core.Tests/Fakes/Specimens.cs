using Bws.Core;

namespace Bws.Core.Tests.Fakes;

/// <summary>
/// The catalogue 05-PRZYPADKI-BRZEGOWE was written to specify.
///
/// Every entry below was read off the owner's machine on 2026-08-01 with the tool's own
/// --json output, not invented. That is the rule the catalogue document sets for itself:
/// a catalogue without a specimen is guesswork. Values are copied exactly, including the
/// display names, which is why they are in Polish.
///
/// The Polish text here is captured data, not authored text, and it earns its place. The
/// catalogue document lists "system in another language" as a case with no evidence
/// behind it - the whole identity rule of ADR-14 exists for it and rests on reasoning
/// alone. These specimens turn a service name and its display name into two visibly
/// different things, so a test that confuses them can no longer pass by accident.
///
/// What this catalogue cannot cover yet is listed at the bottom, and the reason is always
/// the same: the field does not exist on ScmEntry. Half the cases in the document are
/// about binary paths, triggers, recovery actions and dependencies, none of which the
/// tool reads today.
/// </summary>
internal static class Specimens
{
    // -- names and accounts -------------------------------------------------------------

    /// <summary>
    /// A service name with a space in it. Not a curiosity: it decides how the command line
    /// has to quote names, and it is why the query language protects values with quotes.
    /// </summary>
    internal static ScmEntry NameWithASpace => Entries.Any with
    {
        ServiceName = "ProtonVPN WireGuard",
        DisplayName = "ProtonVPN WireGuard",
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Manual),
        DelayedAuto = Reading<bool>.Absent(),
        ProcessId = Reading<int>.Absent()
    };

    /// <summary>
    /// A virtual account. The third category next to system and domain accounts, and the
    /// one that is easy to miss because it looks like a system account.
    /// </summary>
    internal static ScmEntry VirtualAccount => Entries.Any with
    {
        ServiceName = "McmSvc",
        DisplayName = "Usługa zarządzania łącznością mobilną",
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Manual),
        DelayedAuto = Reading<bool>.Absent(),
        Account = Reading<string>.Present(@"NT SERVICE\McmSvc"),
        ProcessId = Reading<int>.Absent()
    };

    /// <summary>A second virtual account, disabled, so a test cannot pass on one example.</summary>
    internal static ScmEntry VirtualAccountDisabled => Entries.Any with
    {
        ServiceName = "OpenVPNService",
        DisplayName = "OpenVPNService",
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Disabled),
        DelayedAuto = Reading<bool>.Absent(),
        Account = Reading<string>.Present(@"NT SERVICE\OpenVPNService"),
        ProcessId = Reading<int>.Absent()
    };

    // -- the four states a field can be in ----------------------------------------------

    /// <summary>
    /// No account at all, which is the ordinary case for a driver. Measured: 496 of 810
    /// entries carry no account, so this is the bulk of the listing rather than an edge.
    /// It is the state that must never be confused with a refusal.
    /// </summary>
    internal static ScmEntry NoAccount => Entries.Any with
    {
        ServiceName = "AppvStrm",
        DisplayName = "AppvStrm",
        EntryType = EntryType.FileSystemDriver,
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Manual),
        DelayedAuto = Reading<bool>.Absent(),
        Account = Reading<string>.Absent(),
        ProcessId = Reading<int>.Absent()
    };

    /// <summary>
    /// Everything refused.
    ///
    /// This one is synthetic and says so. The catalogue document records that reading
    /// configuration through the manager was refused zero times out of 811 on a shell
    /// without administrator rights, and states in as many words that the refusal path
    /// remains unverified on a real system. It cannot be produced on demand, so the only
    /// place it can be exercised at all is here.
    /// </summary>
    internal static ScmEntry Refused => Entries.Any with
    {
        ServiceName = "Locked",
        DisplayName = "Nothing about this one could be read",
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Denied(Entries.AccessDenied, "access denied"),
        DelayedAuto = Reading<bool>.Denied(Entries.AccessDenied, "access denied"),
        Account = Reading<string>.Denied(Entries.AccessDenied, "access denied"),
        ProcessId = Reading<int>.Absent()
    };

    /// <summary>
    /// Configuration read, delay flag refused. The state that cannot exist if the delay is
    /// a sixth start type rather than a field of its own, which is why it is here.
    /// </summary>
    internal static ScmEntry DelayRefused => Entries.Any with
    {
        ServiceName = "HalfRead",
        DisplayName = "Automatic, and nobody could tell whether it is delayed",
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Denied(Entries.AccessDenied, "access denied"),
        ProcessId = Reading<int>.Absent()
    };

    // -- start type and status -----------------------------------------------------------

    /// <summary>
    /// Automatic and not running. The single most useful derived fact in the tool, and the
    /// whole of acceptance scenario one.
    /// </summary>
    internal static ScmEntry AutomaticNotRunning => Entries.Any with
    {
        ServiceName = "AsusUpdateCheck",
        DisplayName = "AsusUpdateCheck",
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        ProcessId = Reading<int>.Absent()
    };

    /// <summary>Delayed automatic, running. sc.exe reports start type 2 for this and for a
    /// plain automatic entry alike, which is why the delay needs its own field.</summary>
    internal static ScmEntry DelayedAutomatic => Entries.Any with
    {
        ServiceName = "BITS",
        DisplayName = "Usługa inteligentnego transferu w tle",
        EntryType = EntryType.SharedProcess,
        Status = EntryStatus.Running,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(true),
        ProcessId = Reading<int>.Present(18044)
    };

    /// <summary>
    /// Delayed automatic and not running. Four of the ten entries acceptance scenario one
    /// returns look like this, which is why telling them apart is a product question and
    /// not only a question of faithful data.
    /// </summary>
    internal static ScmEntry DelayedAutomaticNotRunning => Entries.Any with
    {
        ServiceName = "sppsvc",
        DisplayName = "Ochrona oprogramowania",
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(true),
        Account = Reading<string>.Present(@"NT AUTHORITY\NetworkService"),
        ProcessId = Reading<int>.Absent(),

        // The two triggers the real one carries, read on 2026-08-01. They are what turns
        // this entry from "automatic and did not start" into "waiting to be asked for", and
        // that difference is the whole reason triggers are read at all. One of them is the
        // kind the interop metadata has no name for.
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
        [
            new ServiceTrigger(TriggerKind.Custom, TriggerAction.Start),
            new ServiceTrigger(TriggerKind.CustomSystemStateChange, TriggerAction.Start)
        ])
    };

    /// <summary>
    /// Caught halfway through starting. Transitional states are everyday life rather than
    /// failure, and on a real machine this one exists for a second or two, never when a
    /// test wants it.
    /// </summary>
    internal static ScmEntry Transitional => Entries.Any with
    {
        ServiceName = "PushToInstall",
        DisplayName = "Usługa PushToInstall systemu Windows",
        EntryType = EntryType.SharedProcess,
        Status = EntryStatus.StartPending,
        StartType = Reading<StartType>.Present(Core.StartType.Manual),
        DelayedAuto = Reading<bool>.Absent(),
        ProcessId = Reading<int>.Present(17452)
    };

    // -- drivers --------------------------------------------------------------------------

    /// <summary>
    /// A disabled kernel driver with a generated name.
    ///
    /// The catalogue calls this the best single specimen it has, because on the real
    /// machine it carries four things at once: kernel driver, disabled, missing file, and
    /// pending deletion. Only the first two survive here - the model has no binary path
    /// and no pending-delete state yet.
    /// </summary>
    internal static ScmEntry DisabledKernelDriver => Entries.Any with
    {
        ServiceName = "amduw23g-202073-df09ebb6",
        DisplayName = "amduw23g-202073-df09ebb6",
        EntryType = EntryType.KernelDriver,
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Disabled),
        DelayedAuto = Reading<bool>.Absent(),
        Account = Reading<string>.Absent(),
        ProcessId = Reading<int>.Absent()
    };

    // -- shared processes and per-user services -------------------------------------------

    /// <summary>One of five services sharing process 1900 on the machine this came from.</summary>
    internal static ScmEntry SharedProcessFirst => Entries.Any with
    {
        ServiceName = "DcomLaunch",
        DisplayName = "Program uruchamiający proces serwera DCOM",
        EntryType = EntryType.SharedProcess,
        Status = EntryStatus.Running,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        ProcessId = Reading<int>.Present(1900)
    };

    /// <summary>Another of the same five. Same process, different start type, so a test
    /// about hosting cannot pass by looking at one of them.</summary>
    internal static ScmEntry SharedProcessSecond => Entries.Any with
    {
        ServiceName = "PlugPlay",
        DisplayName = "Plug and Play",
        EntryType = EntryType.SharedProcess,
        Status = EntryStatus.Running,
        StartType = Reading<StartType>.Present(Core.StartType.Manual),
        DelayedAuto = Reading<bool>.Absent(),
        ProcessId = Reading<int>.Present(1900)
    };

    /// <summary>The per-user template: no session of its own, so it is not running.</summary>
    internal static ScmEntry PerUserTemplate => Entries.Any with
    {
        ServiceName = "CDPUserSvc",
        DisplayName = "Usługa użytkownika platformy podłączonych urządzeń",
        EntryType = EntryType.SharedProcess,
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        ProcessId = Reading<int>.Absent()
    };

    /// <summary>
    /// The instance for one session, with the random suffix in both names.
    ///
    /// It carries no account while its own template carries LocalSystem, which was a
    /// surprise when the pair was read and is exactly the sort of difference a hand-made
    /// fixture would have smoothed over.
    /// </summary>
    internal static ScmEntry PerUserInstance => Entries.Any with
    {
        ServiceName = "CDPUserSvc_21aaa4",
        DisplayName = "Usługa użytkownika platformy podłączonych urządzeń_21aaa4",
        EntryType = EntryType.SharedProcess,
        Status = EntryStatus.Running,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        Account = Reading<string>.Absent(),
        ProcessId = Reading<int>.Present(5984)
    };

    // -- dependencies -----------------------------------------------------------------------

    /// <summary>
    /// The longest declared list on the machine, and the only one carrying a load order
    /// group. The leading plus is the whole point: stopping one member of a group does not
    /// necessarily break anything depending on the group, so a cascade that treats
    /// "+NetBIOSGroup" as a service name would be planning around something that does not
    /// exist.
    /// </summary>
    internal static ScmEntry GroupDependency => Entries.Any with
    {
        ServiceName = "RemoteAccess",
        DisplayName = "Routing i dostęp zdalny",
        EntryType = EntryType.SharedProcess,
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Disabled),
        DelayedAuto = Reading<bool>.Absent(),

        // Stored with a lower case first letter on the real machine, unlike every other
        // entry in this catalogue. Account text is case-preserving and never an identity,
        // which is exactly why ADR-14 compares SIDs.
        Account = Reading<string>.Present("localSystem"),
        ProcessId = Reading<int>.Absent(),
        DependsOn = Reading<IReadOnlyList<string>>.Present(["RpcSS", "Bfe", "RasMan", "Http", "+NetBIOSGroup"])
    };

    /// <summary>
    /// Declaring nothing at all. Ordinary rather than missing: 129 of 339 services on the
    /// machine this came from declare no dependency, and every driver in the catalogue is
    /// in the same position.
    /// </summary>
    internal static ScmEntry NoDependencies => Entries.Any with
    {
        ServiceName = "PlugPlayNoDeps",
        DisplayName = "Nothing needs to be running first",
        StartType = Reading<StartType>.Present(Core.StartType.Manual),
        DelayedAuto = Reading<bool>.Absent(),
        ProcessId = Reading<int>.Absent(),
        DependsOn = Reading<IReadOnlyList<string>>.Absent()
    };

    // -- cases the machine does not have --------------------------------------------------

    /// <summary>
    /// Two names differing only in case. Zero occurrences on the real machine, so this is
    /// written rather than captured, and it stays because Windows treats service names as
    /// case-insensitive whether or not anybody has exercised it.
    /// </summary>
    internal static ScmEntry CaseOnlyDifferenceFirst => Entries.Named("Twin", "First twin");

    /// <summary>The other half of the pair above.</summary>
    internal static ScmEntry CaseOnlyDifferenceSecond => Entries.Named("TWIN", "Second twin");

    /// <summary>An ordinary running service, so the catalogue is not made only of oddities.</summary>
    internal static ScmEntry Ordinary => Entries.Any with
    {
        ServiceName = "Spooler",
        DisplayName = "Bufor wydruku",
        Status = EntryStatus.Running,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        ProcessId = Reading<int>.Present(4268)
    };

    // -- start on a condition ------------------------------------------------------------

    /// <summary>
    /// Several triggers of one kind, which is ordinary and not a mistake in the reading.
    ///
    /// Read from the owner's machine: this entry carries six network endpoint triggers,
    /// all the same kind and all the same action, and sc qtriggerinfo lists six.
    /// </summary>
    internal static ScmEntry ManyTriggersOfOneKind => Entries.Any with
    {
        ServiceName = "Appinfo",
        DisplayName = "Informacje o aplikacji",
        Status = EntryStatus.Running,
        StartType = Reading<StartType>.Present(Core.StartType.Manual),
        DelayedAuto = Reading<bool>.Absent(),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
            [.. Enumerable.Repeat(new ServiceTrigger(TriggerKind.NetworkEndpoint, TriggerAction.Start), 6)])
    };

    /// <summary>
    /// Triggers nobody asked for yet. The fourth state, and the reason ADR-13 exists.
    ///
    /// Synthetic, one of the two the catalogue admits to: the tool reads triggers on every
    /// listing today, so this state does not arise from the real manager. It belongs here
    /// anyway, because the whole point of the state is that it must never render like "has
    /// none" - and once expensive data grows past what one listing can afford, it becomes
    /// the ordinary case rather than the odd one.
    ///
    /// Deliberately manual and stopped, so it stays out of the acceptance scenario. A
    /// specimen that exists to prove one thing should not quietly change the answer to a
    /// question about something else.
    /// </summary>
    internal static ScmEntry TriggersNotRead => Entries.Any with
    {
        ServiceName = "TriggersUnknown",
        DisplayName = "Nobody asked yet",
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Manual),
        DelayedAuto = Reading<bool>.Absent(),
        ProcessId = Reading<int>.Absent(),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.NotRead()
    };

    /// <summary>Everything above, as one listing.</summary>
    internal static IReadOnlyList<ScmEntry> All =>
    [
        Ordinary,
        NameWithASpace,
        VirtualAccount,
        VirtualAccountDisabled,
        NoAccount,
        Refused,
        DelayRefused,
        AutomaticNotRunning,
        DelayedAutomatic,
        DelayedAutomaticNotRunning,
        Transitional,
        DisabledKernelDriver,
        SharedProcessFirst,
        SharedProcessSecond,
        PerUserTemplate,
        PerUserInstance,
        GroupDependency,
        NoDependencies,
        CaseOnlyDifferenceFirst,
        CaseOnlyDifferenceSecond,
        ManyTriggersOfOneKind,
        TriggersNotRead
    ];

    /// <summary>A manager that hands back the whole catalogue.</summary>
    internal static FakeScmCatalog Catalog() => new(All);
}
