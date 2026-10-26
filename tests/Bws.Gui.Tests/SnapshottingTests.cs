using System.IO;
using Bws.Core;
using Bws.Core.Snapshots;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Taking a snapshot from the window and comparing one with the machine - the half of W1 that lives
/// past the file dialog, on real files in a folder of its own and a machine the test decides.
/// </summary>
public sealed class SnapshottingTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bws-gui-snapshotting", Guid.NewGuid().ToString("N"));

    public SnapshottingTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task A_snapshot_taken_in_the_window_is_one_the_command_line_reads()
    {
        // One reader for both front ends - SnapshotOnDisk - so this is the command line's own check.
        var model = Model(new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS")));
        var path = Path.Combine(_folder, "taken.json");

        await model.Snapshotting.TakeAsync(path);

        Assert.True(SnapshotOnDisk.TryLoad(path, out var snapshot, out _));
        Assert.Equal(2, snapshot.Entries.Count);
        Assert.Equal(Texts.Of("gui.snapshot.taken.many", 2, "taken.json"), model.Says.Done);
        Assert.False(model.Snapshotting.Busy);
    }

    [Fact]
    public async Task A_file_that_is_not_a_snapshot_is_never_written_over_and_is_left_as_it_was()
    {
        // S-8, decided in the core and reached past nothing the save dialog says: the dialog's "replace
        // it?" is a yes for a snapshot standing there, and only for that.
        var model = Model(new LiveMachine(Rows.Entry("Spooler")));
        var path = Path.Combine(_folder, "somebody-elses.json");

        await File.WriteAllTextAsync(path, "this is not a snapshot", TestContext.Current.CancellationToken);

        await model.Snapshotting.TakeAsync(path);

        Assert.Equal("this is not a snapshot", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Contains(Texts.Of("gui.snapshot.fault.notReplaced", string.Empty).Trim(), model.Says.Problem, StringComparison.Ordinal);
        Assert.Equal(string.Empty, model.Says.Done);
    }

    [Fact]
    public async Task A_target_that_will_be_refused_costs_no_reading_of_the_machine()
    {
        // The command line's courtesy at the same place: the answer is known before the second the
        // machine takes, so the second is not spent.
        var machine = new LiveMachine(Rows.Entry("Spooler"));
        var model = Model(machine);
        var path = Path.Combine(_folder, "refused-early.json");

        await File.WriteAllTextAsync(path, "this is not a snapshot", TestContext.Current.CancellationToken);

        var reads = machine.FullReads;

        await model.Snapshotting.TakeAsync(path);

        Assert.Equal(reads, machine.FullReads);
        Assert.Contains(Texts.Of("gui.snapshot.fault.notReplaced", string.Empty).Trim(), model.Says.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_that_appears_while_the_machine_is_read_is_still_never_written_over()
    {
        // The ask before the reading is a courtesy, the one right before the write is S-8 - and only a
        // file that turns up during the reading tells the two apart.
        var machine = new LiveMachine(Rows.Entry("Spooler"));
        var model = Model(machine);
        var path = Path.Combine(_folder, "arrived-meanwhile.json");
        var reads = machine.FullReads;

        machine.HoldReadings();

        var taking = model.Snapshotting.TakeAsync(path);

        try
        {
            for (var waited = 0; machine.FullReads == reads && waited < 10_000; waited += 10)
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }

            // Past the first ask by now - the reading counts itself before it waits on the hold.
            Assert.True(machine.FullReads > reads, "the reading never started");

            await File.WriteAllTextAsync(path, "somebody else's, written meanwhile", TestContext.Current.CancellationToken);
        }
        finally
        {
            machine.ReleaseReadings();
        }

        await taking.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal("somebody else's, written meanwhile", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Contains(Texts.Of("gui.snapshot.fault.notReplaced", string.Empty).Trim(), model.Says.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reason_a_file_could_not_be_read_cannot_reorder_the_line_either()
    {
        // The reason quotes the path in most cases, so it is escaped like the path beside it.
        var hostile = "exe" + char.ConvertFromUtf32(0x202E) + "txt.json";
        var said = SnapshotWords.Of(new DiskRefusal(DiskFault.Unreadable, "x.json", $"Could not find file '{hostile}'."));

        Assert.Contains("exe<U+202E>txt.json", said, StringComparison.Ordinal);
        Assert.DoesNotContain(char.ConvertFromUtf32(0x202E), said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_snapshot_compared_with_the_machine_it_came_from_finds_nothing()
    {
        var model = Model(new LiveMachine(Rows.Entry("Spooler")));
        var path = Path.Combine(_folder, "same.json");

        await model.Snapshotting.TakeAsync(path);
        await model.Snapshotting.CompareAsync(path);

        Assert.NotNull(model.Snapshotting.Shown);
        Assert.Equal(Texts.Of("gui.diff.same"), model.Snapshotting.Shown.Same);

        // The line that said the machine was being read goes once the answer is on screen.
        Assert.Equal(string.Empty, model.Says.Done);
    }

    [Fact]
    public async Task A_change_made_since_the_snapshot_is_a_row_of_the_comparison()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler"));
        var model = Model(machine);
        var path = Path.Combine(_folder, "before.json");

        await model.Snapshotting.TakeAsync(path);
        machine.Stop("Spooler");
        await model.Snapshotting.CompareAsync(path);

        Assert.Contains(model.Snapshotting.Shown!.Lines, line => line.ServiceName == "Spooler" && line.Before == "Running" && line.After == "Stopped");
    }

    [Fact]
    public async Task A_file_that_is_not_a_snapshot_is_said_at_the_foot_and_nothing_takes_the_middle()
    {
        var model = Model(new LiveMachine(Rows.Entry("Spooler")));
        var path = Path.Combine(_folder, "not-a-snapshot.json");

        await File.WriteAllTextAsync(path, "{}", TestContext.Current.CancellationToken);

        await model.Snapshotting.CompareAsync(path);

        Assert.Null(model.Snapshotting.Shown);
        Assert.Contains(path, model.Says.Problem, StringComparison.Ordinal);
        Assert.False(model.Snapshotting.Busy);
    }

    [Fact]
    public async Task Putting_the_comparison_away_answers_once_and_gives_the_list_back()
    {
        // Escape asks this in line with the panels, so a second press must fall through to them.
        var model = Model(new LiveMachine(Rows.Entry("Spooler")));
        var path = Path.Combine(_folder, "away.json");

        await model.Snapshotting.TakeAsync(path);
        await model.Snapshotting.CompareAsync(path);

        Assert.False(model.ListOnScreen);
        Assert.True(model.Snapshotting.PutAway());
        Assert.False(model.Snapshotting.PutAway());
        Assert.Null(model.Snapshotting.Shown);
        Assert.True(model.ListOnScreen);
    }

    private static MainViewModel Model(LiveMachine machine) => new(machine, new SteppedClock(), new Inspector());

    /// <summary>Every file signed and readable, which is all a snapshot needs from an inspector here.</summary>
    private sealed class Inspector : IBinaryInspector
    {
        public FileInspection Inspect(string file) =>
            new(
                Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.Trusted, 0, "Someone")),
                Reading<string>.Present("1.0.0.0"),
                Reading<string>.Present(new string('a', 64)));
    }
}
