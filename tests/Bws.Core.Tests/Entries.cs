using Bws.Core;

namespace Bws.Core.Tests;

/// <summary>
/// Entries to test against, without going near the service control manager.
///
/// One starting point that every test narrows with <c>with</c>, so a test says only what
/// it actually cares about and a new required field does not have to be added in fifty
/// places.
/// </summary>
internal static class Entries
{
    /// <summary>
    /// ERROR_ACCESS_DENIED, the number every refusal in these fixtures carries.
    ///
    /// Named rather than repeated, because a bare 5 scattered through twenty fixtures is
    /// the kind of thing that gets copied into a place where it means something else.
    /// </summary>
    internal const int AccessDenied = 5;

    /// <summary>
    /// An ordinary running service. Deliberately not neutral: the service name and the
    /// display name differ, and the account is a real one, because a fixture where those
    /// are equal or empty lets a test about identity pass while checking nothing.
    /// </summary>
    internal static ScmEntry Any => new()
    {
        ServiceName = "Spooler",
        DisplayName = "Print Spooler",
        EntryType = EntryType.OwnProcess,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1234),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        Account = Reading<string>.Present("LocalSystem"),
        DependsOn = Reading<IReadOnlyList<string>>.Present(["RPCSS"]),

        // Absent, because that is what a real entry that has been read looks like: measured
        // on a real machine, 688 of 810 have no trigger at all. Not read is a state of its
        // own and has a specimen of its own - starting every fixture there would make the
        // ordinary case the rare one and quietly invert what the catalogue represents.
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),

        // Spooler's own values, read off the machine with the tool. Present rather than
        // missing because that is the ordinary case: five entries of 810 name a file that is
        // not there, so a fixture defaulting to missing would invert what the catalogue
        // represents. The shapes that are awkward to resolve have specimens of their own.
        BinaryPath = Reading<string>.Present(@"C:\WINDOWS\System32\spoolsv.exe"),
        BinaryFile = Reading<string>.Present(@"C:\WINDOWS\System32\spoolsv.exe"),
        BinaryOnDisk = Reading<bool>.Present(true),

        // Not read, because that is what an entry looks like after an ordinary listing.
        // Verifying signatures costs about three seconds and happens only when asked, so a
        // fixture that started out knowing them would make the rare case the default and
        // quietly hide every mistake about the state that actually dominates.
        Signature = Reading<BinarySignature>.NotRead(),
        FileVersion = Reading<string>.NotRead()
    };

    /// <summary>
    /// The same entry after the second pass has run over it.
    ///
    /// Its own starting point rather than a flag on the one above, because most tests care
    /// about one world or the other and mixing them is how a test ends up asserting about
    /// signatures on entries nobody read.
    /// </summary>
    internal static ScmEntry Inspected => Any with
    {
        Signature = Reading<BinarySignature>.Present(
            new BinarySignature(SignatureStatus.Trusted, 0, "Microsoft Windows")),
        FileVersion = Reading<string>.Present("10.0.26100.1")
    };

    internal static ScmEntry Named(string serviceName, string displayName) =>
        Any with { ServiceName = serviceName, DisplayName = displayName };
}
