using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// A run that is going keeps its sheet, its report and the window's hold on it - G-1 and G-2 of the
/// external stability report, 2026-09-29.
///
/// <b>Why these are their own file.</b> Until that day Escape, the close mark, a new preview and the
/// details panel could each put the sheet away in the middle of a run. That dropped the plan and
/// lowered the busy flag while the run went on, so the button to carry out came back live, a second
/// run could start beside the first, and the window's close guard and Interrupt then followed
/// whichever run had started last. CarryingGuards drives the states of ONE run - this file asks what
/// every other door of the window does while that run is going.
///
/// <b>The run is a gate the test holds</b>, through the same seam CarryingGuards uses, so nothing
/// here reaches a service manager.
/// </summary>
public sealed class RunHoldsTheSheetGuards
{
    [Fact]
    public async Task Escape_and_the_close_mark_leave_a_running_sheet_where_it_is()
    {
        var gate = new TaskCompletionSource<BulkRun>();
        var window = await Ready(carriedOutBy: (_, _, _, _, _) => gate.Task);
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        var panel = model.Planned;

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        var plan = panel.Plan;

        var carrying = WpfHost.On(() => window.CarryOut());
        WpfHost.Settled();

        Assert.True(panel.Busy);

        // A query typed while the run goes on, which Escape used to reach by falling through the
        // sheet - and empty under a plan that was still changing the machine.
        WpfHost.On(() => model.QueryText = "Spool");

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        WpfHost.Settled();

        Assert.True(panel.Showing);
        Assert.Same(plan, panel.Plan);
        Assert.True(panel.Busy);
        Assert.Equal("Spool", WpfHost.On(() => model.QueryText));

        // The close mark is grey and says why, rather than being a mark that does nothing.
        Assert.False(WpfHost.On(() => window.PlanPanel.PlanCloseButton.IsEnabled));
        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.blocked.running"),
            WpfHost.On(() => window.PlanPanel.PlanCloseButton.ToolTip as string));

        gate.SetResult(WpfHost.On(() => Ran(panel)));
        Assert.True(await carrying);
        WpfHost.Settled();

        // Over, so both doors work again.
        Assert.True(WpfHost.On(() => window.PlanPanel.PlanCloseButton.IsEnabled));
        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.False(panel.Showing);

        WpfHost.On(window.Close);
    }

    [Fact]
    public async Task Nothing_else_takes_the_sheet_or_starts_a_second_run_while_one_is_going()
    {
        var gate = new TaskCompletionSource<BulkRun>();
        var starts = 0;
        var window = await Ready(carriedOutBy: (_, _, _, _, _) =>
        {
            starts++;

            return gate.Task;
        });
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        var panel = model.Planned;

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        var plan = panel.Plan;

        var carrying = WpfHost.On(() => window.CarryOut());
        WpfHost.Settled();

        var refusal = Bws.Gui.Texts.Of("gui.status.couldNotDo", Bws.Gui.Texts.Of("gui.plan.blocked.running"));

        // A new preview, said in the one line a menu item can use - asked straight after, because
        // the sheet itself refuses the plan too, and would do it without a word.
        Assert.False(await WpfHost.On(() => window.Preview(ActionKind.Start)));
        Assert.Equal(refusal, WpfHost.On(() => model.Says.Problem));

        // The details of a row, and a second press of the button.
        Assert.False(WpfHost.On(() => window.OpenDetailsOf(model.Rows[0])));
        Assert.False(await WpfHost.On(() => window.CarryOut()));

        // And the sheet itself, asked directly: it keeps its plan whoever asks.
        var another = await WpfHost.On(() => model.PlanAsync(new BulkAction(ActionKind.Start, ["Dnscache"])));
        Assert.False(WpfHost.On(() => panel.Show(another)));
        Assert.False(WpfHost.On(() => panel.Hide()));

        Assert.Same(plan, panel.Plan);
        Assert.Equal(1, starts);

        // THE CLOSE GUARD STILL KNOWS THIS RUN, which is what a second run used to take from it.
        var closed = false;
        WpfHost.On(() =>
        {
            window.Closed += (_, _) => closed = true;
            window.Close();
        });
        WpfHost.Settled();

        Assert.False(closed);

        gate.SetResult(WpfHost.On(() => Ran(panel)));
        await carrying;
        WpfHost.Settled();

        Assert.True(closed);
    }

    /// <summary>
    /// The window asks the RUN, not only the sheet. A run handed over through the test seam leaves
    /// the sheet idle and its button live, so the only thing between a press and a second run is the
    /// window's own question about the run it holds.
    /// </summary>
    [Fact]
    public async Task A_press_while_the_window_holds_a_run_starts_nothing()
    {
        var gate = new TaskCompletionSource();
        using var stopping = new CancellationTokenSource();
        var starts = 0;
        var window = await Ready(carriedOutBy: (plan, _, _, _, _) =>
        {
            starts++;

            return Task.FromResult(new BulkRun { Plan = plan, Runs = [] });
        });

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.On(() => window.TakeThisAsARun(gate.Task, stopping));

        Assert.False(await WpfHost.On(() => window.CarryOut()));
        Assert.Equal(0, starts);

        gate.SetResult();
        WpfHost.On(window.Close);
    }

    [Fact]
    public async Task A_run_that_carried_nothing_out_does_not_say_done()
    {
        var window = await Ready(carriedOutBy: (_, _, _, _, _) =>
            Task.FromException<BulkRun>(new InvalidOperationException("A plan with problems.")));

        var panel = WpfHost.On(() => ((MainViewModel)window.DataContext).Planned);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        Assert.False(await WpfHost.On(() => window.CarryOut()));
        WpfHost.Settled();

        // It used to read "Done. All 0 entries are where you asked."
        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.notice.nothingRun"), WpfHost.On(() => panel.Notice));

        WpfHost.On(window.Close);
    }
}
