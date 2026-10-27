using Bws.Core.History;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The change history read again after the window's own plan - logic report H01, 2026-10-27. The panel
/// showed a start type beside a history without the change that set it, until F5. The log shows a change
/// about a second after it is made (tools/history-probe/event-lag.ps1), so the reading waits for it.
/// </summary>
public sealed class HistoryAfterPlanTests
{
    [Fact]
    public async Task The_windows_own_plan_reads_the_history_again_once_the_log_has_had_time_to_hold_it()
    {
        var log = new HistoryLog(HistoryLog.Ordinary());
        var clock = new SteppedClock();
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS")), clock, history: log);

        await model.OpenHistory();

        // THE LOG HOLDS THE CHANGE ONLY ONCE THE TIME HAS PASSED, since the review of PR #96. The first
        // version put it there before the plan, so a reading that read first and waited after would have
        // found it all the same.
        var landsAt = clock.Elapsed + HistoryReading.LogWrites;

        log.Later = (
            new ChangeHistory(
                HistoryLog.Log(), [.. HistoryLog.Ordinary().Changes.Prepend(HistoryLog.ByPerson("Spooler", HistoryLog.Day(21, 9), back: true))]),
            () => clock.Elapsed >= landsAt);

        await model.LoadKeepingAsync();
        await model.History.Out;

        Assert.Equal(2, log.Reads);
        Assert.Equal(8, model.History.Shown!.Filter.Rows.Sum(row => row.Count));
    }

    [Fact]
    public async Task A_plan_reads_no_history_nobody_asked_for()
    {
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = Model(log);

        await model.LoadKeepingAsync();
        await model.History.Out;

        Assert.Equal(0, log.Reads);
    }

    [Fact]
    public async Task A_reading_out_when_the_plan_ends_is_followed_by_one_that_holds_the_change()
    {
        // Begun before the plan's change reached the log, so it cannot be the answer to it.
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = Model(log);

        await model.OpenHistory();

        log.Hold();

        var f5 = model.LoadAsync();

        model.History.AfterOwnChange();
        log.Release();
        await f5;
        await model.History.Out;

        Assert.Equal(3, log.Reads);
    }

    private static MainViewModel Model(HistoryLog log) =>
        new(new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS")), new SteppedClock(), history: log);
}
