using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// The row the window is asked from - one field and the count that answers it.
///
/// It holds no logic at all, which is the whole point of the seam: what the text MEANS is the query
/// language, what it SELECTS is <see cref="ViewModels.MainViewModel"/>, and what the count says is
/// <see cref="ViewModels.Says"/>. All three can be checked without opening a window.
///
/// <b>A UserControl for the same reason FilterRow is one</b> - one mechanism for "a part of the
/// window in its own file" rather than two that have to be told apart. The argument in full is at
/// the head of <see cref="FilterRow"/>, and it was the owner's decision on 2026-08-13.
/// </summary>
public partial class SearchRow : UserControl
{
    public SearchRow() => InitializeComponent();

    /// <summary>
    /// The field itself, so the window can put the caret in it and listen for typing.
    ///
    /// <b>Exposed rather than handled in here, and the reason is the same one FilterRow gives for
    /// its button.</b> Ctrl+F belongs to the WINDOW - it is a shortcut over the whole of it, decided
    /// beside every other shortcut, and a copy of that decision living in this file would be a
    /// second place that has to agree with the first. The typing timer is the window's too: it
    /// exists to keep a once-a-second reading of the machine off somebody's keystrokes, and this
    /// row knows nothing about readings.
    /// </summary>
    internal TextBox Box => QueryBox;
}
