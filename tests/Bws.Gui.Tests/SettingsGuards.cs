using System.Windows;
using System.Windows.Input;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The settings page and what it keeps - `docs/PROJEKT-USTAWIENIA-20261117.md`, owner's decisions D1-D4 and
/// E1-E4 of 2026-11-17: the window's language, read once when it starts, the switch over recent searches,
/// and where the file is - schema 7 of the one file this program keeps.
/// </summary>
public sealed class SettingsGuards
{
    /// <summary>
    /// A kept language is two or three ASCII letters or nothing - it becomes a resource name and a path beside
    /// the program, and the file it comes from is anybody's to edit (rule 25).
    /// </summary>
    [Theory]
    [InlineData("pl", "pl")]
    [InlineData("PL", "pl")]
    [InlineData("fil", "fil")]
    [InlineData(@"..\x", null)]
    [InlineData("../de", null)]
    [InlineData("pt-BR", null)]
    [InlineData("p", null)]
    [InlineData("abcd", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Only_a_code_of_letters_is_kept(string? given, string? kept) =>
        Assert.Equal(kept, KeptChoices.Shaped(given));

    /// <summary>
    /// A letter that is a letter but not an ASCII one is refused too - char.IsLetter would let it through.
    /// Built from its code point, because this repository's files are English and plain ASCII.
    /// </summary>
    [Fact]
    public void A_letter_outside_ascii_is_not_a_code() =>
        Assert.Null(KeptChoices.Shaped("p" + (char)0x0142));

    /// <summary>Both keys read, and every shape that is not theirs read as the key missing - never the file refused.</summary>
    [Theory]
    [InlineData("""{ "columns": [], "language": "pl", "rememberRecentQueries": false, "schemaVersion": 7 }""", "pl", false)]
    [InlineData("""{ "columns": [], "schemaVersion": 6 }""", null, true)]
    [InlineData("""{ "columns": [], "language": 7, "rememberRecentQueries": "no", "schemaVersion": 7 }""", null, true)]
    [InlineData("""{ "columns": [], "language": "..\\x", "schemaVersion": 7 }""", null, true)]
    public void The_two_choices_are_read_and_a_wrong_shape_is_no_choice(string file, string? language, bool remember)
    {
        var early = KeptChoices.FromText(file);

        Assert.NotNull(early);
        Assert.Equal(new KeptChoices(language, remember), early);

        // The window's own reader agrees with the early one - one reader per key for both roads.
        var layouts = ColumnLayouts.Read(file).Layouts;

        Assert.NotNull(layouts);
        Assert.Equal(early, layouts.Choices);
    }

    /// <summary>A file the window would not read as its own gives no choices to the early read either.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "columns": [] }""")]
    [InlineData("""{ "columns": [], "language": "pl", "schemaVersion": 99 }""")]
    [InlineData("""{ "columns": [], "language": "pl", "language": "de", "schemaVersion": 7 }""")]
    public void A_file_the_window_would_not_read_gives_no_choices(string file) =>
        Assert.Null(KeptChoices.FromText(file));

    /// <summary>
    /// Written only when chosen, each in its ordinal place, and schema 7 - a file diffs against yesterday's
    /// copy on nothing, and a default file says nothing about either.
    /// </summary>
    [Fact]
    public void The_choices_are_written_only_when_made_and_in_their_place()
    {
        var plain = ColumnLayouts.Default.Render();

        Assert.DoesNotContain("\"language\"", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("rememberRecentQueries", plain, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\": 7", plain, StringComparison.Ordinal);

        var both = (ColumnLayouts.Default with { FiltersFolded = true, OverviewSeen = true, RecentQueries = ["a"] })
            .WithChoices(new KeptChoices("pl", RememberRecent: true)).Render();
        var off = ColumnLayouts.Default.WithChoices(new KeptChoices(null, RememberRecent: false)).Render();

        Assert.True(Before(both, "filtersFolded", "\"language\""), both);
        Assert.True(Before(both, "\"language\"", "overviewSeen"), both);
        Assert.True(Before(off, "rememberRecentQueries", "schemaVersion"), off);
        Assert.Contains("\"rememberRecentQueries\": false", off, StringComparison.Ordinal);
    }

    /// <summary>Off means none kept - decision E4 - also when the switch arrives from another window's write.</summary>
    [Fact]
    public void Turning_remembering_off_takes_the_searches_with_it()
    {
        var with = ColumnLayouts.Default with { RecentQueries = ["status:running"] };

        Assert.Empty(with.WithChoices(new KeptChoices(null, RememberRecent: false)).RecentQueries);
        Assert.Single(with.WithChoices(new KeptChoices(null, RememberRecent: true)).RecentQueries);
    }

    /// <summary>
    /// The language of a session: a kept code only when it was found, then Windows when it was found, then
    /// English - and a kept code that could not be used is reported rather than dropped (rule 8). Each row is
    /// the kept code, then Windows and what was found, then what is shown, honoured and missing ("-" for none).
    /// </summary>
    [Theory]
    [InlineData("pl", "pl en,pl", "pl pl -")]
    [InlineData("pl", "de en,de", "de - pl")]
    [InlineData("-", "pl en,pl", "pl - -")]
    [InlineData("-", "pl en", "en - -")]
    [InlineData("pl", "pl en", "en - pl")]
    [InlineData(@"..\x", "pl en,pl", "pl - -")]
    public void The_language_is_picked_from_what_was_found(string kept, string machine, string expected)
    {
        static string? Or(string word) => word == "-" ? null : word;

        var (windows, found) = (machine.Split(' ')[0], machine.Split(' ')[1].Split(','));
        var answer = expected.Split(' ');

        Assert.Equal(
            new Languages.LanguagePick(answer[0], Or(answer[1]), Or(answer[2])),
            Languages.Pick(pinned: null, Or(kept), windows, found));
    }

    /// <summary>A pinned code wins over everything, and neither honours nor misses a kept one - the test host's road.</summary>
    [Fact]
    public void A_pinned_language_wins() =>
        Assert.Equal(new Languages.LanguagePick("en", null, null), Languages.Pick("en", "pl", "pl", ["en", "pl"]));

    /// <summary>
    /// The languages found in the assembly and beside the program, by their codes - and a name that only
    /// looks like a language file is passed over rather than sliced into an exception.
    /// </summary>
    [Fact]
    public void The_languages_are_found_by_their_file_names()
    {
        var found = Languages.Found(
            ["Bws.Gui.Resources.gui.en.json", "Bws.Gui.Resources.gui.pl.json", "Bws.Gui.Resources.gui.json", "Something.Else.json"],
            [@"C:\x\languages\gui.de.json", @"C:\x\languages\gui.pt-BR.json", "gui.json", "gui..json", @"C:\x\languages\GUI.FR.JSON"]);

        Assert.Equal(new[] { "de", "en", "fr", "pl" }, found);
    }

    /// <summary>
    /// Two windows: a choice made in one survives the other writing later - decision E3. And a window that
    /// learns from the file that remembering was turned off writes no searches back under it.
    /// </summary>
    [Fact]
    public void A_choice_made_in_one_window_is_not_undone_by_another()
    {
        var file = WpfHost.Nowhere();
        var says = new Says();

        var first = new KeptColumns(file);
        var second = new KeptColumns(file);

        second.ChooseLanguage("pl", says);
        second.RememberRecentIs(remember: false, says);

        first.Remember("status:running", says);
        first.TheFiltersWere(folded: true, says);

        var read = new KeptColumns(file);

        Assert.Equal(new KeptChoices("pl", RememberRecent: false), read.Choices);
        Assert.Empty(read.RecentQueries);
        Assert.True(read.FiltersFolded);
    }

    /// <summary>Off forgets what was kept in the same write, and nothing new is added while it is off.</summary>
    [Fact]
    public void Remembering_off_forgets_and_keeps_nothing_new()
    {
        var file = WpfHost.Nowhere();
        var says = new Says();
        var kept = new KeptColumns(file);

        kept.Remember("status:running", says);
        Assert.Single(new KeptColumns(file).RecentQueries);

        kept.RememberRecentIs(remember: false, says);
        kept.Remember("name:sql*", says);

        Assert.Empty(new KeptColumns(file).RecentQueries);

        kept.RememberRecentIs(remember: true, says);
        kept.Remember("name:sql*", says);

        Assert.Equal(new[] { "name:sql*" }, new KeptColumns(file).RecentQueries);
    }

    /// <summary>Forget recent searches empties the list in the file.</summary>
    [Fact]
    public void Forgetting_all_empties_the_list()
    {
        var file = WpfHost.Nowhere();
        var says = new Says();
        var kept = new KeptColumns(file);

        kept.Remember("a", says);
        kept.Remember("b", says);
        kept.ForgetAll(says);

        Assert.Empty(new KeptColumns(file).RecentQueries);
    }

    /// <summary>
    /// "Same as Windows" first and marked when nothing is kept, the sentence about a Windows language with no
    /// translation, and a choice that opens the language already on screen asks for no restart.
    /// </summary>
    [Fact]
    public void Choosing_the_language_already_on_screen_asks_for_no_restart()
    {
        var chosen = new List<string?>();
        var page = new SettingsPage(English with { WindowsAsks = "pl" }, Quiet with { Choose = chosen.Add });

        Assert.Equal(2, page.Languages.Count);
        Assert.Null(page.Languages[0].Code);
        Assert.True(page.Languages[0].IsOn);
        Assert.True(page.SaysWindowsMissing);

        page.Languages[1].IsOn = true;

        Assert.Equal(new string?[] { "en" }, chosen);
        Assert.False(page.SaysPending);
    }

    /// <summary>A choice that changes the language on screen says when, and offers the restart.</summary>
    [Fact]
    public void Choosing_another_language_says_it_waits_for_the_next_start()
    {
        var chosen = new List<string?>();
        var page = new SettingsPage(English with { Found = ["en", "pl"], WindowsAsks = "pl", Showing = "pl" }, Quiet with { Choose = chosen.Add });

        page.Languages.Single(option => option.Code == "en").IsOn = true;

        Assert.Equal(new string?[] { "en" }, chosen);
        Assert.True(page.SaysPending);
        Assert.Contains("English", page.Pending, StringComparison.Ordinal);
        Assert.False(page.SaysWindowsMissing);
    }

    /// <summary>
    /// A kept language this session cannot open in marks no line and says so - and a choice made on the
    /// page answers that sentence.
    /// </summary>
    [Fact]
    public void A_kept_language_this_window_cannot_open_in_marks_nothing_and_is_said()
    {
        var page = new SettingsPage(English with { Kept = "de" }, Quiet);

        Assert.All(page.Languages, option => Assert.False(option.IsOn));
        Assert.True(page.SaysChoiceMissing);
        Assert.Contains("Deutsch", page.ChoiceMissing, StringComparison.Ordinal);

        page.Languages[0].IsOn = true;

        Assert.False(page.SaysChoiceMissing);
    }

    /// <summary>The restart waits for a plan, forgetting needs something to forget, and no profile means no path.</summary>
    [Fact]
    public void Buttons_say_why_they_cannot_be_pressed()
    {
        var busy = new SettingsPage(English, Quiet with { Busy = () => true });

        Assert.False(busy.CanRestart);
        Assert.Equal(Texts.Of("gui.settings.language.restart.busy"), busy.RestartHint);
        Assert.False(busy.CanForget);
        Assert.Equal(Texts.Of("gui.settings.search.none"), busy.ForgetHint);

        var nowhere = new SettingsPage(English with { File = null }, Quiet);

        Assert.True(nowhere.SaysNoPlace);
        Assert.False(nowhere.HasAPlace);
        Assert.False(nowhere.CanOpenFolder);
    }

    /// <summary>Unticking the box hands the switch out and greys the forget button.</summary>
    [Fact]
    public void Unticking_remembering_hands_it_out()
    {
        var handed = new List<bool>();
        var page = new SettingsPage(English, Quiet with { Recent = () => ["a"], Remember = handed.Add });

        Assert.True(page.CanForget);

        page.RememberRecent = false;

        Assert.Equal(new[] { false }, handed);
        Assert.False(page.CanForget);
    }

    /// <summary>
    /// The folder goes to the shell only when it is a folder, by the desktop from an administrator's window
    /// and directly from any other - and a refusal names the folder.
    /// </summary>
    [Fact]
    public void Only_a_folder_that_is_there_is_handed_to_the_shell()
    {
        var started = new List<string>();
        var desktop = new List<string>();

        string? Start(string path)
        {
            started.Add(path);
            return null;
        }

        string? Desktop(string path)
        {
            desktop.Add(path);
            return null;
        }

        Assert.Equal(Texts.Of("gui.settings.file.open.missing"), ExternalLinks.OpenTheSettingsFolder(string.Empty, _ => true, false, Start, Desktop));
        Assert.Equal(Texts.Of("gui.settings.file.open.missing"), ExternalLinks.OpenTheSettingsFolder(@"C:\x", _ => false, false, Start, Desktop));
        Assert.Empty(started);
        Assert.Empty(desktop);

        Assert.Null(ExternalLinks.OpenTheSettingsFolder(@"C:\x", _ => true, elevated: false, Start, Desktop));
        Assert.Null(ExternalLinks.OpenTheSettingsFolder(@"C:\y", _ => true, elevated: true, Start, Desktop));

        Assert.Equal(new[] { @"C:\x" }, started);
        Assert.Equal(new[] { @"C:\y" }, desktop);

        var refused = ExternalLinks.OpenTheSettingsFolder(@"C:\z", _ => true, false, _ => "No.", Desktop);

        Assert.NotNull(refused);
        Assert.Contains(@"C:\z", refused, StringComparison.Ordinal);
    }

    /// <summary>Ctrl+comma is the settings, and only with Control alone.</summary>
    [Fact]
    public void Ctrl_comma_opens_the_settings()
    {
        Assert.Equal(Shortcut.Settings, Shortcuts.For(Key.OemComma, ModifierKeys.Control));
        Assert.Equal(Shortcut.None, Shortcuts.For(Key.OemComma, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.Equal(Shortcut.None, Shortcuts.For(Key.OemComma, ModifierKeys.None));
    }

    /// <summary>
    /// On a built window: the button brings the page up in place of the list, Escape goes back, Ctrl+comma
    /// brings it again - and a language chosen on the page reaches the file.
    /// </summary>
    [Fact]
    public void The_page_comes_and_goes_and_a_choice_reaches_the_file()
    {
        var file = WpfHost.Nowhere();

        _ = WpfHost.Resources;
        var window = WpfHost.On(() => new MainWindow(file));

        WpfHost.On(() => window.Scope.Settings.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Preferences.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Entries.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Search.Visibility));

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Preferences.Visibility));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Entries.Visibility));

        // Ctrl+comma: the window's own Act hands it on, and the settings door takes it.
        Assert.False(WpfHost.On(() => window.Act(Shortcut.Settings, out _)));
        Assert.True(WpfHost.On(() => SettingsDoor.Heard(Shortcut.Settings, (MainViewModel)window.DataContext)));
        Assert.False(WpfHost.On(() => SettingsDoor.Heard(Shortcut.Help, (MainViewModel)window.DataContext)));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Preferences.Visibility));

        WpfHost.On(() => ((SettingsPage)window.Preferences.DataContext).Languages.Single(option => option.Code == "en").IsOn = true);

        Assert.Equal("en", file.PeekChoices()?.Language);

        WpfHost.On(window.Close);
    }


    /// <summary>A session with English only, Windows asking for English, nothing kept, and a file somewhere.</summary>
    private static readonly SettingsFacts English =
        new(["en"], "en", "en", null, RememberRecent: true, @"C:\nowhere\BetterWindowsServices\bws-preferences.json");

    /// <summary>Nothing kept, no plan running, and every change going nowhere.</summary>
    private static readonly SettingsHands Quiet = new(() => [], () => false, _ => { }, _ => { });

    private static bool Before(string text, string first, string second) =>
        text.IndexOf(first, StringComparison.Ordinal) is >= 0 and var at
        && at < text.IndexOf(second, StringComparison.Ordinal);
}
