using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The presses of the recovery form - which line, and what about it. Everything the form means is in
/// <see cref="RecoveryForm"/>, which has no window (GUI rule 15).
/// </summary>
public partial class RecoveryFormView : UserControl
{
    public RecoveryFormView() => InitializeComponent();

    /// <summary>Where the keyboard lands when the sheet opens on the form: the first part's tick, top of the Tab order.</summary>
    internal Control WayIn => ListTick;

    /// <summary>A button on one line of the list: its kind, or taking it off the list.</summary>
    private void LinePressed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button { DataContext: RecoveryFormItem line } button || DataContext is not RecoveryForm form)
        {
            return;
        }

        if (Equals(button.Tag, "remove"))
        {
            form.Remove(line);
        }
        else
        {
            RecoveryKindChoice.Offer(button, (Style)FindResource("RowMenuItem"), line);
        }

        e.Handled = true;
    }

    private void AddPressed(object sender, RoutedEventArgs e)
    {
        if (DataContext is RecoveryForm form)
        {
            form.Add();
        }
    }
}
