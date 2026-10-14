using System.Windows;
using System.Windows.Input;

namespace Bws.Gui.Tests;

/// <summary>
/// A key pressed on a window the way the framework delivers a person's press - backlog 563.
///
/// <b>Why this exists.</b> Until 2026-10-14 every keyboard test called Shortcuts.For or
/// MainWindow.Act straight, so the route a press really takes - the window's preview handler
/// asking Wanted and handing the answer to Act - had no guard for any key at all. Escape was found
/// not working through a probe that day, and it was the probe that was broken, but had it been the
/// window nothing would have gone red.
///
/// <b>A shown window, because a press needs somewhere to come from.</b> The event is built with the
/// window's presentation source, and a window never shown has none - it also reports Collapsed for
/// everything in it (`docs/10`, trap 36). Shown off screen and without activation, so a test run
/// takes neither the screen nor the keyboard from whoever is using the machine.
///
/// <b>The preview event only.</b> The window takes its keys there and says so in
/// MainWindow.Keyboard.cs. What a control would do with the bubbling event afterwards is the
/// control's own business and not what these tests claim.
/// </summary>
internal static class KeyPresses
{
    /// <summary>Shows the window off screen, unactivated. Call on the interface thread.</summary>
    internal static void ShowAside(Window window)
    {
        window.WindowStyle = WindowStyle.None;
        window.ShowInTaskbar = false;
        window.ShowActivated = false;
        window.Left = -4000;
        window.Show();
        window.UpdateLayout();
    }

    /// <summary>
    /// Presses one key with the keyboard on <paramref name="target"/>, and answers whether the
    /// window took it. Call on the interface thread, on a window shown by <see cref="ShowAside"/>.
    /// </summary>
    internal static bool Press(UIElement target, Key key)
    {
        var source = PresentationSource.FromVisual(target)
            ?? throw new InvalidOperationException("the window was never shown, so a press has nowhere to come from");

        var press = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };

        target.RaiseEvent(press);

        return press.Handled;
    }
}
