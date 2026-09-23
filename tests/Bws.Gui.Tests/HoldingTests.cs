using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// When the list stops rearranging itself, and when it must not.
///
/// <b>Its own file since 2026-08-05, because the size ratchet asked for a seam and this is one.</b>
/// `A10` spends five rules on a list refreshing under somebody's hand, and what it asks for is a
/// policy rather than a feature: hold, or do not hold, and on what evidence.
///
/// <b>The fault that started this file is why it is worth having apart.</b> Holding was applied to
/// the FIRST fill as well - which is not a rearrangement of anything - so a window opening under
/// the pointer never filled, and said "810 entries" over an empty grid until the pointer moved
/// away. Found by tools/gui-probe/check.ps1, which a person has to run, and by nothing in the
/// suite: every view model test loads first and interacts afterwards, which is a person's order
/// and not a script's. Backlog 127.
/// </summary>
public sealed class HoldingTests
{
    /// <summary>A window that opens under the pointer still fills. Backlog 127.</summary>
    [Fact]
    public async Task A_list_that_is_still_empty_fills_even_while_somebody_is_pointing_at_it()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS")), new SteppedClock());

        model.Interacting = true;

        await model.LoadAsync();

        Assert.NotEmpty(model.Rows);
    }

    /// <summary>
    /// And once it has something in it, holding works as it always did - the exception is about
    /// there being nothing to protect, not about the pointer being ignored.
    /// </summary>
    [Fact]
    public async Task A_list_with_rows_in_it_is_still_held_while_somebody_is_pointing_at_it()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS"));
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        model.Interacting = true;
        machine.Remove("BITS");

        await model.RefreshAsync();

        Assert.Equal(2, model.Rows.Count);
    }
}
