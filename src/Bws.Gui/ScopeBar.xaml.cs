using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// The row that says which list the window is showing.
///
/// It holds nothing and decides nothing. Which positions there are is
/// <see cref="ViewModels.Scopes"/>, what each one means to the query language is there too, and
/// what happens when one is pressed is <see cref="ViewModels.MainViewModel.Scope"/> - so all three
/// can be checked without opening a window.
///
/// <b>A UserControl for the same reason FilterRow and SearchRow are</b> - one mechanism for "a part
/// of the window in its own file" rather than two that have to be told apart. The argument in full
/// is at the head of <see cref="FilterRow"/>.
/// </summary>
public partial class ScopeBar : UserControl
{
    public ScopeBar() => InitializeComponent();

    /// <summary>
    /// The Donate button at the right end of this row, since 2026-09-23.
    ///
    /// <b>Forwarded rather than handled here, the shape StatusRow gives its elevation button</b>:
    /// pressing it hands an address to the shell, and the window wires the click where the model
    /// that reports a failure is.
    /// </summary>
    internal Button Donate => DonateButton;

    /// <summary>
    /// The Help button at the very end of this row, since 2026-09-24 - forwarded for the reason
    /// Donate is: the window hangs the menu on it and answers F1 with the same menu.
    /// </summary>
    internal Button Help => HelpButton;
}
