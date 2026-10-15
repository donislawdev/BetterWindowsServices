using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// "Show" over a comparison - the markup says the rest.
///
/// <b>The menu is made here, one MenuItem per choice</b>, the way the bar makes the items under
/// "Snapshots..." - each a real item with its own automation peer from the moment the comparison
/// arrives, rather than containers an ItemsSource makes only once the menu opens (`docs/10` traps 7 and
/// 19). What a choice does is the filter's, bound through the item's style.
/// </summary>
public partial class DiffFilterRow : UserControl
{
    public DiffFilterRow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OfferTheChoices();
    }

    private void ShowAsked(object sender, RoutedEventArgs e) => ButtonMenu.OpenUnder(ShowButton);

    /// <summary>The choices of the comparison now on screen, as items of the menu under the button.</summary>
    private void OfferTheChoices()
    {
        ShowMenu.Items.Clear();

        if (DataContext is not DiffSheet sheet)
        {
            return;
        }

        var style = (Style)FindResource("DiffChoiceItem");

        foreach (var choice in sheet.Filter.Choices)
        {
            ShowMenu.Items.Add(new MenuItem { DataContext = choice, Style = style });
        }
    }
}
