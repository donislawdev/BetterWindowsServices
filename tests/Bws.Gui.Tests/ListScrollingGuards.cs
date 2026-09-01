// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// How the list scrolls, which is a property of the grid and not of any view model.
///
/// <b>Written 2026-08-17, the same day the two settings arrived, and the report behind it could
/// not have been found by any instrument this project owns.</b> The owner said that dragging the
/// scrollbar slowly felt like it snagged. Three timing probes had already been run against it -
/// at rest, per keystroke, and per scroll step at a human pace - and every one came back clean:
/// no step anywhere near a hundred milliseconds. They were all measuring LATENCY, and a jump that
/// happens instantly is fast and jerky at the same time. Smoothness is a different property and
/// none of them can see it.
///
/// <b>So what is asserted here is the SETTING rather than the feel</b>, and that is honest rather
/// than convenient. Nothing in a test process can judge whether a list looks smooth - the owner's
/// eye did that, and confirmed it. What a test can do is stop the setting being removed by
/// somebody who never knew it was doing anything, which is exactly the shape of loss this project
/// keeps paying for: a value that only shows up as a feeling has nothing anchoring it.
///
/// <b>Asked of the built window rather than of the markup text.</b> An attribute in a file and an
/// attached property in force are different claims - a style, a template or a later setter can
/// take either of these away, and reading MainWindow.xaml as text would go on passing while the
/// grid scrolled by item again. This project has been caught by that gap four times, most
/// recently when a mark inherited a trigger from the style it was BasedOn.
/// </summary>
public sealed class ListScrollingGuards
{
    /// <summary>
    /// The list scrolls by whole rows, and this assertion was the other way round until 2026-08-31.
    ///
    /// <b>BOTH DIRECTIONS WERE THE OWNER'S DECISION AND BOTH WERE TAKEN WITH A HAND ON THE
    /// WINDOW.</b> Pixel arrived on 2026-08-17 because dragging the scrollbar slowly felt like it
    /// snagged - at 811 entries in a viewport of about thirty, one pixel of thumb travel is worth
    /// several rows, so an item-scrolled list moves in visible jumps. It went back on 2026-08-31
    /// because of what it cost: measured with row-cost.ps1 -ScrollCost, four runs a variant with the
    /// first discarded, over a list whose rows already exist, Pixel spends 22-38 ms of processor per
    /// step against 8-17 for Item, and the ranges do not overlap. The owner then compared two builds
    /// by hand - same source, one attribute apart - and chose this one.
    ///
    /// <b>What this test can and cannot say.</b> It says the setting is in force on the built grid,
    /// which is what stops it being removed by somebody who never knew it was doing anything. It
    /// says nothing about whether the list feels better, and nothing in a test process can: the
    /// jumpiness this trades for is a judgement made by an eye.
    /// </summary>
    [Fact]
    public void The_list_scrolls_by_whole_rows_rather_than_by_pixel()
    {
        // NOT the same as turning CanContentScroll off, which would also stop between boundaries -
        // by switching virtualisation off and building all 811 rows. Microsoft's own performance
        // guidance lists that as one of the four ways to lose virtualisation without noticing, and
        // the test below is what keeps the two from being confused with each other.
        Assert.Equal(ScrollUnit.Item, OnTheGrid(VirtualizingPanel.GetScrollUnit));
    }

    [Fact]
    public void The_list_reuses_its_row_containers_instead_of_building_new_ones()
    {
        // The default is Standard, which creates a container as a row enters the viewport and
        // throws it away as it leaves - so a drag down eight hundred entries allocates and
        // collects the whole way.
        Assert.Equal(VirtualizationMode.Recycling, OnTheGrid(VirtualizingPanel.GetVirtualizationMode));
    }

    [Fact]
    public void The_list_still_virtualises_its_rows_at_all()
    {
        // THE ONE ABOVE IS MEANINGLESS WITHOUT THIS, which is why it is asserted rather than left
        // to the default it currently is. Recycling describes HOW containers are reused by a panel
        // that virtualises - on a grid that has stopped virtualising it is a setting about nothing,
        // and the two tests above would go on passing over a window building 811 rows at once.
        Assert.True(OnTheGrid(grid => grid.EnableRowVirtualization));
    }

    /// <summary>
    /// A refresh must never be able to suppress the next refresh.
    ///
    /// <b>THE FAILURE THIS GUARDS IS SILENT AND PERMANENT.</b> ScrollChanged fires when the EXTENT
    /// changes under a list that has not moved, which is what a refresh does the moment the machine
    /// gains or loses a service. If a change of zero counted as a scroll, the refresh would start
    /// the timer that suppresses refreshes and the list would stop updating for as long as it was on
    /// screen - with every row still showing something that was true a minute ago, and no message
    /// anywhere. Rule 8 of the project notes, arriving from the side it looks like a courtesy from.
    /// </summary>
    [Fact]
    public void An_extent_that_changed_under_a_still_list_is_not_a_scroll()
    {
        var (still, moved) = InTheWindow(window =>
        {
            window.Moved(0, 0);
            var afterNothing = window.HoldingForScroll;

            window.Moved(12, 0);

            return (afterNothing, window.HoldingForScroll);
        });

        Assert.False(still, "A refresh changing the extent counted as a scroll and would mute the next one.");
        Assert.True(moved, "A real scroll did not hold the machine off at all.");
    }

    /// <summary>Sideways counts too - the columns are wider than the window and that is by design.</summary>
    [Fact]
    public void Moving_the_list_sideways_holds_the_machine_off_as_well()
    {
        Assert.True(InTheWindow(window =>
        {
            window.Moved(0, 12);

            return window.HoldingForScroll;
        }));
    }

    /// <summary>One question asked of a real window, built the way OnTheGrid builds one.</summary>
    private static T InTheWindow<T>(Func<MainWindow, T> ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        _ = WpfHost.Resources;

        var directory = Path.Combine(Path.GetTempPath(), "bws-scrollhold-" + Guid.NewGuid().ToString("N"));

        try
        {
            var window = WpfHost.On(() => new MainWindow(new PreferencesFile(Path.Combine(directory, "kept.json"))));

            try
            {
                return WpfHost.On(() => ask(window));
            }
            finally
            {
                WpfHost.On(window.Close);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// One question asked of the real grid inside a real window.
    ///
    /// The preferences file goes to a directory of its own, because a MainWindow reads its column
    /// layout at startup and writes it back - so without this every run here would read the layout
    /// of whoever is logged in and overwrite it on the way out. `S6d3`, and the same reasoning the
    /// probes in tools/gui-probe carry.
    /// </summary>
    private static T OnTheGrid<T>(Func<DataGrid, T> ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        // FORCED, because every StaticResource in MainWindow.xaml is resolved as the file is read.
        // Without it this passes or throws depending on which test ran first.
        _ = WpfHost.Resources;

        var directory = Path.Combine(Path.GetTempPath(), "bws-scrolling-" + Guid.NewGuid().ToString("N"));

        try
        {
            var window = WpfHost.On(() => new MainWindow(new PreferencesFile(Path.Combine(directory, "kept.json"))));

            try
            {
                return WpfHost.On(() => ask(window.Entries));
            }
            finally
            {
                WpfHost.On(window.Close);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
