using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Snapshots in the window itself - the button, the middle of the window while a comparison is up,
/// and the two ways back. W1 of `docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md`, owner's decisions
/// W-a and W-b.
/// </summary>
public sealed class SnapshotWindowGuards : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bws-gui-snapshot-window", Guid.NewGuid().ToString("N"));

    public SnapshotWindowGuards() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void The_bar_offers_taking_and_comparing_under_one_button()
    {
        var window = WpfHost.Window();

        Assert.Equal(
            ["snapshotTake", "snapshotCompare"],
            WpfHost.On(() => window.Actions.SnapshotsMenu.Items.OfType<System.Windows.Controls.MenuItem>().Select(AutomationProperties.GetAutomationId).ToList()));
        Assert.True(WpfHost.On(() => window.Actions.SnapshotsButton.IsEnabled));
    }

    [Fact]
    public async Task A_comparison_takes_the_middle_and_puts_the_controls_of_the_list_away()
    {
        // The overview's arrangement, for its reason: the box, the filters and the bar act on a list
        // that is not on screen while a comparison is.
        var (window, model) = await Compared();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Comparison.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Entries.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Overview.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Actions.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Search.Visibility));
        Assert.Same(model.Snapshotting.Shown, WpfHost.On(() => window.Comparison.DataContext));
    }

    [Fact]
    public async Task The_way_back_gives_the_list_and_its_controls_back()
    {
        var (window, _) = await Compared();

        WpfHost.On(() => window.Comparison.BackButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Comparison.Visibility));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Entries.Visibility));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Actions.Visibility));
    }

    [Fact]
    public async Task Escape_puts_the_comparison_away_before_it_touches_the_query()
    {
        var (window, model) = await Compared();

        WpfHost.On(() => model.QueryText = "spool");

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.Null(model.Snapshotting.Shown);
        Assert.Equal("spool", WpfHost.On(() => model.QueryText));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Entries.Visibility));
    }

    /// <summary>
    /// The same Escape, pressed rather than called - backlog 563. The test above asks Act straight,
    /// which is the window's decision. This one holds that a key a person presses reaches it.
    ///
    /// <b>With a query in the box, since a review of 2026-10-14</b> - started from an empty one, a
    /// press that put the comparison away AND emptied the query on the way would have passed here.
    /// </summary>
    [Fact]
    public async Task Escape_pressed_on_the_window_puts_the_comparison_away_and_leaves_the_query()
    {
        var (window, model) = await Compared();

        WpfHost.On(() => KeyPresses.ShowAside(window));
        WpfHost.On(() => model.QueryText = "spool");
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => KeyPresses.Press(window.Comparison, Key.Escape)));
        Assert.Null(model.Snapshotting.Shown);
        Assert.Equal("spool", WpfHost.On(() => model.QueryText));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Entries.Visibility));

        WpfHost.On(window.Close);
    }

    [Fact]
    public async Task The_button_is_off_while_the_machine_is_read_and_says_why_on_itself()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler"));
        var model = new MainViewModel(machine, new SteppedClock(), new Inspector());
        var window = WpfHost.Window(model);

        machine.HoldReadings();

        var taking = WpfHost.On(() => model.Snapshotting.TakeAsync(Path.Combine(_folder, "held.json")));

        WpfHost.Until(() => model.Snapshotting.Busy, "the reading to start");

        Assert.False(WpfHost.On(() => window.Actions.SnapshotsButton.IsEnabled));
        Assert.Equal(Texts.Of("gui.action.snapshots.busy"), WpfHost.On(() => window.Actions.SnapshotsButton.ToolTip));

        machine.ReleaseReadings();
        await taking.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(WpfHost.On(() => window.Actions.SnapshotsButton.IsEnabled));
    }

    private async Task<(MainWindow Window, MainViewModel Model)> Compared()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock(), new Inspector());
        var window = WpfHost.Window(model);
        var path = Path.Combine(_folder, "compared.json");

        await WpfHost.On(() => model.Snapshotting.TakeAsync(path)).WaitAsync(TimeSpan.FromSeconds(10));
        await WpfHost.On(() => model.Snapshotting.CompareAsync(path)).WaitAsync(TimeSpan.FromSeconds(10));

        return (window, model);
    }

    private sealed class Inspector : IBinaryInspector
    {
        public FileInspection Inspect(string file) =>
            new(
                Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.Trusted, 0, "Someone")),
                Reading<string>.Present("1.0.0.0"),
                Reading<string>.Present(new string('a', 64)));
    }
}
