using System.Runtime.InteropServices;

namespace Bws.Cli;

/// <summary>
/// What a run does when the console it belongs to is closed: what the first Ctrl+C does.
///
/// <b>Stability report W-11, owner's decision 2026-10-05.</b> Until then the process ended the
/// instant the window closed - .NET 10 no longer installs a handler of its own - so a restart closed
/// between its stop and its start left the service stopped. Measured that day with
/// tools/close-probe/sweep.ps1 on a restart of GamingServices, closed every 100 ms from 300 to 1500:
/// the build before this left the service stopped ten times in thirteen, and this one none.
///
/// <b>A type of its own rather than two lines in Execution.Carry, because letting go of it has a rule.</b>
/// Disposing a signal registration waits for its running handler, and the handler here never returns
/// - so once the window has been closed the registration must NOT be let go, or the main thread
/// waits on it until Windows ends the process, report unwritten (tools/close-probe, mode "release").
/// Owning both the registration and that rule in one place is what lets the analyser see that every
/// path disposes what it should.
/// </summary>
internal sealed class WindowClose : IDisposable
{
    private readonly CancellationTokenSource _closed = new();
    private readonly PosixSignalRegistration _registration;

    /// <param name="interruption">What the first Ctrl+C cancels, cancelled here the same way.</param>
    internal WindowClose(CancellationTokenSource interruption)
    {
        // SIGHUP is how .NET spells CTRL_CLOSE_EVENT on Windows (PosixSignalRegistration).
        _registration = PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ => Hold(interruption));
    }

    /// <summary>
    /// Whether the window has gone. Progress stops being written then: nobody is there to read it,
    /// and a terminal that closes a tab rather than a window was not measured - writes to a closed
    /// classic console were, and none of them threw - so the putting back is not left depending on it.
    /// </summary>
    internal bool Happened => _closed.IsCancellationRequested;

    /// <summary>
    /// <b>Let go only when the window was NOT closed</b> - see the summary of this type. Left alone,
    /// Main returns and the process ends about ten milliseconds later (mode "leave", three runs). A
    /// close arriving between the check and the call still costs the five seconds, and nothing else:
    /// by then the run is over.
    /// </summary>
    public void Dispose()
    {
        if (!Happened)
        {
            _registration.Dispose();
        }

        _closed.Dispose();
    }

    /// <summary>
    /// <b>NEVER RETURNS, AND THAT IS THE WHOLE MECHANISM.</b> Windows runs this on a thread of its own
    /// and ends the process the moment it returns, or five seconds after the close if it does not
    /// (HandlerRoutine, CTRL_CLOSE_EVENT, SPI_GETHUNGAPPTIMEOUT). Holding it is what gives the main
    /// thread those seconds to stop moving forward and put back what this run took down, and once the
    /// run is over the main thread returns from the program, which ends this thread with it. Measured:
    /// 5022 ms between the close and the end of a process holding its handler, 6 ms without one.
    ///
    /// <b>What this cannot catch, said rather than implied:</b> signing out and shutting down end an
    /// interactive console program without any signal (the same page), and a dropped SSH or WinRM
    /// session was not measured. A request already handed to the manager is carried out either way -
    /// it is the waiting, and the steps after it, that a closed window used to take away.
    /// </summary>
    private void Hold(CancellationTokenSource interruption)
    {
        Execution.Stop(_closed);
        Execution.Stop(interruption);
        Thread.Sleep(Timeout.Infinite);
    }
}
