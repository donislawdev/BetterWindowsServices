using System.Collections.Specialized;
using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The cheap way of filling the list, and the fence around it.
///
/// Filling an empty list one row at a time is 810 notifications and a DataGrid answers every
/// one, so the first fill resets in a single step instead. <b>A reset is also exactly how a
/// DataGrid is told to throw away the selection and the scroll position</b> - the two things
/// `A10` names first - so the cheap path is fenced twice: it only runs when the list is empty,
/// and the collection itself refuses to reset while there is anything to lose.
///
/// Both fences are guarded here, because neither is visible from anywhere else. The window
/// tests check that rows survive a refresh as the same objects, and they would go on passing
/// while a reset threw the selection away underneath them - the objects are the same either
/// way. What changes is whether the list says "I changed like this" or "work it out yourself".
/// </summary>
public sealed class RowListTests
{
    [Fact]
    public void Filling_an_empty_list_says_so_once_rather_than_once_per_row()
    {
        var rows = new RowList();
        var notifications = 0;

        rows.CollectionChanged += (_, _) => notifications++;
        rows.ResetTo(Many(810), nothingToPreserve: true);

        Assert.Equal(810, rows.Count);

        // One, not 810. The number is the whole point of the type existing.
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void Resetting_a_list_somebody_is_looking_at_is_refused()
    {
        // The fence, and it is a throw rather than a silent fallback on purpose. A caller
        // reaching for this path while there is a selection has misunderstood what it costs,
        // and quietly doing the slow thing instead would hide that until somebody noticed
        // their selection disappearing on a machine they could not reproduce it on.
        var rows = new RowList();
        rows.ResetTo(Many(3), nothingToPreserve: true);

        Assert.Throws<InvalidOperationException>(() => rows.ResetTo(Many(3), nothingToPreserve: false));
    }

    [Fact]
    public async Task A_refresh_never_resets_the_list_it_already_filled()
    {
        // The property that matters on screen, checked where it can be seen without a screen.
        // After the first fill, every change has to arrive as an insert or a remove - anything
        // else and the DataGrid is being told it cannot work out what moved, which is what
        // takes the selection and the scroll position with it.
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"), Entry("Dhcp"));
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        Assert.Equal(3, model.Rows.Count);

        var resets = 0;
        model.Rows.CollectionChanged += (_, arguments) =>
        {
            if (arguments.Action == NotifyCollectionChangedAction.Reset)
            {
                resets++;
            }
        };

        // Everything a person does to this list, once the list exists.
        model.QueryText = "spool";
        model.QueryText = "status:running";
        model.QueryText = string.Empty;
        model.ShowDrivers = false;
        model.ShowDrivers = true;
        await model.RefreshAsync();
        await model.LoadAsync();

        Assert.Equal(0, resets);
    }

    [Fact]
    public void Two_rows_that_swap_places_are_moved_rather_than_doubled()
    {
        // The order this list is given comes from the service control manager, and Snapshot says
        // in as many words that the manager's order is in no contract and has been seen to move.
        // A full reading happens on F5 and on every install or removal, so a swap is reachable.
        //
        // Before 2026-08-03 the second pass inserted whenever the object at a position was not
        // the one wanted there, which is only correct while the survivors are in the same
        // relative order as the wanted list. Two rows swapping produced [B, A, B] - the same
        // service on screen twice, and a count that no longer matched the query.
        //
        // The invariant that guards this list could not see it: it asks whether the list holds a
        // row the query rejected, and in a swap both rows are wanted.
        var rows = Many(2);
        var list = new RowList();

        list.Reconcile(rows);
        Assert.Equal(rows, list);

        list.Reconcile([rows[1], rows[0]]);

        Assert.Equal([rows[1], rows[0]], list);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void A_wholesale_reordering_leaves_the_list_exactly_as_asked()
    {
        // The pair above is the smallest case. This is the one that would survive a repair which
        // only handled neighbours swapping - reversing the whole list moves every row.
        var rows = Many(6);
        var list = new RowList();

        list.Reconcile(rows);
        list.Reconcile([.. rows.Reverse()]);

        Assert.Equal(rows.Reverse(), list);

        // Said separately, because equality of two sequences would also hold if the list had
        // gained a duplicate the comparison happened not to reach.
        Assert.Equal(rows.Length, list.Count);
        Assert.Equal(rows.Length, list.Distinct().Count());
    }

    [Fact]
    public void Rows_that_leave_and_arrive_around_a_reordering_still_end_up_right()
    {
        // All three passes at once: one row drops out, one arrives, and the survivors change
        // places. Written because the three are only ever exercised apart otherwise, and the
        // fault this file now guards lived in how two of them met.
        var rows = Many(4);
        var list = new RowList();

        list.Reconcile([rows[0], rows[1], rows[2]]);
        list.Reconcile([rows[3], rows[2], rows[0]]);

        Assert.Equal([rows[3], rows[2], rows[0]], list);
        Assert.Equal(3, list.Distinct().Count());
    }

    private static EntryRow[] Many(int count)
    {
        var rows = new EntryRow[count];

        for (var index = 0; index < count; index++)
        {
            rows[index] = EntryRow.Of(Entry("Service" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static ScmEntry Entry(string name) => new()
    {
        ServiceName = name,
        DisplayName = name,
        Description = Reading<string>.Absent(),
        EntryType = EntryType.OwnProcess,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Absent(),
        Account = Reading<string>.Present("LocalSystem"),
        DependsOn = Reading<IReadOnlyList<string>>.Absent(),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
        BinaryPath = Reading<string>.Absent(),
        BinaryFile = Reading<string>.Absent(),
        BinaryOnDisk = Reading<bool>.Absent(),
        Signature = Reading<BinarySignature>.NotRead(),
        FileVersion = Reading<string>.NotRead(),
        BinaryHash = Reading<string>.NotRead(),
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
        SidType = Reading<ServiceSidType>.Absent(),
        SecurityDescriptor = Reading<string>.Absent(),
        ErrorControl = Reading<ErrorControl>.Absent(),
        LoadOrderGroup = Reading<string>.Absent(),
        Memory = Reading<ProcessMemory>.NotRead()
    };
}
