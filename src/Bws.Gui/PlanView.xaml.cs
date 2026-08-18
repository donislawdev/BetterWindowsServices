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
/// <b>It has no way to carry a plan out and that is the state of the packet rather than an oversight.</b>
/// The menu items that open it are worded as questions for the same reason. When the runner arrives,
/// the button it needs belongs here and the wording changes with it.
/// </summary>
public partial class PlanView : UserControl
{
    public PlanView() => InitializeComponent();

    private void CloseRequested(object sender, RoutedEventArgs e) => Dismiss();

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
}
