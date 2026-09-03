// Explicit, because UseWPF swaps the implicit using set.
using System.Windows.Controls;
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
    /// The key a row sorts by is worked out ONCE for that row, not once per comparison.
    ///
    /// <b>Measured before it was built, which is why it exists.</b> A comparer is asked O(n log n)
    /// times for one sort and about ten times for every insertion into an already sorted view - and
    /// a filter widening back out inserts hundreds of rows. For twenty five of the twenty seven
    /// columns the key IS the cell, so each of those calls ran <c>Reads</c>: a <c>string.Join</c>
    /// over a list of names, or a Select plus Distinct plus Join for the triggers column. On 702
    /// insertions into a sorted view: 117.5 ms building the key per comparison against 66.6 ms with
    /// it in hand.
    ///
    /// <b>Counted rather than timed, deliberately.</b> A stopwatch here would measure this machine.
    /// The claim is that the work happens once per row, and that is a number with no spread.
    /// </summary>
    [Fact]
    public void The_key_a_row_sorts_by_is_worked_out_once_rather_than_once_per_comparison()
    {
        var built = 0;

        var column = Counting(() => built++);

        var first = EntryRow.Of(Rows.Entry("Appinfo"));
        var last = EntryRow.Of(Rows.Entry("Winmgmt"));

        var order = Columns.OrderedBy(column, ascending: true);

        for (var asked = 0; asked < 10; asked++)
        {
            order.Compare(first, last);
        }

        Assert.True(
            built == 2,
            $"Two rows compared ten times worked their keys out {built} times. One per row is 2, "
            + "and once per comparison is 20 - so a number near twenty means the key is being "
            + "rebuilt on every call, which is what this guard exists to stop coming back.");
    }

    /// <summary>
    /// A row that has changed sorts by what it says NOW, not by what it said when it was first asked.
    ///
    /// <b>THIS IS THE HALF THAT MATTERS AND THE HALF THAT WOULD FAIL SILENTLY.</b> The list is live:
    /// the machine moves under it once a second, and a row whose status just changed has a new cell
    /// in the status column. A key kept from before that would put the row where it used to belong -
    /// in an order that still looks plausible, with every cell on screen showing the truth. Nothing
    /// would be red and nothing would look wrong.
    ///
    /// The guard is the reason <see cref="EntryRow.Version"/> exists at all, so it asserts through
    /// the same door a real change comes through - an absorbed status - rather than by poking the
    /// counter.
    /// </summary>
    [Fact]
    public void A_row_that_changed_is_sorted_by_what_it_says_now()
    {
        var built = 0;

        var column = Counting(() => built++);

        var moving = EntryRow.Of(Rows.Entry("Spooler"));
        var other = EntryRow.Of(Rows.Entry("Winmgmt"));

        var order = Columns.OrderedBy(column, ascending: true);

        order.Compare(moving, other);
        var beforeTheChange = built;

        moving.Absorb(
            new ScmStatus("Spooler", EntryStatus.Stopped, Reading<int>.Absent()),
            DateTimeOffset.UnixEpoch);

        order.Compare(moving, other);

        Assert.True(
            built > beforeTheChange,
            "A row that absorbed a change was still sorted by the key taken before it. The list is "
            + "live, so that is an order built out of values that are no longer true - and it looks "
            + "exactly like a correct order.");
    }

    /// <summary>
    /// A column whose cell is counted rather than read, so a test can say how often it was asked.
    ///
    /// Everything except <c>Reads</c> is what any text column carries. The identifier is not one of
    /// the catalogue's, on purpose: this column is a probe rather than a stand-in for a real one.
    /// </summary>
    private static Column Counting(Action asked) =>
        new()
        {
            Id = "countedForTheGuard",
            LabelKey = "gui.column.name",
            WidthKey = "ColumnName",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry =>
            {
                asked();

                return entry.ServiceName;
            }
        };

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

    /// <summary>
    /// An order asked for on a grid with no rows yet is MARKED, rather than dropped in silence.
    ///
    /// <b>Backlog 315, and this guard is the reason a line could be DELETED rather than kept as a
    /// net nothing could reach.</b> <c>ListSorting.By</c> used to leave the moment it found no
    /// collection view, before marking anything - so <c>ListColumns.Reapply</c> promised that the
    /// order travels with the layout and delivered it only when the grid's rows binding had
    /// already resolved. Measured over twenty ways back: a window nobody had shown came out
    /// carrying no order SEVENTEEN times, a settled one none out of twenty, and what the file then
    /// held was decided by a fallback in <c>KeptColumns</c> rather than by the caller. The same
    /// probe after this change reads twenty out of twenty.
    ///
    /// <b>A bare grid IS the case rather than standing in for it.</b> An ItemsSource that has not
    /// arrived yet and one that never will are the same thing from in here, and the second is the
    /// one a test can arrange without racing a dispatcher - which is what makes this deterministic
    /// where asserting the same fact through a window was seventeen times in twenty.
    ///
    /// <b>What it deliberately does NOT claim: that the rows are in that order.</b> There are no
    /// rows. The comparer needs a view and is applied when there is one, which is why
    /// <c>MainWindow.SortAsKept</c> still runs after the first reading.
    /// </summary>
    [Fact]
    public void An_order_asked_for_before_the_rows_arrive_is_still_marked_on_the_heading()
    {
        var marked = WpfHost.On(() =>
        {
            var grid = new DataGrid();

            foreach (var column in Columns.All)
            {
                grid.Columns.Add(new DataGridTextColumn { SortMemberPath = column.Id });
            }

            // THE STATE, SAID OUT LOUD RATHER THAN ASSUMED. If a later build hands a bare DataGrid
            // a view of its own, this stops being the case this test says it is and the assertion
            // below would start passing for another reason.
            Assert.Null(grid.ItemsSource);

            ListSorting.By(grid, new KeptSort("displayName", Descending: false));

            return ListSorting.Of(grid);
        });

        Assert.Equal(new KeptSort("displayName", Descending: false), marked);
    }
}
