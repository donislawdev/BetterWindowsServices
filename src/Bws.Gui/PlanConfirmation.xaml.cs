using System.Windows;
using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// The heavy ask at the foot of a plan sheet: the sentence saying why, and the entry's name typed
/// back - or, on a selection, the refusal that stands where the box would.
///
/// <b>Everything it shows is decided by <see cref="ViewModels.Planned"/></b>, as the footer around
/// it is. It left PlanFooter.xaml on 2026-09-29, when the refusal of backlog 475 needed room and
/// that file stood one line under the markup share the size ratchet calls near. What stays in this
/// class is what only a control can answer: which box to hand the keyboard, and whether it is there.
/// </summary>
public partial class PlanConfirmation : UserControl
{
    public PlanConfirmation() => InitializeComponent();

    /// <summary>The box the entry's name is typed into.</summary>
    internal TextBox Box => ConfirmBox;

    /// <summary>
    /// Whether the box is on the screen - not the section. On a refused selection the section
    /// stands with its reason and no box, and the keyboard has nowhere to go here.
    /// </summary>
    internal bool BoxShown => ConfirmSection.Visibility == Visibility.Visible
        && TypingPart.Visibility == Visibility.Visible;
}
