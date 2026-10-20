using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The change history on a built window - J2, owner's decisions J2-a and J2-b of 2026-10-20: "Change
/// history" under "Audit..." puts it where the list was, the list's own controls go while it is there,
/// and the way back gives them back.
/// </summary>
public sealed class HistoryWindowGuards
{
    [Fact]
    public async Task The_item_under_audit_puts_the_history_in_the_middle_and_the_controls_of_the_list_away()
    {
        var (window, model) = await Opened();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.ChangeHistory.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Entries.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Comparison.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Actions.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Search.Visibility));
        Assert.Same(model.History.Shown, WpfHost.On(() => window.ChangeHistory.DataContext));
    }

    [Fact]
    public async Task The_way_back_gives_the_list_and_its_controls_back()
    {
        var (window, model) = await Opened();

        WpfHost.On(() => window.ChangeHistory.BackButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        Assert.Null(model.History.Shown);
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.ChangeHistory.Visibility));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Entries.Visibility));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Actions.Visibility));
    }

    [Fact]
    public async Task Escape_puts_the_history_away_before_it_touches_the_query()
    {
        var (window, model) = await Opened();

        WpfHost.On(() => model.QueryText = "spool");

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.Null(model.History.Shown);
        Assert.Equal("spool", WpfHost.On(() => model.QueryText));
    }

    [Fact]
    public async Task Show_over_the_history_offers_its_two_groups_with_a_line_between_them()
    {
        var (window, model) = await Opened();
        var filter = model.History.Shown!.Filter;

        var items = WpfHost.On(() => window.ChangeHistory.Choices.ShowMenu.Items.Cast<object>().ToList());

        Assert.Equal(filter.Groups[0].Count + 1 + filter.Groups[1].Count, items.Count);
        Assert.IsType<Separator>(items[filter.Groups[0].Count]);
        Assert.Equal("historyShow", WpfHost.On(() => System.Windows.Automation.AutomationProperties.GetAutomationId(window.ChangeHistory.Choices.ShowButton)));

        // A tick in the menu narrows the table under it, the way it does over a comparison.
        WpfHost.On(() => ((MenuItem)items[filter.Groups[0].Count + 2]).IsChecked = true);

        Assert.Equal(2, filter.Rows.Count);
    }

    [Fact]
    public async Task A_snapshot_form_arriving_takes_the_middle_from_the_history()
    {
        // One view where the list was, and the newer thing somebody asked for wins it.
        var (window, model) = await Opened();

        WpfHost.On(model.Snapshotting.Prepare);

        Assert.Null(model.History.Shown);
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.ChangeHistory.Visibility));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.TakeForm.Visibility));
    }

    [Fact]
    public async Task Pressing_a_heading_sorts_the_history_and_pressing_it_again_turns_the_order_round()
    {
        // The table opens newest first with the mark on When (AS-d), so the first press turns it to the
        // oldest first - Spooler on the 18th - and the second back to the driver on the 20th.
        var (window, model) = await Opened();

        var first = WpfHost.On(() =>
        {
            Press(window.ChangeHistory, "gui.history.column.when");
            return ((HistoryRow)window.ChangeHistory.Table.Lines.Items[0]).Entry;
        });

        var again = WpfHost.On(() =>
        {
            Press(window.ChangeHistory, "gui.history.column.when");
            return ((HistoryRow)window.ChangeHistory.Table.Lines.Items[0]).Entry;
        });

        Assert.Equal("Spooler", first);
        Assert.NotEqual("Spooler", again);
        Assert.Equal(4, model.History.Shown!.Filter.Rows.Count);
    }

    [Fact]
    public async Task The_history_opens_newest_first_with_the_mark_on_when_and_again_on_the_next_opening()
    {
        // The grid clears every sort and every mark when its rows become a new collection, and every
        // opening is a new sheet - so the mark has to come back each time, not once.
        var (window, model) = await Opened();

        Assert.Equal((nameof(HistoryRow.When), ListSortDirection.Descending, ListSortDirection.Descending), Sorted(window));

        WpfHost.On(() => model.History.PutAway());
        await Open(window, model);

        Assert.Equal((nameof(HistoryRow.When), ListSortDirection.Descending, ListSortDirection.Descending), Sorted(window));
    }

    [Fact]
    public async Task A_sort_somebody_chose_stays_while_they_search()
    {
        var (window, model) = await Opened();

        WpfHost.On(() => Press(window.ChangeHistory, "gui.history.column.entry"));
        WpfHost.On(() => model.History.Shown!.Filter.SearchText = "bits");
        WpfHost.Settled();

        var sort = WpfHost.On(() => Assert.Single(window.ChangeHistory.Table.Lines.Items.SortDescriptions));

        Assert.Equal(nameof(HistoryRow.Entry), sort.PropertyName);
        Assert.Null(WpfHost.On(() => window.ChangeHistory.Table.Lines.Columns[0].SortDirection));
        Assert.Equal(2, model.History.Shown!.Filter.Rows.Count);
    }

    [Fact]
    public async Task Escape_empties_the_search_first_and_puts_the_history_away_on_the_next_press()
    {
        var (window, model) = await Opened();

        // Typed into the box, and Escape pressed inside the 400 ms the box waits - the text is handed over
        // first, so the press empties this search rather than finding none (G-9).
        WpfHost.On(() => window.ChangeHistory.Choices.Box.Text = "bits");

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.NotNull(model.History.Shown);
        Assert.Equal(string.Empty, model.History.Shown!.Filter.SearchText);
        Assert.Equal(4, model.History.Shown.Filter.Rows.Count);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.Null(model.History.Shown);
    }

    [Fact]
    public async Task Control_F_under_the_history_takes_its_own_search_and_leaves_the_list_box_alone()
    {
        // The list's box is collapsed under the history - Ctrl+F put the caret nowhere before AS-a.
        var (window, _) = await Opened();

        WpfHost.On(() =>
        {
            window.ChangeHistory.Choices.Box.Text = "bits";
            window.Search.Box.Text = "spool";
            window.Search.Box.Select(0, 0);
        });

        Assert.True(WpfHost.On(() => window.Act(Shortcut.FocusQuery, out _)));
        Assert.Equal("bits", WpfHost.On(() => window.ChangeHistory.Choices.Box.SelectedText));
        Assert.Equal(0, WpfHost.On(() => window.Search.Box.SelectionLength));
        Assert.Equal("historySearch", WpfHost.On(() => System.Windows.Automation.AutomationProperties.GetAutomationId(window.ChangeHistory.Choices.Box)));

        // A screen reader cannot see the words behind the empty box, so the box is named by them.
        Assert.Equal(Texts.Of("gui.history.search.hint"), WpfHost.On(() => System.Windows.Automation.AutomationProperties.GetName(window.ChangeHistory.Choices.Box)));
    }

    [Fact]
    public async Task The_export_waits_for_a_reading()
    {
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = Model(log);
        var window = WpfHost.Window(model);

        log.Hold();
        Click(window);
        WpfHost.Until(() => model.History.Shown is not null, "the history to be put in the middle");

        Assert.False(WpfHost.On(() => window.ChangeHistory.ExportButton.IsEnabled));

        log.Release();
        await model.History.Out.WaitAsync(TimeSpan.FromSeconds(10));
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => window.ChangeHistory.ExportButton.IsEnabled));
        Assert.Equal("historyExport", WpfHost.On(() => System.Windows.Automation.AutomationProperties.GetAutomationId(window.ChangeHistory.ExportButton)));
    }

    /// <summary>The sort the history table holds and the mark on When.</summary>
    private static (string Path, ListSortDirection Direction, ListSortDirection? Mark) Sorted(MainWindow window) => WpfHost.On(() =>
    {
        var sort = Assert.Single(window.ChangeHistory.Table.Lines.Items.SortDescriptions);

        return (sort.PropertyName, sort.Direction, window.ChangeHistory.Table.Lines.Columns[0].SortDirection);
    });

    private static void Press(HistoryView view, string headingKey)
    {
        view.Measure(new Size(1200, 800));
        view.Arrange(new Rect(0, 0, 1200, 800));

        var heading = Beneath(view).OfType<DataGridColumnHeader>()
            .Single(one => one.Column is not null && Equals(one.Column.Header, Texts.Of(headingKey)));

        // What a click on a heading runs - the automation peer of a heading offers no Invoke to press,
        // measured 2026-10-20.
        typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(heading, null);
    }

    private static IEnumerable<DependencyObject> Beneath(DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);

            yield return child;

            foreach (var below in Beneath(child))
            {
                yield return below;
            }
        }
    }

    private static MainViewModel Model(HistoryLog log) =>
        new(new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS")), new SteppedClock(), history: log);

    private static void Click(MainWindow window) =>
        WpfHost.On(() => window.Actions.AuditMenu.Items.OfType<MenuItem>()
            .Single(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == "historyOpen")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));

    private static async Task Open(MainWindow window, MainViewModel model)
    {
        Click(window);

        WpfHost.Until(() => model.History.Shown is { Said: "" }, "the history to be read and shown");
        await model.History.Out.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static async Task<(MainWindow Window, MainViewModel Model)> Opened()
    {
        var model = Model(new HistoryLog(HistoryLog.Ordinary()));
        var window = WpfHost.Window(model);

        await Open(window, model);

        return (window, model);
    }
}
