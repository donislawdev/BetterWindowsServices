using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The machine overview, and the two things only a control knows: that somebody clicked a number,
/// and that somebody asked for the list instead.
///
/// <b>It holds no logic at all, which is the whole point of the seam.</b> Which numbers there are is
/// <see cref="ViewModels.Overview"/>, what clicking one MEANS is
/// <see cref="MainViewModel.Ask"/>, and both can be checked without opening a window.
///
/// <b>A UserControl for the same reason SearchRow, FilterRow and StatusRow are</b> - one mechanism
/// for "a part of the window in its own file" rather than two that have to be told apart. The
/// argument in full is at the head of <see cref="FilterRow"/>, and it was the owner's decision on
/// 2026-08-13.
/// </summary>
public partial class OverviewView : UserControl
{
    public OverviewView() => InitializeComponent();

    /// <summary>
    /// Raised when somebody has finished with this screen, either by asking one of its questions or
    /// by asking for the list.
    ///
    /// <b>An event rather than this control writing the file, and the reason is the one every part
    /// of this window gives.</b> Remembering that the overview has been seen means writing to
    /// somebody's profile, and the only thing in this window allowed to do that is
    /// <see cref="KeptColumns"/> - which the window owns and this control has never heard of.
    /// </summary>
    internal event EventHandler? Finished;

    /// <summary>
    /// Asks the question the clicked number stands for.
    ///
    /// <b>The model does the asking and this only says who was clicked</b>, which keeps the two
    /// halves checkable apart: whether a click reaches the right line is about a control, and
    /// whether the right line carries the right query is about a view model.
    /// </summary>
    private void AskThis(object sender, RoutedEventArgs e)
    {
        // Filtered by type rather than cast, so a template holding something unexpected does
        // nothing instead of throwing over a screen somebody is only reading.
        if (sender is not Button { DataContext: OverviewLine line }
            || DataContext is not MainViewModel model)
        {
            return;
        }

        model.Ask(line);

        Finished?.Invoke(this, EventArgs.Empty);
    }

    private void Dismiss(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel model)
        {
            return;
        }

        // The query is deliberately left alone. Somebody who presses this has not asked anything,
        // so putting a question in the box on their way out would be the window deciding what they
        // meant - and they would then have to undo it to see the machine.
        model.ShowingOverview = false;

        Finished?.Invoke(this, EventArgs.Empty);
    }
}
