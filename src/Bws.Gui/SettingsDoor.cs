using System.Windows.Threading;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The way to the settings page, what the page is handed, and what its buttons do -
/// `docs/PROJEKT-USTAWIENIA-20261117.md`.
///
/// <b>A static class beside the window rather than methods of it</b>, the shape RecentSearches.Watch has,
/// because MainWindow stands on the ceiling of methods (tools/margin.ps1) and the page needs the
/// preferences file and the kept layout, which only Arrange holds. For the same reason Ctrl+comma is heard
/// here: the shortcut is named in the one table (Shortcuts), and MainWindow.Act, which stands near its
/// ceiling of forks, answers it with "not mine" so that this handler, after it, can.
/// </summary>
internal static class SettingsDoor
{
    /// <param name="window">The window, its Settings button, its page, and its list.</param>
    /// <param name="model">The window's model - its tabs, its plan and its line for sentences.</param>
    /// <param name="kept">The kept layout, which writes every change.</param>
    /// <param name="file">The preferences file, for where it is.</param>
    /// <param name="handOver">What the window is showing, encoded for the window that replaces it.</param>
    internal static void Watch(MainWindow window, MainViewModel model, KeptColumns kept, PreferencesFile file, Func<string> handOver)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(kept);
        ArgumentNullException.ThrowIfNull(file);

        var page = new SettingsPage(
            new SettingsFacts(Languages.Found(), Languages.WindowsAsks, Texts.Showing, kept.Choices.Language, kept.Choices.RememberRecent, file.HasAPlace ? file.Where : null),
            new SettingsHands(
                () => kept.RecentQueries,
                () => model.Planned.Busy,
                code => kept.ChooseLanguage(code, model.Says),
                remember => kept.RememberRecentIs(remember, model.Says)));

        window.Preferences.DataContext = page;

        // A LANGUAGE KEPT AND NOT USABLE IS SAID AT ONCE, not only on the page - the window opened in another
        // language than the one somebody chose, and the line under it is where the window says such things.
        if (Texts.ChosenButMissing is { } missing)
        {
            model.Says.AboutTheLayout(Texts.Of("gui.settings.language.missing", SettingsPage.NativeName(missing)));
        }

        OpenedBy(window, model, page);
        Buttons(window, model, kept, page, handOver);
    }

    /// <summary>The button, Ctrl+comma, and what the page does when it comes up.</summary>
    private static void OpenedBy(MainWindow window, MainViewModel model, SettingsPage page)
    {
        window.Scope.Settings.Click += (_, _) => model.Tabs.ShowingSettings = true;

        // AFTER the window's own handler, which answers this key with "not mine".
        window.PreviewKeyDown += (_, e) => e.Handled = e.Handled || Heard(Shortcuts.For(e.Key, e.KeyboardDevice.Modifiers), model);

        model.Tabs.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(Tabs.ShowingSettings) && model.Tabs.ShowingSettings)
            {
                page.Rethink();

                // THE KEYBOARD GOES TO THE PAGE, after layout - otherwise it stays on the button in the bar
                // and Tab walks the bar before it reaches anything here (`docs/11` 9.1).
                _ = window.Preferences.Dispatcher.BeginInvoke(DispatcherPriority.Input, window.Preferences.TakeTheKeyboard);
            }
        };

        // Planned.Busy goes up and down while the page may be on screen, and the restart follows it.
        model.Planned.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(Planned.Busy))
            {
                page.Rethink();
            }
        };
    }

    /// <summary>
    /// Ctrl+comma: opens the page and says it took the press - and not while a plan sheet is up, where the
    /// page would change under the question the sheet is asking. Apart from the handler so that a test can
    /// ask it: a test cannot hold Control down on the real keyboard (KeyPresses).
    /// </summary>
    internal static bool Heard(Shortcut wanted, MainViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (wanted != Shortcut.Settings || model.Planned.Showing)
        {
            return false;
        }

        model.Tabs.ShowingSettings = true;

        return true;
    }

    /// <summary>Forget, Open folder and Restart now.</summary>
    private static void Buttons(MainWindow window, MainViewModel model, KeptColumns kept, SettingsPage page, Func<string> handOver)
    {
        window.Preferences.ForgetRecent.Click += (_, _) =>
        {
            kept.ForgetAll(model.Says);
            page.Rethink();
        };

        window.Preferences.OpenFolder.Click += async (_, _) =>
            await window.OpenPage(ExternalLinks.Destination.SettingsFolder).ConfigureAwait(true);

        window.Preferences.Restart.Click += (_, _) => Restart(window, model, kept, handOver);
    }

    /// <summary>
    /// Starts the window again in the chosen language and closes this one - the order of
    /// MainWindow.RestartAsAdministrator, and for its reason: the layout goes on disk before the new window
    /// exists, nothing is written after it (G-7), and a refusal leaves this window as it was.
    /// </summary>
    private static void Restart(MainWindow window, MainViewModel model, KeptColumns kept, Func<string> handOver)
    {
        if (model.Planned.Busy)
        {
            return;
        }

        var carried = handOver();

        kept.KeepNow(window.Entries, model.Says);

        if (Elevation.Reopen(carried) is { } trouble)
        {
            model.Says.CouldNotDo(trouble);
            return;
        }

        kept.Leave();

        window.Close();
    }
}
