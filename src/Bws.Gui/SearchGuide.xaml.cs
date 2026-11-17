using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The guide to the language over the questions in the list under the search box. SearchGuide.xaml
/// says what it holds. This only says which field was pressed: writing it into the box is the
/// window's, through the selection, the same road every row of the list takes (MainWindow.Write).
/// </summary>
public partial class SearchGuide : UserControl
{
    public SearchGuide() => InitializeComponent();

    /// <summary>A field was pressed.</summary>
    internal event EventHandler<FieldPickedEventArgs>? FieldPicked;

    private void FieldClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Suggestion field })
        {
            FieldPicked?.Invoke(this, new FieldPickedEventArgs(field));
        }
    }
}

/// <summary>The field pressed in the guide - its row, with its colon in <see cref="Suggestion.Written"/>.</summary>
internal sealed class FieldPickedEventArgs(Suggestion field) : EventArgs
{
    internal Suggestion Field { get; } = field;
}
