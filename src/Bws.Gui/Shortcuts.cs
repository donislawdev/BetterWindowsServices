using System.Windows.Input;

namespace Bws.Gui;

/// <summary>
/// What a key press means, decided apart from the window that receives it.
///
/// <b>This exists so that the keyboard has a guard at all.</b> A key handler in code behind can
/// only be checked by a person pressing keys, and `docs/11` 9.1 is not a nicety - WCAG 2.1.1
/// asks that everything reachable by mouse be reachable by keyboard, and the tool admins will
/// compare this with, <c>services.msc</c>, has never needed a mouse. Splitting the meaning from
/// the doing leaves the untestable half one line long.
///
/// <b>Deliberately ignorant of state.</b> Whether there is a query to clear is not a question
/// about which key was pressed, and answering it here would put the same rule in two places -
/// the view model already owns it, and says whether it did anything.
/// </summary>
internal static class Shortcuts
{
    /// <summary>
    /// The intent behind a press, or <see cref="Shortcut.None"/> when the key belongs to whatever
    /// has focus.
    ///
    /// The three chosen are the three `docs/11` 9.1 puts first, and each one is what the rest of
    /// Windows already does with that key: F5 refreshes, Ctrl+F goes to the search, Escape backs
    /// out of what you typed. A tool that invents its own is a tool people have to learn.
    /// </summary>
    internal static Shortcut For(Key key, ModifierKeys modifiers)
    {
        // Exactly Control, rather than Control among others. Ctrl+Shift+F and Ctrl+Alt+F belong
        // to whatever else claims them, and swallowing every combination that happens to contain
        // Control is how a program breaks a shortcut it never heard of.
        if (key == Key.F && modifiers == ModifierKeys.Control)
        {
            return Shortcut.FocusQuery;
        }

        // Ctrl+C means copy everywhere in Windows, and over a list of things it means copy the
        // thing - owner's request, 2026-08-13. Which row, and whether the press belongs to this
        // window at all, is decided where the keyboard is known.
        if (key == Key.C && modifiers == ModifierKeys.Control)
        {
            return Shortcut.CopyRow;
        }

        // The rest carry no modifier at all, for the same reason.
        if (modifiers != ModifierKeys.None)
        {
            return Shortcut.None;
        }

        // DOWN AND UP MEAN THE LIST UNDER THE SEARCH BOX, since 2026-09-15 - and only there, which
        // is the window's half to decide, the same way Enter means the details only from the grid.
        // From anywhere else they are the arrows every control already has an opinion about. Tab
        // the same, since 2026-09-25 - and only without a modifier, so Shift+Tab always walks back.
        return key switch
        {
            Key.F5 => Shortcut.Refresh,
            Key.F1 => Shortcut.Help,
            Key.Escape => Shortcut.Back,
            Key.Enter => Shortcut.OpenDetails,
            Key.Down => Shortcut.NextSuggestion,
            Key.Up => Shortcut.PreviousSuggestion,
            Key.Tab => Shortcut.CompleteWord,
            _ => Shortcut.None
        };
    }

    /// <summary>
    /// The character a press should jump to in the list, or null when the press is not that.
    ///
    /// <b>Typed text rather than a key code, and that is rule 3 rather than a preference.</b>
    /// <c>Key.A</c> names a position on the keyboard, not a letter - on a keyboard that is not
    /// American the key sitting there produces something else, and a jump built on key codes would
    /// send somebody to the wrong entry while looking like it worked. What is wanted here is the
    /// character the person actually typed, which is what a text input carries.
    ///
    /// Letters and digits only. Space is excluded on purpose: it belongs to the grid, which uses
    /// it to select, and taking it would break something that already works to add something that
    /// nobody would use.
    /// </summary>
    internal static char? JumpLetter(string? typed)
    {
        if (string.IsNullOrEmpty(typed) || typed.Length != 1)
        {
            return null;
        }

        var character = typed[0];

        return char.IsLetterOrDigit(character) ? character : null;
    }
}

/// <summary>What the window should do about a press. One member per thing a person can ask for.</summary>
internal enum Shortcut
{
    /// <summary>Not ours. The press carries on to whatever has focus.</summary>
    None,

    /// <summary>Read the machine again, in full - including the configuration a tick does not watch.</summary>
    Refresh,

    /// <summary>Put the cursor in the query box, with what is there already selected.</summary>
    FocusQuery,

    /// <summary>
    /// Open the help menu - F1, since 2026-09-24 (UX-GUI-014). The key every Windows program
    /// answers with help, and until that day this one answered it with nothing.
    /// </summary>
    Help,

    /// <summary>
    /// Back out of the innermost thing that can be backed out of.
    ///
    /// <b>Named for what the person means rather than for what happens, and that changed on
    /// 2026-08-13.</b> It was <c>ClearQuery</c>, which was the whole of what Escape did while the
    /// window had one thing to leave. It now has two - the details panel closes first and the query
    /// is emptied only when there is no panel - and a member still called ClearQuery would be a
    /// name that lies about the branch below it. Which order, and why that order, is argued in
    /// <c>docs/04</c> at Paczka 1: one press doing both at once loses somebody their query while
    /// they were reaching for the panel.
    /// </summary>
    Back,

    /// <summary>
    /// Show everything about the chosen entry - `docs/11` 9.1, and the last of the three keys it
    /// asked for on 2026-08-05.
    ///
    /// It waited for a screen to open, deliberately: backlog 59 records that inventing a target for
    /// Enter before `S7` existed would mean `S7` rewriting it.
    /// </summary>
    OpenDetails,

    /// <summary>
    /// Put everything about the chosen entry on the clipboard - owner's request, 2026-08-13.
    ///
    /// <b>Everything rather than the cell under the cursor</b>, which is what Ctrl+C over a grid
    /// usually gives and is almost never what somebody wanted: the columns that are off by default
    /// are the long ones, so a copy limited to what is on screen leaves out the part worth pasting.
    /// The same thing is in the menu under the right button, because a keystroke nobody was told
    /// about is a feature nobody has.
    /// </summary>
    CopyRow,

    /// <summary>
    /// The next row of the list under the search box - or the list itself, when it is closed.
    /// Point 9 of `docs/11` 2.14, decision 4 of its design: Down on a closed list opens it.
    /// </summary>
    NextSuggestion,

    /// <summary>The previous row of the list under the search box. Nothing when it is closed.</summary>
    PreviousSuggestion,

    /// <summary>
    /// Write the chosen row of the list under the search box into the box.
    ///
    /// <b>Never produced by <see cref="Shortcuts.For"/></b>, because the key it belongs to is
    /// Enter, and Enter from the grid means the details - which of the two a press means depends
    /// on where the keyboard is and whether the list is open, and both are the window's to know.
    /// </summary>
    TakeSuggestion,

    /// <summary>
    /// Tab: write the chosen WORD of the list under the search box - the reflex from PowerShell and
    /// every editor, owner's decision of 2026-09-25 (`docs/PROJEKT-PODPOWIEDZI-UX-20260925.md`, T1).
    ///
    /// <b>Never carried out as itself.</b> Tab walks through the window everywhere else, so the
    /// window turns this into <see cref="TakeSuggestion"/> in the box while a list of words is open
    /// and into <see cref="None"/> in every other case - handed back, the press moves the keyboard on
    /// as it always did.
    /// </summary>
    CompleteWord
}
