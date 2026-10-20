using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The change history of the whole machine, in place of the list - the markup says the rest.
///
/// <b>It asks and does not answer</b>, the arrangement every part of this window uses: the way back and
/// the export are events, and the window decides what the middle of it shows and where a file goes.
/// Nothing here knows the list, the comparison or the model exist.
/// </summary>
public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    /// <summary>Somebody asked for the list back.</summary>
    internal event EventHandler? Finished;

    /// <summary>Somebody asked for the change history as a file.</summary>
    internal event EventHandler? ExportRequest;

    /// <summary>The lines as the table shows them, in the order somebody sorted them into.</summary>
    internal IReadOnlyList<HistoryRow> InTheOrderOnScreen() => Table.InTheOrderOnScreen();

    private void Back(object sender, RoutedEventArgs e) => Finished?.Invoke(this, EventArgs.Empty);

    private void Export(object sender, RoutedEventArgs e) => ExportRequest?.Invoke(this, EventArgs.Empty);
}
