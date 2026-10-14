using System.Windows;
using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// What changed since a snapshot, in place of the list - the markup says the rest.
///
/// <b>It asks and does not answer</b>, the arrangement every part of this window uses: the way back
/// is an event, and the window decides what the middle of it shows. Nothing here knows the list, the
/// overview or the model exist.
/// </summary>
public partial class DiffView : UserControl
{
    public DiffView()
    {
        InitializeComponent();
    }

    /// <summary>Somebody asked for the list back.</summary>
    internal event EventHandler? Finished;

    private void Back(object sender, RoutedEventArgs e) => Finished?.Invoke(this, EventArgs.Empty);
}
