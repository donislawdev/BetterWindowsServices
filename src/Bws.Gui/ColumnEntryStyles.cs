using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// Which style an entry in the column picker wears - a heading, or a column somebody can tick.
///
/// <b>It exists because grouping a menu breaks its automation tree.</b> The picker was built with
/// <c>GroupStyle</c> first, which is the obvious way and looked right on screen. Measured from
/// outside with the menu open: twelve togglable elements, every one of them a filter chip, and not
/// one of the seventeen columns - WPF puts a <c>GroupItem</c> between a menu and its items and the
/// menu's peer does not reach through it. A screen reader sees exactly what the probe saw.
///
/// So the headings are entries in the same flat list and this tells them apart. Every entry stays a
/// real <c>MenuItem</c> with a peer of its own, and the grouping is drawn rather than structural.
///
/// <b>Beside the window rather than in the view models</b>, for the reason the whole project keeps:
/// <c>Bws.Integration.Tests</c> references the view model assembly WITHOUT UseWPF, and a view model
/// naming a WPF type would end that quietly. A StyleSelector is a WPF type.
/// </summary>
internal sealed class ColumnEntryStyles : StyleSelector
{
    public override Style? SelectStyle(object item, DependencyObject container)
    {
        var element = container as FrameworkElement;

        return item is ColumnHeading
            ? element?.TryFindResource("ColumnHeadingItem") as Style
            : element?.TryFindResource("ColumnChoiceItem") as Style;
    }
}
