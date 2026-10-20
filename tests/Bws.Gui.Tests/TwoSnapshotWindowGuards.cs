using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// W2 in the window itself - the form before a snapshot in the middle, the menu under "Show" over a
/// comparison, and the headings that name two files. Owner's decisions W2-a, W2-b and W2-d of
/// 2026-10-15. Keys pressed through the window's own route (KeyPresses), not by calling Act.
/// </summary>
public sealed class TwoSnapshotWindowGuards : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bws-gui-two-snapshot-window", Guid.NewGuid().ToString("N"));

    public TwoSnapshotWindowGuards() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Take_snapshot_puts_the_form_in_the_middle_and_the_controls_of_the_list_away()
    {
        // From the menu item, the road a person takes - not from Prepare, which a test could reach
        // while the item went somewhere else.
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());
        var window = WpfHost.Window(model);

        WpfHost.On(() => Item(window, "snapshotTake").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));

        Assert.NotNull(WpfHost.On(() => model.Snapshotting.Form));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.TakeForm.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Entries.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Actions.Visibility));
        Assert.Same(model.Snapshotting.Form, WpfHost.On(() => window.TakeForm.DataContext));
    }

    [Fact]
    public void Escape_pressed_on_the_form_puts_it_away_and_leaves_the_query()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());
        var window = WpfHost.Window(model);

        WpfHost.On(() => KeyPresses.ShowAside(window));
        WpfHost.On(() => model.QueryText = "spool");
        WpfHost.On(model.Snapshotting.Prepare);
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => KeyPresses.Press(window.TakeForm, Key.Escape)));
        Assert.Null(WpfHost.On(() => model.Snapshotting.Form));
        Assert.Equal("spool", WpfHost.On(() => model.QueryText));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Entries.Visibility));

        WpfHost.On(window.Close);
    }

    [Fact]
    public async Task Two_snapshots_head_their_columns_earlier_and_later()
    {
        var (window, _) = await Compared();

        Assert.Equal(Texts.Of("gui.diff.column.earlier"), WpfHost.On(() => window.Comparison.Table.BeforeColumn.Header));
        Assert.Equal(Texts.Of("gui.diff.column.later"), WpfHost.On(() => window.Comparison.Table.AfterColumn.Header));
        Assert.Equal(Texts.Of("gui.diff.files.title"), WpfHost.On(() => window.Comparison.DataContext is DiffSheet sheet ? sheet.Title : null));
    }

    [Fact]
    public async Task The_menu_under_show_holds_every_choice_and_a_tick_narrows_the_table()
    {
        var (window, model) = await Compared();
        var filter = model.Snapshotting.Shown!.Filter;

        Assert.Equal(filter.Choices.Count, WpfHost.On(() => window.Comparison.Choices.ShowMenu.Items.Count));

        // Ticked the way a click ticks it: the item's IsChecked, bound two way to the choice.
        WpfHost.On(() => ((MenuItem)window.Comparison.Choices.ShowMenu.Items[1]).IsChecked = true);
        WpfHost.Settled();

        Assert.True(filter.Choices[1].IsChosen);
        Assert.Equal(filter.Rows.Count, WpfHost.On(() => window.Comparison.Table.Lines.Items.Count));
        Assert.True(filter.Rows.Count < model.Snapshotting.Shown.Lines.Count, "the second choice narrowed nothing");
        Assert.Equal(filter.Button, WpfHost.On(() => window.Comparison.Choices.ShowButton.Content));
    }

    private static MenuItem Item(MainWindow window, string id) =>
        window.Actions.AuditMenu.Items.OfType<MenuItem>().Single(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == id);

    /// <summary>Two files compared in a window: a state that moved and an entry that came.</summary>
    private async Task<(MainWindow Window, MainViewModel Model)> Compared()
    {
        var earlier = TwoFiles.Written(_folder, "earlier.json", TwoFiles.Of(new Side(), Rows.Entry("Spooler")));
        var later = TwoFiles.Written(_folder, "later.json", TwoFiles.Of(new Side { Hour = 1 }, Rows.Stopped("Spooler"), Rows.Entry("Added")));
        var model = new MainViewModel(new LiveMachine(Rows.Entry("BITS")), new SteppedClock());
        var window = WpfHost.Window(model);

        await WpfHost.On(() => model.Snapshotting.CompareFilesAsync(earlier, later)).WaitAsync(TimeSpan.FromSeconds(10));
        WpfHost.Settled();

        return (window, model);
    }
}
