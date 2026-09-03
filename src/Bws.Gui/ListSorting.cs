using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// WHAT ORDER THE LIST IS IN - the click that changes it, the order a file remembers, and the one
/// question only a grid can answer about it.
///
/// <b>Its own file since 2026-08-25, and the size ratchet is what asked.</b> ListColumns crossed
/// five hundred lines when a kept order arrived, and the seam it pointed at is real: that file is
/// about which columns there are, how wide and in what arrangement, and this is about the order of
/// the ROWS underneath them.
///
/// <b>What is NOT here is the comparison itself.</b> That lives in <c>Columns.OrderedBy</c>, where
/// it can be asked without a desktop - what is left here is the half only a grid can do: noticing
/// the click, agreeing which way round it is, and taking the mark off every other heading.
/// </summary>
internal static class ListSorting
{
    /// <summary>
    /// Which column the grid is sorted by, and which way round, or nothing when it is not.
    ///
    /// <b>Read off the headings rather than off the view.</b> A ListCollectionView carries a
    /// CustomSort, which is an IComparer and cannot be asked which column it came from - the
    /// heading is the only place that answer exists, and it is also the place a person sees it.
    ///
    /// A column the catalogue does not know is nothing rather than a name written into somebody's
    /// file, the same rule Harvest above follows for the columns themselves.
    /// </summary>
    internal static KeptSort? Of(DataGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            if (column.SortDirection is { } way
                && Columns.Of(column.SortMemberPath ?? string.Empty) is { } known)
            {
                return new KeptSort(known.Id, way == ListSortDirection.Descending);
            }
        }

        return null;
    }

    /// <summary>
    /// Puts the list in the order a file remembers, or takes every order off it.
    ///
    /// <b>NOT CALLED FROM <see cref="Fill"/>, AND THAT IS THE WHOLE REASON THIS IS ITS OWN
    /// METHOD.</b> Fill runs before any row exists - the grid has no ItemsSource yet, so there is
    /// no view to hand a comparer to and the sort would be dropped in silence. It is applied once
    /// the first reading has arrived, and again whenever the scope moves, which is where Reapply
    /// already stands.
    ///
    /// <b>A column that is turned off is not sorted by</b>, and that is a decision rather than an
    /// omission: a list ordered by something nobody can see is a list whose order cannot be read.
    /// The kept sort stays in the file, so turning the column back on brings the order back with
    /// it.
    ///
    /// The comparison itself is <c>Columns.OrderedBy</c>, the same one a click uses, so a list
    /// restored from a file and a list somebody just clicked are in the same order by
    /// construction rather than by two pieces of code agreeing.
    ///
    /// <b>THE HEADING IS MARKED WHETHER OR NOT THERE IS A VIEW TO SORT, SINCE 2026-09-03, AND
    /// THAT IS THE WHOLE OF BACKLOG 315.</b> This used to leave the moment it found no view,
    /// before marking anything - so a caller that had just asked for an order got one silently
    /// and only sometimes, depending on whether the grid's rows binding had resolved. Measured
    /// over twenty ways back: a window nobody had shown came out of it carrying no order at all
    /// SEVENTEEN times, a settled one none out of twenty. That race then had to be answered a
    /// second time in <c>KeptColumns</c>, by a line seeding the layout for a case nothing could
    /// reach - two roads to one fact, one of them unguarded.
    ///
    /// <b>What is lost by marking without a view is nothing, and what would be lost by not
    /// marking is the caller's promise.</b> No view means no rows, so a heading claiming an order
    /// misrepresents nothing - there is nothing to be out of order. The comparer is still applied
    /// only when there is somewhere to put it, and <c>MainWindow.SortAsKept</c> still exists for
    /// exactly that reason: it runs once the first reading has arrived and hands the view the
    /// comparer this call could not.
    /// </summary>
    internal static void By(DataGrid grid, KeptSort? sort)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var view = CollectionViewSource.GetDefaultView(grid.ItemsSource) as ListCollectionView;

        foreach (var column in grid.Columns)
        {
            column.SortDirection = null;
        }

        if (view is not null)
        {
            view.CustomSort = null;
        }

        if (sort is not { } wanted || Columns.Of(wanted.Id) is not { } known)
        {
            return;
        }

        var heading = grid.Columns.FirstOrDefault(column =>
            string.Equals(column.SortMemberPath, wanted.Id, StringComparison.Ordinal));

        if (heading is null || heading.Visibility != Visibility.Visible)
        {
            return;
        }

        heading.SortDirection = wanted.Descending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        if (view is not null)
        {
            view.CustomSort = Columns.OrderedBy(known, !wanted.Descending);
        }
    }

    /// <summary>
    /// Sorts by what the column says, rather than by the property a binding happens to name.
    ///
    /// <b>This is a repair as much as it is new machinery.</b> Left to the grid, a column sorts by
    /// its binding path, and eleven of the seventeen columns bind through an indexer - which
    /// resolves to no property at all, so the grid would sort by nothing and say nothing about it.
    /// A column that quietly does not sort is worse than one that cannot.
    ///
    /// <b>It also fixes the process identifier, which has been sorting wrongly since sorting was
    /// turned on.</b> That column binds to text, so 103292 came before 9. `Column.Sorts` is where
    /// a column says its order is not its text, and it is the only one that does.
    ///
    /// Nothing is sorted here that the catalogue does not know, and the grid keeps its own
    /// behaviour in that case rather than being overruled by a handler that could not help.
    /// </summary>
    internal static void WhenAHeadingIsClicked(object? sender, DataGridSortingEventArgs e)
    {
        if (sender is not DataGrid grid
            || Columns.Of(e.Column.SortMemberPath ?? string.Empty) is not { } column
            || CollectionViewSource.GetDefaultView(grid.ItemsSource) is not ListCollectionView view)
        {
            return;
        }

        var ascending = e.Column.SortDirection != ListSortDirection.Ascending;

        foreach (var other in grid.Columns)
        {
            other.SortDirection = null;
        }

        e.Column.SortDirection = ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;

        // The comparison itself lives in Columns.OrderedBy, where it can be asked without a
        // desktop. What is left here is the half only a grid can do: noticing the click, agreeing
        // which way round it is, and taking the sort off every other heading.
        view.CustomSort = Columns.OrderedBy(column, ascending);

        e.Handled = true;
    }
}
