using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The table under a comparison, or the sentence in its place - the markup says the rest.
///
/// <b>Two headings are set here rather than bound, and that is WPF rather than taste.</b> A DataGrid
/// column is not an element: it has no place in the tree and inherits no data context, so a binding on
/// its Header has nothing to resolve against. The heading over the values names the sides - "In the
/// snapshot" and "Now", or "Earlier" and "Later" (W2) - so it is taken from the sheet whenever the
/// sheet changes. The rows themselves are an ordinary binding.
/// </summary>
public partial class DiffTable : UserControl
{
    public DiffTable()
    {
        InitializeComponent();

        // By entry, with the mark on its heading saying so (AS-d).
        FirstSort.Keep(Lines, nameof(DiffLine.ServiceName), ListSortDirection.Ascending);

        DataContextChanged += (_, _) => Head();
    }

    /// <summary>
    /// The rows as the table shows them, sorted the way somebody sorted them - the view over the rows
    /// rather than the rows, the argument the list's export makes (MainWindow.Exporting.cs).
    /// </summary>
    internal IReadOnlyList<DiffLine> InTheOrderOnScreen() =>
        CollectionViewSource.GetDefaultView(Lines.ItemsSource) is { } view ? [.. view.Cast<DiffLine>()] : [];

    private void Head()
    {
        if (DataContext is DiffSheet sheet)
        {
            BeforeColumn.Header = sheet.BeforeHeading;
            AfterColumn.Header = sheet.AfterHeading;
        }
    }
}
