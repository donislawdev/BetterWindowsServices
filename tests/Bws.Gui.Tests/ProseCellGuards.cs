// Explicit, because UseWPF swaps the implicit using set and takes what this needs out of it.
using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The one column that is written for a person to read, and the reason it is not allowed to grow.
///
/// <b>Backlog 192 was built on 2026-09-01 and reverted the same day, by the owner, by looking at
/// it.</b> A chosen row wrapped its description over four lines - the wiring worked, the cap was
/// arithmetic rather than a guess, and it looked bad on the screen. That last judgement is the one
/// thing this project has no arithmetic for and the owner is the only reader who can make it.
///
/// <b>THIS FILE NOW GUARDS THE DECISION RATHER THAN THE FEATURE, and that is the point of keeping
/// it.</b> Three tests here used to hold the growing in place. A revert that simply deleted them
/// would leave the next session free to build the same thing again from the same complaint, which
/// is still open and still real: a description runs to 1251 characters on this machine and the cell
/// gives it one line with an ellipsis. What answers that today is Enter, which opens the details
/// panel, and the context menu, which copies it. Both were already there.
///
/// <b>What this cannot say:</b> whether a future answer looks right. That stays the owner's eye,
/// which is the division `docs/11` draws for every slice of this window - and the division that
/// just decided this.
/// </summary>
public sealed class ProseCellGuards
{
    /// <summary>
    /// The description cell is the same height as every other cell, whatever row is chosen.
    ///
    /// <b>Asked of the style rather than of a rendered row</b>, because a trigger is what made the
    /// row grow and a trigger is what would bring it back. A style with no triggers cannot change
    /// with the row it is drawn in, which is the whole of the decision in one assertion.
    /// </summary>
    [Fact]
    public void The_description_cell_does_not_grow_for_a_chosen_row()
    {
        _ = WpfHost.Resources;

        var prose = (Style)WpfHost.Resources["CellProse"];

        Assert.Empty(prose.Triggers);

        // AND NOT BY SETTING IT ALWAYS EITHER, which is the other way back to a list of paragraphs
        // and would pass the assertion above. Wrapping is off for this cell exactly as it is for
        // every other one, and it is inherited from CellText rather than set here.
        Assert.DoesNotContain(
            prose.Setters.OfType<Setter>(),
            setter => setter.Property == TextBlock.TextWrappingProperty);
    }

    /// <summary>
    /// The description column is still built with its own style, so the next answer has a place.
    ///
    /// <b>The style says nothing more than <c>CellText</c> today and the name is still worth
    /// holding.</b> This column is the one that carries sentences rather than values, and a revert
    /// that also dissolved the name would make the next attempt start by rediscovering which column
    /// that is - which is what <c>ColumnFace.Prose</c> exists to answer.
    /// </summary>
    [Fact]
    public void The_description_column_is_still_the_one_built_from_prose()
    {
        var grid = Built();

        var prose = (Style)WpfHost.Resources["CellProse"];

        var built = WpfHost.On(() => grid.Columns
            .OfType<DataGridTextColumn>()
            .Count(column => ReferenceEquals(column.ElementStyle, prose)));

        Assert.Equal(1, built);
    }

    private static DataGrid Built()
    {
        _ = WpfHost.Resources;

        var bar = new ColumnBar();

        // Every column on, because the description is off by default and a plan that left it out
        // would make the guard above pass over a column that is not there.
        var plan = ColumnPlan.Of(
            new ColumnLayout([.. Columns.All.Select(column => new KeptColumn(column.Id, Shown: true, Width: null))]),
            EntryScope.Services);

        bar.Follow(plan);

        return WpfHost.On(() =>
        {
            var built = new DataGrid();

            ListColumns.Fill(built, bar, plan);

            return built;
        });
    }
}
