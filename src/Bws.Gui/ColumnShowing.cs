using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// Whether a column is on the list, and whether somebody chose it - two answers since 2026-09-24.
///
/// <b>They were one answer until the details panel started taking the description off the list</b>
/// (UX-GUI-012, owner's decision). The saved layout reads which columns are on OFF THE GRID, because
/// the grid is where the order and the widths live - see <see cref="ListColumns.Harvest"/> - so a
/// column collapsed for the panel would have gone into the profile as a column somebody turned off,
/// on every close with the panel open, and nothing on screen would have said so until the next
/// start opened without it. Found by reading the harvest before building the change rather than by
/// a test, and pinned since by <c>A_column_away_for_the_panel_is_kept_as_chosen_in_the_saved_layout</c>.
///
/// <b>An attached value on the column rather than a second argument to the harvest</b>, and the
/// reason is where the answer is needed: the harvest is reached from two paths in KeptColumns - a
/// scope change and a write - and neither is handed the picker, so threading one through would
/// change their signatures to carry a flag the column can carry itself.
///
/// <b>Its own file because the file it serves stands five lines under the size ratchet's band</b>
/// and this is a subject of its own - the two answers and the one place that writes both.
/// </summary>
internal static class ColumnShowing
{
    /// <summary>Whether somebody chose the column. True until told otherwise, which is what a column built by nobody means.</summary>
    private static readonly DependencyProperty ChosenProperty = DependencyProperty.RegisterAttached(
        "Chosen", typeof(bool), typeof(ColumnShowing), new PropertyMetadata(true));

    /// <summary>
    /// Puts a choice onto its column: on the list or off it, and chosen or not - always together,
    /// so the two cannot be written apart.
    /// </summary>
    internal static void Apply(DataGridColumn column, ColumnChoice choice)
    {
        column.Visibility = choice.IsOnList ? Visibility.Visible : Visibility.Collapsed;
        column.SetValue(ChosenProperty, choice.IsShown);
    }

    /// <summary>Whether somebody chose the column, which is what a layout file keeps.</summary>
    internal static bool IsChosen(DataGridColumn column) => (bool)column.GetValue(ChosenProperty);
}
