using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What a window that has gone still does, and what it stops doing. Backlog 300.
///
/// <b>Its own file since 2026-09-03, and the size ratchet is what asked - the fifth time in one
/// session.</b> The subject is narrow and it is not the subject of <see cref="WindowGuards"/>: that
/// class is about a window being BUILT, which is where the two worst faults in `docs/10` lived.
/// This is about the moment after the last one, when work asked for by a window nobody can see any
/// longer comes back with an answer.
/// </summary>
public sealed class ClosedWindowGuards
{
    /// <summary>
    /// A reading that comes back after the window has gone writes nothing - backlog 300.
    ///
    /// <b>WHAT THIS IS NOT, said first, because the report that raised it claimed something bigger
    /// and the claim was measured and is false.</b> It said a continuation returning to a closed
    /// dispatcher throws, the throw lands on a pool thread, and the process dies.
    /// `tools/gui-probe/close-during-load.ps1` closed this window fifteen times, ten of them
    /// genuinely inside the expensive pass, and all fifteen exited with code 0 in 69-117 ms.
    ///
    /// <b>What is left is smaller and real:</b> between the window closing and the process ending,
    /// a reading already out was coming back to rebuild rows, re-run a query and move a status line
    /// for a screen that had gone. Stopping the WORK is a separate decision with a cost, and it is
    /// written out at <see cref="Readings.NoLongerWanted"/>.
    ///
    /// <b>Driven through the window rather than the model, because the wiring is the half that can
    /// silently not happen.</b> The model refusing to absorb is one line, and a window that never
    /// tells it would look exactly like this test passing.
    /// </summary>
    [Fact]
    public async Task A_reading_that_comes_back_after_the_window_has_gone_writes_nothing()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler", "Print Spooler"));
        var model = new MainViewModel(machine, new SteppedClock());
        var window = WpfHost.Window(model);

        // Held inside the manager, which is the only way to have a reading genuinely in flight
        // while something else happens - a double is otherwise finished before the next line runs.
        machine.HoldReadings();

        var reading = WpfHost.On(model.LoadAsync);

        WpfHost.On(window.Close);
        WpfHost.Settled();

        machine.ReleaseReadings();

        await reading;

        // The entry the machine really holds never reaches the rows, because nobody is looking.
        Assert.Empty(model.Rows);

        // And the reading did happen, so this is a guard about what was done with the answer
        // rather than about a reading that never started.
        Assert.Equal(1, machine.FullReads);
    }
}
