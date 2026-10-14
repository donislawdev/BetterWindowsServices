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

        await File.WriteAllTextAsync(path, "this is not a snapshot");

        await model.Snapshotting.TakeAsync(path);

        Assert.Equal("this is not a snapshot", await File.ReadAllTextAsync(path));
        Assert.Contains(Texts.Of("gui.snapshot.fault.notReplaced", string.Empty).Trim(), model.Says.Problem, StringComparison.Ordinal);
        Assert.Equal(string.Empty, model.Says.Done);
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

        await File.WriteAllTextAsync(path, "{}");

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
