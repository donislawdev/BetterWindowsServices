using System.IO;
using System.Text;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The change history written to a file - owner's decision AS-c of 2026-10-20: the sentences the window
/// says above the table first, what narrows the table, then the lines as the table shows them, with the
/// account's SID beside its name and every protection the list's own export has.
/// </summary>
public sealed class HistoryExportingTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bws-gui-history-export", Guid.NewGuid().ToString("N"));

    public HistoryExportingTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void The_file_says_what_the_log_can_speak_for_and_what_is_shown_before_the_table()
    {
        var sheet = Read();
        var lines = Lines(HistoryExporting.AsCsv(sheet, sheet.Filter.Rows, ","));
        var heading = lines.IndexOf(Heading());

        Assert.Equal(Texts.Of("gui.history.title"), lines[0]);
        Assert.True(heading > 0, "no heading row");
        Assert.Equal(string.Empty, lines[heading - 1]);
        Assert.All(sheet.Admissions, admission => Assert.InRange(Index(lines, admission), 1, heading - 2));
        Assert.InRange(Index(lines, sheet.Filter.Button), 1, heading - 2);
        Assert.InRange(Index(lines, sheet.Filter.Showing), 1, heading - 2);

        // Four lines for seven changes, and nothing after them.
        Assert.Equal(heading + 1 + 4, lines.Count - 1);
        Assert.Equal(string.Empty, lines[^1]);
    }

    [Fact]
    public void A_line_carries_its_count_as_a_number_and_the_account_by_sid()
    {
        // Spooler, switched once by a person: the screen leaves Times and First time blank, the file
        // writes 1 - a spreadsheet sums numbers - and the SID the screen does not show.
        var sheet = Read();
        var spooler = sheet.Filter.Rows.Single(row => row.Entry == "Spooler");
        var bits = sheet.Filter.Rows.First(row => row.Entry == "BITS");

        var lines = Lines(HistoryExporting.AsCsv(sheet, [spooler, bits], ","));

        Assert.Equal(Line(spooler.When, "1", string.Empty, "Spooler", spooler.Change, spooler.By, HistoryLog.PersonSid), lines[^3]);
        Assert.Equal(Line(bits.When, "2", bits.Since, "BITS", bits.Change, bits.By, HistoryLog.SystemSid), lines[^2]);
    }

    [Fact]
    public void A_search_is_said_in_the_file_and_only_the_lines_it_leaves_go_in()
    {
        var sheet = Read();

        sheet.Filter.SearchText = "system";

        var text = HistoryExporting.AsCsv(sheet, sheet.Filter.Rows, ",");
        var lines = Lines(text);

        // Found as the export writes a sentence - the quotes round the search are doubled inside a quoted value.
        Assert.True(Index(lines, Texts.Of("gui.history.export.searched", "system")) > 0, "no sentence about the search");
        Assert.True(Index(lines, Texts.Of("gui.history.showing.narrowedGrouped.many", 4, 7, 2)) > 0, "no sentence about how many");
        Assert.DoesNotContain(HistoryLog.PersonSid, text, StringComparison.Ordinal);
    }

    [Fact]
    public void There_is_nothing_to_write_until_a_reading_has_come_back_and_nothing_after_a_failure()
    {
        var sheet = new HistorySheet();

        Assert.False(sheet.Exportable);

        sheet.Read(HistoryLog.Empty());
        Assert.True(sheet.Exportable);

        sheet.Failed("Access is denied.");
        Assert.False(sheet.Exportable);
    }

    [Fact]
    public async Task The_window_writes_it_with_the_mark_a_spreadsheet_needs_and_says_how_many_lines()
    {
        var model = await Opened();
        var path = Path.Combine(_folder, "change-history.csv");
        var rows = model.History.Shown!.Filter.Rows;

        Assert.Null(await model.History.ExportAsync(path, rows));

        var bytes = await File.ReadAllBytesAsync(path);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.Contains(HistoryLog.SystemSid, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal(HistoryExporting.Done(rows.Count, "change-history.csv"), model.Says.Done);
    }

    [Fact]
    public async Task A_place_the_file_cannot_go_is_said_and_nothing_is_claimed()
    {
        var model = await Opened();

        var trouble = await model.History.ExportAsync(_folder, model.History.Shown!.Filter.Rows);

        var ends = Texts.Of("gui.history.export.failed", "REASON").Split("REASON");

        Assert.NotNull(trouble);
        Assert.StartsWith(ends[0], trouble, StringComparison.Ordinal);
        Assert.EndsWith(ends[1], trouble, StringComparison.Ordinal);
        Assert.Contains(trouble, model.Says.Problem, StringComparison.Ordinal);
        Assert.Equal(string.Empty, model.Says.Done);
    }

    [Fact]
    public async Task With_the_history_put_away_nothing_is_written()
    {
        var model = await Opened();
        var rows = model.History.Shown!.Filter.Rows.ToList();
        var path = Path.Combine(_folder, "late.csv");

        model.History.PutAway();

        Assert.Null(await model.History.ExportAsync(path, rows));
        Assert.False(File.Exists(path));
    }

    private static HistorySheet Read()
    {
        var sheet = new HistorySheet();

        sheet.Read(HistoryLog.Ordinary());

        return sheet;
    }

    private static async Task<MainViewModel> Opened()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock(), history: new HistoryLog(HistoryLog.Ordinary()));

        await model.OpenHistory();

        return model;
    }

    private static string Heading() => Line(
        Texts.Of("gui.history.column.when"),
        Texts.Of("gui.history.column.times"),
        Texts.Of("gui.history.column.since"),
        Texts.Of("gui.history.column.entry"),
        Texts.Of("gui.history.column.change"),
        Texts.Of("gui.history.column.by"),
        Texts.Of("gui.history.export.sid"));

    /// <summary>One line as the export writes it - through the list's own writer, so quoting is the export's rule, not this test's.</summary>
    private static string Line(params string[] values)
    {
        var text = new StringBuilder();

        Exporting.Line(text, values, ",");

        return text.ToString().TrimEnd('\r', '\n');
    }

    private static List<string> Lines(string text) => [.. text.Split("\r\n")];

    private static int Index(List<string> lines, string sentence) =>
        lines.FindIndex(line => line == sentence || line == "\"" + sentence.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"");
}
