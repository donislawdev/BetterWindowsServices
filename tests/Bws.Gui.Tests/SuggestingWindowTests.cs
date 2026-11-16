using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.SearchBoxKeys;

namespace Bws.Gui.Tests;

/// <summary>
/// The list under the search box as the WINDOW wires it - the box's events reaching the model.
/// Everything the list decides is asked of the model without a window (SuggestingTests,
/// SuggestingTypingTests), and the keys that belong to it are in KeyboardTests.
/// </summary>
public sealed class SuggestingWindowTests
{
    /// <summary>
    /// The box's two events reach the list from the window - its text through typing, its caret
    /// through a move that changes no text.
    ///
    /// <b>Not through writing a row</b>, which is where this used to be held: since PR 22 writing a
    /// row tells the list itself (Suggesting.Wrote), so a test of it stayed green with both events
    /// unwired - the full gate before 0.3.0 found that as two mutations nobody killed, backlog 464.
    /// </summary>
    [Fact]
    public void Typing_in_the_box_opens_the_list_and_a_caret_moved_alone_closes_it()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Typed(window, string.Empty, 0);
        Keyed(window, "sta");

        Assert.True(model.Suggesting.IsOpen);
        Assert.Equal(["status", "start"], model.Suggesting.Offered.Select(row => row.Word));

        WpfHost.On(() => window.Search.Box.CaretIndex = 1);

        Assert.Equal("sta", WpfHost.On(() => window.Search.Box.Text));
        Assert.False(model.Suggesting.IsOpen);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A press beside the box closes the list, and a press in the box or in the list does not - the
    /// owner's report of 2026-11-16 that only Escape closed it.
    ///
    /// <b>Raised in the tunnel, the way a real press starts</b>: the window's handler sits on the
    /// preview event, and a press raised by hand on an element routes from the window down to it.
    /// The grid stands for "anywhere else" because it takes no keyboard on a press here, so the lock
    /// on the box losing the keyboard cannot be what closes the list.
    /// </summary>
    [Fact]
    public void A_press_beside_the_box_closes_the_list_and_a_press_in_the_box_or_the_list_does_not()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Typed(window, string.Empty, 0);
        Keyed(window, "sta");
        Assert.True(model.Suggesting.IsOpen);

        // The list lives in a popup, whose content may route a press somewhere other than this
        // window - so which places hold the list is asked directly as well as through a press.
        Assert.True(WpfHost.On(() => window.Search.Holds(window.Search.List)));
        Assert.False(WpfHost.On(() => window.Search.Holds(window.Entries)));

        Pressed(window.Search.Box);
        Assert.True(model.Suggesting.IsOpen);

        Pressed(window.Search.List);
        Assert.True(model.Suggesting.IsOpen);

        Pressed(window.Entries);
        Assert.False(model.Suggesting.IsOpen);

        WpfHost.On(window.Close);
    }

    private static void Pressed(System.Windows.UIElement element) =>
        WpfHost.On(() => element.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
            System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent
        }));
}
