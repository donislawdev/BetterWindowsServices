using Bws.Core;

namespace Bws.Core.Tests.Fakes;

/// <summary>
/// The specimens about how an entry shares a process, and about the per-user family.
///
/// Split out of Specimens.cs on 2026-08-25 because the size ratchet said so rather than
/// because anybody read the file and wanted it shorter. That file stood exactly on the
/// test ceiling of 793 lines, and the per-user pair below needed one more line each to
/// say which side of the family it is on - a field they had claimed nothing about since
/// the day they were captured.
///
/// The seam is the section marker that was already in the file. Nothing here was
/// rewritten in the move: these are the same readings from the same machine.
/// </summary>
internal static partial class Specimens
{
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
        PerUserRole = PerUserRole.Template,
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
        PerUserRole = PerUserRole.Instance,
        DisplayName = "Usługa użytkownika platformy podłączonych urządzeń_21aaa4",
        EntryType = EntryType.SharedProcess,
        Status = EntryStatus.Running,
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        Account = Reading<string>.Absent(),
        ProcessId = Reading<int>.Present(5984)
    };

}
