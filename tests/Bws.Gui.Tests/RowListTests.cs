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
