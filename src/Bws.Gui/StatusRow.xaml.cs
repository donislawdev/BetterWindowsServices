using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// The two lines under the list: what an answer admits about itself, and what is wrong with the
/// question.
///
/// It holds no logic at all, which is the whole point of the seam - what a result has to admit is
/// decided in <see cref="ViewModels.Sentences"/> and held by <see cref="ViewModels.Says"/>, both of
/// which can be checked without opening a window.
///
/// <b>A UserControl for the same reason SearchRow and FilterRow are</b> - one mechanism for "a part
/// of the window in its own file" rather than two that have to be told apart. The argument in full
/// is at the head of <see cref="FilterRow"/>, and it was the owner's decision on 2026-08-13.
/// </summary>
public partial class StatusRow : UserControl
{
    public StatusRow() => InitializeComponent();

    /// <summary>
    /// The button offering the rights this session does not have, so the window can wire it.
    ///
    /// <b>Exposed rather than handled in here, and this one is not the usual argument about where a
    /// decision belongs.</b> Pressing it starts a second copy of this program, and
    /// <c>Elevation.cs</c> is the only file in the product allowed to name a process at all - held
    /// by a guard that reads the sources rather than by a convention. A click handler here would be
    /// a second file reaching for that, and it would have to report its failure through a model this
    /// row deliberately does not hold.
    ///
    /// The same shape as <see cref="FilterRow.Picker"/>: the control lives with its row, and what
    /// pressing it MEANS lives with the window.
    /// </summary>
    internal Button Elevate => ElevateButton;
}
