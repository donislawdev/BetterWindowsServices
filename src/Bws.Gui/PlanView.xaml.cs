using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    /// <summary>
    /// Somebody pressed the way out under a failure, and it carries which failure.
    ///
    /// <b>An event for the same reason as the others, and here it is the strongest of the three.</b>
    /// Pressing this closes this sheet and opens another with a plan worked out against the machine
    /// as it is at that moment - which means asking the manager, off this thread, and deciding what
    /// happens if a second ask arrives while the first is still being worked out. All of that lives
    /// where a preview is already opened from a menu, and none of it belongs to a panel.
    /// </summary>
    internal event EventHandler<ForceAsked>? ForceRequest;

    /// <summary>
    /// Somebody asked for one of the terminal commands, and it carries which one.
    ///
    /// <b>An event for the same reason as the two above, and here the reason is sharper.</b> The
    /// clipboard belongs to whatever process grabbed it last, so a copy genuinely fails on a
    /// working machine - and the window is the only thing that can SAY so, because it owns the
    /// status line. A panel writing to the clipboard itself would be a second place that has to
    /// remember rule 8, and the first one already does it correctly.
    /// </summary>
    internal event EventHandler<CommandAsked>? CopyRequest;

    public PlanView()
    {
        InitializeComponent();

        // THE FOOTER'S TWO PRESSES ARE PASSED ON UNCHANGED, and that is all this sheet does with
        // them. The foot of the sheet moved to a file of its own on 2026-09-07 - PlanFooter.xaml
        // says why - and the window still hears exactly the two events it always heard, so nothing
        // outside these two lines had to learn that the ask lives somewhere else now.
        Footer.CarryOutRequest += (_, _) => CarryOutRequest?.Invoke(this, EventArgs.Empty);
        Footer.InterruptRequest += (_, _) => InterruptRequest?.Invoke(this, EventArgs.Empty);
    }

    private void CloseRequested(object sender, RoutedEventArgs e) => Dismiss();

    /// <summary>
    /// Somebody pressed the way out under one failure.
    ///
    /// <b>Caught on the ItemsControl rather than on the button, which is the same shape the copy
    /// buttons use and for the same reason</b> - the template lives in Themes/Plan.xaml, a plain
    /// dictionary with no class behind it, so a Click named there would resolve against nothing.
    ///
    /// <b>The failure is read off the button's Tag rather than off anything else.</b> A plan over
    /// several picked rows can fail on any of them, so the selection, the focused item and this
    /// panel's own DataContext are all ways of escalating something that is not the thing under
    /// the finger - and this is the one press in this window where that would end the wrong
    /// process.
    /// </summary>
    private void ForceRequested(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button pressed || pressed.Tag is not PlanFailure failure)
        {
            return;
        }

        ForceRequest?.Invoke(this, new ForceAsked(failure));
    }

    /// <summary>
    /// Which failure the way out was pressed under.
    ///
    /// <b>A type for one value, because the analyser asks for one</b> - MA0046 wants the second
    /// parameter of an event handler to be an EventArgs. The same trade <see cref="CommandAsked"/>
    /// takes, one event along.
    /// </summary>
    internal sealed class ForceAsked(PlanFailure failure) : EventArgs
    {
        internal PlanFailure Failure { get; } = failure;
    }

    /// <summary>
    /// One copy button under one command.
    ///
    /// <b>Caught on the ItemsControl rather than on the button, and that is pulapka 10 in a new
    /// shape.</b> The template lives in Themes/Plan.xaml, which is a plain dictionary with no class
    /// behind it, so a Click named there would resolve against nothing. Click is a routed event, so
    /// the compiled markup one level up can hold the handler and let it bubble.
    ///
    /// <b>The command is read off the button's Tag rather than off the selection.</b> A plan over
    /// several picked rows prints one line each, and anything else - the focused item, the panel's
    /// DataContext - is a way of copying a command that is not the one under the finger.
    /// </summary>
    /// <summary>
    /// The wheel over anything in the body scrolls the BODY. The owner's remark of 2026-09-16 over
    /// a sheet of twenty commands: "the scroll stops working" - each command sits in a viewer of
    /// its own that scrolls sideways, and a ScrollViewer marks every wheel event handled whether
    /// or not it has anywhere to go, so with boxes under the pointer the body never heard it.
    ///
    /// <b>Taken in the tunnel and raised again on the body</b>, because nothing in the theme file
    /// could say otherwise: the framework's switch for it is not settable from markup, and a quiet
    /// viewer of our own cannot be named from a theme (`docs/10` trap 10). The preview tunnels
    /// from the window down and reaches the body before the command's viewer, and a wheel raised
    /// on the body goes to the body's own scrolling first. A raised event is the bubbling one
    /// alone - the input system pairs preview with bubble, RaiseEvent does not - so it cannot
    /// come back through here.
    /// </summary>
    private void WheelOverTheBody(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        e.Handled = true;
        Body.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = Body
        });
    }

    private void CopyCommandRequested(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Button pressed || pressed.Tag is not string command)
        {
            return;
        }

        CopyRequest?.Invoke(this, new CommandAsked(command));
        SaySoFor(pressed);
    }

    /// <summary>
    /// Which command was asked for.
    ///
    /// <b>A type for one string, because the analyser asks for one</b> - MA0046 wants the second
    /// parameter of an event handler to be an EventArgs, and the alternative was an event that says
    /// somebody pressed something and leaves the caller to work out which line it was.
    /// </summary>
    internal sealed class CommandAsked(string command) : EventArgs
    {
        internal string Command { get; } = command;
    }

    /// <summary>
    /// Turns one copy button into "Copied" and back again.
    ///
    /// <b>The clipboard says nothing when it works, so the button has to.</b> Without a word,
    /// somebody who is not sure whether the press landed presses it again - which is harmless here
    /// and is exactly the doubt this removes.
    ///
    /// <b>It is said even when the copy failed, and that is deliberate rather than sloppy.</b> A
    /// refusal puts a sentence in the status line, which is louder than this and says more - two
    /// reports of one failure in two places would read as two failures.
    /// </summary>
    private void SaySoFor(Button pressed)
    {
        // THE WORD IT HAD, NOT A FIXED KEY, since 2026-09-16: the button over a whole section says
        // "Copy all" and the one beside a line says "Copy", and both come here.
        var settled = pressed.Content;

        // AND THE WIDTH IT HAD. The style floors the width at the wider of the two short words, and
        // "Copy all" is wider than "Copied" - so without this the button would shrink under the
        // pointer when its word changed, which is GUI rule 3 broken by a press. Frozen at what it
        // measured rather than at a token per button, because the next copy button would need a
        // token of its own and the rule is one for all of them.
        pressed.MinWidth = Math.Max(pressed.MinWidth, pressed.ActualWidth);
        pressed.Content = TryFindResource("gui.plan.copied");

        var clock = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        clock.Tick += (_, _) =>
        {
            clock.Stop();

            // ONLY IF NOTHING ELSE HAS TOUCHED IT. An ItemsControl recycles nothing here, but the
            // panel can close and reopen inside two seconds, and putting a word back onto a button
            // that now belongs to a different command is the kind of fault nobody would look for.
            if (ReferenceEquals(pressed.Content, TryFindResource("gui.plan.copied")))
            {
                pressed.Content = settled;
            }
        };

        clock.Start();
    }

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
    /// The manager's own name for the entry, under a title carrying the one a person recognises.
    ///
    /// Exposed for the same reason as the heading above it, and with one thing more to check: this
    /// line has to GO when there is nothing to say, or a heading is left with a reserved gap under
    /// it - backlog 203, the fault this panel has already had once.
    /// </summary>
    internal TextBlock Subtitle => PlanSubtitle;

    /// <summary>
    /// The line saying why this cannot be carried out here.
    ///
    /// <b>Read off the control rather than off the model</b>, because the fault it exists against is
    /// exactly the one a dead binding produces: a session that cannot change anything, a live
    /// button, and nothing on screen to say so.
    /// </summary>
    internal TextBlock Blocked => Footer.Blocked;

    /// <summary>
    /// The box the entry's name has to be typed into before a process may be ended, and whether it
    /// is on the screen at all.
    ///
    /// <b>Handed through from the footer rather than reached for, so nothing outside this sheet has
    /// to know the ask lives in a file of its own now.</b> Six guards read this panel by name, and
    /// a seam that made every one of them say Footer.Confirm would be a seam charging for itself
    /// in every test that goes near it.
    /// </summary>
    internal System.Windows.Controls.TextBox Confirm => Footer.Confirm;

    /// <summary>Whether the confirmation box and its label are on the screen at all.</summary>
    internal bool ConfirmShown => Footer.ConfirmShown;

    /// <summary>
    /// The sheet itself, so its height can be measured against the window it has to fit inside.
    ///
    /// <b>Backlog 278 is a question about this rectangle</b> - at 1000 by 800 the sheet once ran
    /// past the bottom of the window and took half of the main button with it. Adding anything to
    /// the footer reopens that question, and the design that asked for a confirmation box put its
    /// cost at "about seventy units" and said in as many words that the figure was an estimate.
    /// </summary>
    internal System.Windows.Controls.Border TheSheet => Sheet;

    /// <summary>
    /// Where the keyboard goes when this sheet opens.
    ///
    /// <b>THE ONE THING KEEPING ENTER FROM ENDING A PROCESS, and it is a property rather than a
    /// call so that a test can ask what it chose.</b> A window built for a test is never put on a
    /// screen, so nothing in this assembly can assert that a control really took focus - Focus()
    /// answers false for everything in an unshown window. What CAN be checked is the decision, and
    /// the decision is the half that would be wrong.
    ///
    /// <b>The box when it is there, and the way out when it is not</b> - the design's fifth
    /// collision, taken as recommended. Neither is the button that ends anything, which is the
    /// whole of what this has to guarantee.
    ///
    /// <b>The close mark rather than a Cancel button, and that is a departure from the drawing
    /// worth naming.</b> The design's panels put a Cancel in the footer - this sheet has never had
    /// one - it has the mark in the corner and Escape, which do exactly what a Cancel would. Adding
    /// a third button to say a third time what two things already say would have been a new control
    /// in the one row the design was trying to keep from growing.
    /// </summary>
    internal System.Windows.Controls.Control WayIn => ConfirmShown ? Footer.Confirm : PlanCloseButton;

    /// <summary>
    /// Sends the keyboard to <see cref="WayIn"/>, and says whether anything took it.
    ///
    /// <b>At Input priority rather than now, because "shown" and "arranged" are two moments.</b> A
    /// control that has just been given a Visibility has not been through layout yet, and focus
    /// offered to something with no size is focus that goes nowhere quietly.
    /// </summary>
    /// <remarks>
    /// The operation is discarded on purpose and the analyser asks for it to be said - MA0134.
    /// Nothing waits for a focus to land: whoever opened the sheet has already answered whether it
    /// opened, and the keyboard arriving one layout pass later is the whole point of the priority.
    /// </remarks>
    internal void TakeTheKeyboard() =>
        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            () => WayIn.Focus());

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
    internal IReadOnlyList<string> FailureLines =>
        [.. FailureList.Items.OfType<PlanFailure>().Select(failure => failure.Text)];

    /// <summary>
    /// The ways out offered under those failures, as they reach the screen.
    ///
    /// <b>Read off the items rather than off the model, for the reason every other line here is.</b>
    /// Planned can be perfectly right about which failure may be escalated while the button binds
    /// to nothing - and a dead binding here paints a section that looks like a dead end, which is
    /// indistinguishable from the state this whole slice exists to remove.
    /// </summary>
    internal IReadOnlyList<string> OfferLines =>
        [.. FailureList.Items.OfType<PlanFailure>().Where(failure => failure.HasOffer)
            .Select(failure => failure.Label)];

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
    /// Whether the command that would ask for the same thing is on the screen. Never after a run.
    ///
    /// The mirror of the line above, and the pair is the point: before a run the panel offers the
    /// command that would do this, after one it offers the command that would undo it. Both at once
    /// puts a stale instruction in the most prominent place on the panel.
    /// </summary>
    internal bool CommandsShown => CommandSection.Visibility == Visibility.Visible;

    /// <summary>
    /// The button that changes a machine, so a test can ask whether it is live.
    ///
    /// <b>The control rather than the model's answer, and the difference is the whole point.</b>
    /// Planned.CanCarryOut can be perfectly right while nothing binds to it, and a button that stays
    /// live through a run is a second ask one click away - which is exactly the kind of fault a
    /// binding that resolves to nothing produces in silence.
    /// </summary>
    internal Button CarryOut => Footer.CarryOut;

    /// <summary>The way to stop a run, which only exists while there is one.</summary>
    internal Button Interrupt => Footer.Interrupt;
}
