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
        RequiredBy = Reading<IReadOnlyList<string>>.NotRead(),
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

    /// <summary>
    /// The pattern a session's copy is made from - `A11`.
    ///
    /// <b>Stopped, and that is measured rather than tidy.</b> On this machine on 2026-08-25, 0 of
    /// 23 templates were running against 8 of their 23 instances. A template that ran would be a
    /// fixture nothing on a real machine can produce, and every test about what a folded row says
    /// about its family would be asserting about a shape that does not exist.
    ///
    /// <b>Still a shared process, because the roles answer different questions about one entry.</b>
    /// `docs/03` says so in as many words - the type bits carry both, and flattening a template to
    /// something other than what it shares would quietly change what <c>type:sharedProcess</c>
    /// covers.
    /// </summary>
    internal static ScmEntry Template(string name) => Stopped(name) with
    {
        EntryType = EntryType.SharedProcess,
        PerUserRole = PerUserRole.Template
    };

    /// <summary>
    /// One session's copy, named the way Windows names them.
    ///
    /// <b>Its display name is its own service name, which is measured and is not a shortcut
    /// here.</b> All 23 instances on this machine answer with their bare name where the template
    /// answers with a translated sentence - so a session copy carries no readable name at all, and
    /// a fixture that gave it one would hide half of what folding buys.
    /// </summary>
    internal static ScmEntry Instance(string name) => Entry(name, name) with
    {
        EntryType = EntryType.SharedProcess,
        PerUserRole = PerUserRole.Instance
    };

    /// <summary>
    /// The pair that punishes grouping by name, copied off a real machine.
    ///
    /// <b>`CLAUDE.md` and `docs/03` both record it because it is the whole reason the fold asks the
    /// bits first.</b> <c>Power</c> is a plain share process - running, automatic, the power service
    /// - and <c>Power_a17007</c> is a plain own process that simply has a hex-looking tail. Counting
    /// the family by suffix gives 24 and 24 on this machine where the bits give 23 and 23, and the
    /// two it adds are these. A window that folded by name would file the power service away as
    /// session noise.
    ///
    /// Returned as a pair rather than as two calls, because a test that used one without the other
    /// would be asserting about a name with nothing to be confused with.
    /// </summary>
    internal static (ScmEntry Looks, ScmEntry LikeATail) TheirNamesLookLikeAFamily() =>
        (Entry("Power") with { EntryType = EntryType.SharedProcess },
            Entry("Power_a17007") with { EntryType = EntryType.OwnProcess });
}
