using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// More than one row at a time, and what a copy does with them. `C2`, packet 2 of `S7`.
///
/// <b>Every copy test that existed before 2026-08-18 picked exactly one row, so all of them stayed
/// green through this change and not one of them could see it.</b> That is the whole reason this file
/// exists: a grid still set to take a single row would satisfy every one of those assertions, and so
/// would a copy that quietly used the first of five.
///
/// <b>The selection is read at the moment it is needed and never kept</b>, which is the repair
/// described at Copy in the window's code behind - a binding into a list that reconciles itself once
/// a second is another party in the middle of `A10`. These tests do the same, so what they exercise
/// is the path the window really takes.
/// </summary>
public sealed class SelectionGuards
{
    /// <summary>
    /// The grid takes more than one row, asked of the grid rather than of the markup.
    ///
    /// <b>Worth a line of its own because everything else here would pass without it.</b> WPF drops a
    /// second selected item silently under Single, so a copy of two rows would come back as a copy of
    /// one and every assertion about the text would be about the wrong thing.
    /// </summary>
    [Fact]
    public void The_list_takes_more_than_one_row()
    {
        var window = WpfHost.Window();

        Assert.Equal(DataGridSelectionMode.Extended, WpfHost.On(() => window.Entries.SelectionMode));

        WpfHost.On(window.Close);
    }

    [Fact]
    public void A_copy_of_two_rows_carries_both_of_them()
    {
        var window = WpfHost.Window();
        var rows = Fill(window, "Spooler", "W32Time");

        WpfHost.On(() => window.Entries.SelectedItems.Add(rows[1]));
        WpfHost.Settled();

        var names = Copying.Name(Picked(window));

        Assert.Equal("Spooler" + Environment.NewLine + "W32Time", names);

        // And the long form, which is the one somebody pastes into a ticket. Asserted by containment
        // rather than by whole text, because what a full copy says is the column catalogue's business
        // and DetailsGuards holds that - here the question is only whether BOTH entries are in it.
        var everything = Copying.Everything(Picked(window));

        Assert.Contains("Spooler", everything, StringComparison.Ordinal);
        Assert.Contains("W32Time", everything, StringComparison.Ordinal);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A RIGHT CLICK ON A ROW OUTSIDE THE SELECTION REPLACES IT, and this is the fault the analysis of
    /// this step found before the code was written.
    ///
    /// Setting a row selected only ever ADDS once the grid takes more than one. So without the
    /// replacement, right clicking a sixth row while five were highlighted would open a menu acting on
    /// six - which is the confidently wrong answer that PointAtRowBeforeMenu was written to prevent,
    /// arriving by the back door.
    /// </summary>
    [Fact]
    public void A_menu_opened_on_a_row_outside_the_selection_acts_on_that_row_alone()
    {
        var window = WpfHost.Window();
        var rows = Fill(window, "Spooler", "W32Time");

        WpfHost.On(() => window.Entries.SelectedItems.Add(rows[1]));
        WpfHost.Settled();

        Assert.Equal(2, WpfHost.On(() => window.Entries.SelectedItems.Count));

        // A third row, which nobody picked. This is what a right click on it has to do.
        var stranger = WpfHost.On(() =>
        {
            var extra = EntryRow.Of(Rows.Entry("Dnscache", "DNS Client"));

            window.Entries.ItemsSource = new[] { rows[0], rows[1], extra };

            return extra;
        });

        WpfHost.Settled();
        WpfHost.On(() => window.PointAt(stranger));
        WpfHost.Settled();

        Assert.Equal("Dnscache", Copying.Name(Picked(window)));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// And a right click INSIDE the selection keeps it, or there would be no way to reach the menu for
    /// five rows at once - which is the entire point of the selection.
    /// </summary>
    [Fact]
    public void A_menu_opened_on_a_row_inside_the_selection_keeps_the_whole_selection()
    {
        var window = WpfHost.Window();
        var rows = Fill(window, "Spooler", "W32Time");

        WpfHost.On(() => window.Entries.SelectedItems.Add(rows[1]));
        WpfHost.Settled();

        WpfHost.On(() => window.PointAt(rows[1]));
        WpfHost.Settled();

        Assert.Equal("Spooler" + Environment.NewLine + "W32Time", Copying.Name(Picked(window)));

        WpfHost.On(window.Close);
    }

    // AN ENTRY WITH NOTHING TO SAY CONTRIBUTING NO LINE was asked here through the description copy
    // until 2026-09-24, when that copy left the row menu. The name copy cannot ask it - a name is
    // never empty - and Copying.Joined says so beside the branch.

    [Fact]
    public void Nothing_picked_is_nothing_to_copy_rather_than_an_empty_line()
    {
        Assert.Null(Copying.Name([]));
        Assert.Null(Copying.Everything([]));
    }

    // -- fixtures --------------------------------------------------------------------------

    private static EntryRow[] Fill(MainWindow window, params string[] names)
    {
        var rows = names.Select(name => EntryRow.Of(Rows.Entry(name, name))).ToArray();

        WpfHost.On(() =>
        {
            window.Entries.ItemsSource = rows;
            window.Entries.SelectedItem = rows[0];
        });

        WpfHost.Settled();

        return rows;
    }

    /// <summary>The rows the grid is holding, read the way the window reads them.</summary>
    private static IReadOnlyList<EntryRow> Picked(MainWindow window) =>
        WpfHost.On(() => (IReadOnlyList<EntryRow>)[.. window.Entries.SelectedItems.OfType<EntryRow>()]);
}
