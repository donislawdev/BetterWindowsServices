using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the settings page is wired to on a built window - `docs/PROJEKT-USTAWIENIA-20261117.md`: its buttons
/// and its switch reach the file, the restart waits for a plan being carried out and the page is told when
/// one starts and ends, the keyboard lands on the marked language, and the languages beside the program are
/// found in their folder. Apart from SettingsGuards, which stands near the crowd line of long test files.
/// </summary>
public sealed class SettingsDoorGuards
{
    /// <summary>
    /// Forget recent searches and the switch reach the file through the window's own kept layout, and a plan
    /// being carried out greys the restart, tells the page so, and keeps a press from starting anything.
    /// </summary>
    [Fact]
    public void The_page_reaches_the_file_and_the_restart_waits_for_a_run()
    {
        var file = WpfHost.Nowhere();

        new KeptColumns(file).Remember("status:running", new Says());

        _ = WpfHost.Resources;
        var window = WpfHost.On(() => new MainWindow(file));
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        var page = WpfHost.On(() => (SettingsPage)window.Preferences.DataContext);
        var told = new List<string?>();
        var started = new List<string>();

        WpfHost.On(() => window.Preferences.ForgetRecent.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        Assert.Empty(new KeptColumns(file).RecentQueries);

        WpfHost.On(() => page.RememberRecent = false);

        Assert.False(file.PeekChoices()?.RememberRecent);

        WpfHost.On(() => page.PropertyChanged += (_, changed) => told.Add(changed.PropertyName));
        WpfHost.On(model.Planned.Starting);

        Assert.Contains(nameof(SettingsPage.CanRestart), told);
        Assert.False(WpfHost.On(() => page.CanRestart));

        WpfHost.On(() => SettingsDoor.Restart(window, model, new KeptColumns(file), () => "carried", carried =>
        {
            started.Add(carried);
            return null;
        }));

        Assert.Empty(started);

        WpfHost.On(model.Planned.NoLongerRunning);

        Assert.True(WpfHost.On(() => page.CanRestart));
    }

    /// <summary>
    /// The keyboard lands on the marked language, and on the first line when none is marked - a kept language
    /// this window cannot open in marks nothing.
    /// </summary>
    [Theory]
    [InlineData("pl", "pl")]
    [InlineData("de", null)]
    public void The_keyboard_lands_on_the_marked_language(string kept, string? landing)
    {
        _ = WpfHost.Resources;

        var landed = WpfHost.On(() =>
        {
            var view = new SettingsView
            {
                DataContext = new SettingsPage(
                    new SettingsFacts(["en", "pl"], "en", "en", kept, RememberRecent: true, File: null),
                    new SettingsHands(() => [], () => false, _ => { }, _ => { }, () => (false, false)))
            };

            view.Measure(new Size(900, 900));
            view.Arrange(new Rect(0, 0, 900, 900));
            view.UpdateLayout();
            view.TakeTheKeyboard();

            return view.Landing()?.DataContext as LanguageOption;
        });

        // NOT VACUOUS: a list that was never drawn has no line to land on, and would fail here.
        Assert.NotNull(landed);
        Assert.Equal(landing, landed.Code);
    }

    /// <summary>
    /// The language files in a folder by their names, and nothing from a folder that is not there - the
    /// listing a session without administrator rights reads beside the program (S-9).
    /// </summary>
    [Fact]
    public void The_languages_beside_the_program_are_listed_from_their_folder()
    {
        var folder = Path.Combine(Path.GetDirectoryName(WpfHost.Nowhere(seenTheOverview: false).Where)!, "languages");

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "gui.de.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "gui.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), string.Empty);

        Assert.Equal(new[] { "de", "en" }, Languages.Found([], Languages.BesideFiles(folder)));
        Assert.Empty(Languages.BesideFiles(Path.Combine(folder, "missing")));
    }
}
