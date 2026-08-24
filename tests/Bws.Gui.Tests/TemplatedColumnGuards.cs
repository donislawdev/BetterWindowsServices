using System.Windows;
using System.Windows.Controls;

namespace Bws.Gui.Tests;

/// <summary>
/// A cell drawn by a template holds the template, not a presenter around it - backlog 222.
///
/// <b>WHY A GUARD FOR ONE OVERRIDE.</b> The saving is invisible in every way this project can
/// normally see it: the window looks identical, every test passes either way, and the only thing
/// that changes is one element and one binding in every cell of every mark column of every realised
/// row. Going back to <c>DataGridTemplateColumn</c> would put them all back and nothing would say
/// so.
///
/// <b>What this does NOT cover, said plainly:</b> whether the marks still show the right shape for
/// the right row once the list has recycled its containers. That was measured with
/// tools/gui-probe/row-cost.ps1 -Recycled rather than asserted here - the counts do not move after
/// the list is run past itself and back - and no test in this project can see a pixel.
/// </summary>
public sealed class TemplatedColumnGuards
{
    [Fact]
    public void A_templated_cell_holds_the_template_rather_than_a_presenter_around_it()
    {
        var made = WpfHost.On(() =>
        {
            var column = new UnwrappedColumn
            {
                CellTemplate = (DataTemplate)WpfHost.Resources["StatusCell"]
            };

            return column.Content(new DataGridCell(), new object());
        });

        Assert.NotNull(made);

        Assert.False(
            made is ContentPresenter,
            "The cell is wrapped in a presenter again, which is the pair of elements backlog 222 removed.");
    }

    /// <summary>
    /// A column with no template behaves the way the framework says it does.
    ///
    /// The fallback is not defensive noise: CellTemplate is settable and nullable, so this has to
    /// answer rather than throw from inside a cell being realised, which is a place an exception is
    /// very hard to read back to its cause.
    /// </summary>
    [Fact]
    public void A_column_with_no_template_falls_back_rather_than_throwing()
    {
        var made = WpfHost.On(() => new UnwrappedColumn().Content(new DataGridCell(), new object()));

        Assert.Null(made);
    }
}
