using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// "Show" over a table - the markup says the rest.
///
/// <b>The menu is made here, one MenuItem per choice</b>, the way the bar makes the items under
/// "Audit..." - each a real item with its own automation peer from the moment the choices arrive,
/// rather than containers an ItemsSource makes only once the menu opens (`docs/10` traps 7 and 19).
/// What a choice does is the choices' own, bound through the item's style. A line goes between two
/// groups, because each group is its own question with its own tick.
/// </summary>
public partial class ShowRow : UserControl
{
    private string _named = string.Empty;

    public ShowRow()
    {
        InitializeComponent();

        DataContextChanged += (_, changed) =>
        {
            // THE CHOICES CAN BE MADE AGAIN UNDER THE SAME ROW - the change history makes them again for
            // every reading, because the counts in them are that reading's. So the menu follows Groups,
            // and lets go of whatever it followed before, or an old history would keep rebuilding a
            // menu that is no longer on screen.
            if (changed.OldValue is INotifyPropertyChanged before)
            {
                before.PropertyChanged -= GroupsMoved;
            }

            if (changed.NewValue is INotifyPropertyChanged now)
            {
                now.PropertyChanged += GroupsMoved;
            }

            OfferTheChoices();
        };
    }

    /// <summary>
    /// The name of the table this row narrows, which the automation names start with - "diff" gives
    /// "diffShow" and "diffShowing", the names the comparison's row had before it was shared.
    /// </summary>
    public string Named
    {
        get => _named;
        set
        {
            _named = value;
            AutomationProperties.SetAutomationId(ShowButton, value + "Show");
            AutomationProperties.SetAutomationId(ShowingText, value + "Showing");
        }
    }

    private void ShowAsked(object sender, RoutedEventArgs e) => ButtonMenu.OpenUnder(ShowButton);

    private void GroupsMoved(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IRowChoices.Groups))
        {
            OfferTheChoices();
        }
    }

    /// <summary>The choices of the table now on screen, as items of the menu under the button.</summary>
    private void OfferTheChoices()
    {
        ShowMenu.Items.Clear();

        if (DataContext is not IRowChoices choices)
        {
            return;
        }

        var style = (Style)FindResource("ShowChoiceItem");

        foreach (var group in choices.Groups)
        {
            if (ShowMenu.Items.Count > 0)
            {
                ShowMenu.Items.Add(new Separator());
            }

            foreach (var choice in group)
            {
                ShowMenu.Items.Add(new MenuItem { DataContext = choice, Style = style });
            }
        }
    }
}
