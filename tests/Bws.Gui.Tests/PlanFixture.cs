using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A window with a plan on screen, and a run of that plan made by hand.
///
/// <b>Its own file since 2026-08-19, and the size ratchet is what asked - for the seventh time in
/// this window's tests and the seventh time pointing at a real seam.</b> Two classes need these:
/// <see cref="CarryingGuards"/>, which is about the window carrying a plan out, and
/// <see cref="PlanReportGuards"/>, which is about what the panel says once there is a result. They
/// were one class until the file went past five hundred lines.
///
/// <b>Brought in with <c>using static</c> at both call sites</b>, so a test still reads
/// <c>Ready()</c> and <c>Ran(panel)</c> rather than naming a fixture it does not care about.
///
/// <b>NOTHING HERE TOUCHES A MACHINE, and that is the property the whole pair depends on.</b> The
/// run below is a record built by hand out of the plan that is on screen - no manager is asked
/// anything - because a test that carried a plan out for real would stop services on whatever
/// computer ran the suite.
/// </summary>
internal static class PlanFixture
{
    /// <summary>
    /// A run of the plan that is actually on screen, made by hand.
    ///
    /// <b>Built FROM the panel's own plan rather than from a plan of its own</b>, so what the tests
    /// read back is a report about the steps beside it - the pairing `ADR-11` exists for. A run made
    /// up separately would assert that words reach a screen while saying nothing about whether they
    /// are words about the right plan.
    /// </summary>
    internal static BulkRun Ran(Planned panel, string? refusing = null)
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
    internal static async Task<MainWindow> Ready(
        bool elevated = true,
        Func<BulkPlan, CancellationToken, Action<PlanStep, int>, Task<BulkRun>>? carriedOutBy = null)
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

        var window = WpfHost.Window(model, carriedOutBy: carriedOutBy);

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
