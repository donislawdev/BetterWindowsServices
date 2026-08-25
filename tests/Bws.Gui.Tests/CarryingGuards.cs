using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// The window carrying a plan out, checked everywhere except where it would change this machine.
///
/// <b>WHAT IS NOT HERE, SAID FIRST BECAUSE IT IS THE HONEST HALF: nothing below presses the button
/// for real.</b> A test that did would stop services on whatever machine ran the suite, which is the
/// project's hard rule about writes rather than an oversight - writes go to a machine somebody can
/// throw away. So the boundary is drawn exactly where the machine begins: the guard clause that
/// keeps a press from reaching the manager at all IS tested, and every state a run puts this panel
/// into is driven by hand and read back off the window.
///
/// <b>Read off the window rather than off the model, which is why this file exists at all.</b>
/// <see cref="Planned"/>'s own tests can be perfectly right while nothing binds to them - and this
/// project has been caught four times by markup that binds correctly and paints something else. A
/// button left live through a run is a second ask one click away, and it would look fine.
/// </summary>
public sealed class CarryingGuards
{
    /// <summary>
    /// <b>THE ONE THAT MATTERS MOST, and the only one that goes near the road to a manager.</b>
    /// A press with nothing to carry out has to turn back before it builds anything - so the answer
    /// is handed over rather than assumed from a disabled button, which is a statement about pixels.
    /// </summary>
    [Fact]
    public async Task A_press_with_nothing_to_carry_out_turns_back_before_it_reaches_a_manager()
    {
        var window = await Ready();

        Assert.False(await WpfHost.On(() => window.CarryOut()));

        WpfHost.On(window.Close);
    }

    [Fact]
    public async Task The_button_is_live_only_while_there_is_a_plan_nobody_has_carried_out()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        // Nothing on screen, nothing to press.
        Assert.False(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));

        // While it is happening. A live button here is a second ask one click away, arriving before
        // the first has reached the screen.
        WpfHost.On(panel.Starting);
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));

        // And after. The steps stay on screen so somebody can read what happened against what was
        // going to - which is exactly why the button must not still offer to do it again.
        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The way to stop a run exists while a run does, and not before it.
    ///
    /// <b>A separate button rather than the first one changing its word</b>, which is a safety
    /// decision: a verb that changes under a finger turns a double click into an instruction nobody
    /// gave.
    /// </summary>
    [Fact]
    public async Task The_way_to_stop_a_run_is_on_screen_only_while_one_is_happening()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Interrupt.Visibility));

        WpfHost.On(panel.Starting);
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.PlanPanel.Interrupt.Visibility));

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Interrupt.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Nothing in the way means no line reserved for saying so.
    ///
    /// <b>The block backlog 203 missed, and it was the only one it missed.</b> Every other
    /// conditional part of this panel took its heading off the screen with it on 2026-08-19 - this
    /// one had no visibility of its own, so an empty sentence stood in the tree always, carrying
    /// its margin. That is the gap between the state sentence and the first section in the owner's
    /// first screenshot.
    /// </summary>
    [Fact]
    public async Task Nothing_in_the_way_means_no_line_reserved_for_saying_so()
    {
        var window = await Ready();

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Blocked.Visibility));

        WpfHost.On(window.Close);

        // ELEVATION HANDED OVER RATHER THAN INHERITED, for the reason the guard below gives: on an
        // elevated session the default would hide this half and the assertion would pass empty.
        var refused = await Ready(elevated: false);

        Assert.True(WpfHost.On(() => refused.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => refused.PlanPanel.Blocked.Visibility));

        WpfHost.On(refused.Close);
    }

    /// <summary>
    /// The button says WHY it is grey, and it says the first thing standing in the way.
    ///
    /// <b>Four things switch that button off and the panel named one of them.</b> No elevation, a
    /// run under way, a run that has finished, and a plan with no step that could be taken - and the
    /// line in the panel speaks about elevation alone, so three of the four ways this control
    /// refuses said nothing at all.
    ///
    /// <b>ShowOnDisabled is asserted rather than assumed, and without it none of this is readable.</b>
    /// WPF stops serving tooltips for a disabled control, so a reason written into a binding and left
    /// at that is a sentence that exists everywhere except on the screen.
    /// </summary>
    [Fact]
    public async Task The_button_says_why_it_is_grey_rather_than_only_refusing()
    {
        var refused = await Ready(elevated: false);

        Assert.True(WpfHost.On(() => refused.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => refused.PlanPanel.CarryOut.IsEnabled));

        Assert.True(
            WpfHost.On(() => ToolTipService.GetShowOnDisabled(refused.PlanPanel.CarryOut)),
            "The reason is bound to a control that will not show a tooltip while it is disabled.");

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.blocked.notElevated"),
            WpfHost.On(() => refused.PlanPanel.CarryOut.ToolTip as string));

        WpfHost.On(refused.Close);

        // AND ON A SESSION THAT COULD CARRY IT OUT, the button says what it would do - the sentence
        // it carried before any of this, which is the branch a chain of reasons is easiest to lose.
        var window = await Ready();

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.carryOut.hint"),
            WpfHost.On(() => window.PlanPanel.CarryOut.ToolTip as string));

        // ONCE IT HAS HAPPENED THE ANSWER CHANGES, because the button is grey for a new reason and
        // the old sentence would send somebody looking for administrator rights they already have.
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.On(() => model.Planned.Finished(Ran(model.Planned)));
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.blocked.alreadyDone"),
            WpfHost.On(() => window.PlanPanel.CarryOut.ToolTip as string));

        WpfHost.On(window.Close);
    }


    /// <summary>
    /// The other two reasons the button goes quiet, which were arms of a chain and nothing else.
    ///
    /// <b>Four reasons were written and two were tested</b>, which is the shape this project calls
    /// prose with nothing watching it. A plan with no step that could be taken and a run already
    /// under way are both ordinary - the first arrives whenever somebody picks an entry the plan
    /// refuses, and the second is every second of every run.
    /// </summary>
    [Fact]
    public async Task The_button_names_the_other_two_things_that_keep_it_quiet()
    {
        var window = await Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        // A run under way: the button is quiet and says which one, rather than what it would do.
        WpfHost.On(model.Planned.Starting);
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.blocked.running"),
            WpfHost.On(() => window.PlanPanel.CarryOut.ToolTip as string));

        WpfHost.On(window.Close);

        // A plan with nothing in it that could be done. An entry that is not in the catalogue any
        // more is the ordinary road to this: a service can go away between being listed and being
        // asked about, which is why the plan has a problem for it at all.
        var refusing = await Ready();
        var over = WpfHost.On(() => (MainViewModel)refusing.DataContext);

        Assert.True(WpfHost.On(() => over.Planned.Show(
            over.Plan(new BulkAction(ActionKind.Stop, ["nothing-is-called-this"])))));

        WpfHost.Settled();

        Assert.False(WpfHost.On(() => refusing.PlanPanel.CarryOut.IsEnabled));

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.blocked.nothingToRun"),
            WpfHost.On(() => refusing.PlanPanel.CarryOut.ToolTip as string));

        WpfHost.On(refusing.Close);
    }

    /// <summary>
    /// The reach for the corner of the window, half way through changing a machine.
    ///
    /// <b>THE STATE (T) OF RULE 10, AND UNTIL 2026-08-19 NOTHING EXECUTED IT AT ALL.</b> Searching
    /// the whole test tree for <c>OnClosing</c> found exactly one hit and it was
    /// <c>BackgroundWorkGuards</c>, which knows the method by name and guards its SHAPE - that a
    /// file holding an <c>async void</c> is on a list with a written reason. A green guard beside a
    /// method nobody had ever run.
    ///
    /// <b>Three assertions rather than one, because the failure this guards has three ways to
    /// arrive.</b> Closing anyway ends the process and leaves a cascade switched off in silence -
    /// which is exactly what two presses of Ctrl+C used to do in the command line. Refusing and NOT
    /// asking the run to stop hangs the window on a corner nobody can use. Refusing forever leaves
    /// somebody with a window that will not go away.
    /// </summary>
    [Fact]
    public async Task A_close_during_a_run_is_refused_once_and_taken_again_when_the_run_ends()
    {
        var window = await Ready();
        var gate = new TaskCompletionSource();
        using var stopping = new CancellationTokenSource();
        var closed = false;

        // HANDED A RUN RATHER THAN STARTING ONE, which is the seam MainWindow.TakeThisAsARun exists
        // for and the reason it takes a task rather than a plan. A real run here would stop services
        // on whatever machine runs this suite.
        WpfHost.On(() =>
        {
            window.Closed += (_, _) => closed = true;
            window.TakeThisAsARun(gate.Task, stopping);
        });

        WpfHost.On(window.Close);
        WpfHost.Settled();

        Assert.False(closed);
        Assert.True(stopping.IsCancellationRequested);

        // And the run ends. The steps that give back what earlier ones took have run by now, which
        // is what asking a run to stop means here rather than abandoning it.
        gate.SetResult();
        WpfHost.Settled();

        Assert.True(closed);
    }

    /// <summary>
    /// Pressing the interrupt reaches the run, rather than only appearing on a screen.
    ///
    /// <b>The other half of a guard that already existed, and the gap is worth naming.</b>
    /// <see cref="The_way_to_stop_a_run_is_on_screen_only_while_one_is_happening"/> asserts that the
    /// button is visible exactly while a run is - which is a statement about VISIBILITY. Whether
    /// pressing it does anything was guarded by nothing until 2026-08-19.
    ///
    /// <b>Driven through the button's own click event rather than by calling the handler</b>, so the
    /// whole road is exercised: the routed event, the panel raising <c>InterruptRequest</c>, and the
    /// window turning that into a cancellation. Calling the handler would assert that a method this
    /// test found does what this test expects.
    /// </summary>
    [Fact]
    public async Task Pressing_the_interrupt_reaches_the_run_rather_than_only_the_screen()
    {
        var window = await Ready();
        var gate = new TaskCompletionSource();
        using var stopping = new CancellationTokenSource();

        WpfHost.On(() => window.TakeThisAsARun(gate.Task, stopping));

        Assert.False(stopping.IsCancellationRequested);

        WpfHost.On(() => window.PlanPanel.Interrupt.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        WpfHost.Settled();

        Assert.True(stopping.IsCancellationRequested);

        gate.SetResult();
        WpfHost.On(window.Close);
    }

    /// <summary>
    /// THE LINE THIS PANEL MUST NEVER LOSE, now that it has three things to say instead of one.
    ///
    /// A list of steps reads as a report of something done in every one of the three states, so the
    /// sentence above it is what tells somebody which one they are looking at. All three are asserted
    /// as DIFFERENT rather than as particular words - the words are in a language file and belong
    /// there, and a test naming them would break every time somebody reworded one.
    /// </summary>
    [Fact]
    public async Task The_sentence_says_which_of_the_three_states_this_is()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var before = WpfHost.On(() => window.PlanPanel.Notice.Text);

        WpfHost.On(panel.Starting);
        WpfHost.Settled();

        var during = WpfHost.On(() => window.PlanPanel.Notice.Text);

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        var after = WpfHost.On(() => window.PlanPanel.Notice.Text);

        Assert.NotEqual(string.Empty, before);
        Assert.NotEqual(before, during);
        Assert.NotEqual(during, after);
        Assert.NotEqual(before, after);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A SESSION THAT CANNOT CHANGE ANYTHING SAYS SO, AND THE BUTTON IS DEAD BEFORE ANYBODY REACHES
    /// FOR IT.
    ///
    /// <b>The owner's decision of 2026-08-18, which the first version of this panel did not keep -
    /// and a screenshot is what found it.</b> The first look at this panel was on a session without
    /// administrator rights, with the window already saying so at the bottom, and the button was
    /// live with nothing beside it to explain. That is the decision broken in the direction that
    /// costs the most: a press, a column of refusals from the manager, and somebody working out why.
    ///
    /// Both halves are asserted, because either alone is a different fault. A dead button with no
    /// sentence is a feature that looks broken, and a sentence with a live button is a warning
    /// nobody has to believe.
    /// </summary>
    [Fact]
    public async Task A_session_that_cannot_change_anything_says_so_before_anybody_presses()
    {
        var window = await Ready(elevated: false);

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));
        Assert.NotEqual(string.Empty, WpfHost.On(() => window.PlanPanel.Blocked.Text));

        // And it is not there when there is nothing in the way, so the line never becomes furniture.
        var elevated = await Ready();

        Assert.True(WpfHost.On(() => elevated.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Equal(string.Empty, WpfHost.On(() => elevated.PlanPanel.Blocked.Text));

        WpfHost.On(window.Close);
        WpfHost.On(elevated.Close);
    }

    /// <summary>
    /// A SECTION WITH NOTHING IN IT TAKES ITS HEADING OFF THE SCREEN WITH IT. Backlog 203, and it
    /// was the second thing the owner said about the first screenshot of this panel.
    ///
    /// <b>Asserted on the section being on the screen rather than on the list being empty</b>, and
    /// the difference is the whole fault: an empty list under a heading reading "Not included, and
    /// why" states something false, and a test reading only the lines would call it correct.
    /// </summary>
    [Fact]
    public async Task A_section_with_nothing_in_it_takes_its_heading_off_the_screen()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        // Nothing was refused and nothing has run, so neither section belongs on the screen.
        Assert.False(WpfHost.On(() => window.PlanPanel.ProblemsShown));
        Assert.False(WpfHost.On(() => window.PlanPanel.WayBackShown));

        // And the way back arrives with its heading the moment there is one.
        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => window.PlanPanel.WayBackShown));

        WpfHost.On(window.Close);
    }

}
