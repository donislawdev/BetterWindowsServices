using Bws.Core.History;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The change history read again after the window's own plan - logic report H01, 2026-10-27. A seam the
/// size ratchet asked for, cut by subject: what the log owes the window, and how long it takes to pay.
/// </summary>
public sealed partial class HistoryReading
{
    /// <summary>
    /// How long the window gives the System log to show a change Windows has just made, before it reads
    /// the log after its own plan.
    ///
    /// <b>Measured, not guessed, and the measurement changed the fix.</b> tools/history-probe/event-lag.ps1
    /// changed one service's start type with sc.exe and queried the log from the moment sc.exe returned:
    /// the event 7040 appeared after 1000-1029 ms, six times of six, and never on the first query. The log
    /// is fed by an event tracing session that hands its events over once a second (its FlushTimer is 1 on
    /// that machine, and Microsoft gives one second as the least such a session flushes at). So reading
    /// the log at once after a plan read it before the plan's own change was in it.
    ///
    /// <b>Two periods, not one</b> - a margin for a machine busy at that moment. A machine configured to
    /// flush less often shows the change at the next reading, F5 or the view opened again, and that is the
    /// price of waiting a fixed time rather than watching the log.
    /// </summary>
    internal static readonly TimeSpan LogWrites = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The window changed the machine since the reading out right now began, so the log owes it a change
    /// that reading cannot hold. Read and written on the window's thread only.
    /// </summary>
    private bool _owed;

    /// <summary>
    /// The window's own plan has run: the log again once it has had time to hold what the plan did, but only
    /// once somebody has asked for it. Until 2026-10-27 a plan read the machine again and left the history
    /// as it was, so the panel showed a start type next to a history without the change that set it. Not
    /// awaited by the end of the run - the reading is kept in <see cref="Out"/>.
    ///
    /// <b>A reading already out when the plan ended began before the plan's change reached the log</b>, so
    /// it is followed by one more rather than taken as the answer.
    /// </summary>
    internal void AfterOwnChange()
    {
        if (!_asked)
        {
            return;
        }

        _owed = true;

        if (Out.IsCompleted)
        {
            Out = ReadAsync();
        }
    }

    /// <summary>
    /// The log read once, and again for as long as the window's own plan owes it a change the reading
    /// began too early to hold - one task, so whoever awaits <see cref="Out"/> awaits the answer that holds it.
    /// What one reading does and the failures it has are at <see cref="ReadOnceAsync"/>.
    /// </summary>
    private async Task ReadAsync()
    {
        do
        {
            var owed = _owed;

            _owed = false;

            await ReadOnceAsync(owed).ConfigureAwait(true);
        }
        while (_owed);
    }

    /// <summary>On the pool thread the reading runs on, from start to finish - the wait first, when it is owed.</summary>
    private ChangeHistory Read(IChangeHistoryReader reader, bool owed)
    {
        if (owed)
        {
            _clock.Wait(LogWrites);
        }

        return reader.Read();
    }
}
