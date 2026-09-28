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
}
