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
        Account = Reading<string>.Present("LocalSystem")
    };

    internal static ScmEntry Named(string serviceName, string displayName) =>
        Any with { ServiceName = serviceName, DisplayName = displayName };
}
