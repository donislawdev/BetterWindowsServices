namespace Bws.Core.Tests.Fakes;

/// <summary>
/// The specimens of what Windows does when a service fails - backlog 555, 2026-11-23. Their own file because
/// Specimens.cs stood at the ceiling for test files, and here rather than in <see cref="All"/>, whose size
/// other tests count.
/// </summary>
internal static partial class Specimens
{
    // -- what Windows does when it fails (backlog 555) ------------------------------------------

    /// <summary>
    /// Every kind of item with its own delay, a count that starts again only at boot, a program with the
    /// fail count appended, a message, and the flag on - everything services.msc's recovery tab can hold.
    /// </summary>
    internal static ScmEntry RecoveryOfEveryKind => Entries.Any with
    {
        ServiceName = "Watched",
        DisplayName = "Restarts, then runs a program, then restarts the computer",
        Recovery = Reading<RecoverySettings>.Present(new RecoverySettings(
            new RecoveryList(
                [
                    new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1)),
                    new RecoveryItem(RecoveryAction.RunProgram, TimeSpan.FromMinutes(2)),
                    new RecoveryItem(RecoveryAction.RestartComputer, TimeSpan.FromMinutes(5))
                ],
                Timeout.InfiniteTimeSpan)
            {
                Command = @"C:\Tools\notify.cmd /fail=%1%",
                RebootMessage = "Restarting after a failure of Watched"
            },
            OnNonCrashFailures: true))
    };

    /// <summary>
    /// MSiSCSI's shape on a clean Windows, measured 2026-11-23: two restarts and an item that does nothing,
    /// and a program and a message named although no item runs a program or restarts the computer.
    /// </summary>
    internal static ScmEntry RecoveryWithAProgramThatNeverRuns => Entries.Any with
    {
        ServiceName = "Leftover",
        DisplayName = "Names a program it never runs",
        Recovery = Reading<RecoverySettings>.Present(new RecoverySettings(
            new RecoveryList(
                [
                    new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMilliseconds(120000)),
                    new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMilliseconds(300000)),
                    new RecoveryItem(RecoveryAction.Nothing, TimeSpan.Zero)
                ],
                TimeSpan.FromSeconds(18000))
            {
                Command = "customScript.cmd",
                RebootMessage = "See Note 3 below"
            },
            OnNonCrashFailures: true))
    };
}
