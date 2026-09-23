using System.Windows;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A window restarted with administrator rights opens where the one without them was -
/// UX-GUI-004 (c). HandOverTests holds what crosses, and this holds what the new window does with it.
///
/// <b>Measured before the change:</b> the new session opened on nothing - no list, no search, no
/// selection, no plan - so stopping one service was five steps done twice.
/// </summary>
public sealed class HandOverGuards
{
    [Fact]
    public async Task The_list_the_search_the_picked_rows_and_the_open_plan_come_back()
    {
        var (window, model) = await Opened();

        var carried = new HandOver
        {
            Scope = EntryScope.Everything,
            Query = "Spool",
            Picked = ["spooler", "NoLongerHere"],
            Asked = ActionKind.Stop
        };

        await WpfHost.On(() => window.TakeOverAsync(carried, refused: false));
        WpfHost.Until(() => window.PlanPanel.Visibility == Visibility.Visible, "the plan the old window had open came back");

        Assert.False(WpfHost.On(() => model.ShowingOverview));
        Assert.Equal(EntryScope.Everything, WpfHost.On(() => model.Scope));
        Assert.Equal("Spool", WpfHost.On(() => model.QueryText));

        // Matched the way the manager matches names - without case - and by name, never by label.
        Assert.Equal(["Spooler"], WpfHost.On(() => window.Entries.SelectedItems.OfType<EntryRow>().Select(row => row.ServiceName).ToArray()));

        // Asked again, so built again from the machine as it is now.
        Assert.Contains(WpfHost.On(() => window.PlanPanel.StepLines), line => line.Contains("Spooler", StringComparison.Ordinal));

        // And what did not come back is said.
        Assert.Contains(Texts.Of("gui.handOver.gone.one", 1), WpfHost.On(() => model.Says.Problem), StringComparison.Ordinal);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// THE OTHER HALF: what the window without rights writes down - the list, the search, the picked
    /// rows by their manager's names, and the plan that is open. No plan open, no plan handed over.
    /// </summary>
    [Fact]
    public async Task What_the_window_hands_over_is_what_is_on_its_screen()
    {
        var (window, model) = await Opened();

        WpfHost.On(() =>
        {
            model.Scope = EntryScope.Everything;
            window.Entries.UnselectAll();
            window.Entries.SelectedItems.Add(model.Rows.First(row => row.ServiceName == "W32Time"));
        });

        WpfHost.Settled();

        var closed = WpfHost.On(() => window.HandOverNow());

        Assert.Equal(EntryScope.Everything, closed.Scope);
        Assert.Equal(["W32Time"], closed.Picked);
        Assert.Null(closed.Asked);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Restart)));
        WpfHost.Until(() => window.PlanPanel.Visibility == Visibility.Visible, "the restart plan opened");

        Assert.Equal(ActionKind.Restart, WpfHost.On(() => window.HandOverNow()).Asked);

        WpfHost.On(window.Close);
    }

    [Fact]
    public async Task A_refused_hand_over_is_said_and_changes_nothing()
    {
        var (window, model) = await Opened();

        await WpfHost.On(() => window.TakeOverAsync(null, refused: true));

        Assert.Contains(Texts.Of("gui.handOver.refused"), WpfHost.On(() => model.Says.Problem), StringComparison.Ordinal);
        Assert.Equal(string.Empty, WpfHost.On(() => model.QueryText));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));

        WpfHost.On(window.Close);
    }

    [Fact]
    public async Task A_selection_left_behind_is_said_and_no_plan_opens_over_nothing()
    {
        var (window, model) = await Opened();

        var carried = new HandOver { Scope = EntryScope.Services, Query = "", Picked = [], PickedLeftBehind = true };

        await WpfHost.On(() => window.TakeOverAsync(carried, refused: false));

        Assert.Contains(Texts.Of("gui.handOver.pickedLeftBehind"), WpfHost.On(() => model.Says.Problem), StringComparison.Ordinal);
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));

        WpfHost.On(window.Close);
    }

    private static async Task<(MainWindow Window, MainViewModel Model)> Opened()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler", "Print Spooler"), Rows.Entry("W32Time", "Windows Time"), Rows.Driver("disk")),
            new SteppedClock())
        {
            Planned = new Planned { Elevated = true }
        };

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        WpfHost.On(() => window.Entries.ItemsSource = model.Rows);
        WpfHost.Settled();

        return (window, model);
    }
}
