using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The one field of a kept layout a person can hand edit into nonsense: how wide a column is.
///
/// <b>Its own file since 2026-09-03, and the size ratchet is what asked - the fourth time in one
/// session and the fourth time pointing at a subject rather than a line count.</b> Everything left
/// in <see cref="KeptColumnGuards"/> is about a layout travelling correctly: what the grid is told,
/// what comes back off it, what is written down. This is about the answer to a file that says
/// something a grid cannot do.
///
/// <b>Why the width and nothing else.</b> The identifiers in that file are checked against the
/// catalogue and the flags are true or false, so both have an answer already. A width is a number
/// somebody can type, a converter is happy to read every number there is, and the column then makes
/// its own width its FLOOR - so one bad value reaches the layout pass twice and takes the whole
/// list with it, with nothing on screen saying why.
///
/// <b>What these do NOT claim:</b> nothing here says anything about a pixel on a screen. A grid
/// laid out in this assembly reported widths the real window did not have - `docs/10` section 8 -
/// so a claim about pixels belongs to <c>tools/gui-probe/columns.ps1</c> against a real window.
/// </summary>
public sealed class KeptWidthGuards
{
    /// <summary>
    /// A width that is not a width leaves the column where the theme puts it, and is named.
    ///
    /// The one field in the file a person can plausibly hand edit into nonsense - identifiers are
    /// checked against the catalogue and the flags are true or false. Rule 8: the column quietly
    /// being the wrong size is exactly the fault nobody would investigate.
    /// </summary>
    [Fact]
    public void A_width_that_cannot_be_read_falls_back_to_the_theme_and_is_named()
    {
        _ = WpfHost.Resources;

        var bar = new ColumnBar();
        var refused = new List<string>();

        var grid = WpfHost.On(() =>
        {
            var built = new DataGrid();

            refused.AddRange(ListColumns.Fill(
                built,
                bar,
                ColumnPlan.Of(
                    new ColumnLayout([new KeptColumn("status", Shown: true, Width: "as wide as it likes")]),
                    EntryScope.Services)));

            return built;
        });

        Assert.Equal(["status"], refused);

        var theme = WpfHost.On(() => (DataGridLength)WpfHost.Resources["ColumnStatus"]);
        var status = WpfHost.On(() => grid.Columns.First(column => column.SortMemberPath == "status"));

        Assert.Equal(theme.Value, WpfHost.On(() => status.Width.Value));
    }

    /// <summary>
    /// A number the converter is happy to read and no screen could show is not a width - backlog
    /// 308(a).
    ///
    /// <b>The converter refuses text that is not a number and accepts every number there is</b>, so
    /// <c>1e300</c> came through as a perfectly good width. The column then makes its own width its
    /// floor, so the grid was asked to lay out something that cannot be drawn and the whole list
    /// went with it - with nothing on screen saying why, which is the fault nobody would
    /// investigate.
    ///
    /// <b>Both spellings, because a share is held to the same number as a pixel count.</b> A star
    /// weight of that size is not a width either, and it reaches the same layout pass with the same
    /// result - a rule that guarded only the absolute ones would be a rule about how the file
    /// happens to be spelled.
    /// </summary>
    [Theory]
    [InlineData("1e300")]
    [InlineData("1e300*")]
    [InlineData("-40")]
    [InlineData("NaN")]
    public void A_width_no_screen_could_show_falls_back_to_the_theme_and_is_named(string written)
    {
        _ = WpfHost.Resources;

        var bar = new ColumnBar();
        var refused = new List<string>();

        var grid = WpfHost.On(() =>
        {
            var built = new DataGrid();

            refused.AddRange(ListColumns.Fill(
                built,
                bar,
                ColumnPlan.Of(
                    new ColumnLayout([new KeptColumn("status", Shown: true, Width: written)]),
                    EntryScope.Services)));

            return built;
        });

        Assert.Equal(["status"], refused);

        var theme = WpfHost.On(() => (DataGridLength)WpfHost.Resources["ColumnStatus"]);
        var status = WpfHost.On(() => grid.Columns.First(column => column.SortMemberPath == "status"));

        Assert.Equal(theme.Value, WpfHost.On(() => status.Width.Value));

        // The floor follows the width that was really used, so a width nobody could draw would
        // reach the grid twice over.
        Assert.Equal(theme.Value, WpfHost.On(() => status.MinWidth));
    }
}
