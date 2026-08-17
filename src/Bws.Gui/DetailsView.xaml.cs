using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The details panel, which does as little as a part of a window can.
///
/// Everything it shows is decided by <see cref="Chosen"/>, so what the panel will say about an
/// entry can be checked without opening a window - the same split <see cref="MainWindow"/> makes
/// and for the same reason. What is left here is the one thing only a control knows: that somebody
/// pressed the button.
///
/// <b>A UserControl rather than a template in the theme, and it is the reason this file is short.</b>
/// A theme file has no code-behind class, so a close button living there would have to be found by
/// name at construction and its click taken by the window - which is what the row of filters does,
/// and it cost a lookup, a refusal and a guard. A part of the window with behaviour of its own is
/// what a UserControl is for.
/// </summary>
public partial class DetailsView : UserControl
{
    public DetailsView() => InitializeComponent();

    private void CloseRequested(object sender, RoutedEventArgs e) => Dismiss();

    /// <summary>
    /// Puts the panel away, and says whether there was one to put away.
    ///
    /// <b>Apart from the handler so that it can be checked at all</b> - a handler the framework
    /// calls is reachable only by clicking, and the answer is the part worth asserting: a button
    /// that does nothing looks exactly like a feature that is not there.
    ///
    /// <b>Not named Close.</b> This is a control inside a window, and a method called Close on
    /// something inside a window reads as closing the window - which is the one thing this must
    /// never be mistaken for in a tool that stops services.
    /// </summary>
    internal bool Dismiss() => (DataContext as Chosen)?.Hide() ?? false;
}
