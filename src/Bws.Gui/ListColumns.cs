using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// Turns the seventeen columns of `A8` into columns a DataGrid can show.
///
/// <b>Beside the window rather than in the view models, and that boundary is proved by a
/// build.</b> `Bws.Integration.Tests` references Bws.Gui deliberately WITHOUT UseWPF, so the fact
/// that no view model knows what WPF is gets checked by that project compiling at all - and a
/// DataGridColumn is as WPF as anything gets. What each column IS lives in
/// <see cref="Columns"/>, where it can be checked without a window.
///
/// <b>Nothing here binds ON a column, and that is a trap this window has already fallen into.</b>
/// Columns are not in the visual tree and inherit no data context, which is why a column header
/// could never bind to the view model and takes its text from a resource instead. So visibility is
/// SET from a subscription rather than bound, and the width and the header are read once out of
/// the theme and the language file.
/// </summary>
internal static class ListColumns
{
    /// <summary>
    /// Builds every column once and keeps them all, showing the ones that are on.
    ///
    /// <b>Built once and hidden rather than added and removed, which is a decision about what a
    /// person expects.</b> A column somebody widened, then turned off, then turned on again comes
    /// back the width they left it and in the place they dragged it to - because it is the same
    /// object, still carrying its Width and its DisplayIndex. Rebuilding it would silently throw
    /// both away, and the person would have no way to tell that from a bug.
    /// </summary>
    internal static void Fill(DataGrid grid, ColumnBar bar)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(bar);

        foreach (var choice in bar.Choices)
        {
            var column = Build(choice.Column, grid);

            Show(column, choice.IsShown);

            choice.PropertyChanged += (_, changed) =>
            {
                if (changed.PropertyName == nameof(ColumnChoice.IsShown))
                {
                    Show(column, choice.IsShown);
                }
            };

            grid.Columns.Add(column);
        }

        grid.Sorting += SortByWhatTheCellSays;
    }

    private static void Show(DataGridColumn column, bool shown) =>
        column.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

    private static DataGridColumn Build(Column column, FrameworkElement grid)
    {
        var built = column.Face switch
        {
            ColumnFace.Status => Templated(grid, "StatusCell"),
            ColumnFace.StartType => Templated(grid, "StartTypeCell"),
            ColumnFace.Number => Written(grid, column, "CellNumber", "ColumnHeadingNumber"),
            ColumnFace.Fixed => Written(grid, column, "CellFixed", headerStyle: null),
            _ => Written(grid, column, "CellText", headerStyle: null)
        };

        built.Header = Texts.Of(column.LabelKey);
        built.Width = (DataGridLength)grid.FindResource(column.WidthKey);

        // A starred column gives way and needs a floor under it. A fixed one is already sized to
        // the widest thing it can hold, and a floor on top of that would be a second number
        // claiming to decide the same width.
        if (built.Width.IsStar)
        {
            built.MinWidth = (double)grid.FindResource("ColumnFloor");
        }

        // NOT USED BY THE GRID, WHICH IS WHY IT CAN CARRY THIS. Sorting is handled below rather
        // than left to the grid, so this path is never resolved against a row - it is how the
        // handler finds out which column was clicked, by identity rather than by header text or by
        // position.
        built.SortMemberPath = column.Id;

        return built;
    }

    private static DataGridColumn Templated(FrameworkElement grid, string template) =>
        new DataGridTemplateColumn { CellTemplate = (DataTemplate)grid.FindResource(template) };

    private static DataGridColumn Written(
        FrameworkElement grid, Column column, string cellStyle, string? headerStyle)
    {
        var built = new DataGridTextColumn
        {
            // Through the indexer, like every other cell. The identifier is the language-neutral
            // half of the column and the heading is the translated half, and only one of them may
            // ever be used to ask a row for something.
            Binding = new Binding($"[{column.Id}]") { Mode = BindingMode.OneWay },
            ElementStyle = (Style)grid.FindResource(cellStyle)
        };

        if (headerStyle is not null)
        {
            built.HeaderStyle = (Style)grid.FindResource(headerStyle);
        }

        return built;
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
    private static void SortByWhatTheCellSays(object? sender, DataGridSortingEventArgs e)
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
