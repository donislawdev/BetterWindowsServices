using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Memory read again for rows the tick moved to another process - logic report R07, and the review of PR #96,
/// which found three faults in how: one pass over the listing per moved row, a row the panel was reading left
/// out of the list the tick narrowed, and a failure that went on being said beside a figure read since.
/// </summary>
public sealed class MovedMemoryTests
{
    [Fact]
    public async Task A_host_restarting_its_services_is_read_in_one_pass()
    {
        // Three services in one host, the way svchost holds them - it restarts, and all three move in one tick.
        var memory = new Memory();
        var machine = new LiveMachine(Rows.Entry("BITS"), Rows.Entry("Dnscache"), Rows.Entry("Spooler"));
        var model = new MainViewModel(machine, new SteppedClock(), reader: memory);
        await model.LoadAsync();
        model.QueryText = "memory:>1MB";
        await model.RefreshAsync();
        var asked = memory.Asked;

        machine.Start("BITS", 5001);
        machine.Start("Dnscache", 5001);
        machine.Start("Spooler", 5001);
        await model.RefreshAsync();

        // One question for the one process behind them. A pass per moved row asked it three times.
        Assert.Equal(1, memory.Asked - asked);
        Assert.Equal(3, model.Rows.Count);
        Assert.All(model.Rows, row => Assert.Equal(new ProcessMemory(Memory.Of(5001), 2048, 3), row.Entry.Memory.Value));
    }

    [Fact]
    public async Task A_row_the_panel_is_reading_is_in_the_list_the_tick_narrows()
    {
        // The panel claims the moved row the moment it moves. The tick reads it as well, so the list is
        // narrowed with its figure in hand rather than before the panel's answer comes back.
        var memory = new Memory();
        var machine = new LiveMachine(Rows.Entry("Spooler"));
        var model = new MainViewModel(machine, new SteppedClock(), reader: memory);
        await model.LoadAsync();
        model.QueryText = "memory:>1MB";
        await model.RefreshAsync();
        var row = model.Rows.Single();
        await Open(model, row);

        memory.HoldForTwo();
        machine.Start("Spooler", 4321);
        await model.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        memory.Release();
        await model.Chosen.CatchingUp.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(Memory.Of(4321), row.Entry.Memory.Value!.WorkingSet);
        Assert.Contains(row, model.Rows);
    }

    [Fact]
    public async Task A_failure_to_read_memory_goes_once_the_next_process_is_read()
    {
        var memory = new Memory { FailsFor = 4321 };
        var machine = new LiveMachine(Rows.Entry("Spooler"));
        var model = new MainViewModel(machine, new SteppedClock(), reader: memory);
        await model.LoadAsync();
        var row = model.Rows.Single();
        await Open(model, row);

        machine.Start("Spooler", 4321);
        await Moved(model);

        Assert.Contains("The process went away mid-read", model.Chosen.Notice, StringComparison.Ordinal);

        machine.Start("Spooler", 5555);
        await Moved(model);

        Assert.Equal(Memory.Of(5555), row.Entry.Memory.Value!.WorkingSet);
        Assert.Equal(string.Empty, model.Chosen.Notice);
    }

    /// <summary>The tick, and the panel's own reading it set off - with a ceiling, for the reason PanelReadingTests.Open gives.</summary>
    private static async Task Moved(MainViewModel model)
    {
        await model.RefreshAsync();
        await model.Chosen.CatchingUp.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static async Task Open(MainViewModel model, EntryRow row)
    {
        model.Chosen.Row = row;
        Assert.True(model.Chosen.Show());

        await model.Chosen.CatchingUp.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A memory reader whose figure says which process it was read for, that fails for one process, and that
    /// can hold its questions until two are waiting - the panel's and the tick's - so neither can finish first.
    /// </summary>
    private sealed class Memory : IProcessMemoryReader
    {
        private readonly ManualResetEventSlim _gate = new(initialState: true);
        private int _asked;
        private int _waiting;

        internal static long Of(int processId) => processId * 1024L;

        internal int Asked => Volatile.Read(ref _asked);

        internal int FailsFor { get; init; }

        internal void HoldForTwo()
        {
            Volatile.Write(ref _waiting, 0);
            _gate.Reset();
        }

        internal void Release() => _gate.Set();

        public Reading<ProcessMemory> Read(int processId)
        {
            Interlocked.Increment(ref _asked);

            if (Interlocked.Increment(ref _waiting) >= 2)
            {
                _gate.Set();
            }

            _gate.Wait(TimeSpan.FromSeconds(10));

            return processId == FailsFor
                ? throw new InvalidOperationException("The process went away mid-read.")
                : Reading<ProcessMemory>.Present(new ProcessMemory(Of(processId), 2048, 1));
        }
    }
}
