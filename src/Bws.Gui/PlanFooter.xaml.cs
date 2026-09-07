using System.Windows;
using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// The foot of the plan sheet, which does as little as a part of a window can.
///
/// <b>Everything it shows is decided by <see cref="ViewModels.Planned"/></b>, exactly as the sheet
/// around it is, so what the footer will say and whether its button is live can both be checked
/// without opening a window. What is left here is the one thing only a control knows: that somebody
/// pressed something.
///
/// <b>It passes presses upward rather than acting on them, and that is the seam rather than
/// ceremony.</b> The window owns the run - it holds the token that stops one, it refuses to close
/// while one is in flight, and it is on the short list of files allowed to build a writer at all.
/// A footer that reached for a manager would be a third composition point where rule 1 of the
/// project's untouchable rules allows one.
///
/// <b>Two events rather than letting the clicks bubble to the sheet, which they would.</b> Click is
/// routed, so a handler on the sheet's own markup would catch both - and would then have to tell
/// them apart by looking at what was pressed, which is the shape that turns "which button" into a
/// string comparison. The panel above already carries one handler of that kind, for the copy
/// buttons, and it is that way because a template in a plain dictionary has no class behind it.
/// This has one.
/// </summary>
public partial class PlanFooter : UserControl
{
    /// <summary>Somebody pressed the button under the plan.</summary>
    internal event EventHandler? CarryOutRequest;

    /// <summary>Somebody asked a run in progress to stop before its next step.</summary>
    internal event EventHandler? InterruptRequest;

    public PlanFooter() => InitializeComponent();

    /// <summary>
    /// The button that changes a machine, so a test can ask whether it is live.
    ///
    /// <b>The control rather than the model's answer, and the difference is the whole point.</b>
    /// Planned.CanCarryOut can be perfectly right while nothing binds to it, and a button that
    /// stays live through a run is a second ask one click away - which is exactly the kind of fault
    /// a binding that resolves to nothing produces in silence.
    /// </summary>
    internal Button CarryOut => CarryOutButton;

    /// <summary>The way to stop a run, which only exists while there is one.</summary>
    internal Button Interrupt => InterruptButton;

    /// <summary>The line saying why this cannot be carried out here.</summary>
    internal TextBlock Blocked => PlanBlocked;

    /// <summary>Which step is happening, while one is.</summary>
    internal TextBlock Progress => PlanProgress;

    /// <summary>
    /// The box the entry's name has to be typed into, and where the keyboard is sent when it is
    /// there.
    ///
    /// <b>Exposed rather than kept private, because the two questions worth asking about it cannot
    /// be asked from anywhere else:</b> whether it is on the screen at all for a plan that ends
    /// more than one entry, and whether it is what the sheet hands the keyboard to - which is what
    /// keeps Enter on an open forcing sheet from ending a process.
    /// </summary>
    internal TextBox Confirm => ConfirmBox;

    /// <summary>Whether the box and its label are on the screen at all, heading included.</summary>
    internal bool ConfirmShown => ConfirmSection.Visibility == Visibility.Visible;

    private void CarryOutRequested(object sender, RoutedEventArgs e) =>
        CarryOutRequest?.Invoke(this, EventArgs.Empty);

    private void InterruptRequested(object sender, RoutedEventArgs e) =>
        InterruptRequest?.Invoke(this, EventArgs.Empty);
}
