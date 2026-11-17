using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// When a search goes on the list of searches made lately, and when one comes off it - the
/// window's half of the owner's first choice of 2026-11-16. What the list holds and how it is kept
/// is <see cref="RecentQueries"/> and <see cref="KeptColumns"/>, and both can be checked without a
/// window. What is left here is the part only a window knows: where the keyboard is and which key
/// was pressed.
///
/// <b>A SEARCH IS KEPT WHEN SOMEBODY IS DONE TYPING IT</b> - the keyboard leaves the box, Enter is
/// pressed in it with no list open, or the window closes - and only when it reads. Never on a
/// keystroke: the box narrows the list as somebody types, so every prefix of a word is a query
/// that reads, and keeping those would fill five rows with "s", "sp" and "spo". Leaving the box is
/// the moment the text stops being typed and starts being used - a row clicked, a chip pressed, an
/// action taken. A query with a mistake in it is not kept: it is not a search anybody made.
///
/// <b>A class of its own rather than more of MainWindow</b>, which stands on its ceiling of methods.
/// Wired once, from the place that holds the box, the model and the layout file together.
/// </summary>
internal static class RecentSearches
{
    /// <summary>Ties the box to the list of searches made lately.</summary>
    internal static void Watch(Window window, TextBox box, MainViewModel model, KeptColumns kept)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(box);

        model.Suggesting.RecentQueries = () => kept.RecentQueries;

        box.LostKeyboardFocus += (_, _) => Keep(box, model, kept);

        // Enter with no list open belongs to nobody (MainWindow.Wanted) and reaches the box as an
        // ordinary key. With a list open the window takes it in the tunnel, and it never gets here.
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                Keep(box, model, kept);
            }
        };

        // Delete on a search made lately forgets it. Only on such a row and only with the list open,
        // and the list of them opens only on an empty box - so the key has nothing of its own to do
        // there, and everywhere else it deletes a character as it always did.
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None
                && model.Suggesting is { IsOpen: true, Chosen: { IsRecent: true } chosen })
            {
                kept.Forget(chosen.Word, model.Says);
                model.Suggesting.Forgot(box.Text);
                e.Handled = true;
            }
        };

        window.Closing += (_, _) => Keep(box, model, kept);
    }

    /// <summary>
    /// Keeps the query in the box when it reads. The box's text is handed to the model first - the
    /// binding waits 400 ms after a key, and Enter or closing the window comes inside that as often
    /// as not (SearchRow.Commit says what reading the older text costs).
    /// </summary>
    private static void Keep(TextBox box, MainViewModel model, KeptColumns kept)
    {
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

        var typed = model.AsTyped();

        if (RecentQueries.Worth(box.Text, typed))
        {
            kept.Remember(typed.Text, model.Says);
        }
    }
}
