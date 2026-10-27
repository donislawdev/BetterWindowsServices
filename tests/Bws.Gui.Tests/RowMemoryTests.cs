using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What a row's memory says once the tick has moved its process - logic report R07, 2026-10-27. The figure
/// is a measurement of one process, so it may not outlive the process it measured.
/// </summary>
public sealed class RowMemoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_stopped_entry_has_no_process_to_use_memory()
    {
        var row = EntryRow.Of(Measured(1234));

        Assert.True(row.Absorb(new ScmStatus("Spooler", EntryStatus.Stopped, Reading<int>.Absent()), Now));

        // What MemoryPass says of an entry with no process - a fact, not a gap.
        Assert.Equal(ReadOutcome.Absent, row.Entry.Memory.Outcome);
    }

    [Fact]
    public void Another_process_has_memory_nobody_has_read()
    {
        var row = EntryRow.Of(Measured(1234));

        row.Absorb(new ScmStatus("Spooler", EntryStatus.Running, Reading<int>.Present(4321)), Now);

        Assert.Equal(ReadOutcome.NotRead, row.Entry.Memory.Outcome);
    }

    [Fact]
    public void The_same_process_keeps_its_figure_whatever_its_state_does()
    {
        var row = EntryRow.Of(Measured(1234));

        row.Absorb(new ScmStatus("Spooler", EntryStatus.StopPending, Reading<int>.Present(1234)), Now);

        Assert.True(row.Entry.Memory.IsPresent);
    }

    [Fact]
    public void Memory_never_read_stays_never_read()
    {
        // A listing without the memory family read nobody's - a stop says nothing new about that.
        var row = EntryRow.Of(Rows.Entry("Spooler"));

        row.Absorb(new ScmStatus("Spooler", EntryStatus.Stopped, Reading<int>.Absent()), Now);

        Assert.Equal(ReadOutcome.NotRead, row.Entry.Memory.Outcome);
    }

    private static ScmEntry Measured(int processId) => Rows.Entry("Spooler") with
    {
        ProcessId = Reading<int>.Present(processId),
        Memory = Reading<ProcessMemory>.Present(new ProcessMemory(processId * 1024L, 2048, 1))
    };
}
