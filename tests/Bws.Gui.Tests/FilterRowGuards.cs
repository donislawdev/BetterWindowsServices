using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// How the window ASKS, as a thing on a window: the search box and the row of filters.
///
/// <b>Its own file since 2026-08-13, and the seam is the product's own.</b> That row left
/// MainWindow.xaml the same day - backlog 182 for the seam, 185 for the shape - so the guards over
/// it followed. The size ratchet asked at the same moment, which is how these two came to be
/// looked at together rather than separately.
///
/// <see cref="FilterChipTests"/> holds what a chip MEANS and what clicking it writes into the box.
/// This holds the two things that only exist once the row is on a window: that the box says what
/// can be typed into it, and that the row itself takes no focus.
/// </summary>
public sealed class FilterRowGuards
{
    /// <summary>
    /// THE SEARCH BOX CARRIES THE QUERY LANGUAGE, and it is the only thing in this window that does
    /// - owner's decision, 2026-08-13, replacing a button called "Examples".
    ///
    /// <b>The claim is unchanged and its home is not: a way into the language that carries nothing
    /// looks exactly like a language that is not there.</b> It used to be a menu under a button one
    /// click away from the box those questions go into, which is one click too many for the thing
    /// this window is best at - `docs/11` section 6 grades "recognition rather than recall" as one
    /// of this window's two weakest heuristics.
    ///
    /// <b>Every question is asserted whole rather than counted</b>, because a count passes over six
    /// empty strings. The regular expression is asserted by name for the same reason it is written
    /// out at all: slashes are the one piece of syntax nobody guesses, and nothing else on screen
    /// says they exist.
    ///
    /// Asked of the tooltip the box really carries, not of the composer, so a binding that stopped
    /// reaching it goes red here.
    /// </summary>
    [Fact]
    public void The_search_box_carries_the_query_language()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.Settled();

        var tip = WpfHost.On(() => window.Search.Box.ToolTip) as string;

        Assert.False(string.IsNullOrWhiteSpace(tip), "The search box says nothing about what goes in it.");

        foreach (var example in model.Examples)
        {
            Assert.Contains(example.Query, tip, StringComparison.Ordinal);
            Assert.Contains(example.Label, tip, StringComparison.Ordinal);
        }

        Assert.Contains("/", tip, StringComparison.Ordinal);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The row holding the filters takes no focus of its own.
    ///
    /// <b>A guard on a shape that arrived with the seam of 2026-08-13.</b> The row stopped being a
    /// Grid, which is focusable by nothing, and became a UserControl - which IS a ContentControl,
    /// and a ContentControl is focusable and a tab stop BY DEFAULT. Measured before the two
    /// attributes were written rather than assumed, `docs/10` trap 8. Without them Tab lands on the
    /// row before it reaches the first chip inside it.
    ///
    /// <b>This is exactly the failure nothing else would see.</b> A phantom tab stop breaks no
    /// binding, throws nothing, paints nothing differently - it is only felt by somebody working
    /// the window from the keyboard, which <c>docs/11</c> 9.1 says is how an administrator works.
    ///
    /// Asked of the built element rather than of the markup, so it also says the attributes reached
    /// it rather than merely being written.
    /// </summary>
    [Fact]
    public void The_filters_row_is_not_a_stop_on_the_way_to_the_filters()
    {
        var window = WpfHost.Window();

        Assert.False(
            WpfHost.On(() => window.Filters.Focusable),
            "The row holding the filters takes focus itself, so Tab stops on the container before "
            + "it reaches a single chip.");

        Assert.False(
            WpfHost.On(() => window.Filters.IsTabStop),
            "The row holding the filters is a tab stop, which is a step in the keyboard order that "
            + "nobody chose and that does nothing when it is reached.");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// No filter chip is laid out past the right edge of the row that holds them - backlog 293.
    ///
    /// <b>The fault this holds against sat on a photograph for a day before anybody read it.</b>
    /// At 1000 by 800 the chips ran off the right-hand end: "Could not check" showed as "Could
    /// r", "Runs while disabled" as "Runs", and one state chip was cut through a letter. Nothing
    /// was clipped in the sense a narrow column clips text - the chips were standing outside the
    /// window, where they can be neither read nor clicked.
    ///
    /// <b>The cause was one element of markup that had been there since the row was built.</b>
    /// Each group is a label beside a WrapPanel of chips, and the two sat in a HORIZONTAL
    /// StackPanel - which measures its children with infinite width. A WrapPanel handed a line
    /// with no end never wraps, so the group grew to the width of all its chips in a row, and the
    /// WrapPanel outside it can break BETWEEN groups and never inside one.
    ///
    /// <b>THE YARDSTICK IS THE ROW RATHER THAN THE WINDOW, AND THE FIRST VERSION OF THIS GUARD
    /// USED THE WINDOW AND PROVED NOTHING.</b> Measured with the fault put back by hand: the
    /// widest chip ended 700 across against a row ending at 385, and against a WINDOW of 640 it
    /// would have needed a window narrower than this one is allowed to be. The chips share their
    /// row with two buttons, so the edge that matters is the one they are given.
    ///
    /// <b>At the narrowest size this window may be, which is a resource rather than a number
    /// somebody liked.</b> WidthWindowLeast is what MainWindow.xaml declares as MinWidth, so a
    /// build that lets the window get smaller brings this guard with it. At the size the window
    /// OPENS with every group fits, and the broken markup and the repaired markup are the same
    /// picture - which is why a month of looking at the default size never found this.
    ///
    /// <b>Shown rather than only measured, for the reason <c>PlanViewGuards</c> writes out at
    /// length:</b> an ItemsControl builds no containers until a layout pass runs, so a guard over
    /// a window nobody showed would be counting chips that are not in the tree yet, and passing.
    /// </summary>
    [Fact]
    public void No_filter_chip_stands_outside_a_narrow_window()
    {
        var window = WpfHost.Window();

        var (chips, worst, name, edge) = WpfHost.On(() =>
        {
            window.Width = (double)WpfHost.Resources["WidthWindowLeast"];
            window.Height = (double)WpfHost.Resources["HeightWindowLeast"];
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;
            window.Show();
            window.UpdateLayout();

            var dress = (Style)WpfHost.Resources["FilterChip"];
            var found = new List<ToggleButton>();

            Collect(window.Filters, dress, found);

            var over = found
                .Select(chip => (
                    Right: chip.TransformToAncestor(window).Transform(new Point(chip.ActualWidth, 0)).X,
                    Says: chip.Content as string))
                .OrderByDescending(pair => pair.Right)
                .First();

            var host = window.Filters.Chips;
            var hostRight = host.TransformToAncestor(window).Transform(new Point(host.ActualWidth, 0)).X;

            return (found.Count, over.Right, over.Says, hostRight);
        });

        // NOT VACUOUS, and that half matters as much as the other: a walk that found nothing would
        // have no widest chip and no way to fail. Fifteen is under the number this build has and
        // over any accident.
        Assert.True(chips >= 15, $"Only {chips} filter chips were built, so this proved nothing.");

        Assert.True(
            worst <= edge,
            $"The chip \"{name}\" ends {worst} across, in a row that ends at {edge} - so it is "
            + "standing outside the row, where it can be neither read nor clicked. A group whose "
            + "chips cannot wrap grows to the width of all of them, and the row around it can "
            + "only break between groups.");

        WpfHost.On(window.Close);
    }

    private static void Collect(DependencyObject from, Style dress, List<ToggleButton> found)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(from); index++)
        {
            var child = VisualTreeHelper.GetChild(from, index);

            if (child is ToggleButton button && button.Style == dress)
            {
                found.Add(button);
            }

            Collect(child, dress, found);
        }
    }
}
