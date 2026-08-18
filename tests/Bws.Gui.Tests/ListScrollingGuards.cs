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
    [Fact]
    public void The_list_scrolls_by_pixel_rather_than_by_whole_rows()
    {
        // ScrollUnit is the one that answers the complaint. A DataGrid scrolls by ITEM by default,
        // so the viewport can only ever come to rest on a row boundary - over 811 entries in a
        // viewport of about thirty, one pixel of scrollbar travel is worth several rows, so a slow
        // drag moves in visible jumps rather than sliding.
        //
        // It is NOT the same as turning CanContentScroll off, which buys the same smoothness by
        // switching virtualisation off and building all 811 rows. Microsoft's own performance
        // guidance lists that as one of the four ways to lose virtualisation without noticing, and
        // the test below is what keeps the two from being confused with each other.
        Assert.Equal(ScrollUnit.Pixel, OnTheGrid(VirtualizingPanel.GetScrollUnit));
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
