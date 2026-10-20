using System.ComponentModel;
using System.Windows;
using Bws.Core.History;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The search over the two tables of the audit - owner's decisions AS-a and AS-b of 2026-10-20: plain
/// text beside "Show", narrowing together with its ticks, and in the change history before the changes
/// are grouped, so a line never counts a change the table is not showing. And AS-d: each table opens
/// sorted, with the mark on its heading.
/// </summary>
public sealed class AuditSearchTests
{
    [Fact]
    public void A_search_narrows_the_changes_before_they_are_grouped()
    {
        // BITS switched twice each way by SYSTEM: four changes on two lines, out of seven.
        var filter = new HistoryFilter(HistoryLog.Ordinary().Changes);

        filter.SearchText = "system";

        Assert.Equal([2, 2], filter.Rows.Select(row => row.Count));
        Assert.All(filter.Rows, row => Assert.Equal("BITS", row.Entry));
        Assert.Equal(Texts.Of("gui.history.showing.narrowedGrouped.many", 4, 7, 2), filter.Showing);
    }

    [Fact]
    public void A_search_and_a_tick_narrow_together_and_a_search_that_finds_nothing_says_so()
    {
        var filter = new HistoryFilter(HistoryLog.Ordinary().Changes);
        var installations = filter.Groups[1].Single(choice => choice.Name == Texts.Of("gui.history.show.installed"));

        installations.IsChosen = true;
        filter.SearchText = "WinDivert";

        Assert.Equal(2, Assert.Single(filter.Rows).Count);

        // Spooler was never installed, so nothing is left - and the words come back one space apart.
        filter.SearchText = "  spooler   ";

        Assert.Empty(filter.Rows);
        Assert.Equal(Texts.Of("gui.history.search.nothing", "spooler"), filter.Showing);

        // Emptied, the tick alone again.
        filter.SearchText = string.Empty;

        Assert.Equal(2, Assert.Single(filter.Rows).Count);
    }

    [Fact]
    public void A_date_typed_as_text_keeps_the_changes_of_that_day()
    {
        // The day as the table writes it, in this machine's time - the way somebody reads it off the screen.
        var changes = HistoryLog.Ordinary().Changes;
        var day = HistoryWords.When(HistoryLog.Day(16, 7))[..10];
        var filter = new HistoryFilter(changes) { SearchText = day };

        var expected = changes.Count(change => HistoryWords.When(change.At).StartsWith(day, StringComparison.Ordinal));

        Assert.Equal(expected, filter.Rows.Sum(row => row.Count));
        Assert.All(filter.Rows, row => Assert.StartsWith(day, row.When, StringComparison.Ordinal));
        Assert.True(expected < changes.Count, "the day chosen keeps every change, so this proves nothing");
    }

    [Fact]
    public void The_search_reads_the_display_name_and_the_sid_the_table_leaves_out()
    {
        ServiceChange[] changes =
        [
            HistoryLog.ByPerson("Spooler", HistoryLog.Day(18, 9)) with { DisplayName = "Print Spooler" },
            HistoryLog.BySystem("BITS", HistoryLog.Day(18, 8))
        ];
        var filter = new HistoryFilter(changes);

        filter.SearchText = "print";
        Assert.Equal("Spooler", Assert.Single(filter.Rows).Entry);

        filter.SearchText = HistoryLog.SystemSid;
        Assert.Equal("BITS", Assert.Single(filter.Rows).Entry);
    }

    [Fact]
    public void The_search_stays_when_the_log_is_read_again_and_says_nothing_over_an_empty_log()
    {
        var filter = new HistoryFilter(HistoryLog.Ordinary().Changes) { SearchText = "spooler" };
        var rows = filter.Rows;

        filter.Renew(HistoryLog.Ordinary().Changes);

        Assert.Same(rows, filter.Rows);
        Assert.Equal("spooler", filter.SearchText);
        Assert.Equal("Spooler", Assert.Single(filter.Rows).Entry);

        // A log just cleared: no table, no row over one, and no sentence about a search finding nothing
        // - the sentences above the table already say the log is empty.
        filter.SearchText = "nothing like this";
        filter.Renew([]);

        Assert.False(filter.Offered);
        Assert.Equal(string.Empty, filter.Showing);
    }

    [Fact]
    public void A_search_over_a_comparison_narrows_with_its_choice_and_says_when_nothing_is_left()
    {
        var sheet = DiffFilterTests.Mixed();
        var filter = sheet.Filter;

        filter.SearchText = "ADDED";

        Assert.Equal(["Added"], filter.Rows.Select(row => row.ServiceName));
        Assert.Equal(Texts.Of("gui.diff.showing.many", 1, 3), filter.Showing);

        // "Gone" is seen on one side only, never a difference.
        filter.Choices[1].IsChosen = true;
        filter.SearchText = "gone";

        Assert.Empty(filter.Rows);
        Assert.Equal(Texts.Of("gui.diff.search.nothing", "gone"), filter.Showing);

        filter.SearchText = string.Empty;

        Assert.Equal(["Added", "Moved"], filter.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public void The_comparison_opens_sorted_by_entry_and_gives_way_when_the_search_leaves_nothing()
    {
        _ = WpfHost.Resources;
        var sheet = DiffFilterTests.Mixed();
        var table = WpfHost.On(() => new DiffTable { DataContext = sheet });

        WpfHost.Settled();

        var (path, direction, mark) = WpfHost.On(() =>
        {
            var sort = Assert.Single(table.Lines.Items.SortDescriptions);

            return (sort.PropertyName, sort.Direction, table.Lines.Columns[0].SortDirection);
        });

        Assert.Equal(nameof(DiffLine.ServiceName), path);
        Assert.Equal(ListSortDirection.Ascending, direction);
        Assert.Equal(ListSortDirection.Ascending, mark);

        WpfHost.On(() => sheet.Filter.SearchText = "nothing like this");
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => table.Lines.Visibility));

        // And "no differences" stays the comparison's own sentence - this one found three.
        Assert.Equal(string.Empty, sheet.Same);
    }
}
