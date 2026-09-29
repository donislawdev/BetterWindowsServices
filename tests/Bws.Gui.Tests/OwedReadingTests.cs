using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Readings that used to be lost or left stale - G-5, G-6 and X-1 of the external stability report,
/// 2026-09-29.
///
/// <b>G-5.</b> A full reading asked for while another was out was dropped, so the one the window
/// asks for after a plan has run could vanish under a tick, and the list kept showing what the plan
/// had just changed. <b>G-6.</b> Only a full reading lowered the failed flag, so one failed tick left
/// the window saying it could not read until something moved. <b>X-1.</b> A name handed over twice
/// went into the list twice as the same row.
/// </summary>
public sealed class OwedReadingTests
{
    [Fact]
    public async Task A_full_reading_asked_for_while_a_tick_is_out_is_carried_out_after_it()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler", "Print Spooler"), Rows.Entry("W32Time", "Windows Time"));
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        machine.HoldReadings();

        var tick = model.RefreshAsync();

        // What the window asks for once a plan has run - while the tick is still inside the manager.
        await model.LoadKeepingAsync();

        // Something only a full reading sees.
        machine.Rename("Spooler", "Print Spooler, renamed");
        machine.ReleaseReadings();

        await tick;

        Assert.Equal(2, machine.FullReads);
        Assert.Equal("Print Spooler, renamed", model.Rows.Single(row => row.ServiceName == "Spooler").DisplayName);
    }

    [Fact]
    public async Task A_tick_that_works_after_one_that_failed_brings_the_window_back()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler", "Print Spooler"), Rows.Entry("W32Time", "Windows Time"));
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        // A question nothing answers, so the empty face is the thing on screen.
        model.QueryText = "nothing-is-called-this";

        machine.FailNext = new InvalidOperationException("The manager went away for a moment.");
        await model.RefreshAsync();

        Assert.Equal(ListFace.Failed, model.Says.Face);

        // The next tick works and nothing on the machine moved.
        await model.RefreshAsync();

        Assert.NotEqual(ListFace.Failed, model.Says.Face);
        Assert.DoesNotContain("went away", model.Says.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void A_name_handed_over_twice_is_one_row()
    {
        var index = new RowIndex(new SteppedClock());
        var first = Rows.Entry("Spooler", "Print Spooler");

        index.Absorb([first, Rows.Entry("SPOOLER", "Print Spooler again"), Rows.Entry("W32Time", "Windows Time")]);

        Assert.Equal(2, index.Ordered.Count);
        Assert.Equal("Print Spooler", index.Ordered[0].DisplayName);
    }
}
