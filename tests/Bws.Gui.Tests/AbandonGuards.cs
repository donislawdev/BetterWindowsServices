using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// The second way of stopping a run in the window, asked of the BUTTONS - backlog 497, 2026-09-30.
///
/// <b>What is at stake is one press nobody meant.</b> The second level stops watching and puts
/// nothing back, so a double click on Interrupt reaching it would leave a machine half taken down
/// with nothing given back. UnderwayTests asks the rule. This asks whether the window keeps it: the
/// run is a gate the test holds, reached through CarriedOutBy with both tokens, so what each press
/// really asked of the run is read off the tokens rather than off the screen.
/// </summary>
public sealed class AbandonGuards
{
    [Fact]
    public async Task A_double_press_on_Interrupt_never_reaches_the_second_level()
    {
        var run = await Running();
        var (window, panel, asks) = (run.Window, run.Panel, run.Asks);
        var footer = window.PlanPanel.Footer;

        WpfHost.On(() => Press(footer.Interrupt));
        WpfHost.On(() => Press(footer.Interrupt));
        WpfHost.Settled();

        Assert.True(asks.Stopping.IsCancellationRequested);
        Assert.False(asks.Abandoning.IsCancellationRequested);
        Assert.False(WpfHost.On(() => footer.Interrupt.IsEnabled));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => footer.AbandonButton.Visibility));
        Assert.Equal(Texts.Of("gui.plan.notice.interrupting"), WpfHost.On(() => panel.Notice));

        // A press that reaches the second button before it is offered - by an instrument, or a
        // routed event from anywhere - is refused at the door, not only by the button being away.
        WpfHost.On(() => Press(footer.AbandonButton));

        Assert.False(asks.Abandoning.IsCancellationRequested);

        await Finish(run);
    }

    /// <summary>
    /// Offered once the wait is over, to the LEFT of Interrupt and without moving it, and a press then
    /// reaches the run. The picture of the foot is left in artifacts/gui.
    /// </summary>
    [Fact]
    public async Task The_second_level_appears_to_the_left_and_reaches_the_run()
    {
        var run = await Running();
        var (window, panel, asks, clock) = (run.Window, run.Panel, run.Asks, run.Clock);
        var footer = window.PlanPanel.Footer;

        WpfHost.On(() => Press(footer.Interrupt));
        var before = Drawn.Of(window.PlanPanel, 1000, 800).Around(footer.Interrupt);

        clock.Advance(Underway.ArmsAfter);
        WpfHost.On(() => panel.Underway.Tick());
        WpfHost.Settled();

        var drawn = Drawn.Of(window.PlanPanel, 1000, 800);
        var abandon = drawn.Around(footer.AbandonButton);
        var interrupt = drawn.Around(footer.Interrupt);

        drawn.Save("plan-footer-abandonable-1000x800.png");

        Assert.True(WpfHost.On(() => footer.AbandonButton.IsEnabled));
        Assert.True(abandon.Width > 0, "the second button was never laid out");
        Assert.True(abandon.X + abandon.Width <= interrupt.X, $"the second button {abandon} is not left of Interrupt {interrupt}");
        Assert.Equal(before, interrupt);

        WpfHost.On(() => Press(footer.AbandonButton));
        WpfHost.Settled();

        Assert.True(asks.Abandoning.IsCancellationRequested);
        Assert.False(WpfHost.On(() => footer.AbandonButton.IsEnabled));
        Assert.Equal(Texts.Of("gui.plan.notice.abandoning"), WpfHost.On(() => panel.Notice));

        await Finish(run);
    }

    /// <summary>
    /// Reaching for the corner of the window asks the first level through the same door, and the sheet
    /// says the window is waiting. A second reach is not the second level.
    /// </summary>
    [Fact]
    public async Task Closing_during_a_run_asks_the_first_level_and_says_the_window_waits()
    {
        var run = await Running();
        var (window, panel, asks) = (run.Window, run.Panel, run.Asks);
        var closed = false;

        WpfHost.On(() =>
        {
            window.Closed += (_, _) => closed = true;
            window.Close();
            window.Close();
        });
        WpfHost.Settled();

        Assert.False(closed);
        Assert.True(asks.Stopping.IsCancellationRequested);
        Assert.False(asks.Abandoning.IsCancellationRequested);
        Assert.False(WpfHost.On(() => window.PlanPanel.Footer.Interrupt.IsEnabled));
        Assert.Equal(
            Texts.Of("gui.plan.notice.because", Texts.Of("gui.plan.notice.interrupting"), Texts.Of("gui.plan.notice.closing")),
            WpfHost.On(() => panel.Notice));

        run.Gate.SetResult(WpfHost.On(() => Ran(panel)));
        await run.Carrying;
        WpfHost.Settled();

        Assert.True(closed);
    }

    private sealed class Asks
    {
        internal CancellationToken Stopping { get; set; }

        internal CancellationToken Abandoning { get; set; }
    }

    /// <summary>
    /// A run held open by the test, and everything a guard needs to read or end it. Properties rather
    /// than a positional record, whose constructor would be a sixth parameter over the ceiling.
    /// </summary>
    private sealed class Held
    {
        internal required MainWindow Window { get; init; }

        internal required Planned Panel { get; init; }

        internal required Asks Asks { get; init; }

        internal required TaskCompletionSource<BulkRun> Gate { get; init; }

        internal required Task<bool> Carrying { get; init; }

        internal required SteppedClock Clock { get; init; }
    }

    private static async Task<Held> Running()
    {
        var gate = new TaskCompletionSource<BulkRun>();
        var asks = new Asks();
        var clock = new SteppedClock();
        var window = await Ready(
            carriedOutBy: (_, _, stopping, abandoning, _) =>
            {
                asks.Stopping = stopping;
                asks.Abandoning = abandoning;

                return gate.Task;
            },
            clock: clock);
        var panel = WpfHost.On(() => ((MainViewModel)window.DataContext).Planned);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));

        var carrying = WpfHost.On(() => window.CarryOut());
        WpfHost.Settled();

        Assert.True(panel.Busy);

        return new Held { Window = window, Panel = panel, Asks = asks, Gate = gate, Carrying = carrying, Clock = clock };
    }

    private static async Task Finish(Held run)
    {
        run.Gate.SetResult(WpfHost.On(() => Ran(run.Panel)));
        await run.Carrying;
        WpfHost.Settled();
        WpfHost.On(run.Window.Close);
    }

    private static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
}
