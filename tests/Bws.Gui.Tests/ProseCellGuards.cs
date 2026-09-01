// Explicit, because UseWPF swaps the implicit using set and takes what this needs out of it.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The one column that is written for a person to read, and the one row that is allowed to be tall.
///
/// <b>Backlog 192, owner's decision 2026-09-01 out of three roads.</b> A description runs to 1251
/// characters on the machine this was built on and the cell gave it one line with an ellipsis, so
/// the column showed the opening words of a sentence and nothing else. The chosen row now wraps it,
/// up to four lines, and the rest of the list is exactly what it was.
///
/// <b>What these guards can and cannot say.</b> They hold the wiring - that the description column
/// is built with the style that grows, that the style only grows for a chosen row, and that the cap
/// really is four lines rather than a number that used to be four. <b>Whether it LOOKS right is the
/// owner's eye and nothing here claims otherwise</b>, which is the division `docs/11` draws for
/// every slice of this window.
/// </summary>
public sealed class ProseCellGuards
{
    /// <summary>
    /// The description column is built with the style that grows, and its neighbours are not.
    ///
    /// <b>Asked of the built column rather than of the catalogue</b>, because the catalogue naming a
    /// face proves nothing about what reaches the grid - the mapping from a face to a style lives in
    /// <c>ListColumns</c> and is exactly the link that can be left out.
    /// </summary>
    [Fact]
    public void The_description_is_the_only_column_built_to_grow()
    {
        var grid = Built();

        var prose = (Style)WpfHost.Resources["CellProse"];

        var grew = WpfHost.On(() => grid.Columns
            .OfType<DataGridTextColumn>()
            .Where(column => ReferenceEquals(column.ElementStyle, prose))
            .Select(column => column.SortMemberPath)
            .ToList());

        Assert.Equal(["description"], grew);
    }

    /// <summary>
    /// It grows only while its row is the chosen one, and the trigger asks the cell rather than the
    /// row.
    ///
    /// <b>The row template lost a FindAncestor trigger on 2026-08-31 because walking the tree per
    /// row was costing bindings</b> - eight to seven - so this checks that the reach here is the
    /// short one: a TextBlock asking the cell it sits in, one level up, rather than the row.
    /// </summary>
    [Fact]
    public void It_wraps_for_a_chosen_row_and_for_nothing_else()
    {
        var prose = (Style)WpfHost.Resources["CellProse"];

        var trigger = Assert.IsType<DataTrigger>(Assert.Single(prose.Triggers));

        // AS TEXT RATHER THAN AS A BOOLEAN, AND THAT IS XAML RATHER THAN A SLIP. A DataTrigger read
        // from markup keeps its Value as the string the file held until the binding it watches gives
        // it a type to convert against - so this came back "Expected: True, Actual: True" the first
        // time it ran, which is a comparison of a bool against a string printing identically.
        Assert.Equal("True", trigger.Value);

        var asked = Assert.IsType<Binding>(trigger.Binding);

        Assert.Equal("IsSelected", asked.Path.Path);
        Assert.Equal(typeof(DataGridCell), asked.RelativeSource.AncestorType);

        var wrapping = trigger.Setters.OfType<Setter>()
            .Single(setter => setter.Property == TextBlock.TextWrappingProperty);

        Assert.Equal(TextWrapping.Wrap, wrapping.Value);

        // AND THE OTHER HALF: nothing wraps until then. A style that wrapped always would pass the
        // assertion above and would make every row of a 810 row list a paragraph.
        Assert.DoesNotContain(
            prose.Setters.OfType<Setter>(),
            setter => setter.Property == TextBlock.TextWrappingProperty);
    }

    /// <summary>
    /// The cap is four lines, and it is four because the arithmetic says so.
    ///
    /// <b>WPF has no TextBlock.MaxLines - that property belongs to WinUI</b> - so the cap is a
    /// height, and a height is only a number of lines while something says how tall a line is. The
    /// two values live apart in the theme, so one can be changed without the other and the cap
    /// would quietly stop being four.
    ///
    /// <b>Four rather than any other number, and it is measured rather than chosen.</b> Over the 800
    /// entries of this machine 448 carry a description at all, the median is 95 characters and the
    /// ninetieth is 321 - so four lines is the whole text for most of the list, and the tail stays
    /// with the details panel that already shows it in full.
    /// </summary>
    [Fact]
    public void The_cap_is_four_lines_rather_than_a_height_that_used_to_be()
    {
        var line = (double)WpfHost.Resources["HeightProseLine"];
        var cap = (double)WpfHost.Resources["HeightProseWhenChosen"];

        Assert.Equal(4, cap / line);
    }

    private static DataGrid Built()
    {
        _ = WpfHost.Resources;

        var bar = new ColumnBar();

        // Every column on, because the description is off by default and a plan that left it out
        // would make the first guard pass over a column that is not there.
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
