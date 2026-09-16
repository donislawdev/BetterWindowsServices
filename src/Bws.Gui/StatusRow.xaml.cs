using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The line under the list: what the answer admits about itself, the way out of a session with no
/// administrator rights, and what is wrong with the question.
/// </summary>
public partial class StatusRow : UserControl
{
    public StatusRow() => InitializeComponent();

    internal Button Elevate => ElevateButton;

    /// <summary>The words in the notice line that press the switch - for the guard that presses them.</summary>
    internal System.Windows.Documents.Hyperlink NoticeLinkWords => FoldLink;

    /// <summary>
    /// The words "Show every instance" inside the sentence about folded copies, pressed.
    ///
    /// <b>Point 8(d) of `docs/11` 2.14.</b> The sentence used to name the switch and leave the
    /// reader to find it on the row above - now the name is the switch. It only ever turns the
    /// folding OFF: the sentence exists because copies are folded, so there is nothing to fold
    /// back from where it stands, and the switch above is where that is done.
    ///
    /// <b>One line of view code, the same shape as every other Click in this window</b>: the
    /// decision is the model's property, and this hands the press to it.
    /// </summary>
    private void ShowEveryInstance(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel model)
        {
            model.ShowingEveryInstance = true;
        }
    }
}
