using Bws.Core;

namespace Bws.Cli.Tests;

/// <summary>
/// Entries for the renderers of this assembly to print.
///
/// <b>Moved here on 2026-10-05 from the two test classes that each kept one</b>, when a third
/// needed both - the documents a script reads and the text a person reads are now asked about the
/// same hostile names (stability report C-1 and C-4), and three private copies of one record with
/// twenty seven fields is three places a new field has to be added to.
/// </summary>
internal static class Entries
{
    /// <summary>One entry with the fields a listing row needs and little else.</summary>
    internal static ScmEntry Plain(string name, string displayName) => new()
    {
        ServiceName = name,
        DisplayName = displayName,
        Description = Reading<string>.Present(name + " description"),
        EntryType = EntryType.OwnProcess,
        PerUserRole = PerUserRole.None,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1234),
        AcceptsStop = Reading<bool>.Present(true),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        Account = Reading<string>.Present("LocalSystem"),
        DependsOn = Reading<IReadOnlyList<string>>.Absent(),
        RequiredBy = Reading<IReadOnlyList<string>>.NotRead(),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
        BinaryPath = Reading<string>.Present(@"C:\Windows\System32\svchost.exe"),
        BinaryFile = Reading<string>.Present(@"C:\Windows\System32\svchost.exe"),
        BinaryOnDisk = Reading<bool>.Present(true),
        Signature = Reading<BinarySignature>.NotRead(),
        FileVersion = Reading<string>.NotRead(),
        BinaryHash = Reading<string>.NotRead(),
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
        SidType = Reading<ServiceSidType>.Absent(),
        SecurityDescriptor = Reading<string>.Absent(),
        ErrorControl = Reading<ErrorControl>.Present(Core.ErrorControl.Normal),
        LoadOrderGroup = Reading<string>.Absent(),
        Memory = Reading<ProcessMemory>.NotRead()
    };

    /// <summary>One entry with every field read and holding something, for the report of `show`.</summary>
    internal static ScmEntry Full => new()
    {
        ServiceName = "Spooler",
        DisplayName = "Print Spooler",
        Description = Reading<string>.Present("Spools print jobs."),
        EntryType = EntryType.OwnProcess,
        PerUserRole = PerUserRole.None,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1234),
        AcceptsStop = Reading<bool>.Present(true),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        Account = Reading<string>.Present("LocalSystem"),
        DependsOn = Reading<IReadOnlyList<string>>.Present(["RPCSS"]),
        RequiredBy = Reading<IReadOnlyList<string>>.NotRead(),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
        BinaryPath = Reading<string>.Present(@"C:\Windows\System32\spoolsv.exe"),
        BinaryFile = Reading<string>.Present(@"C:\Windows\System32\spoolsv.exe"),
        BinaryOnDisk = Reading<bool>.Present(true),
        Signature = Reading<BinarySignature>.Present(
            new BinarySignature(SignatureStatus.Trusted, 0, "Microsoft Windows")),
        FileVersion = Reading<string>.Present("10.0.26100.1"),
        BinaryHash = Reading<string>.Present("abc123"),
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Present(["SeTcbPrivilege"]),
        SidType = Reading<ServiceSidType>.Present(ServiceSidType.Unrestricted),
        SecurityDescriptor = Reading<string>.Present("O:SYG:SYD:(A;;CCLCSWLOCRRC;;;AU)"),
        ErrorControl = Reading<ErrorControl>.Present(Core.ErrorControl.Normal),
        LoadOrderGroup = Reading<string>.Present("SpoolerGroup"),
        Memory = Reading<ProcessMemory>.Present(new ProcessMemory(18_000_000, 20_000_000, 1))
    };
}
