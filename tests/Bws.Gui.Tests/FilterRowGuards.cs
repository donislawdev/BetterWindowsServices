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

        var tip = WpfHost.On(() => window.QueryBox.ToolTip) as string;

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
}
