using System.Diagnostics;
using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A window nobody can see asks the machine nothing - backlog 467, G-5 of the external performance
/// report of 2026-09-28.
///
/// <b>Minimised is the case that was missing.</b> WPF leaves IsVisible true on a minimised window,
/// and the timer was stopped only when IsVisible changed - so a window minimised for an afternoon
/// read the manager once a second all afternoon while a comment beside the timer said it did not.
///
/// <b>Held by what the machine is asked, not by the timer.</b> The fake machine counts every
/// reading, which is the effect this is about, and a property handing the timer out for a test is
/// one more thing the product carries for nobody (DeadCodeGuards).
/// </summary>
public sealed class MinimisedWindowGuards
{
    /// <summary>
    /// Minimised asks nothing for longer than the timer's own second, and coming back asks at once
    /// rather than a second later - the list on screen is what the machine looked like when the
    /// window went away.
    /// </summary>
    [Fact]
    public async Task A_minimised_window_stops_asking_and_asks_at_once_when_it_comes_back()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler", "Print Spooler"));
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        WpfHost.On(() =>
        {
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;
            window.Show();
        });

        // The first look reads the machine and starts the timer, whose tick is a cheap reading.
        WpfHost.Until(() => machine.StatusReads > 0, "the window started asking once a second");

        WpfHost.On(() => window.WindowState = WindowState.Minimized);

        // A reading already out when the window went away is let finish, or it would be counted
        // against the minimised window.
        await Task.Delay(200);
        WpfHost.Settled();

        var minimised = Asked(machine);

        await Task.Delay(1500);

        Assert.Equal(minimised, Asked(machine));

        WpfHost.On(() => window.WindowState = WindowState.Normal);

        // "At once" is sooner than the timer's first tick, which is a second after it starts.
        var clock = Stopwatch.StartNew();

        while (Asked(machine) == minimised && clock.ElapsedMilliseconds < 600)
        {
            await Task.Delay(10);
        }

        Assert.True(
            Asked(machine) > minimised,
            "Coming back from minimised asked the machine nothing for 600 ms - the timer's first tick is a second away.");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A window opened minimised - a shortcut set to "Run: Minimized" - reads the machine once for
    /// its first look and then waits to be looked at. The first look starts the timer itself, so it
    /// has to ask the same question the rest of this file asks.
    /// </summary>
    [Fact]
    public async Task A_window_opened_minimised_reads_once_and_waits_to_be_looked_at()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler", "Print Spooler"));
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        WpfHost.On(() =>
        {
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;
            window.WindowState = WindowState.Minimized;
            window.Show();
        });

        // The model was read once before the window, and the first look reads it again.
        WpfHost.Until(() => machine.FullReads >= 2, "the first look read the machine");

        await Task.Delay(200);
        WpfHost.Settled();

        var looked = Asked(machine);

        await Task.Delay(1500);

        Assert.Equal(looked, Asked(machine));

        WpfHost.On(() => window.WindowState = WindowState.Normal);

        var clock = Stopwatch.StartNew();

        while (Asked(machine) == looked && clock.ElapsedMilliseconds < 600)
        {
            await Task.Delay(10);
        }

        Assert.True(Asked(machine) > looked, "Coming back to a window opened minimised asked the machine nothing for 600 ms.");

        WpfHost.On(window.Close);
    }

    private static int Asked(LiveMachine machine) => machine.StatusReads + machine.FullReads;
}
