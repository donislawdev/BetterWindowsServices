using System.IO;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A layout file the window promised to leave alone is left alone - G-3 of the external stability
/// report of 2026-09-29.
///
/// <b>Three files and one window that has handed itself over, and every write the window can make
/// is tried against each.</b> Until 2026-10-05 both writes were unconditional: the window said "it
/// was left alone" about a file from another schema and then, at the first column moved or simply at
/// closing, wrote the default layout over it. The same for a damaged file that could not be moved
/// aside - the only copy of what somebody had - and for a good file that was merely held for a
/// moment while the window read it.
///
/// <b>Each lock is let go BEFORE the writes</b>, which is what makes these tests mean anything: a
/// file still held would refuse the write on its own, and the test would pass on the old code.
/// </summary>
public sealed class LeftAloneGuards : IDisposable
{
    private readonly List<string> _made = [];

    [Fact]
    public void A_file_from_another_schema_is_not_written_over_by_anything_the_window_does()
    {
        var file = Fresh();
        const string Newer = """{ "columns": [], "schemaVersion": 9 }""";

        File.WriteAllText(file.Where, Newer);

        EveryWrite(new KeptColumns(file));

        Assert.Equal(Newer, File.ReadAllText(file.Where));
    }

    [Fact]
    public void A_file_nobody_could_open_is_not_written_over_once_it_frees_up()
    {
        var file = Fresh();
        var good = ColumnLayouts.Default.Render();

        File.WriteAllText(file.Where, good);

        LayoutReading reading;
        KeptColumns kept;

        using (new FileStream(file.Where, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            reading = file.Read();
            kept = new KeptColumns(file);
        }

        EveryWrite(kept);

        Assert.Equal(good, File.ReadAllText(file.Where));

        // And the sentence does not claim a move nobody tried.
        Assert.Equal(Bws.Gui.Texts.Of("gui.layout.unopened", reading.Unopened!), kept.Trouble([]));
    }

    [Fact]
    public void A_damaged_file_that_could_not_be_moved_is_not_written_over_later()
    {
        var file = Fresh();

        File.WriteAllText(file.Where, "not a layout");

        KeptColumns kept;

        // Readable and unmovable at once - the content comes back and the rename is refused.
        using (new FileStream(file.Where, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            kept = new KeptColumns(file);
        }

        EveryWrite(kept);

        Assert.Equal("not a layout", File.ReadAllText(file.Where));
    }

    /// <summary>
    /// G-7: the window restarting as administrator writes its layout before the replacement starts
    /// and nothing afterwards, so the closing write no longer races the replacement reading the same
    /// file. The first half is the control - without Leave the same press does write.
    /// </summary>
    [Fact]
    public void A_window_that_has_handed_itself_over_writes_nothing_more()
    {
        var file = Fresh();
        var kept = new KeptColumns(file);

        WpfHost.On(() => kept.KeepNow(new DataGrid(), new Says()));

        var written = File.ReadAllText(file.Where);

        kept.Leave();
        EveryWrite(kept);

        Assert.Equal(written, File.ReadAllText(file.Where));

        new KeptColumns(file).TheFiltersWere(folded: true, new Says());

        Assert.NotEqual(written, File.ReadAllText(file.Where));
    }

    /// <summary>Every way the window writes the file - a column moved, the chips folded, the overview put away.</summary>
    private static void EveryWrite(KeptColumns kept)
    {
        WpfHost.On(() => kept.KeepNow(new DataGrid(), new Says()));
        kept.TheFiltersWere(folded: true, new Says());
        kept.TheOverviewWasSeen(new Says());
    }

    private PreferencesFile Fresh()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "bws-left-alone-tests",
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);
        _made.Add(directory);

        return new PreferencesFile(directory);
    }

    public void Dispose()
    {
        foreach (var directory in _made.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
