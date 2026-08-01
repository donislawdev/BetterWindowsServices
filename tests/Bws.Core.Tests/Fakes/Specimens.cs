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
    /// <summary>
    /// The descriptor that most of this machine shares, read off it on 2026-08-01 and
    /// agreeing with <c>sc sdshow</c> apart from the two deliberate differences: it carries
    /// the owner and the group, which sc does not print, and not the audit list, which sc
    /// does.
    ///
    /// Named rather than repeated because the sharing is itself a measured fact - 810
    /// entries hold only 121 distinct descriptors - and because a constant makes it obvious
    /// which specimens differ on purpose.
    /// </summary>
    private const string CommonDescriptor =
        "O:SYG:SYD:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)" +
        "(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)";

    /// <summary>
    /// What a driver looks like in this family: it asks for no privileges and has no
    /// identity of its own, because it has no token for either to go into. Measured across
    /// all 471 drivers on the machine, every one of them answers this way.
    ///
    /// Absent rather than refused or empty. The manager answers the question and the answer
    /// is that there is nothing here.
    /// </summary>
    private static ScmEntry AsADriver(ScmEntry entry) => entry with
    {
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
        SidType = Reading<ServiceSidType>.Absent(),
        SecurityDescriptor = Reading<string>.Present(CommonDescriptor)
    };

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
        ProcessId = Reading<int>.Absent(),

        // Quoted, with two quoted arguments after it, and the file is gone. Both halves are
        // real: this is one of the five entries on the machine naming a file that is not
        // there, and it is manual, which is why it is not an orphan by the glossary and is
        // still worth being able to ask about.
        BinaryPath = Reading<string>.Present(
            @"""C:\Program Files\Proton\VPN\v4.4.1\ProtonVPN.WireGuardService.exe"" " +
            @"""C:\Program Files\Proton\VPN\v4.4.1\ServiceData\WireGuard\ProtonVPN.conf"" ""udp"""),
        BinaryFile = Reading<string>.Present(
            @"C:\Program Files\Proton\VPN\v4.4.1\ProtonVPN.WireGuardService.exe"),
        BinaryOnDisk = Reading<bool>.Present(false)
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
        ProcessId = Reading<int>.Absent(),

        // Hosted in svchost, so the launch command names the host and not the service. The
        // file that actually holds the code is a different field the tool does not read yet,
        // which means "the file is there" says less about a shared-process service than it
        // does about anything else.
        BinaryPath = Reading<string>.Present(@"C:\WINDOWS\system32\svchost.exe -k McmSvc -p -s McmSvc"),
        BinaryFile = Reading<string>.Present(@"C:\WINDOWS\system32\svchost.exe"),
        BinaryOnDisk = Reading<bool>.Present(true),

        // Declares no privileges and has an identity of its own, which is the combination
        // that shows the two are unrelated questions. A virtual account is named after the
        // service, so it is easy to assume the SID type follows from it. It does not.
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
        SidType = Reading<ServiceSidType>.Present(ServiceSidType.Unrestricted),
        SecurityDescriptor = Reading<string>.Present(
            "O:SYG:SYD:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRRC;;;LS)" +
            "(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLORC;;;IU)(A;;CCLCSWLOCRRC;;;SU)")
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
        ProcessId = Reading<int>.Absent(),

        // The \SystemRoot\ form, which is how 237 of 825 entries name their file. Checked
        // as written it names nothing, and that is how a naive existence check ends up
        // calling almost the whole machine broken.
        BinaryPath = Reading<string>.Present(@"\SystemRoot\system32\drivers\AppvStrm.sys"),
        BinaryFile = Reading<string>.Present(@"C:\WINDOWS\system32\drivers\AppvStrm.sys"),
        BinaryOnDisk = Reading<bool>.Present(true)
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
        ProcessId = Reading<int>.Absent(),

        // Refused along with the rest, because they all come from the one configuration call
        // that was turned down. Leaving them present here would make the fixture describe a
        // reading the manager cannot produce.
        BinaryPath = Reading<string>.Denied(Entries.AccessDenied, "access denied"),
        BinaryFile = Reading<string>.Denied(Entries.AccessDenied, "access denied"),
        BinaryOnDisk = Reading<bool>.Denied(Entries.AccessDenied, "access denied"),

        RequiredPrivileges = Reading<IReadOnlyList<string>>.Denied(Entries.AccessDenied, "access denied"),
        SidType = Reading<ServiceSidType>.Denied(Entries.AccessDenied, "access denied"),

        // Refused as well, although it is read through a handle of its own and could in
        // principle differ. Measured under a restricted token on 2026-08-01: all three
        // entries that refuse the configuration handle refuse READ_CONTROL too. The
        // entry that refuses only one of them is a specimen of its own further down.
        SecurityDescriptor = Reading<string>.Denied(Entries.AccessDenied, "access denied")
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
        ProcessId = Reading<int>.Absent(),

        // A service asking for nothing and given no identity, which is what most software
        // from outside Windows looks like here. It is also the permissive shape rather than
        // the careful one: asking for no privileges leaves the account's whole set in place.
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
        SidType = Reading<ServiceSidType>.Absent(),
        SecurityDescriptor = Reading<string>.Present(CommonDescriptor)
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
    /// pending deletion. Three of the four survive here now that the binary path is read -
    /// only the pending-delete state is still missing from the model.
    /// </summary>
    internal static ScmEntry DisabledKernelDriver => AsADriver(Entries.Any with
    {
        ServiceName = "amduw23g-202073-df09ebb6",
        DisplayName = "amduw23g-202073-df09ebb6",
        EntryType = EntryType.KernelDriver,
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Disabled),
        DelayedAuto = Reading<bool>.Absent(),
        Account = Reading<string>.Absent(),
        ProcessId = Reading<int>.Absent(),

        BinaryPath = Reading<string>.Present(
            @"\SystemRoot\System32\DriverStore\FileRepository\u0202073.inf_amd64_3c7f18bc022bf004\B026184\amdkmdag.sys"),
        BinaryFile = Reading<string>.Present(
            @"C:\WINDOWS\System32\DriverStore\FileRepository\u0202073.inf_amd64_3c7f18bc022bf004\B026184\amdkmdag.sys"),
        BinaryOnDisk = Reading<bool>.Present(false)
    });

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
        ProcessId = Reading<int>.Present(1900),

        // Ten privileges, including the one an audit asks about first. Kept in the manager's
        // own order, which is not alphabetical and is not ours to tidy.
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Present(
        [
            "SeAssignPrimaryTokenPrivilege",
            "SeAuditPrivilege",
            "SeChangeNotifyPrivilege",
            "SeCreateGlobalPrivilege",
            "SeDebugPrivilege",
            "SeImpersonatePrivilege",
            "SeIncreaseQuotaPrivilege",
            "SeTcbPrivilege",
            "SeBackupPrivilege",
            "SeRestorePrivilege"
        ]),

        SidType = Reading<ServiceSidType>.Present(ServiceSidType.Unrestricted),
        SecurityDescriptor = Reading<string>.Present(
            "O:SYG:SYD:(A;;CCLCLORC;;;AU)(A;;CCDCLCSWRPWPDTLORCWDWO;;;SY)" +
            "(A;;CCLCSWRPWPDTLORCWDWO;;;BA)(A;;CCLCLO;;;BU)")
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

    // -- where the launch command points ---------------------------------------------------

    /// <summary>
    /// An unquoted path with spaces in it and no arguments, so the whole string is the file.
    ///
    /// The shape that breaks the obvious way of splitting a command, and it broke it here:
    /// cutting at the first space resolved this to "C:\Program" and reported the file as
    /// missing. Two entries on the machine have this shape and both were wrong that way.
    ///
    /// Windows itself resolves it by trying the prefixes shortest first, which is also what
    /// makes an unquoted path a finding in its own right - a file planted at C:\Program.exe
    /// would be run instead. Naming that is C5 of the specification and not this slice.
    /// </summary>
    internal static ScmEntry UnquotedPathWithSpaces => Entries.Any with
    {
        ServiceName = "UpcElevationService",
        DisplayName = "Ubisoft UPC Elevation Service",
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Manual),
        DelayedAuto = Reading<bool>.Absent(),
        ProcessId = Reading<int>.Absent(),

        BinaryPath = Reading<string>.Present(
            @"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher Core\UpcElevationService.exe"),
        BinaryFile = Reading<string>.Present(
            @"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher Core\UpcElevationService.exe"),
        BinaryOnDisk = Reading<bool>.Present(true)
    };

    /// <summary>
    /// A driver naming no file of its own, which the manager answers with a default.
    ///
    /// 29 of 825 entries on the machine are like this and every one is a driver. The path is
    /// absent and the file is still known, which is a pairing nothing else in the catalogue
    /// produces - and one that a reader assuming "no path means no file" would get wrong.
    /// </summary>
    internal static ScmEntry DriverWithNoPathOfItsOwn => AsADriver(Entries.Any with
    {
        ServiceName = "Beep",
        DisplayName = "Beep",
        EntryType = EntryType.KernelDriver,
        Status = EntryStatus.Running,
        StartType = Reading<StartType>.Present(Core.StartType.System),
        DelayedAuto = Reading<bool>.Absent(),
        Account = Reading<string>.Absent(),
        ProcessId = Reading<int>.Absent(),

        BinaryPath = Reading<string>.Absent(),
        BinaryFile = Reading<string>.Present(@"C:\WINDOWS\System32\drivers\Beep.sys"),
        BinaryOnDisk = Reading<bool>.Present(true)
    });

    /// <summary>
    /// Nothing to run and nothing to assume.
    ///
    /// Synthetic, and the third the catalogue admits to: every entry naming no file on the
    /// machine is a driver, and a driver always has the default above. It is here because
    /// otherwise <c>file:none</c> is a word in the language with nothing it can ever match,
    /// and a word that cannot match anything is indistinguishable from a broken one.
    /// </summary>
    internal static ScmEntry NothingToRun => Entries.Any with
    {
        ServiceName = "PathLess",
        DisplayName = "Names no file at all",
        Status = EntryStatus.Stopped,
        StartType = Reading<StartType>.Present(Core.StartType.Manual),
        DelayedAuto = Reading<bool>.Absent(),
        ProcessId = Reading<int>.Absent(),

        BinaryPath = Reading<string>.Absent(),
        BinaryFile = Reading<string>.Absent(),
        BinaryOnDisk = Reading<bool>.Absent()
    };

    // -- permissions ----------------------------------------------------------------------

    /// <summary>
    /// A write-restricted identity, which is the stronger of the two kinds and the rarer:
    /// 11 services on the machine this was read from, against 250 unrestricted.
    ///
    /// It declares exactly one privilege while carrying the tighter identity, so a test that
    /// assumed the two move together would fail on it. They are separate settings and this
    /// specimen is the counter-example.
    /// </summary>
    internal static ScmEntry RestrictedIdentity => Entries.Any with
    {
        ServiceName = "BFE",
        DisplayName = "Aparat filtrowania bazowego",
        EntryType = EntryType.SharedProcess,
        Status = EntryStatus.Running,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        Account = Reading<string>.Present(@"NT AUTHORITY\LocalService"),
        ProcessId = Reading<int>.Present(3268),

        RequiredPrivileges = Reading<IReadOnlyList<string>>.Present(["SeAuditPrivilege"]),
        SidType = Reading<ServiceSidType>.Present(ServiceSidType.Restricted),
        SecurityDescriptor = Reading<string>.Present(
            "O:SYG:SYD:(A;;CCLCLORC;;;AU)(A;;CCDCLCSWRPLORCWDWO;;;SY)" +
            "(A;;CCLCSWRPLORCWDWO;;;BA)(A;;CCLCLO;;;BU)")
    };

    /// <summary>
    /// Everything read except the permissions, which is the case that decides how this
    /// family is read at all.
    ///
    /// Measured under a restricted token on 2026-08-01: five entries of 810 - LSM,
    /// NetSetupSvc, pla, QWAVE and QWAVEdrv - open for configuration and refuse when
    /// READ_CONTROL is asked for. If the descriptor were read on the same handle as the
    /// configuration, these five would lose their start type, their account and their launch
    /// path in exchange for a field they were never going to hand over. The values here are
    /// LSM's own, with the descriptor as that token sees it.
    ///
    /// Also the only specimen where one field is refused while its neighbours are read. The
    /// whole four-state model exists for this shape, and until this family there was nothing
    /// on this machine that produced it.
    /// </summary>
    internal static ScmEntry DescriptorRefused => Entries.Any with
    {
        ServiceName = "LSM",
        DisplayName = "Menedżer sesji lokalnych",
        EntryType = EntryType.SharedProcess,
        Status = EntryStatus.Running,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        ProcessId = Reading<int>.Present(1096),

        RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
        SidType = Reading<ServiceSidType>.Present(ServiceSidType.Unrestricted),
        SecurityDescriptor = Reading<string>.Denied(Entries.AccessDenied, "access denied")
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
        TriggersNotRead,
        UnquotedPathWithSpaces,
        DriverWithNoPathOfItsOwn,
        NothingToRun,
        RestrictedIdentity,
        DescriptorRefused
    ];

    /// <summary>A manager that hands back the whole catalogue.</summary>
    internal static FakeScmCatalog Catalog() => new(All);

    // -- after the second pass --------------------------------------------------------------

    /// <summary>
    /// The same catalogue with signatures read, which is what a listing looks like after
    /// <c>--signatures</c>.
    ///
    /// A separate list rather than a change to the one above, because both worlds are real
    /// and most runs are the first one. Every entry above has its signature not read, and
    /// that is not a gap in the fixtures - it is the ordinary state, since verifying them
    /// costs about five seconds and only happens when somebody asks.
    ///
    /// The values are the shape a real machine produces, measured on 2026-08-01 across 544
    /// distinct files: 535 trusted, 4 signed by nobody, and the rest with no file to ask
    /// about. <c>BTHMODEM</c> is one of the four real ones by name.
    /// </summary>
    internal static IReadOnlyList<ScmEntry> Inspected =>
    [
        .. All.Select(entry => entry with
        {
            Signature = SignatureFor(entry),
            FileVersion = entry.BinaryFile.IsPresent
                ? Reading<string>.Present("10.0.26100.1")
                : Reading<string>.Absent()
        }),

        // A real unsigned file, by name, so the unsigned case is not represented only by
        // something invented. One of exactly four on the machine this was read from.
        Entries.Any with
        {
            ServiceName = "BTHMODEM",
            DisplayName = "Sterownik komunikacyjny modemu Bluetooth",
            EntryType = EntryType.KernelDriver,
            Status = EntryStatus.Stopped,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            Account = Reading<string>.Absent(),
            ProcessId = Reading<int>.Absent(),
            BinaryPath = Reading<string>.Present(@"\SystemRoot\System32\drivers\bthmodem.sys"),
            BinaryFile = Reading<string>.Present(@"C:\WINDOWS\System32\drivers\bthmodem.sys"),
            BinaryOnDisk = Reading<bool>.Present(true),
            Signature = Reading<BinarySignature>.Present(
                new BinarySignature(SignatureStatus.NotSigned, unchecked((int)0x800B0100), Publisher: null)),
            FileVersion = Reading<string>.Absent()
        }
    ];

    /// <summary>
    /// What the second pass would conclude about one specimen.
    ///
    /// Refusals travel: an entry whose configuration was refused never yielded a path, so
    /// nobody could look at a file, and the reason stays the reason.
    /// </summary>
    private static Reading<BinarySignature> SignatureFor(ScmEntry entry) => entry.BinaryFile.Outcome switch
    {
        ReadOutcome.Present => Reading<BinarySignature>.Present(
            new BinarySignature(SignatureStatus.Trusted, 0, "Microsoft Windows")),
        ReadOutcome.Denied => Reading<BinarySignature>.Denied(entry.BinaryFile.ErrorCode, entry.BinaryFile.Reason!),
        _ => Reading<BinarySignature>.Absent()
    };
}
