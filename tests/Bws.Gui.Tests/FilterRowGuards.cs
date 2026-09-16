using System.Windows;
using System.Windows.Controls;
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

    /// <summary>
    /// Every filter group has a row of its own and the five labels stand in one column - point 7
    /// of the review in `docs/11` 2.14, GUI rule 13: a form is a grid with a column of labels.
    ///
    /// <b>What was wrong is on the photograph of 2026-09-15:</b> the groups flowed in a WrapPanel,
    /// so "About the entry" landed in the middle of the fourth row beside the chips of "Disagrees
    /// with itself", and the labels were wherever the wrap left them.
    ///
    /// <b>Measured at two widths, because the two answer different questions.</b> At the width the
    /// window opens with, every group fits on one line and the column is the whole point. At the
    /// narrowest width the widest group wraps inside its row, and the labels have to stay a
    /// column while the chips beside them fold - which is what the shared size scope buys and a
    /// per-group Auto column would lose.
    /// </summary>
    [Theory]
    [InlineData("WidthWindowLeast")]
    [InlineData("WidthWindowOpens")]
    public void Every_filter_group_has_its_own_row_and_the_labels_stand_in_one_column(string width)
    {
        var window = WpfHost.Window();

        var groups = WpfHost.On(() =>
        {
            window.Width = width == "WidthWindowOpens" ? window.Width : (double)WpfHost.Resources[width];
            window.Height = (double)WpfHost.Resources["HeightWindowLeast"];
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;
            window.Show();
            window.UpdateLayout();

            var host = window.Filters.Chips;
            var dress = (Style)WpfHost.Resources["FilterChip"];

            return Enumerable.Range(0, host.Items.Count)
                .Select(at => (FrameworkElement)host.ItemContainerGenerator.ContainerFromIndex(at))
                .Select(container =>
                {
                    var label = Descendants(container).OfType<TextBlock>().First();
                    var chips = new List<ToggleButton>();
                    Collect(container, dress, chips);
                    var first = chips.OrderBy(chip => chip.TransformToAncestor(window).Transform(new Point()).Y)
                        .ThenBy(chip => chip.TransformToAncestor(window).Transform(new Point()).X)
                        .First();

                    var at = container.TransformToAncestor(window).Transform(new Point());

                    return (
                        Label: label.Text,
                        LabelX: label.TransformToAncestor(window).Transform(new Point()).X,
                        FirstChipX: first.TransformToAncestor(window).Transform(new Point()).X,
                        Top: at.Y,
                        Bottom: at.Y + container.ActualHeight);
                })
                .ToArray();
        });

        Assert.True(groups.Length >= 4, $"only {groups.Length} filter groups were built, so this proved nothing");

        // ONE COLUMN: every label starts where the first does, and so does every first chip.
        foreach (var group in groups)
        {
            Assert.True(
                Math.Abs(group.LabelX - groups[0].LabelX) <= 1,
                $"the label \"{group.Label}\" starts at {group.LabelX} where \"{groups[0].Label}\" starts at {groups[0].LabelX} - the labels are not a column");
            Assert.True(
                Math.Abs(group.FirstChipX - groups[0].FirstChipX) <= 1,
                $"the chips of \"{group.Label}\" start at {group.FirstChipX} where those of \"{groups[0].Label}\" start at {groups[0].FirstChipX} - the label column is not one width");
        }

        // ONE ROW EACH: no group starts above the bottom of the one before it.
        for (var at = 1; at < groups.Length; at++)
        {
            Assert.True(
                groups[at].Top >= groups[at - 1].Bottom - 1,
                $"\"{groups[at].Label}\" starts at {groups[at].Top}, beside \"{groups[at - 1].Label}\" which ends at {groups[at - 1].Bottom} - two groups share a row");
        }

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The Filters button is exactly as wide folded as open - point 8(a) of the review in
    /// `docs/11` 2.14. Its triangle pointed down at 8 by 5 and right at 5 by 8, so the button
    /// folded was three pixels narrower than open and Columns and Show every instance stepped
    /// sideways on every press - GUI rule 3, nothing jumps, broken by a glyph.
    ///
    /// <b>Measured off a laid-out window rather than off the geometry</b>, because the fix is a
    /// square the Path is given, and whether the Path honours it is a fact about layout.
    /// </summary>
    [Fact]
    public void The_filters_button_is_as_wide_folded_as_open()
    {
        var window = WpfHost.Window();

        var (open, folded) = WpfHost.On(() =>
        {
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;
            window.Show();
            window.UpdateLayout();

            var toggle = window.Filters.Switch;
            var opened = toggle.ActualWidth;

            toggle.IsChecked = false;
            window.UpdateLayout();

            return (opened, toggle.ActualWidth);
        });

        Assert.True(open > 40, $"the button measured {open} wide, which is not a laid out button");
        Assert.Equal(open, folded);

        WpfHost.On(window.Close);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var at = 0; at < VisualTreeHelper.GetChildrenCount(root); at++)
        {
            var child = VisualTreeHelper.GetChild(root, at);

            yield return child;

            foreach (var under in Descendants(child))
            {
                yield return under;
            }
        }
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
