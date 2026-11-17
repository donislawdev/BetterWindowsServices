using System.IO;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The file under the settings page - `docs/PROJEKT-USTAWIENIA-20261117.md`: how it is read before the first
/// sentence, when a choice of the page is this window's to write and when another window's later choice is
/// written instead (decision E3), what Restart now does when the file or the start refuses, and how often the
/// page asks the disk. Apart from SettingsGuards, which holds the page and the keys, because that file stands
/// near the crowd line of long test files (tools/margin.ps1).
/// </summary>
public sealed class SettingsFileGuards
{
    /// <summary>
    /// The early reader takes a language only from a file the window then reads as its own. A file the
    /// window would quarantine - no columns, a schema it does not read, not a layout at all - gives no
    /// language either, or the window would open in a language taken from a file it calls broken.
    /// </summary>
    [Theory]
    [InlineData("""{ "columns": [], "language": "pl", "schemaVersion": 7 }""")]
    [InlineData("""{ "language": "pl", "schemaVersion": 7 }""")]
    [InlineData("""{ "columns": {}, "language": "pl", "schemaVersion": 7 }""")]
    [InlineData("""{ "columns": [], "language": "pl", "schemaVersion": "7" }""")]
    [InlineData("""{ "columns": [], "language": "pl", "schemaVersion": 0 }""")]
    [InlineData("""{ "columns": [], "language": "pl", "schemaVersion": 99 }""")]
    [InlineData("""{ "columns": [], "language": "pl", "language": "de", "schemaVersion": 7 }""")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("not json")]
    public void The_early_reader_refuses_what_the_window_refuses(string file)
    {
        var early = KeptChoices.FromText(file);
        var window = ColumnLayouts.Read(file).Layouts;

        Assert.Equal(window is null, early is null);
        Assert.Equal(window?.Choices, early);
    }

    /// <summary>
    /// Read to the ceiling and not a byte past it, by counting what arrives rather than by asking the size
    /// first - a file replaced after that question cannot get round it (rule 25). Both roads read this way.
    /// </summary>
    [Fact]
    public void The_file_is_never_read_past_its_ceiling()
    {
        var path = Path.Combine(Folder(), PreferencesFile.Name);

        File.WriteAllText(path, new string(' ', (int)KeptChoices.Biggest));
        Assert.Equal((int)KeptChoices.Biggest, KeptChoices.ReadText(path)?.Length);

        File.AppendAllText(path, " ");
        Assert.Null(KeptChoices.ReadText(path));
        Assert.Null(KeptChoices.Peek(path));

        var reading = new PreferencesFile(Path.GetDirectoryName(path)!).Read();

        Assert.NotNull(reading.Unreadable);
        Assert.NotNull(reading.MovedAside);
    }

    /// <summary>
    /// A file that cannot be opened now, or holds bytes that are not text, gives no choices and is left where
    /// it is - the window's own read says why a moment later, and only that read may move a file aside.
    /// </summary>
    [Fact]
    public void A_file_that_cannot_be_read_now_gives_no_choices()
    {
        var file = WpfHost.Nowhere();

        using (new FileStream(file.Where, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Null(file.PeekChoices());
        }

        File.WriteAllBytes(file.Where, [0x7B, 0xC3, 0x28, 0x7D]);

        Assert.Null(file.PeekChoices());
        Assert.True(File.Exists(file.Where));
    }

    /// <summary>
    /// A choice is this window's only until it is on disk. After that a later choice made in another window
    /// is what this one writes back - one window turns remembering on again and keeps a search, the other
    /// turns it off and picks another language later, and the first writing again must not undo either.
    /// </summary>
    [Fact]
    public void A_choice_made_later_in_another_window_is_not_undone()
    {
        var file = WpfHost.Nowhere();
        var says = new Says();
        var first = new KeptColumns(file);
        var second = new KeptColumns(file);

        first.ChooseLanguage("pl", says);
        first.RememberRecentIs(remember: false, says);
        first.RememberRecentIs(remember: true, says);
        first.Remember("status:running", says);

        second.ChooseLanguage("de", says);
        second.RememberRecentIs(remember: false, says);

        first.Remember("name:sql*", says);
        first.TheFiltersWere(folded: true, says);

        var read = new KeptColumns(file);

        Assert.Equal(new KeptChoices("de", RememberRecent: false), read.Choices);
        Assert.Empty(read.RecentQueries);
        Assert.True(read.FiltersFolded);
    }

    /// <summary>
    /// A choice the file refused stays this window's, and the next write that goes through puts it there -
    /// over what another window wrote before it, because this one is the newer word.
    /// </summary>
    [Fact]
    public void A_choice_the_file_refused_is_kept_by_the_next_write()
    {
        var file = WpfHost.Nowhere();
        var says = new Says();
        var kept = new KeptColumns(file);

        new KeptColumns(file).ChooseLanguage("de", says);

        // Held open the way another program can hold it: readable, but not to be replaced.
        using (new FileStream(file.Where, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            kept.ChooseLanguage("pl", says);
        }

        Assert.Equal("de", file.PeekChoices()?.Language);
        Assert.NotEmpty(says.Problem);

        kept.TheFiltersWere(folded: true, says);

        Assert.Equal("pl", file.PeekChoices()?.Language);
    }

    /// <summary>
    /// Restart now: a write the file refused stops it before anything is started and the window stays with
    /// the sentence, a start that is refused is said and the window stays, and one that goes through hands
    /// over what the window shows and stops this window writing.
    /// </summary>
    [Fact]
    public void Restart_now_stops_where_the_file_or_the_start_refuses()
    {
        var file = WpfHost.Nowhere();
        _ = WpfHost.Resources;
        var window = WpfHost.On(() => new MainWindow(file));
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        var started = new List<string>();
        var kept = new KeptColumns(file);

        WpfHost.On(() => SettingsDoor.Restart(window, model, new KeptColumns(Refusing()), () => "carried", Start(started, null)));

        Assert.Empty(started);
        Assert.NotEmpty(WpfHost.On(() => model.Says.Problem));

        WpfHost.On(() => SettingsDoor.Restart(window, model, kept, () => "carried", Start(started, "No.")));

        Assert.Equal(new[] { "carried" }, started);
        Assert.Contains("No.", WpfHost.On(() => model.Says.Problem), StringComparison.Ordinal);

        WpfHost.On(() => SettingsDoor.Restart(window, model, kept, () => "carried", Start(started, null)));

        Assert.Equal(2, started.Count);

        // Left: nothing this window does from here on reaches the file.
        kept.TheFiltersWere(folded: true, new Says());

        Assert.False(new KeptColumns(file).FiltersFolded);
    }

    /// <summary>
    /// The page asks the disk when it is built, when it looks again and after a change it handed out - never
    /// from a getter, which a binding calls on every render, and never without a file to ask about.
    /// </summary>
    [Fact]
    public void The_page_asks_the_disk_only_when_it_looks_again()
    {
        var asked = 0;
        var there = (File: false, Folder: false);
        var hands = new SettingsHands(() => [], () => false, _ => { }, _ => { }, () => { asked++; return there; });
        var page = new SettingsPage(new SettingsFacts(["en", "pl"], "en", "en", null, RememberRecent: true, @"C:\nowhere\bws-preferences.json"), hands);

        _ = (page.SaysFileNotYet, page.CanOpenFolder, page.OpenFolderHint, page.SaysFileNotYet);

        Assert.Equal(1, asked);
        Assert.True(page.SaysFileNotYet);
        Assert.False(page.CanOpenFolder);

        there = (true, true);
        page.Languages[^1].IsOn = true;

        Assert.Equal(2, asked);
        Assert.False(page.SaysFileNotYet);
        Assert.True(page.CanOpenFolder);

        page.RememberRecent = false;
        page.Rethink();

        Assert.Equal(4, asked);

        new SettingsPage(new SettingsFacts(["en"], "en", "en", null, RememberRecent: true, File: null), hands).Rethink();

        Assert.Equal(4, asked);
    }

    /// <summary>What the page is handed about the disk: nothing before the first write, both after, nothing without a profile.</summary>
    [Fact]
    public void The_file_and_its_folder_are_looked_at_where_they_are()
    {
        var file = new PreferencesFile(Path.Combine(Folder(), "profile"));

        Assert.Equal((false, false), file.OnDisk());
        Assert.Null(file.Write(ColumnLayouts.Default));
        Assert.Equal((true, true), file.OnDisk());
        Assert.Equal((false, false), new PreferencesFile(string.Empty).OnDisk());
    }

    /// <summary>A folder of its own for one test, beside the ones WpfHost hands out.</summary>
    private static string Folder() => Path.GetDirectoryName(WpfHost.Nowhere(seenTheOverview: false).Where)!;

    /// <summary>A preferences file whose folder cannot be made - a file stands where its parent should be.</summary>
    private static PreferencesFile Refusing()
    {
        var blocker = Path.Combine(Folder(), "blocker");

        File.WriteAllText(blocker, string.Empty);

        return new PreferencesFile(Path.Combine(blocker, "inside"));
    }

    /// <summary>A start that writes down what it was handed and answers as told - null for "started".</summary>
    private static Func<string, string?> Start(List<string> started, string? answer) => carried =>
    {
        started.Add(carried);
        return answer;
    };
}
