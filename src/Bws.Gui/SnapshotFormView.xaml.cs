using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Bws.Gui;

/// <summary>
/// The view before a snapshot is taken, in place of the list - the markup says the rest.
///
/// <b>It asks and does not answer</b>, like the comparison beside it: saving and going back are events,
/// and the window decides which dialog opens and what the middle of it shows.
/// </summary>
public partial class SnapshotFormView : UserControl
{
    public SnapshotFormView()
    {
        InitializeComponent();
    }

    /// <summary>Somebody asked for the list back.</summary>
    internal event EventHandler? Finished;

    /// <summary>Somebody asked to choose where the snapshot goes, and to take it.</summary>
    internal event EventHandler? SaveRequest;

    /// <summary>
    /// Puts the keyboard in the note, once the view is on screen - somebody who opened this from the
    /// menu is about to type, or to press Enter and go straight to the dialog.
    ///
    /// <b>At input priority rather than now</b>, the reason GiveTheBoxTheKeyboard gives in the window:
    /// the view was collapsed a moment ago, and the keyboard cannot land in a box that is not laid out.
    /// </summary>
    internal void TakeTheKeyboard() =>
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, () => NoteBox.Focus());

    private void Back(object sender, RoutedEventArgs e) => Finished?.Invoke(this, EventArgs.Empty);

    private void Save(object sender, RoutedEventArgs e) => SaveRequest?.Invoke(this, EventArgs.Empty);
}
