using System.Windows.Input;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A key that goes through the window's own route rather than past it - backlog 563. Why the
/// route needed a guard of its own is at <see cref="KeyPresses"/>.
///
/// <b>Escape, because it is the key that was in doubt</b>, and because its answer goes both ways:
/// a press that did something is taken, and a press that found nothing to do is handed back to
/// whatever needs it next. Both halves are what the preview handler decides, so one test holds the
/// key reaching the window and the answer reaching the framework. The comparison's Escape is in
/// SnapshotWindowGuards, beside the comparison.
/// </summary>
public sealed class PressedKeyGuards
{
    [Fact]
    public void Escape_pressed_in_the_box_clears_the_query_and_a_second_press_is_handed_back()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.On(() => KeyPresses.ShowAside(window));

        // On the interface thread, because this model is the window's DataContext - the reason
        // KeyboardTests gives for the same line.
        WpfHost.On(() => model.QueryText = "name:spooler");
        WpfHost.Settled();

        Assert.True(
            WpfHost.On(() => KeyPresses.Press(window.Search.Box, Key.Escape)),
            "Escape cleared nothing it could see, or cleared it without saying the press was taken");
        Assert.Equal(string.Empty, WpfHost.On(() => model.QueryText));

        // Nothing left to back out of, so the key is not swallowed - an empty box leaves Escape alone.
        Assert.False(WpfHost.On(() => KeyPresses.Press(window.Search.Box, Key.Escape)));

        WpfHost.On(window.Close);
    }
}
