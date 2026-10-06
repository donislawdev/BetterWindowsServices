using System.Collections.Concurrent;
using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What a full reading keeps about files, and when it keeps nothing - the owner's S-1 decision.
///
/// <b>Written 2026-09-29, W4 of the performance series, backlog 466.</b> Until then every full
/// reading verified every signature again, about 12 s of processor over 797 entries on the machine
/// it was measured on. The owner decided: after a plan, after a change to what is installed, and
/// when a question needs a family the window has not read, the window keeps what it knows about a
/// file whose path did not change - and F5 verifies everything again, always. `ADR-13` carries the
/// decision and its price. Each of the four ways a full reading starts has its test here.
///
/// <b>Counted rather than timed</b>, for the reason <see cref="SecondPhaseTests"/> gives: what the
/// decision is about is WHICH files are verified, and a count says that on any machine.
/// </summary>
public sealed class KeptFileAnswersTests
{
    private const string Spooler = @"C:\Windows\System32\spoolsv.exe";
    private const string TimeService = @"C:\Windows\System32\w32time.dll";

    [Fact]
    public async Task A_plan_keeps_what_the_window_knows_about_files()
    {
        var (model, inspector, _) = await Verified();

        await model.LoadKeepingAsync();

        // Not one file asked about again, and the answers are still on the rows - the question
        // that needed them still has both entries in it.
        Assert.Equal(2, inspector.Asked.Count);
        Assert.Equal(2, model.Rows.Count);
    }

    [Fact]
    public async Task F5_verifies_every_file_again()
    {
        // The other half of the decision, and the one that keeps the price bounded: whatever went
        // stale since the last F5 goes away at the next one.
        var (model, inspector, _) = await Verified();

        await model.LoadAsync();

        Assert.Equal(4, inspector.Asked.Count);
        Assert.Equal(2, inspector.Asked.Count(file => file == Spooler));
    }

    [Fact]
    public async Task After_something_is_installed_only_its_file_is_verified()
    {
        var (model, inspector, machine) = await Verified();
        const string Installed = @"C:\Program Files\Vendor\agent.exe";

        machine.Install(Rows.Entry("VendorAgent") with { BinaryFile = Reading<string>.Present(Installed) });

        await model.RefreshAsync();

        Assert.Equal([Installed], inspector.Asked.Skip(2));
        Assert.Equal(3, model.Rows.Count);
    }

    [Fact]
    public async Task A_column_that_needs_another_family_does_not_verify_signatures_again()
    {
        var inspector = new CountingInspector();
        var memory = new CountingMemory();
        var model = new MainViewModel(Machine(), new SteppedClock(), inspector, memory);
        var columns = new ColumnBar();

        model.ColumnsNeed = () => columns.Needs;

        await model.LoadAsync();

        model.QueryText = "signed:no";

        await model.RefreshAsync();

        columns.Choices.Single(choice => choice.Column.Id == "memory").IsShown = true;

        await model.RefreshAsync();

        Assert.True(memory.Asked > 0, "Turning the memory column on did not read memory.");
        Assert.Equal(2, inspector.Asked.Count);
    }

    /// <summary>A window over two entries, with both files verified once by a question that needed them.</summary>
    private static async Task<(MainViewModel Model, CountingInspector Inspector, LiveMachine Machine)> Verified()
    {
        var inspector = new CountingInspector();
        var machine = Machine();
        var model = new MainViewModel(machine, new SteppedClock(), inspector, new CountingMemory());

        await model.LoadAsync();

        model.QueryText = "signed:no";

        await model.RefreshAsync();

        Assert.Equal(2, inspector.Asked.Count);

        return (model, inspector, machine);
    }

    private static LiveMachine Machine() => new(
        Rows.Entry("Spooler") with { BinaryFile = Reading<string>.Present(Spooler) },
        Rows.Entry("W32Time") with { BinaryFile = Reading<string>.Present(TimeService) });

    /// <summary>
    /// Says every file is unsigned, so both entries answer signed:no, and remembers which files it
    /// was asked about. Concurrent, because the pass asks several at once.
    /// </summary>
    private sealed class CountingInspector : IBinaryInspector
    {
        internal ConcurrentQueue<string> Asked { get; } = [];

        private Reading<BinarySignature> ReadSignature(string file)
        {
            Asked.Enqueue(file);

            return Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.NotSigned, 0, "Someone"));
        }

        public FileInspection Inspect(string file) =>
            new(ReadSignature(file), Reading<string>.Present("1.0.0.0"), Reading<string>.Present(new string('a', 64)));
    }

    private sealed class CountingMemory : IProcessMemoryReader
    {
        private int _asked;

        internal int Asked => _asked;

        public Reading<ProcessMemory> Read(int processId)
        {
            Interlocked.Increment(ref _asked);

            return Reading<ProcessMemory>.Present(new ProcessMemory(1024, 2048, 1));
        }
    }
}
