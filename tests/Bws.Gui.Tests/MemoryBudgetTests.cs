using Bws.Core;
using Bws.Gui.ViewModels;
using Xunit.Abstractions;

namespace Bws.Gui.Tests;

/// <summary>
/// What the window holds, and whether it keeps holding more of it.
///
/// Written 2026-08-02, after a table in `docs/04` showed that five of six performance budgets
/// in the specification had nothing whatsoever holding them - and that the one about memory
/// had been broken since the window was built without anybody finding out. A budget with no
/// guard is a wish, and these two turn the half of that budget that can be tested into
/// something a build can refuse.
///
/// <b>They measure the managed heap, not the process.</b> That is a deliberate narrowing and
/// the reason is measured: over an hour at rest the window's working set moved between 110.8
/// and 152.2 MB while nothing in the program changed, because Windows trims it - so a guard
/// on the process would fail and pass for reasons that are not ours. Private bytes are steady
/// but include the WPF and graphics stack, which a test without a window cannot even load.
/// The managed heap is the part this code decides, and it is the part that can regress.
///
/// The layers around it are measured rather than guarded, by `tools/memory-probe/ladder.ps1`.
/// On 2026-08-02 they came out: 6.7 MB for the .NET runtime, <b>48.1 MB for WPF and the
/// graphics stack before a line of our code</b>, 3.2 for a DataGrid, 12.5 for 810 rows in it.
/// None of that is ours to fix, and none of it belongs in a test that has no window.
/// </summary>
public sealed class MemoryBudgetTests(ITestOutputHelper output)
{
    /// <summary>
    /// How many entries to load. 810 because that is what the machine this project is
    /// measured against has, so the figure below subtracts against the ladder.
    /// </summary>
    private const int Entries = 810;

    /// <summary>
    /// What 810 loaded entries may hold on the managed heap, in bytes.
    ///
    /// A ratchet on today's measurement, exactly like the file length ceilings: nothing here
    /// says this is a good number, it says this is the number and it is not allowed to grow.
    ///
    /// <b>Measured 2026-08-02, three runs, identical to a tenth of a megabyte: 0.8 MB, being
    /// 0.6 MB of entries and 0.2 MB of rows.</b> The ceiling is a little under twice that,
    /// which is room for a collector's noise and not room for a regression - the measurement
    /// is stable enough that it does not need more.
    ///
    /// <b>The number is worth reading before it is guarded.</b> The ladder in
    /// `tools/memory-probe` put the whole window at 96.5-97.3 MB of private bytes, of which
    /// about 26.5 is ours rather than WPF's. This says the DATA in that 26.5 is 0.8. What the
    /// window holds is not what it costs: the rest is assemblies, XAML, the theme and code
    /// compiled on the way past. Anybody arriving here to make the window lighter by holding
    /// less should read that sentence twice before starting.
    ///
    /// It may only ever go down.
    /// </summary>
    private const long RowsCeilingBytes = 3L * 1024 * 1024 / 2;

    /// <summary>
    /// What a thousand ticks may add. Effectively nothing, and that is the point.
    ///
    /// A tick builds a status for every entry and hands it to the rows. If any of that were
    /// retained, a thousand of them would add hundreds of megabytes - so this threshold does
    /// not need to be tight to catch a leak, it needs to be small enough that a real leak
    /// cannot hide under it. One megabyte over a thousand ticks is one kilobyte a tick, and a
    /// tick that retained one kilobyte would cost 3.6 MB an hour.
    /// </summary>
    private const long TickDriftBytes = 1L * 1024 * 1024;

    private const int Ticks = 1000;

    [Fact]
    public async Task A_loaded_listing_holds_no_more_than_it_did()
    {
        // Three readings, not two, and the middle one is the reason. The first attempt built
        // the entries before it started measuring, so it reported 0.1 MB - the rows alone -
        // and would have gone on passing while an entry doubled in size. Splitting the two
        // says which layer moved, which is the difference between a guard and an alarm.
        var empty = Settled();
        var machine = new LiveMachine(Many(Entries));
        var loadedData = Settled();

        var model = new MainViewModel(machine, new SteppedClock());
        await model.LoadAsync();

        // Read while the model is still reachable, then touch it afterwards. Without the last
        // line the collection below is free to take the whole thing away, and the test would
        // measure nothing while passing - the same shape as a test comparing two empty results.
        var after = Settled();

        var data = loadedData - empty;
        var rows = after - loadedData;
        var held = after - empty;

        Assert.Equal(Entries, model.Rows.Count);

        output.WriteLine(
            $"HELD {held / 1024.0 / 1024.0:F1} MB for {Entries} entries " +
            $"= {data / 1024.0 / 1024.0:F1} MB of entries + {rows / 1024.0 / 1024.0:F1} MB of rows, " +
            $"ceiling {RowsCeilingBytes / 1024 / 1024} MB");

        Assert.True(
            held < RowsCeilingBytes,
            $"A loaded listing now holds {held / 1024.0 / 1024.0:F1} MB on the managed heap for " +
            $"{Entries} entries, past a ceiling of {RowsCeilingBytes / 1024 / 1024} MB set at what it " +
            "held on 2026-08-02. Either something began holding more per entry, or the ceiling " +
            "needs an argument rather than a raise - it may only ever go down.");

        GC.KeepAlive(model);
    }

    [Fact]
    public async Task A_thousand_ticks_hold_nothing()
    {
        // The question `docs/08` item 26 asked, answered where it can be answered in seconds
        // rather than in an hour. An hour of the real window at rest was measured separately
        // on the same day and agreed - private bytes 98.2 at the first sample and 98.8 at the
        // last - but that run cannot be repeated on every build, and this can.
        var machine = new LiveMachine(Many(Entries));
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        // After the first tick rather than after the load, so what is measured is the steady
        // state. The first tick can settle things a load left half done, and counting that as
        // drift would put a real cost under the heading of a leak.
        await model.RefreshAsync();
        var before = Settled();

        for (var tick = 0; tick < Ticks; tick++)
        {
            await model.RefreshAsync();
        }

        var after = Settled();
        var drift = after - before;

        output.WriteLine($"DRIFT {drift / 1024.0:F0} KB over {Ticks} ticks of {Entries} entries");

        Assert.True(
            drift < TickDriftBytes,
            $"A thousand ticks added {drift / 1024.0 / 1024.0:F1} MB to the managed heap. The tick " +
            "builds a status for every entry every second, so anything it retains is a leak that " +
            "shows up as a window nobody can leave open all day.");

        GC.KeepAlive(model);
    }

    /// <summary>
    /// The heap with nothing pending, read the same way both times.
    ///
    /// Twice, because one collection leaves finalisable objects waiting for a second pass and
    /// the difference between one and two collections is larger than the drift being measured.
    /// </summary>
    private static long Settled()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        return GC.GetTotalMemory(forceFullCollection: true);
    }

    private static ScmEntry[] Many(int count)
    {
        var entries = new ScmEntry[count];

        for (var index = 0; index < count; index++)
        {
            entries[index] = One("Service" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return entries;
    }

    /// <summary>
    /// An entry with every field carrying something, because an entry of empty readings would
    /// measure the shape of the record and not what a real listing holds.
    /// </summary>
    private static ScmEntry One(string name) => new()
    {
        ServiceName = name,
        DisplayName = name + " display name, about as long as a real one is",

        // ABOUT AS LONG AS A REAL ONE, AND THIS FIELD IS THE LONGEST TEXT A LISTING NOW CARRIES.
        // The longest measured on this machine is 1251 characters, which would be a fixture
        // measuring the worst case rather than the ordinary one - the sentence below is the
        // length a Windows description usually runs to. Whether the average is nearer this or
        // nearer 1251 is NOT MEASURED, and it matters to this test alone rather than to the
        // product, because a listing holds one of these per entry.
        Description = Reading<string>.Present(
            name + " keeps something on this machine working. If you turn this service off, "
            + "whatever depends on it stops working, and anything that explicitly depends on it "
            + "will fail to start."),

        EntryType = EntryType.OwnProcess,
        PerUserRole = PerUserRole.None,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1234),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        Account = Reading<string>.Present(@"NT AUTHORITY\LocalService"),
        DependsOn = Reading<IReadOnlyList<string>>.Present(["RPCSS", "http"]),
        RequiredBy = Reading<IReadOnlyList<string>>.NotRead(),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
        BinaryPath = Reading<string>.Present(@"C:\WINDOWS\System32\svchost.exe -k netsvcs -p"),
        BinaryFile = Reading<string>.Present(@"C:\WINDOWS\System32\svchost.exe"),
        BinaryOnDisk = Reading<bool>.Present(true),
        Signature = Reading<BinarySignature>.NotRead(),
        FileVersion = Reading<string>.NotRead(),
        BinaryHash = Reading<string>.NotRead(),
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Present(["SeChangeNotifyPrivilege"]),
        SidType = Reading<ServiceSidType>.Present(ServiceSidType.Unrestricted),
        SecurityDescriptor = Reading<string>.Present("O:SYG:SYD:(A;;CCLCSWLOCRRC;;;AU)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)"),
        ErrorControl = Reading<ErrorControl>.Present(Core.ErrorControl.Normal),
        LoadOrderGroup = Reading<string>.Absent(),
        Memory = Reading<ProcessMemory>.NotRead()
    };
}
