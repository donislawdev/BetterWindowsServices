using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// "Pause..." and "Resume..." on a window - the buttons, the plan they open, its sentences and the one after
/// the run (backlog 555 part b, 2026-11-25). Nothing touches a machine: the plan is built against a machine
/// made here, and the run is a record made by hand from the plan on screen (<see cref="PlanFixture"/>).
/// </summary>
public sealed class PauseWindowGuards
{
    [Fact]
    public async Task Pause_over_two_entries_that_take_it_opens_their_plan_and_the_run_says_they_are_paused()
    {
        var window = await Picked(Taking("Spooler", EntryStatus.Running), Taking("W32Time", EntryStatus.Running));
        var panel = Press(window, window.Actions.Pause);

        var steps = WpfHost.On(() => panel.Plan!.Plans.SelectMany(plan => plan.Steps).ToList());

        Assert.All(steps, step => Assert.Equal(StepOperation.Pause, step.Operation));
        Assert.Equal(2, steps.Count);
        Assert.Equal(Texts.Of("gui.plan.carryOut.pause.many", 2), WpfHost.On(() => panel.CarryOutLabel));
        Assert.Contains(Texts.Of("gui.plan.doing.pause"), WpfHost.Drawn(window.PlanPanel.Heading), StringComparison.Ordinal);

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        Assert.Equal(Texts.Of("gui.plan.notice.done.paused.many", 2), WpfHost.On(() => window.PlanPanel.Notice.Text));

        WpfHost.On(window.Close);
    }

    [Fact]
    public async Task Resume_over_two_paused_entries_opens_their_plan_and_the_run_says_they_are_running()
    {
        var window = await Picked(Taking("Spooler", EntryStatus.Paused), Taking("W32Time", EntryStatus.Paused));
        var panel = Press(window, window.Actions.Resume);

        Assert.All(
            WpfHost.On(() => panel.Plan!.Plans.SelectMany(plan => plan.Steps).ToList()),
            step => Assert.Equal(StepOperation.Resume, step.Operation));
        Assert.Equal(Texts.Of("gui.plan.carryOut.resume.many", 2), WpfHost.On(() => panel.CarryOutLabel));

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        Assert.Equal(Texts.Of("gui.plan.notice.done.running.many", 2), WpfHost.On(() => window.PlanPanel.Notice.Text));

        WpfHost.On(window.Close);
    }

    [Fact]
    public async Task Entries_that_take_no_pause_are_refused_in_one_sentence_naming_each_and_nothing_can_be_carried_out()
    {
        // Most services take no pause, so pressing Pause over a selection is mostly this - one sentence, not one each.
        var window = await Picked(Rows.Entry("Spooler", "Print Spooler"), Rows.Entry("W32Time", "Windows Time"));
        var panel = Press(window, window.Actions.Pause);

        var refused = Assert.Single(WpfHost.On(() => panel.Problems.Select(problem => problem.Text).ToList()));

        Assert.Equal(Texts.Of("gui.plan.problem.pauseNotAccepted.many", 2, "Spooler, W32Time"), refused);
        Assert.False(WpfHost.On(() => panel.CanCarryOut));

        WpfHost.On(window.Close);
    }

    [Fact]
    public void A_refusal_of_one_entry_is_said_in_the_singular_with_its_own_sentence()
    {
        var problem = new PlanProblem(PlanProblemKind.StoppedNotPaused, "Spooler", []);

        Assert.Equal(PlanWords.Describe(problem), Assert.Single(PlanWords.Describe([problem])));
        Assert.Equal(Texts.Of("gui.plan.problem.stoppedNotPaused.one", "Spooler"), PlanWords.Describe(problem));
    }

    [Fact]
    public void What_keeps_running_and_the_other_two_refusals_have_sentences_of_their_own()
    {
        Assert.Equal(
            [
                Texts.Of("gui.plan.warning.dependentsKeepRunning.one", "LanmanWorkstation", "SessionEnv"),
                Texts.Of("gui.plan.warning.dependentsKeepRunning.many", "LanmanWorkstation", "SessionEnv, Netlogon"),
                Texts.Of("gui.plan.warning.dependentsUnreadable", "LanmanWorkstation")
            ],
            PlanWords.Describe(
            [
                new PlanWarning(PlanWarningKind.DependentsKeepRunning, "LanmanWorkstation", ["SessionEnv"]),
                new PlanWarning(PlanWarningKind.DependentsKeepRunning, "LanmanWorkstation", ["SessionEnv", "Netlogon"]),
                new PlanWarning(PlanWarningKind.DependentsUnreadable, "LanmanWorkstation", [])
            ]));

        Assert.Equal(
            Texts.Of("gui.plan.problem.notRunning.one", "Spooler"),
            PlanWords.Describe(new PlanProblem(PlanProblemKind.NotRunning, "Spooler", [])));
        Assert.Equal(
            Texts.Of("gui.plan.problem.pauseNotAccepted.one", "Spooler"),
            PlanWords.Describe(new PlanProblem(PlanProblemKind.PauseNotAccepted, "Spooler", [])));
    }

    private static ScmEntry Taking(string name, EntryStatus status) =>
        Rows.Entry(name, name) with { Status = status, AcceptsPause = Reading<bool>.Present(true) };

    /// <summary>
    /// A window looking at these entries with the first two picked - <see cref="PlanFixture.Ready"/> on a
    /// machine of the test's own, because whether an entry takes a pause is the whole subject here.
    /// </summary>
    private static async Task<MainWindow> Picked(params ScmEntry[] entries)
    {
        var model = new MainViewModel(new LiveMachine(entries), new SteppedClock()) { Planned = new Planned { Elevated = true } };

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        WpfHost.On(() =>
        {
            window.Entries.ItemsSource = model.Rows;
            window.Entries.SelectedItem = model.Rows[0];
            window.Entries.SelectedItems.Add(model.Rows[1]);
        });

        WpfHost.Settled();

        return window;
    }

    private static Planned Press(MainWindow window, Button button)
    {
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        WpfHost.On(() => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        WpfHost.Until(() => panel.Plan is not null, "the plan arrived");

        return panel;
    }
}
