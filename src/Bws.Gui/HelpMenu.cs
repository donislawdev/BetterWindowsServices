namespace Bws.Gui;

/// <summary>
/// What the Help button offers: the query language page on the project's website, and nothing
/// else since 2026-09-24 - owner's decision.
///
/// <b>IT HELD NINE ITEMS FOR ONE DAY.</b> UX-GUI-014 built it earlier the same day with the six
/// keyboard shortcuts, the project's website and the version of this program, and the owner cut it
/// to this one. The price is said rather than left for somebody to find: F5, Ctrl+F,
/// Ctrl+A and Esc are named in no text of the window again (the half of UX-GUI-010 this menu had
/// answered), and the window no longer says its own version - `bws --version` does. Ctrl+C and
/// Enter are still written beside their items in the row menu.
///
/// <b>STILL A MENU WITH ONE ITEM RATHER THAN A BUTTON THAT OPENS THE PAGE</b> - owner's choice
/// between the two. A press on "Help" that leaves the program for a browser with nothing in between
/// would be a surprise, and a menu can take a second item back without the button changing what
/// it does.
///
/// <b>A LIST OF DATA BUILT BY <see cref="RowMenu.Build"/>, NOT A SECOND MENU WRITTEN OUT</b> - GUI
/// rules 2 and 8, and the item is the row menu's entry for that reason.
///
/// <b>The page goes out through <see cref="ExternalLinks"/> and nothing else</b>, exactly as Donate
/// does - an elevated window hands the address to the desktop, and this program still connects to
/// nothing (`ADR-19`).
/// </summary>
internal static class HelpMenu
{
    internal static IReadOnlyList<IReadOnlyList<RowMenuEntry>> GroupsFor(MainWindow window) =>
    [
        [
            new RowMenuEntry("gui.help.queryLanguage", null, () => window.OpenPage(ExternalLinks.OpenQueryLanguagePageAsync))
        ]
    ];
}
