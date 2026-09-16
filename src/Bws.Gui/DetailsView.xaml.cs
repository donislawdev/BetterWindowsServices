using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The details panel, which does as little as a part of a window can.
///
/// Everything it shows is decided by <see cref="Chosen"/>, so what the panel will say about an
/// entry can be checked without opening a window - the same split <see cref="MainWindow"/> makes
/// and for the same reason. What is left here is the two things only a control knows: that
/// somebody pressed the button, and where its own scroll bar stands.
///
/// <b>A UserControl rather than a template in the theme, and it is the reason this file is short.</b>
/// A theme file has no code-behind class, so a close button living there would have to be found by
/// name at construction and its click taken by the window - which is what the row of filters does,
/// and it cost a lookup, a refusal and a guard. A part of the window with behaviour of its own is
/// what a UserControl is for.
/// </summary>
public partial class DetailsView : UserControl
{
    public DetailsView()
    {
        InitializeComponent();
        DataContextChanged += Rebound;
    }

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

    /// <summary>
    /// Follows the model's <see cref="Chosen.Opened"/> across whatever model the panel is bound
    /// to - the window's, or the catalogue's specimens.
    ///
    /// <b>Unsubscribed from the old one first</b>, which is the teardown case: a handler left on a
    /// model nobody shows any more would keep this control alive and keep scrolling a panel nobody
    /// is looking at.
    /// </summary>
    private void Rebound(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is Chosen before)
        {
            before.Opened -= StartAtTheTop;
        }

        if (e.NewValue is Chosen after)
        {
            after.Opened += StartAtTheTop;
        }
    }

    /// <summary>
    /// Every opening starts at the top, and only an opening does.
    ///
    /// <b>The scroll position outlives the entry, and that was measured on the owner's own capture
    /// of 2026-09-16:</b> the panel opened on a new entry with the first lines of its description
    /// above the fold, because swapping the sections underneath a ScrollViewer leaves its offset
    /// where it was. A refresh of the same entry keeps the offset on purpose - somebody reading a
    /// long description must not have it snatched away once a second - so the model says which of
    /// the two just happened, rather than the view guessing from a property change.
    /// </summary>
    private void StartAtTheTop(object? sender, System.EventArgs e) => Body.ScrollToTop();
}
