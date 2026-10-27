using Bws.Core.History;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the window's change history may claim about the log - logic report H02 and H03, 2026-10-27. A
/// missing account is not a finding about who made a change, and how far back a log reaches is not a
/// promise that nothing between then and now is missing.
/// </summary>
public sealed class HistoryClaimsTests
{
    [Fact]
    public void A_change_with_no_account_recorded_is_not_counted_as_not_by_system()
    {
        // H03: the log may record a change without an account, and it may have been SYSTEM's.
        var unknown = HistoryLog.ByPerson("Spooler", HistoryLog.Day(21, 9)) with { By = null };
        var filter = new HistoryFilter([.. HistoryLog.Ordinary().Changes.Prepend(unknown)]);

        Assert.Equal(Texts.Of("gui.history.show.counted", Texts.Of("gui.history.show.notSystem"), 3), filter.Groups[1][1].Label);

        filter.Groups[1][1].IsChosen = true;

        Assert.DoesNotContain(filter.Rows, row => row.Group.Latest.By is null);
    }

    [Fact]
    public void No_log_is_promised_to_have_lost_nothing_and_an_archived_one_says_where_its_past_is()
    {
        // H02, the command line's rule: the reach is said and nothing more, and an archived log keeps its
        // older events in files the window does not read.
        var circular = HistoryWords.Admissions(HistoryLog.Ordinary(), [], oneEntry: false);
        var archived = HistoryWords.Admissions(
            HistoryLog.Ordinary() with { Log = HistoryLog.Log() with { Mode = LogMode.AutoBackup } }, [], oneEntry: false);

        Assert.DoesNotContain(circular, line => line.Contains("lost nothing", StringComparison.Ordinal));
        Assert.DoesNotContain(Texts.Of("gui.history.log.autoBackup"), circular);
        Assert.Contains(Texts.Of("gui.history.log.autoBackup"), archived);
    }
}
