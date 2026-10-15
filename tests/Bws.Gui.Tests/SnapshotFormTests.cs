using System.IO;
using Bws.Core;
using Bws.Core.Snapshots;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The view before a snapshot is taken - owner's decision W2-d of 2026-10-15, backlog 561: what rights
/// the reading runs with, and a note kept inside the file.
/// </summary>
public sealed class SnapshotFormTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bws-gui-snapshot-form", Guid.NewGuid().ToString("N"));

    public SnapshotFormTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Taking_a_snapshot_opens_the_form_in_place_of_the_list_and_says_the_rights()
    {
        // The same fact the line at the foot of the window says, read from the same place.
        var model = Model();

        model.Snapshotting.Prepare();

        var form = Assert.IsType<SnapshotForm>(model.Snapshotting.Form);

        Assert.False(model.ListOnScreen);
        Assert.Equal(model.Says.Elevated ? Texts.Of("gui.snapshot.form.elevated") : Texts.Of("gui.snapshot.form.unelevated"), form.Rights);
        Assert.True(form.Ready);
    }

    [Fact]
    public async Task The_note_typed_in_the_form_is_kept_inside_the_snapshot_and_the_form_goes()
    {
        var model = Model();
        var path = Path.Combine(_folder, "noted.json");

        model.Snapshotting.Prepare();
        model.Snapshotting.Form!.Note = "  Before the October patch  ";

        await model.Snapshotting.TakeAsync(path);

        Assert.True(SnapshotOnDisk.TryLoad(path, out var snapshot, out _));
        Assert.Equal("Before the October patch", snapshot.Metadata.Note);
        Assert.Null(model.Snapshotting.Form);
        Assert.True(model.ListOnScreen);
    }

    [Fact]
    public async Task A_note_of_spaces_is_no_note_at_all()
    {
        // The command line writes no note when it is given none, and so does the window.
        var model = Model();
        var path = Path.Combine(_folder, "blank.json");

        model.Snapshotting.Prepare();
        model.Snapshotting.Form!.Note = "   ";

        await model.Snapshotting.TakeAsync(path);

        Assert.True(SnapshotOnDisk.TryLoad(path, out var snapshot, out _));
        Assert.Null(snapshot.Metadata.Note);
    }

    [Fact]
    public async Task A_refused_target_keeps_the_form_with_its_note_for_another_folder()
    {
        var model = Model();
        var path = Path.Combine(_folder, "somebody-elses.json");

        await File.WriteAllTextAsync(path, "this is not a snapshot");

        model.Snapshotting.Prepare();
        model.Snapshotting.Form!.Note = "Before the patch";

        await model.Snapshotting.TakeAsync(path);

        Assert.Equal("Before the patch", model.Snapshotting.Form?.Note);
        Assert.Equal("this is not a snapshot", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public void Putting_the_form_away_answers_once_and_gives_the_list_back()
    {
        // Escape asks this in line with the panels, so a second press must fall through to them.
        var model = Model();

        model.Snapshotting.Prepare();

        Assert.True(model.Snapshotting.PutAway());
        Assert.False(model.Snapshotting.PutAway());
        Assert.True(model.ListOnScreen);
    }

    [Fact]
    public async Task The_save_button_is_off_while_the_machine_is_read_for_the_snapshot()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler"));
        var model = new MainViewModel(machine, new SteppedClock(), new Inspector());

        model.Snapshotting.Prepare();
        machine.HoldReadings();

        var taking = model.Snapshotting.TakeAsync(Path.Combine(_folder, "held.json"));

        try
        {
            WpfHost.Until(() => model.Snapshotting.Busy, "the reading to start");

            Assert.False(model.Snapshotting.Form!.Ready);
        }
        finally
        {
            machine.ReleaseReadings();
        }

        await taking.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static MainViewModel Model() => new(new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS")), new SteppedClock(), new Inspector());

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
