using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The plan panel, which does as little as a part of a window can.
///
/// Everything it shows is decided by <see cref="Planned"/>, so what the panel will say about an
/// operation can be checked without opening a window - the same split the details panel makes and for
/// the same reason. What is left here is the one thing only a control knows: that somebody pressed
/// the button.
///
/// <b>IT ASKS RATHER THAN CARRIES OUT, and that is the seam rather than a limitation.</b> Since
/// 2026-08-19 this panel has a button that changes a machine - and what it does when pressed is
/// raise an event. The window is what owns the run: it holds the token that stops it, it refuses to
/// close while one is in flight, and it is on the list of files allowed to build a writer at all.
/// A panel that reached for the manager itself would be a second composition point, which is
/// precisely what rule 1 of the project's untouchable rules exists to keep to one.
/// </summary>
public partial class PlanView : UserControl
{
    /// <summary>
    /// Somebody pressed the button under the plan.
    ///
    /// <b>An event rather than a call, for the reason at the head of this class</b> - and the same
    /// shape this window already uses to reach a menu opened under a button, so it introduces no
    /// mechanism of its own.
    /// </summary>
    internal event EventHandler? CarryOutRequest;

    /// <summary>Somebody asked a run in progress to stop before its next step.</summary>
    internal event EventHandler? InterruptRequest;

    public PlanView() => InitializeComponent();

    private void CloseRequested(object sender, RoutedEventArgs e) => Dismiss();

    private void CarryOutRequested(object sender, RoutedEventArgs e) =>
        CarryOutRequest?.Invoke(this, EventArgs.Empty);

    private void InterruptRequested(object sender, RoutedEventArgs e) =>
        InterruptRequest?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Puts the panel away, and says whether there was one to put away.
    ///
    /// <b>Apart from the handler so that it can be checked at all</b> - a handler the framework calls
    /// is reachable only by clicking, and the answer is the part worth asserting: a button that does
    /// nothing looks exactly like a feature that is not there.
    ///
    /// <b>Not named Close</b>, for the reason the details panel gives: a method called Close on
    /// something inside a window reads as closing the window, which is the one thing it must never be
    /// mistaken for in a tool that stops services.
    /// </summary>
    internal bool Dismiss() => (DataContext as Planned)?.Hide() ?? false;

    /// <summary>
    /// The heading, so a test can read what a person would read.
    ///
    /// <b>Exposed for the reason the empty state exposes its sentence: a binding that resolves to
    /// nothing is silent.</b> Nothing in a build says so and nothing on a screenshot does either - a
    /// dead binding here paints a blank panel, which is indistinguishable from a feature that has not
    /// been built. What the panel DECIDES is covered without a window in Planned's own tests, and this
    /// is the one question that cover cannot ask.
    /// </summary>
    internal TextBlock Heading => PlanHeading;

    /// <summary>The line saying nothing has happened, which is the one this panel must never lose.</summary>
    internal TextBlock Notice => PlanNotice;

    /// <summary>
    /// The line saying why this cannot be carried out here.
    ///
    /// <b>Read off the control rather than off the model</b>, because the fault it exists against is
    /// exactly the one a dead binding produces: a session that cannot change anything, a live
    /// button, and nothing on screen to say so.
    /// </summary>
    internal TextBlock Blocked => PlanBlocked;

    /// <summary>
    /// The steps as they reach the screen.
    ///
    /// Read off the control's own items rather than off the model, so a binding that resolved to
    /// nothing comes back empty rather than coming back right from the other side of it.
    /// </summary>
    internal IReadOnlyList<string> StepLines =>
        [.. StepList.Items.OfType<PlanLine>().Select(line => line.Text)];

    /// <summary>The entries that got no plan, as they reach the screen.</summary>
    internal IReadOnlyList<string> ProblemLines => [.. ProblemList.Items.OfType<string>()];

    /// <summary>The command lines, as they reach the screen. `E5`.</summary>
    internal IReadOnlyList<string> CommandLines => [.. CommandList.Items.OfType<string>()];

    /// <summary>What did not work, as it reaches the screen.</summary>
    internal IReadOnlyList<string> FailureLines => [.. FailureList.Items.OfType<string>()];

    /// <summary>The way back, as it reaches the screen.</summary>
    internal IReadOnlyList<string> WayBackLines => [.. WayBackList.Items.OfType<string>()];

    /// <summary>
    /// Whether the refusals section is on the screen at all, heading included.
    ///
    /// <b>Its visibility rather than its contents, because those are two different faults.</b> An
    /// empty list under a heading reading "Not included, and why" states something false, and the
    /// list being empty is exactly what a test reading only the lines would call correct.
    ///
    /// <b>The section's own Visibility rather than IsVisible on the list inside it</b>, and that is
    /// a fact about the harness rather than about the panel: a window built for a test is never put
    /// on a screen, so IsVisible answers false for everything in it - a guard reading that would
    /// pass by agreeing with nothing. Found by writing the guard and watching it fail on the half
    /// that was supposed to be true.
    /// </summary>
    internal bool ProblemsShown => ProblemSection.Visibility == Visibility.Visible;

    /// <summary>Whether the way back is on the screen at all. Never before a run.</summary>
    internal bool WayBackShown => WayBackSection.Visibility == Visibility.Visible;

    /// <summary>
    /// The button that changes a machine, so a test can ask whether it is live.
    ///
    /// <b>The control rather than the model's answer, and the difference is the whole point.</b>
    /// Planned.CanCarryOut can be perfectly right while nothing binds to it, and a button that stays
    /// live through a run is a second ask one click away - which is exactly the kind of fault a
    /// binding that resolves to nothing produces in silence.
    /// </summary>
    internal Button CarryOut => CarryOutButton;

    /// <summary>The way to stop a run, which only exists while there is one.</summary>
    internal Button Interrupt => InterruptButton;
}
