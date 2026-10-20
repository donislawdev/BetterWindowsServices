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

    private static async Task<(MainWindow Window, MainViewModel Model)> Opened()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS")), new SteppedClock(), history: new HistoryLog(HistoryLog.Ordinary()));
        var window = WpfHost.Window(model);

        WpfHost.On(() => window.Actions.AuditMenu.Items.OfType<MenuItem>()
            .Single(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == "historyOpen")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));

        WpfHost.Until(() => model.History.Shown is { Said: "" }, "the history to be read and shown");
        await model.History.Out.WaitAsync(TimeSpan.FromSeconds(10));

        return (window, model);
    }
}
