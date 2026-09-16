using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// How the panel about one entry opens - from Enter, from a double click, and from the row menu -
/// and the one road all three take.
///
/// <b>Its own file since 2026-09-15, and the reason is a road that was one lane wide.</b> The panel
/// opened from Enter and from nothing else: no double click, no item in the menu, and not one
/// sentence in the language file mentioning Enter. `docs/11` 9.1 holds that everything a mouse can
/// do a keyboard can do, and that direction was kept. The other direction had no guard, so a person
/// with a mouse had 223 lines of panel behind a key nobody told them about - found by a review of
/// the window as a stranger would meet it, not by a test. Three doors now, one road, here.
///
/// <b>A partial rather than a type of its own</b> for the reason the menu file gives: what happens
/// here reads the grid's selection and the grid's containers, and both are the window's to read.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Shows everything about one entry, putting the plan sheet away first.
    ///
    /// <b>The one road, since 2026-09-15.</b> Enter used to do these three things inside
    /// <see cref="Act"/>, and a double click and a menu item doing them again would have been the
    /// shape this window has been caught by before - two roads to one fact, one of them repaired
    /// while the other stayed wrong (2026-09-02, a row and a cell). So Enter goes through here too.
    ///
    /// <b>The entry is made the selection first</b>, by the same rule a right click uses: a row
    /// already picked keeps the whole selection, one outside it replaces it. Without that a double
    /// click on a row outside a selection of five would open a panel about a row the list does
    /// not show as chosen, and the next Ctrl+C would copy the five.
    ///
    /// <b>The two panels share a column, so opening one puts the other away.</b> Arranged here
    /// rather than by either of them, because neither has any business knowing the other exists -
    /// the window is what owns the layout they compete for. The plan sheet does the same in the
    /// other direction, at Preview.
    /// </summary>
    internal bool OpenDetailsOf(EntryRow entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        PointAt(entry);

        _model.Chosen.Row = entry;
        _model.Planned.Hide();

        return _model.Chosen.Show();
    }

    /// <summary>
    /// A double click on a row opens the panel about it. On anything else in the grid it is not
    /// ours.
    ///
    /// <b>The row under the pointer, not the selection</b>, for the reason the menu keeps a
    /// pointed row: the panel shows one entry, and the one somebody clicked twice is the only
    /// honest answer. What is under the pointer is asked the way the right click asks it, in
    /// <see cref="RowUnder"/>, so a heading, a column's resize grip, the scroll bar and the empty
    /// space under the last row all answer nothing - and a double click on the grip still does what
    /// the grid does with it, which is fit the column, because this leaves the press unhandled.
    ///
    /// <b>The left button only.</b> The event is raised for every button, and a double right click
    /// is two menus, not a panel.
    ///
    /// <b>The plan sheet never sees this.</b> It lies over the whole window with a scrim that takes
    /// every hit - `docs/10` trap 11, a painted brush takes the pointer - so a double click while a
    /// plan is on screen reaches the scrim and stops there.
    /// </summary>
    private void OpenDetailsOnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || RowUnder(e.OriginalSource) is not { } entry)
        {
            return;
        }

        e.Handled = OpenDetailsOf(entry);
    }

    /// <summary>
    /// The entry whose row a press landed on, or nothing when it landed on no row.
    ///
    /// <c>ContainerFromElement</c> rather than a hand written walk up the visual tree, and that is
    /// not only shorter: the thing under a pointer can be a content element rather than a visual
    /// one, and <c>VisualTreeHelper.GetParent</c> throws on those. A press on a heading resolves to
    /// no row container, which is the answer that keeps a heading's own gestures its own.
    /// </summary>
    private EntryRow? RowUnder(object? source) =>
        source is DependencyObject element
            && ItemsControl.ContainerFromElement(Entries, element) is DataGridRow row
            && row.Item is EntryRow entry
            ? entry
            : null;
}
