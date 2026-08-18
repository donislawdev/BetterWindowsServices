using System.Windows;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

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
    /// A run that half worked says which half, with the manager's own words.
    ///
    /// <b>Rule 8 of the project's untouchable rules at the moment it matters most.</b> A partial
    /// answer reported as "done" is the silent failure that rule exists against, and here the person
    /// is holding a machine other people depend on.
    /// </summary>
    [Fact]
    public async Task What_did_not_work_reaches_the_screen_with_the_reason()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        // Nothing has run, so there is nothing to admit to and no heading claiming otherwise.
        Assert.Empty(WpfHost.On(() => window.PlanPanel.FailureLines));

        WpfHost.On(() => panel.Finished(Ran(panel, refusing: "Spooler")));
        WpfHost.Settled();

        var failures = WpfHost.On(() => window.PlanPanel.FailureLines);

        Assert.Contains(failures, line => line.Contains("Spooler", StringComparison.Ordinal));
        Assert.Contains(failures, line => line.Contains("Access is denied", StringComparison.Ordinal));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The way back reaches the screen as commands somebody can type. `ADR-11`'s reversible promise,
    /// in the cheapest honest form it has, arriving in a window for the first time.
    /// </summary>
    [Fact]
    public async Task The_way_back_reaches_the_screen_as_commands()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Empty(WpfHost.On(() => window.PlanPanel.WayBackLines));

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        var back = WpfHost.On(() => window.PlanPanel.WayBackLines);

        Assert.Contains("bws start Spooler", back);
        Assert.Contains("bws start W32Time", back);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A NEW PLAN DROPS THE REPORT OF THE LAST RUN, and this is the worst bug this panel could have
    /// had: what happened to five services, sitting under the steps of a plan for five different
    /// ones, with nothing on screen to say the two do not belong together.
    /// </summary>
    [Fact]
    public async Task Asking_about_something_else_drops_the_report_of_the_last_run()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        WpfHost.On(() => panel.Finished(Ran(panel, refusing: "Spooler")));
        WpfHost.Settled();

        Assert.NotEmpty(WpfHost.On(() => window.PlanPanel.FailureLines));

        Assert.True(WpfHost.On(() => window.Preview(ActionKind.Start)));
        WpfHost.Settled();

        Assert.Empty(WpfHost.On(() => window.PlanPanel.FailureLines));
        Assert.Empty(WpfHost.On(() => window.PlanPanel.WayBackLines));

        // And it can be carried out again, because it is a different ask rather than the same one.
        Assert.True(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));

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

    // -- fixtures --------------------------------------------------------------------------

    /// <summary>
    /// A run of the plan that is actually on screen, made by hand.
    ///
    /// <b>Built FROM the panel's own plan rather than from a plan of its own</b>, so what the tests
    /// read back is a report about the steps beside it - the pairing `ADR-11` exists for. A run made
    /// up separately would assert that words reach a screen while saying nothing about whether they
    /// are words about the right plan.
    /// </summary>
    private static BulkRun Ran(Planned panel, string? refusing = null)
    {
        var plan = panel.Plan!;

        return new BulkRun
        {
            Plan = plan,
            Runs =
            [
                .. plan.Plans.Select(one => new PlanRun
                {
                    Plan = one,
                    Results = [.. one.Steps.Select(step => Result(step, refused: step.ServiceName == refusing))],
                    Cancelled = false,
                    Ceiling = TimeSpan.FromMinutes(1)
                })
            ]
        };
    }

    private static StepResult Result(PlanStep step, bool refused) => new()
    {
        Step = step,
        Outcome = refused ? StepOutcome.Failed : StepOutcome.Succeeded,
        SkippedBecause = null,
        Status = refused ? EntryStatus.Running : EntryStatus.Stopped,
        ErrorCode = refused ? 5 : 0,
        Error = refused ? "Access is denied." : null,
        Milliseconds = 10
    };

    /// <summary>
    /// A window looking at a small machine, with two entries picked. The same fixture
    /// <see cref="PlanViewGuards"/> uses, and for the reason written there: the reading happens
    /// before the model reaches the window, so nothing is read while bindings are live.
    /// </summary>
    private static async Task<MainWindow> Ready(bool elevated = true)
    {
        var machine = new LiveMachine(
            Rows.Entry("Spooler", "Print Spooler"),
            Rows.Entry("W32Time", "Windows Time"),
            Rows.Entry("Dnscache", "DNS Client"));

        // ELEVATION IS HANDED OVER RATHER THAN INHERITED FROM WHOEVER RAN THE SUITE, and that is
        // the difference between a test and a coincidence: on an elevated session the default
        // would be true and every assertion below would pass without the code doing anything.
        var model = new MainViewModel(machine, new SteppedClock())
        {
            Planned = new Planned { Elevated = elevated }
        };

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        WpfHost.On(() =>
        {
            window.Entries.ItemsSource = model.Rows;
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Spooler");
            window.Entries.SelectedItems.Add(model.Rows.First(row => row.ServiceName == "W32Time"));
        });

        WpfHost.Settled();

        return window;
    }
}
