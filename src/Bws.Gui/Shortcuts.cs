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

        // The rest carry no modifier at all, for the same reason.
        if (modifiers != ModifierKeys.None)
        {
            return Shortcut.None;
        }

        return key switch
        {
            Key.F5 => Shortcut.Refresh,
            Key.Escape => Shortcut.ClearQuery,
            _ => Shortcut.None
        };
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

    /// <summary>Empty the query box.</summary>
    ClearQuery
}
