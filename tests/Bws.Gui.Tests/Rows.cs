using Bws.Core;

namespace Bws.Gui.Tests;

/// <summary>
/// The entries these tests build rows out of.
///
/// <b>Extracted 2026-08-05 because the size ratchet asked for a seam, and this was the honest
/// one</b> - the same shape as the extraction of Machines.cs out of the core's property tests. It
/// was already duplicated: CellFaceTests arrived with its own copy of the same twenty-line
/// factory, which is two places for one answer to "what does a plain entry look like".
///
/// Every field is set on purpose rather than defaulted. An entry carries four read states per
/// field, and a factory that left them to whatever a record initialises to would be quietly
/// deciding which of the four every test is about.
/// </summary>
internal static class Rows
{
    internal static ScmEntry Entry(string name) => Entry(name, name + " display name");

    internal static ScmEntry Entry(string name, string displayName) => new()
    {
        ServiceName = name,
        DisplayName = displayName,
        Description = Reading<string>.Present(name + " description"),
        EntryType = EntryType.OwnProcess,
        PerUserRole = PerUserRole.None,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1234),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Absent(),
        Account = Reading<string>.Present("LocalSystem"),
        DependsOn = Reading<IReadOnlyList<string>>.Absent(),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
        BinaryPath = Reading<string>.Absent(),
        BinaryFile = Reading<string>.Absent(),
        BinaryOnDisk = Reading<bool>.Absent(),
        Signature = Reading<BinarySignature>.NotRead(),
        FileVersion = Reading<string>.NotRead(),
        BinaryHash = Reading<string>.NotRead(),
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
        SidType = Reading<ServiceSidType>.Absent(),
        SecurityDescriptor = Reading<string>.Absent(),
        ErrorControl = Reading<ErrorControl>.Absent(),
        LoadOrderGroup = Reading<string>.Absent(),
        Memory = Reading<ProcessMemory>.NotRead()
    };

    /// <summary>Stopped, and with no process either - the two go together and forgetting the
    /// second is how a test ends up asserting about a service that is somehow both.</summary>
    internal static ScmEntry Stopped(string name) => Entry(name) with
    {
        Status = EntryStatus.Stopped,
        ProcessId = Reading<int>.Absent()
    };

    internal static ScmEntry Driver(string name) => Entry(name) with
    {
        EntryType = EntryType.KernelDriver
    };

    /// <summary>
    /// The other kind of driver, which exists here because <c>type:driver</c> means BOTH.
    ///
    /// `docs/07` records that deliberately: "Windows has two kinds of driver and no word for both",
    /// so the scope holding drivers has to hold this one too. A fixture with only kernel drivers in
    /// it would let a scope that quietly meant <c>type:kernelDriver</c> pass every test.
    /// </summary>
    internal static ScmEntry FileSystemDriver(string name) => Entry(name) with
    {
        EntryType = EntryType.FileSystemDriver
    };
}
