using System.IO;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A comparison written to a file - owner's decision W2-c of 2026-10-15: the sentences the window says
/// above the table first, then the rows as the table shows them, with every protection the list's own
/// export has.
/// </summary>
public sealed class DiffExportingTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bws-gui-diff-export", Guid.NewGuid().ToString("N"));

    public DiffExportingTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void The_file_says_everything_said_above_the_table_before_the_table()
    {
        // Rule 8 in a file: a reader who never saw the window still learns that one side was read
        // without administrator rights before reading a single row.
        var sheet = DiffFilterTests.Mixed();
        var lines = Lines(DiffExporting.AsCsv(sheet, sheet.Filter.Rows, ","));
        var heading = lines.IndexOf(string.Join(",", Texts.Of("gui.diff.column.entry"), Texts.Of("gui.column.displayName"), Texts.Of("gui.diff.column.what"), Texts.Of("gui.diff.column.earlier"), Texts.Of("gui.diff.column.later")));

        Assert.Equal(sheet.Title, lines[0]);
        Assert.True(heading > 0, "no heading row");
        Assert.Equal(string.Empty, lines[heading - 1]);
        Assert.All(sheet.Caveats, caveat => Assert.InRange(Index(lines, caveat), 1, heading - 2));
        Assert.All(sheet.Sides, side => Assert.InRange(Index(lines, side.About), 1, heading - 2));
        Assert.Equal(sheet.Lines.Select(line => line.ServiceName), lines.Skip(heading + 1).Where(line => line.Length > 0).Select(line => line.Split(',')[0]));
    }

    [Fact]
    public void A_narrowed_table_is_said_in_the_file_and_only_its_rows_go_in()
    {
        var sheet = DiffFilterTests.Mixed();

        sheet.Filter.Choices[1].IsChosen = true;

        var text = DiffExporting.AsCsv(sheet, sheet.Filter.Rows, ",");

        Assert.Contains(Texts.Of("gui.diff.export.shown.many", sheet.Filter.Button, 2, 3), text, StringComparison.Ordinal);
        Assert.DoesNotContain("Gone,", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_search_over_the_comparison_is_said_in_the_file()
    {
        // AS-a, 2026-10-20: "1 of 3 rows" under "All rows" would read as a menu that lost count.
        var sheet = DiffFilterTests.Mixed();

        sheet.Filter.SearchText = "added";

        var lines = Lines(DiffExporting.AsCsv(sheet, sheet.Filter.Rows, ","));

        Assert.True(Index(lines, Texts.Of("gui.diff.export.searched", "added")) > 0, "no sentence about the search");
        Assert.True(Index(lines, Texts.Of("gui.diff.export.shown.many", sheet.Filter.Button, 1, 3)) > 0, "no sentence about how many");
    }

    [Fact]
    public void A_value_from_a_snapshot_that_a_spreadsheet_would_run_arrives_inert()
    {
        // A display name comes from whoever installed the service, and a snapshot file from whoever
        // wrote it - the list export's argument, met by calling the same code.
        var hostile = "=cmd|' /c calc'!A1";
        var sheet = TwoFiles.Sheet(TwoFiles.Of(new Side()), TwoFiles.Of(new Side { Hour = 1 }, Rows.Entry("Evil", hostile)));

        var row = Lines(DiffExporting.AsCsv(sheet, sheet.Filter.Rows, ",")).Last(line => line.StartsWith("Evil,", StringComparison.Ordinal));

        Assert.Contains("'" + hostile, row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_window_writes_it_with_the_mark_a_spreadsheet_needs_and_says_how_many_rows()
    {
        var model = await Compared();
        var path = Path.Combine(_folder, "comparison.csv");
        var rows = model.Snapshotting.Shown!.Filter.Rows;

        Assert.Null(await model.Snapshotting.ExportAsync(path, rows));

        var bytes = await File.ReadAllBytesAsync(path);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.Equal(DiffExporting.Done(rows.Count, "comparison.csv"), model.Says.Done);
    }

    [Fact]
    public async Task A_place_the_file_cannot_go_is_said_and_nothing_is_claimed()
    {
        var model = await Compared();

        var trouble = await model.Snapshotting.ExportAsync(_folder, model.Snapshotting.Shown!.Filter.Rows);

        // The reason Windows gave sits between what failed and the way out (`docs/11` section 5) -
        // so the sentence is held by both of its ends rather than by a reason this test cannot know.
        var ends = Texts.Of("gui.diff.export.failed", "REASON").Split("REASON");

        Assert.NotNull(trouble);
        Assert.StartsWith(ends[0], trouble, StringComparison.Ordinal);
        Assert.EndsWith(ends[1], trouble, StringComparison.Ordinal);

        // The foot puts its own opening before it ("That did not happen."), so the sentence is in it, not all of it.
        Assert.Contains(trouble, model.Says.Problem, StringComparison.Ordinal);
        Assert.Equal(string.Empty, model.Says.Done);
    }

    private async Task<MainViewModel> Compared()
    {
        var earlier = TwoFiles.Written(_folder, "earlier.json", TwoFiles.Of(new Side(), Rows.Entry("Spooler")));
        var later = TwoFiles.Written(_folder, "later.json", TwoFiles.Of(new Side { Hour = 1 }, Rows.Stopped("Spooler")));
        var model = new MainViewModel(new LiveMachine(Rows.Entry("BITS")), new SteppedClock());

        await model.Snapshotting.CompareFilesAsync(earlier, later);

        return model;
    }

    private static List<string> Lines(string text) => [.. text.Split("\r\n")];

    private static int Index(List<string> lines, string sentence) =>
        lines.FindIndex(line => line == sentence || line == "\"" + sentence.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"");
}
