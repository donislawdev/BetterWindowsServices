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

        Assert.Equal(
            [
                "gui.menu.details",
                "-",
                "gui.menu.copyName",
                "gui.menu.copyDisplayName",
                "gui.menu.copyDescription",
                "gui.menu.copyAll",
                "-",
                "gui.menu.previewStop",
                "gui.menu.previewStart",
                "gui.menu.previewRestart"
            ],
            shape);

        var details = (MenuItem)items[0];

        Assert.Equal(Texts.Of("gui.menu.details"), WpfHost.On(() => details.Header));
        Assert.Equal("Enter", WpfHost.On(() => details.InputGestureText));

        // Every entry has words, and only the first has a key.
        foreach (var entry in WpfHost.On(() => items.OfType<MenuItem>().Select(item => (RowMenuEntry)item.DataContext).ToList()))
        {
            Assert.NotEqual(entry.LabelKey, entry.Label);
            Assert.Equal(entry.LabelKey == "gui.menu.details" ? "Enter" : string.Empty, entry.Gesture);
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
