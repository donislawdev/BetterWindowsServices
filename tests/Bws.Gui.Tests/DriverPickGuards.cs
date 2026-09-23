using System.Windows;
using System.Windows.Controls;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A selection of drivers alone, which this tool looks at and does not touch - UX-GUI-003 of the
/// audit of 2026-09-23.
///
/// <b>Measured before the change, on a window without administrator rights:</b> with 3ware picked
/// on the Drivers list all six write buttons were live, and each of the six plans - the start type
/// included - opened to an empty list of steps, the refusal under it, and a red sentence advising a
/// restart as administrator that would have changed nothing
/// (artifacts/shots/package3-20260923/walk-probe.log).
/// </summary>
public sealed class DriverPickGuards
{
    /// <summary>
    /// Drivers alone turn every write button off, and each says why - the reason is the tool, not
    /// the selection, so "pick one or more entries" would send somebody to pick what they picked.
    /// A service beside the driver turns them back on: the plan then says what happens to each.
    /// </summary>
    [Fact]
    public async Task Drivers_alone_turn_the_write_buttons_off_and_say_why()
    {
        var (window, model) = await Opened();

        Pick(window, model, "disk");

        foreach (var button in Writes(window))
        {
            Assert.False(WpfHost.On(() => button.IsEnabled), WpfHost.On(() => button.Name) + " is live over a driver");
            Assert.Equal(Texts.Of("gui.action.onlyDrivers"), WpfHost.On(() => button.ToolTip as string));
        }

        Pick(window, model, "disk", "Spooler");

        Assert.True(WpfHost.On(() => window.Actions.Stop.IsEnabled));
        Assert.Equal(Texts.Of("gui.action.stop.hint"), WpfHost.On(() => window.Actions.Stop.ToolTip as string));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The row menu keeps its items live over any selection (RowMenu says why), so the refusal
    /// stands where the menu and the bar both arrive - the window's Preview - and a menu item has no
    /// tooltip, so the reason goes to the status line. No plan opens.
    /// </summary>
    [Fact]
    public async Task Asking_for_a_plan_over_drivers_alone_opens_none_and_says_why()
    {
        var (window, model) = await Opened();

        Pick(window, model, "disk");

        Assert.False(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));
        Assert.Contains(Texts.Of("gui.action.onlyDrivers"), WpfHost.On(() => model.Says.Problem), StringComparison.Ordinal);

        WpfHost.On(window.Close);
    }

    private static async Task<(MainWindow Window, MainViewModel Model)> Opened()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler", "Print Spooler"), Rows.Driver("disk")), new SteppedClock())
        {
            Scope = EntryScope.Everything
        };

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        WpfHost.On(() => window.Entries.ItemsSource = model.Rows);
        WpfHost.Settled();

        return (window, model);
    }

    private static void Pick(MainWindow window, MainViewModel model, params string[] names)
    {
        WpfHost.On(() =>
        {
            window.Entries.UnselectAll();

            foreach (var name in names)
            {
                window.Entries.SelectedItems.Add(model.Rows.First(row => row.ServiceName == name));
            }
        });

        WpfHost.Settled();
    }

    private static Button[] Writes(MainWindow window) =>
    [
        window.Actions.Stop, window.Actions.Start, window.Actions.Restart,
        window.Actions.ForceStop, window.Actions.ForceRestart, window.Actions.StartType
    ];
}
