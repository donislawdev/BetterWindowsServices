using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// What differs between two sides, in place of the list - the markup says the rest.
///
/// <b>It asks and does not answer</b>, the arrangement every part of this window uses: the way back
/// and the export are events, and the window decides what the middle of it shows and where a file
/// goes. Nothing here knows the list, the overview or the model exist.
/// </summary>
public partial class DiffView : UserControl
{
    public DiffView()
    {
        InitializeComponent();
    }

    /// <summary>Somebody asked for the list back.</summary>
    internal event EventHandler? Finished;

    /// <summary>Somebody asked for the comparison as a file.</summary>
    internal event EventHandler? ExportRequest;

    /// <summary>The rows as the table shows them, in the order somebody sorted them into.</summary>
    internal IReadOnlyList<DiffLine> InTheOrderOnScreen() => Table.InTheOrderOnScreen();

    private void Back(object sender, RoutedEventArgs e) => Finished?.Invoke(this, EventArgs.Empty);

    private void Export(object sender, RoutedEventArgs e) => ExportRequest?.Invoke(this, EventArgs.Empty);
}
