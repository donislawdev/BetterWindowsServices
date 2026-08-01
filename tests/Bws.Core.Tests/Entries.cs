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
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent()
    };

    internal static ScmEntry Named(string serviceName, string displayName) =>
        Any with { ServiceName = serviceName, DisplayName = displayName };
}
