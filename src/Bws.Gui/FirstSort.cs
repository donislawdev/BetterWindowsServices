using System.ComponentModel;
using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// The order a read only table opens in, sorted for real and marked on its heading - owner's decision
/// AS-d of 2026-10-20: the change history opened newest first with no mark on any heading, so nothing
/// said that a heading sorts, and the owner never pressed one.
///
/// <b>A REAL SORT, NOT A MARK OVER AN ORDER TAKEN FROM ELSEWHERE.</b> The rows already arrive newest
/// first, and a mark alone would be true only for as long as whoever makes them keeps that order. A sort
/// description makes the mark true by construction, and pressing the heading turns it round the way
/// pressing any heading does.
///
/// <b>LAID DOWN AGAIN WHENEVER THE ROWS ARE A NEW COLLECTION, and only when nobody has sorted them.</b>
/// The grid clears every sort, and every heading's mark, when its ItemsSource changes - the change
/// history is a new sheet on every opening - so a sort laid once at load would be gone the second time.
/// The same collection refilled (Show, the search, F5) keeps whatever order somebody chose.
///
/// <b>Called from the table's code with nameof, not set in its markup.</b> A property name written in
/// markup is a string nothing checks, and the attached property that would carry it has setters no code
/// calls - which DeadCodeGuards rightly refuses.
///
/// <b>Heard through the property's descriptor, which holds the grid for the life of the process.</b> The
/// tables that keep a first sort live as long as the window, and the price in the catalogue - every cell
/// a grid held to the end - is a window that is closed at the end of a look anyway. Loaded and Unloaded
/// would free them, and are never raised on a window that is not shown, which is every window the tests
/// build.
/// </summary>
internal static class FirstSort
{
    private static readonly DependencyPropertyDescriptor Rows =
        DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid));

    /// <summary>Sorts the table by this property now, and again whenever its rows become a new collection nobody has sorted.</summary>
    /// <param name="path">The SortMemberPath of one of its columns - the property the column shows.</param>
    internal static void Keep(DataGrid grid, string path, ListSortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(grid);

        Rows.AddValueChanged(grid, (_, _) => Sort(grid, path, direction));
        Sort(grid, path, direction);
    }

    /// <summary>The first order on rows nobody has sorted - nothing while there are no rows, or a sort is there.</summary>
    private static void Sort(DataGrid grid, string path, ListSortDirection direction)
    {
        if (grid.ItemsSource is null || grid.Items.SortDescriptions.Count > 0)
        {
            return;
        }

        grid.Items.SortDescriptions.Add(new SortDescription(path, direction));

        foreach (var column in grid.Columns)
        {
            column.SortDirection = string.Equals(column.SortMemberPath, path, StringComparison.Ordinal) ? direction : null;
        }
    }
}
