using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The details panel as a thing on a window, rather than as a view model.
///
/// <b>This class exists because of a fault this project has been caught by four times:</b> XAML
/// that binds correctly and paints something other than what it promised. An empty list from
/// internal view models, column headings outside the visual tree, a dead row background, a text box
/// whose style replaced the library's instead of adding to it - every one of them had a green build
/// and a view model answering perfectly.
///
/// <see cref="ChosenGuards"/> holds what the panel KNOWS. This holds the one question that class
/// cannot ask: whether any of it reaches the window.
/// </summary>
public sealed class DetailsViewGuards
{
    /// <summary>
    /// The panel is off the window until somebody asks, and on it afterwards.
    ///
    /// <b>Collapsed rather than hidden, and the difference is the width of the list.</b> The column
    /// it sits in is sized to its content, so a hidden panel would keep its 380 points and take
    /// them from the rows for the whole time nobody was looking at it.
    ///
    /// Asked of the element rather than of the view model, which is the whole point: the state is a
    /// trigger in the panel's own style, and a trigger that never fires looks exactly like a view
    /// model that never changed.
    /// </summary>
    [Fact]
    public void The_panel_is_off_the_window_until_somebody_asks_for_it()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        // SETTLED BEFORE EVERY READ, because a trigger hanging off a binding is worked out by the
        // dispatcher AFTER the code that moved the source - so reading the visibility straight away
        // gets the value from before, and only when the machine is busy enough to make the gap
        // visible. WpfHost.Settled carries the rest of that argument.
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));

        // ON THE INTERFACE THREAD, and that is not ceremony either. This model is the window's
        // DataContext, so setting a property on it raises PropertyChanged into live bindings, and
        // from the test thread that is a cross thread touch.
        WpfHost.On(() => model.Chosen.Row = EntryRow.Of(Rows.Entry("Spooler", "Print Spooler")));

        Assert.True(WpfHost.On(() => model.Chosen.Show()));

        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.DetailsPanel.Visibility));

        Assert.True(WpfHost.On(() => window.DetailsPanel.Dismiss()));

        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.DetailsPanel.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The panel takes no focus of its own, exactly as the row of filters does not.
    ///
    /// <b>A UserControl is a ContentControl, and a ContentControl is focusable and a tab stop by
    /// default</b> - measured on 2026-08-13, `docs/10` trap 8. Without the two lines that turn it
    /// off, Tab stops on the panel before it reaches the close button inside it, and nothing about
    /// that is visible on a screenshot.
    /// </summary>
    [Fact]
    public void The_panel_is_not_a_stop_on_the_way_to_the_button_inside_it()
    {
        var window = WpfHost.Window();

        Assert.False(
            WpfHost.On(() => window.DetailsPanel.Focusable),
            "The panel takes focus itself, so Tab stops on a container that does nothing.");

        Assert.False(
            WpfHost.On(() => window.DetailsPanel.IsTabStop),
            "The panel is a tab stop of its own, in front of the only control it holds.");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Closing it from the button does the same thing Escape does, and says so.
    ///
    /// <b>The button is the visible way out and Escape is the fast one</b> - `docs/11` 9.2, third
    /// heuristic, which this product's own review names as its weakest. Two exits that disagree
    /// would be worse than one.
    /// </summary>
    [Fact]
    public void The_button_and_the_key_are_the_same_way_out()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        // SETTLED FIRST, because the panel is handed its half of the model through a binding and a
        // binding is transferred by the dispatcher rather than by the line that set it. Without
        // this the panel can still be looking at nothing when the first question is asked, which
        // shows up only when the machine is busy - see WpfHost.Settled.
        WpfHost.Settled();

        // Nothing open, so the button has nothing to do and says so - the same answer Escape gets.
        Assert.False(WpfHost.On(() => window.DetailsPanel.Dismiss()));

        WpfHost.On(() => model.Chosen.Row = EntryRow.Of(Rows.Entry("Spooler")));
        WpfHost.On(() => model.Chosen.Show());

        Assert.True(WpfHost.On(() => window.DetailsPanel.Dismiss()));
        Assert.False(model.Chosen.Showing);

        WpfHost.On(window.Close);
    }
}
