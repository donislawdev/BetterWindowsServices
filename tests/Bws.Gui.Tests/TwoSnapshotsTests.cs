using System.IO;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Two snapshots compared in the window - W2 of `docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md`, owner's
/// decision W2-a: the one taken first is the earlier side, and the words say "two snapshots".
/// </summary>
public sealed class TwoSnapshotsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bws-gui-two-snapshots", Guid.NewGuid().ToString("N"));

    public TwoSnapshotsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task The_one_taken_first_is_the_earlier_side_whichever_was_chosen_first()
    {
        // Chosen later-file first: the comparison must still read Running before and Stopped after,
        // or the same pair would turn an addition into a removal by the order of two clicks.
        var earlier = TwoFiles.Written(_folder, "tuesday.json", TwoFiles.Of(new Side { Hour = 1 }, Rows.Entry("Spooler")));
        var later = TwoFiles.Written(_folder, "wednesday.json", TwoFiles.Of(new Side { Hour = 2 }, Rows.Stopped("Spooler")));
        var model = new MainViewModel(new LiveMachine(Rows.Entry("BITS")), new SteppedClock());

        await model.Snapshotting.CompareFilesAsync(later, earlier);

        var sheet = Assert.IsType<DiffSheet>(model.Snapshotting.Shown);

        Assert.Contains(sheet.Lines, line => line.ServiceName == "Spooler" && line.Before == "Running" && line.After == "Stopped");
        Assert.Contains("tuesday.json", sheet.Sides[0].About, StringComparison.Ordinal);
        Assert.Contains("wednesday.json", sheet.Sides[1].About, StringComparison.Ordinal);
        Assert.False(model.ListOnScreen);
    }

    [Fact]
    public void Two_snapshots_are_said_as_two_snapshots_and_never_as_a_snapshot_and_this_machine()
    {
        var sheet = TwoFiles.Sheet(
            TwoFiles.Of(new Side { Machine = "STAGING" }, Rows.Entry("Spooler")),
            TwoFiles.Of(new Side { Machine = "PRODUCTION", Hour = 1 }, Rows.Entry("Spooler"), Rows.Entry("Added")));

        Assert.Equal(Texts.Of("gui.diff.files.title"), sheet.Title);
        Assert.Equal((Texts.Of("gui.diff.column.earlier"), Texts.Of("gui.diff.column.later")), (sheet.BeforeHeading, sheet.AfterHeading));
        Assert.Contains(Texts.Of("gui.diff.files.caveat.machine"), sheet.Caveats);
        Assert.DoesNotContain(Texts.Of("gui.diff.caveat.machine"), sheet.Caveats);
        Assert.Equal(Texts.Of("gui.diff.files.row.added"), Assert.Single(sheet.Lines).What);
    }

    [Fact]
    public void Both_notes_are_shown_and_both_sides_say_their_rights()
    {
        var sheet = TwoFiles.Sheet(
            TwoFiles.Of(new Side { Elevated = false, Note = "Before the patch" }, Rows.Entry("Spooler")),
            TwoFiles.Of(new Side { Hour = 1, Note = "After the patch" }, Rows.Entry("Spooler")));

        Assert.Equal(
            [Texts.Of("gui.diff.note", "Before the patch"), Texts.Of("gui.diff.note", "After the patch")],
            sheet.Sides.Select(side => side.Note));
        Assert.Contains("without", sheet.Sides[0].About, StringComparison.Ordinal);
        Assert.DoesNotContain("without", sheet.Sides[1].About, StringComparison.Ordinal);
        Assert.Contains(Texts.Of("gui.diff.files.caveat.elevation"), sheet.Caveats);
    }

    [Fact]
    public async Task A_second_file_that_is_not_a_snapshot_is_named_and_nothing_takes_the_middle()
    {
        var good = TwoFiles.Written(_folder, "good.json", TwoFiles.Of(new Side(), Rows.Entry("Spooler")));
        var bad = Path.Combine(_folder, "bad.json");
        var model = new MainViewModel(new LiveMachine(Rows.Entry("BITS")), new SteppedClock());

        await File.WriteAllTextAsync(bad, "{}", TestContext.Current.CancellationToken);
        await model.Snapshotting.CompareFilesAsync(good, bad);

        Assert.Null(model.Snapshotting.Shown);
        Assert.Contains(bad, model.Says.Problem, StringComparison.Ordinal);
        Assert.False(model.Snapshotting.Busy);
        Assert.True(model.ListOnScreen);
    }

    [Fact]
    public async Task A_snapshot_compared_with_itself_is_the_sentence_for_two_snapshots()
    {
        // Legal and degenerate (stan D): the same file chosen twice finds nothing, and says so in the
        // words for two files rather than the words for this machine.
        var only = TwoFiles.Written(_folder, "only.json", TwoFiles.Of(new Side(), Rows.Entry("Spooler")));
        var model = new MainViewModel(new LiveMachine(Rows.Entry("BITS")), new SteppedClock());

        await model.Snapshotting.CompareFilesAsync(only, only);

        Assert.Equal(Texts.Of("gui.diff.files.same"), model.Snapshotting.Shown?.Same);
        Assert.Empty(model.Snapshotting.Shown!.Lines);
    }

    [Fact]
    public async Task Two_files_are_compared_without_reading_the_machine()
    {
        // No inspector here at all - a window that could not read signatures still compares two files,
        // and the machine is not read for a comparison it plays no part in.
        var machine = new LiveMachine(Rows.Entry("BITS"));
        var model = new MainViewModel(machine, new SteppedClock());
        var one = TwoFiles.Written(_folder, "one.json", TwoFiles.Of(new Side(), Rows.Entry("Spooler")));
        var reads = machine.FullReads;

        await model.Snapshotting.CompareFilesAsync(one, one);

        Assert.NotNull(model.Snapshotting.Shown);
        Assert.Equal(reads, machine.FullReads);
    }
}
