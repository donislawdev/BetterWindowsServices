// Explicit, because UseWPF swaps the implicit using set.
using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// How a column SORTS, kept apart from what a column says.
///
/// <b>Split out of ColumnGuards on 2026-08-26 when the size ratchet fired</b>, and the seam
/// was already there: everything left behind asks what a cell reads and which mark it wears,
/// while these five ask how two rows compare. Sorting was turned on separately - backlog 150,
/// 2026-08-10 - and arrived with its own defect, which is the first test below.
/// </summary>
public sealed class ColumnOrderGuards
{
    /// <summary>
    /// The process identifier is ordered as a NUMBER, and until 2026-08-11 it was not.
    ///
    /// <b>A defect found while rewriting the sort rather than a feature.</b> The column binds to
    /// text and DataGrid sorts by the binding path, so from the day sorting was turned on -
    /// 2026-08-10, backlog 150 - 103292 came before 9. Nothing could see it: the sort worked, the
    /// numbers moved, and only reading the column tells you the order is alphabetical.
    /// </summary>
    [Fact]
    public void The_process_id_is_ordered_as_a_number_rather_than_as_text()
    {
        var column = Columns.Of("processId")!;

        var small = Rows.Entry("Small") with { ProcessId = Reading<int>.Present(9) };
        var large = Rows.Entry("Large") with { ProcessId = Reading<int>.Present(103292) };

        Assert.True(
            column.SortKey(small)!.CompareTo(column.SortKey(large)) < 0,
            "9 has to come before 103292. Compared as text it does not, which is what this column did.");

        // And the text really is the other way round, so the claim above is measuring a difference
        // rather than agreeing with itself.
        Assert.True(
            string.CompareOrdinal(column.Reads(small), column.Reads(large)) > 0,
            "The cells no longer sort the wrong way as text, so this guard is proving nothing.");
    }

    /// <summary>An entry with no process sorts as nothing rather than as zero, which is a value.</summary>
    [Fact]
    public void An_entry_with_no_process_has_no_number_to_order_by()
    {
        var column = Columns.Of("processId")!;

        Assert.Null(column.SortKey(Rows.Stopped("Spooler")));
    }

    /// <summary>
    /// The order a column puts two rows in, asked of the comparison rather than of a grid.
    ///
    /// Ascending and descending are both claimed, because a comparer that returns the same answer
    /// whichever way round it was asked sorts perfectly and reverses nothing.
    /// </summary>
    [Fact]
    public void A_column_orders_two_rows_by_what_it_says_and_reverses_when_asked()
    {
        var column = Columns.Of("serviceName")!;

        var first = EntryRow.Of(Rows.Entry("Appinfo"));
        var last = EntryRow.Of(Rows.Entry("Winmgmt"));

        Assert.True(Columns.OrderedBy(column, ascending: true).Compare(first, last) < 0);
        Assert.True(Columns.OrderedBy(column, ascending: false).Compare(first, last) > 0);
    }

    /// <summary>
    /// A row with nothing in this column sorts below every row that has something, and reversing
    /// moves the whole group rather than scattering it.
    ///
    /// The same thing the empty string already does in every column that says nothing with one -
    /// a column where "no value" behaved differently would be a rule nobody could learn by using
    /// the list.
    /// </summary>
    [Fact]
    public void A_row_with_nothing_in_a_column_sorts_below_one_that_has_something()
    {
        var column = Columns.Of("processId")!;

        var running = EntryRow.Of(Rows.Entry("Spooler"));
        var stopped = EntryRow.Of(Rows.Stopped("Dhcp"));

        Assert.True(Columns.OrderedBy(column, ascending: true).Compare(stopped, running) < 0);
        Assert.True(Columns.OrderedBy(column, ascending: true).Compare(stopped, stopped) == 0);
        Assert.True(Columns.OrderedBy(column, ascending: false).Compare(stopped, running) > 0);
    }

    /// <summary>Anything that is not a row has nothing to compare, and says so rather than throwing.</summary>
    [Fact]
    public void Something_that_is_not_a_row_has_no_order()
    {
        var order = Columns.OrderedBy(Columns.Of("serviceName")!, ascending: true);

        Assert.Equal(0, order.Compare(null, null));
        Assert.True(order.Compare(null, EntryRow.Of(Rows.Entry("Spooler"))) < 0);
    }
}
