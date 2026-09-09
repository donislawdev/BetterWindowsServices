using System.Windows;
using System.Windows.Controls;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// A window sitting on a sheet that would end a process, and the pieces needed to get one there.
///
/// <b>Its own file since 2026-09-07, and the size ratchet is what asked</b> - the same shape
/// <see cref="PlanFixture"/> took for the same reason a fortnight earlier. Two classes need these:
/// <see cref="ForcedStopViewGuards"/>, which is about the way out working, and
/// <see cref="ForcedStopLayoutGuards"/>, which is about what it costs and what it looks like.
///
/// <b>Brought in with <c>using static</c> at both call sites</b>, so a test still reads
/// <c>ForcedSheet()</c> rather than naming a fixture it does not care about.
///
/// <b>NOTHING HERE ENDS A PROCESS.</b> The run below is a record built by hand and handed to the
/// window through the seam that exists for exactly that - the only run this window can start on its
/// own is a real one against a real manager, and a suite that started one would stop services on
/// whatever computer ran it.
/// </summary>
internal static class ForcedStopFixture
{
    /// <summary>What the panel is showing, which is where its own answers can be read.</summary>
    internal static Planned Sheeted(Bws.Gui.MainWindow window) =>
        WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

    /// <summary>
    /// The template really draws a button, and it really carries the failure it is about.
    ///
    /// <b>Loaded from the dictionary the window merges rather than searched for in a visual tree,
    /// and that is what makes it deterministic.</b> A window built for a test is never shown, so
    /// whether an ItemsControl has generated its containers is a question about timing. What is
    /// asked instead is the thing that would be wrong: whether the template a section names holds
    /// a button at all, and whether that button takes the item with it - because a press that
    /// reaches the window with an empty Tag escalates nothing, silently.
    /// </summary>
    internal static (object? Tag, object? Content, Visibility Showing) OfferedBy(PlanFailure failure)
    {
        var button = WpfHost.On(() =>
        {
            var template = (DataTemplate)Application.Current.FindResource("PlanFailureTemplate");
            var drawn = (FrameworkElement)template.LoadContent();

            drawn.DataContext = failure;

            return ((Panel)drawn).Children.OfType<Button>().Single();
        });

        // SETTLED BETWEEN HANDING OVER THE DATA AND READING THE CONTROL, and the two really are
        // two moments. A binding is re-evaluated by the dispatcher at a lower priority than the
        // code that set the DataContext, so reading Tag in the same call answers with what was
        // there before - which is null, and looks exactly like a template that carries nothing.
        WpfHost.Settled();

        // READ INSIDE THE CALL RATHER THAN HANDED BACK, because a DependencyObject answers only
        // the thread that owns it.
        return WpfHost.On(() => (button.Tag, button.Content, button.Visibility));
    }

    /// <summary>
    /// A window sitting on a sheet that would end a process, reached the only way there is - a
    /// stop that gave up, and the offer under it.
    /// </summary>
    internal static async Task<Bws.Gui.MainWindow> ForcedSheet()
    {
        var window = await Ready(carriedOutBy: GaveUp);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.True(await WpfHost.On(window.CarryOut));
        WpfHost.Settled();

        var offered = WpfHost.On(() => Sheeted(window).Failures.First(one => one.HasOffer));

        Assert.True(await WpfHost.On(() => window.Force(offered)));
        WpfHost.Settled();

        return window;
    }

    /// <summary>
    /// A run in which every step was watched and none of them arrived, with the process still
    /// holding the entry.
    ///
    /// <b>Handed to the window rather than carried out, through the seam that exists for exactly
    /// this.</b> The only run this window can start on its own is a real one against a real
    /// manager, and a suite that started one would stop services on whatever computer ran it.
    /// </summary>
    internal static Task<BulkRun> GaveUp(
        BulkPlan plan,
        TimeSpan ceiling,
        CancellationToken stopping,
        Action<PlanStep, int> announce) =>
        Task.FromResult(new BulkRun
        {
            Plan = plan,
            Runs =
            [
                .. plan.Plans.Select(one => new PlanRun
                {
                    Plan = one,
                    Results =
                    [
                        .. one.Steps.Select(step => new StepResult
                        {
                            Step = step,
                            Outcome = StepOutcome.TimedOut,
                            SkippedBecause = null,
                            Status = Bws.Core.EntryStatus.Running,
                            ProcessId = Bws.Core.Reading<int>.Present(1234),
                            ErrorCode = 0,
                            Error = null,
                            Milliseconds = 60_000
                        })
                    ],
                    Cancelled = false,
                    Ceiling = TimeSpan.FromSeconds(60)
                })
            ]
        });
}
