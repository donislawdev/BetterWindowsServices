using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>The table under the change history, or the sentence in its place - the markup says the rest.</summary>
public partial class HistoryTable : UserControl
{
    public HistoryTable()
    {
        InitializeComponent();

        // Newest first, with the mark on When saying so (AS-d).
        FirstSort.Keep(Lines, nameof(HistoryRow.When), ListSortDirection.Descending);
    }

    /// <summary>
    /// The lines as the table shows them, sorted the way somebody sorted them - the view over the rows
    /// rather than the rows, the argument the comparison's export makes (DiffTable).
    /// </summary>
    internal IReadOnlyList<HistoryRow> InTheOrderOnScreen() =>
        CollectionViewSource.GetDefaultView(Lines.ItemsSource) is { } view ? [.. view.Cast<HistoryRow>()] : [];
}
