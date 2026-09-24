namespace Bws.Gui;

/// <summary>
/// The window's half of folding the filter chips away: it opens the way this profile last left it,
/// and every press of the toggle is written down. UX-GUI-007, owner's decision 2026-09-24.
///
/// <b>Why it is remembered now and was not before.</b> The chips took nearly half the height of the
/// window above the list, and the toggle folding them was forgotten at every start - FilterRow.xaml
/// said so, and gave the reason: the one file this program keeps is a contract, and a new field is a
/// schema bump. The owner made that bump (schema 5, <see cref="ViewModels.ColumnLayouts.FiltersFolded"/>).
///
/// <b>A first run still opens with the chips showing</b> - complaint 7 of `docs/11` was closed by
/// making them visible, and nothing here reopens it: no file, or a file that never folded them, is
/// "open".
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Sets the toggle from the kept profile and writes each press back. Called once, from the
    /// constructor, after the kept layout has been read.
    ///
    /// <b>Set BEFORE the handlers are hung</b>, so opening the window writes nothing: the file
    /// already says what the toggle is being set to.
    /// </summary>
    private void KeepTheFiltersAsLeft()
    {
        if (_kept is not { } kept)
        {
            return;
        }

        Filters.Switch.IsChecked = !kept.FiltersFolded;

        Filters.Switch.Checked += (_, _) => kept.TheFiltersWere(folded: false, _model.Says);
        Filters.Switch.Unchecked += (_, _) => kept.TheFiltersWere(folded: true, _model.Says);
    }
}
