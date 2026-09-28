using System.Windows.Input;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The two ways a test puts text into the window's search box - set, or typed key by key - shared by
/// every file that asks the window about the list under the box.
///
/// <b>Out of KeyboardTests on 2026-09-28</b>, when a new test of the box's wiring would have pushed
/// that file past the share of its ceiling the size ratchet allows. One copy of each, because the
/// two long notes below are what the helpers are worth and a second copy would drift from the first.
/// </summary>
internal static class SearchBoxKeys
{
    /// <summary>
    /// Characters typed into the box the way a keyboard types them - through the text input event
    /// the box's own editor answers - rather than by setting its text.
    ///
    /// <b>The difference is the order of the box's two events, and since 2026-09-25 the list cares.</b>
    /// Setting Text reports the new text with the caret at zero and moves the caret after, which the
    /// list reads as typing followed by a caret move - and a caret move closes it. A keystroke
    /// inserts at the caret and reports the text with the caret already past what it inserted.
    /// </summary>
    internal static void Keyed(MainWindow window, string characters) =>
        WpfHost.On(() =>
        {
            foreach (var character in characters)
            {
                var composition = new TextComposition(InputManager.Current, window.Search.Box, character.ToString());

                window.Search.Box.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                {
                    RoutedEvent = TextCompositionManager.TextInputEvent
                });
            }
        });

    /// <summary>
    /// What the box holds after somebody typed it, with the keyboard in the box.
    ///
    /// <b>SETTLED FIRST, AND THAT LINE COST AN HOUR ON 2026-09-16.</b> The box's Text binding
    /// attaches LAZILY - queued to the data-bind engine at DataBind priority when the window is
    /// built - and an Invoke from the test thread runs at Send, ahead of that queue. So text set
    /// into a freshly built window was overwritten a moment later by the binding attaching and
    /// transferring the model's empty query into the box: the list then held the six examples
    /// rather than the fields, and only sometimes. Found from a stack trace on TextChanged
    /// (BindingExpression.AttachToContext under DataBindEngine.Run), not by reasoning. A shown
    /// window has drained that queue long before anybody types - this host never shows one.
    /// </summary>
    internal static void Typed(MainWindow window, string text, int caret)
    {
        WpfHost.Settled();

        WpfHost.On(() =>
        {
            window.Search.Box.Text = text;
            window.Search.Box.CaretIndex = caret;
            ((MainViewModel)window.DataContext).Suggesting.Keyboard(present: true);
        });
    }
}
