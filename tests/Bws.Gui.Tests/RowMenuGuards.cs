// Explicit, because UseWPF swaps the implicit using set.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The menu on a row and the three doors to the panel about one entry - the menu's own item, a
/// double click, and Enter - driven through the window.
///
/// <b>Written 2026-09-15, the day the menu left the markup for a list and the panel got its
/// second and third door.</b> Until then the panel opened from Enter alone and nothing on screen
/// said so: no item, no double click, not one sentence naming the key. `docs/11` 9.1 guards the
/// other direction - everything a mouse does, a keyboard does - and that direction stayed green
/// over this the whole time, which is why these exist.
///
/// <b>The mouse gestures are raised as routed events on realised containers</b>, the way
/// <see cref="CopyingAndPanelGuards"/> raises Click on an item: the handler wired in the markup is
/// the thing under test, and calling the method behind it would pass over a window where the
/// attribute had been deleted. Realising the containers means laying the window's CONTENT out -
/// never the window, which lays out nothing when nobody has shown it and answers zeros that read
/// as passing, `docs/10` trap 19. The count of realised rows is asserted for the same reason.
/// </summary>
public sealed class RowMenuGuards
{
    /// <summary>
    /// The menu on a row, key by key, with "-" for a rule between groups.
    ///
    /// <b>The action bar's words since 2026-09-24 - UX-GUI-018, owner's decision.</b> The same keys
    /// as the bar, so one verb has one name wherever it is met, and the sixth verb the menu lacked
    /// until that day holds the four settings under it. A field rather than a literal inside the
    /// test because the test stood at the length ratchet's edge.
    /// </summary>
    private static readonly string[] Shape =
    [
        "gui.menu.details",
        "-",
        "gui.menu.copyName",
        "gui.menu.copyAll",
        "-",
        "gui.action.stop",
        "gui.action.start",
        "gui.action.restart",
        "gui.action.forceStop",
        "gui.action.forceRestart",
        "gui.menu.startType"
    ];

    /// <summary>
    /// The menu starts with the item Enter and a double click stand for, and writes the key beside
    /// it - and the rest is two groups behind rules, every item with words rather than a key.
    ///
    /// <b>The key is asserted on the ITEM, not only on the entry</b>: the entry saying "Enter" and
    /// the style never binding InputGestureText would leave the menu looking exactly as it did
    /// before, with the hole this closes still open.
    /// </summary>
    [Fact]
    public void The_menu_starts_with_show_details_and_writes_its_key_beside_it()
    {
        var window = WpfHost.Window();
        WpfHost.Settled();

        var items = WpfHost.On(() => window.Entries.ContextMenu!.Items.Cast<object>().ToList());

        var shape = WpfHost.On(() => items.Select(item => item switch
        {
            MenuItem { DataContext: RowMenuEntry entry } => entry.LabelKey,
            Separator => "-",
            _ => "?"
        }).ToList());

        Assert.Equal(Shape, shape);

        var details = (MenuItem)items[0];

        Assert.Equal(Texts.Of("gui.menu.details"), WpfHost.On(() => details.Header));
        Assert.Equal("Enter", WpfHost.On(() => details.InputGestureText));

        // AND Ctrl+C BESIDE "Copy everything" SINCE 2026-09-24 - UX-GUI-010 found the key named in
        // no text of the window, and that item is what the key does. On the item, for the reason
        // above. Found by its key rather than by its place, since the menu lost two copies on
        // 2026-09-24 and a place is what moves.
        Assert.Equal("Ctrl+C", WpfHost.On(() => items.OfType<MenuItem>()
            .Single(item => item.DataContext is RowMenuEntry { LabelKey: "gui.menu.copyAll" })
            .InputGestureText));

        // Every entry has words, and only those two have a key.
        foreach (var entry in WpfHost.On(() => items.OfType<MenuItem>().Select(item => (RowMenuEntry)item.DataContext).ToList()))
        {
            Assert.NotEqual(entry.LabelKey, entry.Label);
            Assert.Equal(
                entry.LabelKey switch
                {
                    "gui.menu.details" => "Enter",
                    "gui.menu.copyAll" => "Ctrl+C",
                    _ => string.Empty
                },
                entry.Gesture);
        }
    }

    /// <summary>
    /// A right click on the second of two rows and then the details item opens the panel about the
    /// SECOND row, whatever was selected before.
    ///
    /// <b>The pointed row rather than the selection, and both rows are selected first to make the
    /// difference visible.</b> With both selected a right click keeps both - that is what the
    /// selection is for - and the grid's SelectedItem stays the first. A menu item reading the
    /// selection would show the first row. The person pointed at the second.
    /// </summary>
    [Fact]
    public void The_details_item_shows_the_row_that_was_pointed_at()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        var (rows, containers) = Realised(window);

        WpfHost.On(() =>
        {
            window.Entries.SelectedItem = rows[0];
            window.Entries.SelectedItems.Add(rows[1]);
        });

        // Mouse.PreviewMouseDown rather than PreviewMouseRightButtonDown, because the second is a
        // DIRECT event WPF re-raises on each element as the first tunnels through it - raised by
        // hand on the row, it would reach the row and nothing above it. The grid's handler is on
        // the re-raised one, and the tunnel is what brings it there with the row as its origin.
        WpfHost.On(() => containers[1].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right)
        {
            RoutedEvent = Mouse.PreviewMouseDownEvent
        }));

        Assert.Equal(2, WpfHost.On(() => window.Entries.SelectedItems.Count));

        var item = CopyingAndPanelGuards.MenuItemFor(window, "gui.menu.details");

        WpfHost.On(() => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.DetailsPanel.Visibility));
        Assert.Same(rows[1], WpfHost.On(() => model.Chosen.Row));
    }

    /// <summary>
    /// A double click on a row opens the panel about that row. On a heading it opens nothing.
    ///
    /// <b>The heading half is the one that would fail quietly.</b> A handler that took every
    /// double click on the grid would open a panel about whatever row was selected when somebody
    /// double clicked a heading to fit its column - a panel nobody asked for, over a gesture that
    /// still did what it does.
    /// </summary>
    [Fact]
    public void A_double_click_on_a_row_opens_the_panel_and_one_on_a_heading_does_not()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        var (rows, containers) = Realised(window);

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));

        WpfHost.On(() => DoubleClick(window, containers[1], MouseButton.Left));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.DetailsPanel.Visibility));
        Assert.Same(rows[1], WpfHost.On(() => model.Chosen.Row));
        Assert.Equal([rows[1]], WpfHost.On(() => window.Entries.SelectedItems.OfType<EntryRow>().ToList()));

        // Put it away, then the heading: a double click there is the grid's own gesture.
        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));

        var heading = WpfHost.On(() => Beneath(window.Entries).OfType<DataGridColumnHeader>().First(one => one.Column is not null));

        WpfHost.On(() => DoubleClick(window, heading, MouseButton.Left));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));

        // And the right button twice on a row is not a double click this window answers.
        WpfHost.On(() => DoubleClick(window, containers[0], MouseButton.Right));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));
    }

    /// <summary>
    /// Opening the panel puts the plan sheet away, from all three doors - the arrangement Enter
    /// has had since the sheet arrived, kept when the doors were added.
    /// </summary>
    [Fact]
    public async Task Opening_the_panel_by_double_click_puts_the_plan_sheet_away()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        var (_, containers) = Realised(window);

        // A plan over a name nothing is called, which is a plan with a problem and nothing to run
        // - the cheapest sheet there is, and the same one CarryingGuards puts up.
        var plan = await WpfHost.On(() => model.PlanAsync(new Bws.Core.Planning.BulkAction(
            Bws.Core.Planning.ActionKind.Stop, ["nothing-is-called-this"])));

        Assert.True(WpfHost.On(() => model.Planned.Show(plan)));
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => model.Planned.Showing));

        WpfHost.On(() => DoubleClick(window, containers[0], MouseButton.Left));
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => model.Planned.Showing));
        Assert.True(WpfHost.On(() => model.Chosen.Showing));
    }

    /// <summary>
    /// The two forcing items ask for their own kind, over one row - and over two, the item does
    /// nothing and the status line says why.
    ///
    /// <b>The items are pressed through what a press calls</b>, because the shape test above proves
    /// the words are there and nothing about what stands behind them. An item wearing the forcing
    /// key and wired to the ordinary stop would pass that test and open the wrong plan.
    ///
    /// <b>The refusal over two is asserted here as well as on the bar</b>, since this is the
    /// entrance that has no button to grey: `docs/ANALIZA-FORCE` 15.6 keeps a forced stop over
    /// several entries unreachable, and a menu item that opened one anyway would be the hole the
    /// bar closed, reopened one right click away.
    /// </summary>
    [Fact]
    public async Task The_forcing_items_open_their_own_plan_over_one_row_and_refuse_over_two()
    {
        var window = await PlanFixture.Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        var entries = WpfHost.On(() => window.Entries.ContextMenu!.Items
            .OfType<MenuItem>()
            .Select(item => (RowMenuEntry)item.DataContext)
            .ToList());

        var forceStop = entries.Single(entry => entry.LabelKey == "gui.action.forceStop");
        var forceRestart = entries.Single(entry => entry.LabelKey == "gui.action.forceRestart");

        // Ready leaves two rows picked: the item does nothing, and says so where the window says
        // everything an item could not do.
        await WpfHost.On(forceStop.Act);
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => model.Planned.Showing));
        Assert.Contains(
            Texts.Of("gui.action.force.onlyOne"),
            WpfHost.On(() => model.Says.Problem),
            StringComparison.Ordinal);

        WpfHost.On(() =>
        {
            window.Entries.UnselectAll();
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Spooler");
        });
        WpfHost.Settled();

        await WpfHost.On(forceStop.Act);
        WpfHost.Until(
            () => model.Planned.Plan?.Action.Kind == Bws.Core.Planning.ActionKind.ForceStop,
            "the sheet opened with the forced stop the item asked for");

        await WpfHost.On(forceRestart.Act);
        WpfHost.Until(
            () => model.Planned.Plan?.Action.Kind == Bws.Core.Planning.ActionKind.ForceRestart,
            "the sheet came back with the forced restart the item asked for");

        Assert.Equal(["Spooler"], WpfHost.On(() => model.Planned.Plan!.Action.ServiceNames));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// "Set startup type" on a row holds the same four settings as the action bar's menu, each with
    /// what it means on hover - and choosing one opens the plan the bar's would - UX-GUI-018.
    ///
    /// <b>Both menus are asked, because one method fills both</b> and the promise is that they
    /// cannot say different things: the same four labels, the same four tooltips, in the same
    /// order.
    ///
    /// <b>The setting is clicked as a click, raised on the item under the header</b>, because the
    /// header has no handler of its own on purpose - Click bubbles to it - and a handler added there
    /// later would open a second plan over the first.
    /// </summary>
    [Fact]
    public async Task The_start_type_item_holds_the_bar_s_four_settings_and_one_opens_its_plan()
    {
        var window = await PlanFixture.Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        var holder = WpfHost.On(() => window.Entries.ContextMenu!.Items
            .OfType<MenuItem>()
            .Single(item => item.DataContext is RowMenuEntry { LabelKey: "gui.menu.startType" }));

        var onRow = WpfHost.On(() => holder.Items.OfType<MenuItem>().ToList());
        var onBar = WpfHost.On(() => window.Actions.StartType.ContextMenu!.Items.OfType<MenuItem>().ToList());

        Assert.Equal(4, onRow.Count);

        Assert.Equal(
            WpfHost.On(() => onBar.Select(item => (item.Header as string, item.ToolTip as string)).ToList()),
            WpfHost.On(() => onRow.Select(item => (item.Header as string, item.ToolTip as string)).ToList()));

        Assert.All(
            WpfHost.On(() => onRow.Select(item => item.ToolTip as string).ToList()),
            hint => Assert.False(string.IsNullOrWhiteSpace(hint)));

        Assert.Equal(
            Texts.Of("gui.start.mark.delayed"),
            WpfHost.On(() => onRow[1].ToolTip as string));

        WpfHost.On(() =>
        {
            window.Entries.UnselectAll();
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Spooler");
        });
        WpfHost.Settled();

        WpfHost.On(() => onRow[3].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));

        WpfHost.Until(
            () => model.Planned.Plan?.Action.Kind == Bws.Core.Planning.ActionKind.SetStartType,
            "the sheet opened with the startup type the row's menu asked for");

        Assert.Equal(["Spooler"], WpfHost.On(() => model.Planned.Plan!.Action.ServiceNames));
        Assert.Equal(
            Bws.Core.Planning.StartSetting.Disabled,
            WpfHost.On(() => model.Planned.Plan!.Action.To));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A double click delivered the way WPF delivers one: raised on the GRID, with the element
    /// under the pointer as its origin.
    ///
    /// <c>MouseDoubleClick</c> is a DIRECT routed event - Control raises its own on every control
    /// in the route when a second click arrives, with the origin carried over. Raised by hand on
    /// the row it would reach the row and never the grid, whose handler is the one under test. So
    /// the origin is set first - a Source set before the raise becomes the OriginalSource and stays
    /// it - and the raise is on the grid, which is what the grid sees in the product.
    /// </summary>
    private static void DoubleClick(MainWindow window, DependencyObject origin, MouseButton button)
    {
        var press = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = Control.MouseDoubleClickEvent,
            Source = origin
        };

        window.Entries.RaiseEvent(press);
    }

    /// <summary>
    /// Two rows on the grid, with their containers realised by laying the window's content out.
    ///
    /// The content and never the window - trap 19 - and the count is asserted, because a grid that
    /// realised nothing would leave nothing to raise an event on and every claim above would be a
    /// claim about nothing.
    /// </summary>
    private static (IReadOnlyList<EntryRow> Rows, IReadOnlyList<DataGridRow> Containers) Realised(MainWindow window)
    {
        var rows = new[]
        {
            EntryRow.Of(Rows.Entry("Spooler", "Print Spooler")),
            EntryRow.Of(Rows.Entry("W32Time", "Windows Time"))
        };

        WpfHost.On(() => window.Entries.ItemsSource = rows);
        WpfHost.Settled();

        var containers = WpfHost.On(() =>
        {
            var content = (FrameworkElement)window.Content;

            content.Measure(new Size(1100, 700));
            content.Arrange(new Rect(0, 0, 1100, 700));
            content.UpdateLayout();

            return rows
                .Select((_, index) => window.Entries.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow)
                .ToList();
        });

        Assert.All(containers, container => Assert.NotNull(container));

        return (rows, containers.Select(container => container!).ToList());
    }

    /// <summary>Every visual under a node, depth first.</summary>
    private static IEnumerable<DependencyObject> Beneath(DependencyObject node)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
        {
            var child = VisualTreeHelper.GetChild(node, index);

            yield return child;

            foreach (var deeper in Beneath(child))
            {
                yield return deeper;
            }
        }
    }
}
