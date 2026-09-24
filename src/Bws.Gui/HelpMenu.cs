namespace Bws.Gui;

/// <summary>
/// What the Help button offers: the keyboard shortcuts, the two pages on the project's website, and
/// the version of this program. UX-GUI-014 with the shortcut half of UX-GUI-010, owner's decision
/// 2026-09-24 - until that day the window named Ctrl+F, Ctrl+C and Ctrl+A nowhere and said its own
/// version nowhere.
///
/// <b>A LIST OF DATA BUILT BY <see cref="RowMenu.Build"/>, NOT A SECOND MENU WRITTEN OUT</b> - GUI
/// rules 2 and 8. An item here has exactly the shape of an item on a row: words, a key written
/// beside them, and what a press does. So it is the row menu's entry and the row menu's item style,
/// and the name "row" on them is the one place this reuse shows.
///
/// <b>EVERY SHORTCUT ITEM DOES WHAT IT NAMES, through the same <see cref="MainWindow.Act"/> the key
/// goes through.</b> A menu that described the keys while a second road carried them out would be
/// two answers to one question, which this window was caught by on 2026-09-02. The one exception is
/// Ctrl+A, which is the grid's own key rather than one of ours, and the item asks the grid for the
/// same thing.
///
/// <b>The two pages go out through <see cref="ExternalLinks"/> and nothing else</b>, exactly as
/// Donate does - an elevated window hands the address to the desktop, and this program still
/// connects to nothing (`ADR-19`).
/// </summary>
internal static class HelpMenu
{
    internal static IReadOnlyList<IReadOnlyList<RowMenuEntry>> GroupsFor(MainWindow window) =>
    [
        [
            new RowMenuEntry("gui.help.find", "gui.help.find.gesture", () => Pressed(window, Shortcut.FocusQuery)),
            new RowMenuEntry("gui.help.refresh", "gui.help.refresh.gesture", () => Pressed(window, Shortcut.Refresh)),
            new RowMenuEntry("gui.help.details", "gui.menu.details.gesture", () => Pressed(window, Shortcut.OpenDetails)),
            new RowMenuEntry("gui.help.copy", "gui.menu.copyAll.gesture", () => Pressed(window, Shortcut.CopyRow)),
            new RowMenuEntry("gui.help.selectAll", "gui.help.selectAll.gesture", () => Task.FromResult(window.SelectEverything())),
            new RowMenuEntry("gui.help.back", "gui.help.back.gesture", () => Pressed(window, Shortcut.Back))
        ],
        [
            new RowMenuEntry("gui.help.queryLanguage", null, () => window.OpenPage(ExternalLinks.OpenQueryLanguagePageAsync)),
            new RowMenuEntry("gui.help.projectPage", null, () => window.OpenPage(ExternalLinks.OpenProjectPageAsync))
        ],
        [
            new RowMenuEntry("gui.help.version", null, () => Copied(window), () => VersionNumber)
        ]
    ];

    /// <summary>
    /// What the version item says and copies: the stamped number, or words saying there is none -
    /// rule 8 of CLAUDE.md applied to one number rather than a guess that looks like one.
    /// </summary>
    internal static string VersionNumber => Release.Number ?? Texts.Of("gui.help.versionUnknown");

    /// <summary>
    /// A press of the key the item names. The work the key starts travels with it, so an item
    /// that reads the machine again is awaited as the key's own press is.
    /// </summary>
    private static Task Pressed(MainWindow window, Shortcut shortcut)
    {
        window.Act(shortcut, out var work);

        return work;
    }

    /// <summary>
    /// The version on the clipboard, for somebody reporting a fault. A refusal from the clipboard is
    /// said in the status line by <see cref="MainWindow.Put"/>, as it is for a copied row.
    /// </summary>
    private static Task Copied(MainWindow window)
    {
        window.Put(VersionNumber);

        return Task.CompletedTask;
    }
}
