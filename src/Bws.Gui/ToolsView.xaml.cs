using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The Tools tab - `docs/PROJEKT-TOOLS-20261020.md`.
///
/// <b>It decides nothing.</b> Which sections and cards there are is <see cref="ToolsPage"/>, whether a card
/// can be pressed is the card's, and what a press opens is the window's (MainWindow.OpenFresh, the door a
/// plan for one named entry comes through). This forwards the press, the shape ActionBar gives its buttons.
/// </summary>
public partial class ToolsView : UserControl
{
    public ToolsView() => InitializeComponent();

    /// <summary>A card was pressed - the window opens its plan.</summary>
    internal event EventHandler<CardAsked>? CardRequest;

    private void CardPressed(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ToolCard card })
        {
            CardRequest?.Invoke(this, new CardAsked(card));
        }
    }
}

/// <summary>Which card was pressed.</summary>
internal sealed class CardAsked(ToolCard card) : EventArgs
{
    internal ToolCard Card { get; } = card;
}
