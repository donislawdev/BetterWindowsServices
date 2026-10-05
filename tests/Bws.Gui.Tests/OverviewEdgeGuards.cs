using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The machine overview in the two states the external stability report of 2026-09-29 found it
/// wrong in - G-9, points four and five.
///
/// <b>Model only, no window</b>: what the screen counts and which list a click lands on are both
/// decided by <see cref="MainViewModel"/>, and the screen only draws them.
/// </summary>
public sealed class OverviewEdgeGuards
{
    /// <summary>
    /// A first reading that FAILED left an empty index behind, and the screen counted it - six zeroes
    /// that looked exactly like a machine with nothing wrong on it. The placeholder stays until a
    /// reading has actually arrived.
    /// </summary>
    [Fact]
    public async Task A_first_reading_that_failed_counts_nothing_rather_than_zero()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler"))
        {
            FailNext = new InvalidOperationException("the manager would not open")
        };

        var model = new MainViewModel(machine, new SteppedClock()) { ShowingOverview = true };

        await model.LoadAsync();

        Assert.All(model.Overview, line => Assert.Equal(Texts.Of("gui.overview.notCounted"), line.CountText));

        await model.LoadAsync();

        Assert.Contains(model.Overview, line => line.CountText == "1");
    }

    /// <summary>
    /// Every number on the overview is counted over every row and each line carries `!type:driver`,
    /// so a click from the Drivers tab landed on an empty list under a card that had just said one.
    /// The click moves to the list that can answer it - and leaves a list that already can alone.
    /// </summary>
    [Fact]
    public async Task A_line_asked_from_the_drivers_tab_lands_on_a_list_that_can_answer_it()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("disk")), new SteppedClock());

        await model.LoadAsync();

        model.Scope = EntryScope.Drivers;
        model.ShowingOverview = true;

        var running = model.Overview.First(line => line.Query.StartsWith("status:running", StringComparison.Ordinal));

        Assert.Equal(1, running.Count);

        model.Ask(running);

        Assert.Equal(EntryScope.Everything, model.Scope);
        Assert.Equal(["Spooler"], model.Rows.Select(row => row.ServiceName));

        model.Scope = EntryScope.Services;
        model.ShowingOverview = true;
        model.Ask(running);

        Assert.Equal(EntryScope.Services, model.Scope);
    }
}
