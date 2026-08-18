namespace Bws.Gui.ViewModels;

/// <summary>
/// What goes on the clipboard, for however many rows somebody has picked.
///
/// <b>Its own class since 2026-08-18, when the list started taking more than one row.</b> These four
/// answers used to live on <see cref="Chosen"/>, which is about the ONE entry the details panel is
/// showing - and a copy is now about the selection, which is a different set on a different schedule.
/// Leaving them there and adding four more for many entries would have been two answers to "what does
/// a copy say", which is the duplication this project pays for most often.
///
/// <b>Nothing here is about the panel and nothing on the panel is about copying any more</b>, so
/// Chosen went back to the purpose written at its head rather than growing a second one.
///
/// <b>Static, because there is no state to hold.</b> The selection is read at the moment somebody
/// asks - the window never keeps it, and the argument for that is at Copy in the code behind: a
/// binding into a list that reconciles itself once a second is another party in the middle of `A10`.
///
/// <b>One entry per line, and the order is the caller's.</b> For the window that is the order on
/// screen rather than the order somebody clicked, so a copy of five entries can be read against the
/// list it came from.
/// </summary>
internal static class Copying
{
    /// <summary>
    /// The internal names, which is what a script takes.
    ///
    /// Never the display name for this one: `ADR-14` says identity is the internal name, and a
    /// runbook line built from a translated display name works on one machine and not the next.
    /// </summary>
    internal static string? Name(IReadOnlyList<EntryRow> rows) => Joined(rows.Select(row => row.ServiceName));

    /// <summary>The names a person recognises, which is what a ticket takes.</summary>
    internal static string? DisplayName(IReadOnlyList<EntryRow> rows) =>
        Joined(rows.Select(row => row.DisplayName));

    /// <summary>
    /// What the entries say about themselves.
    ///
    /// Read through the column catalogue like every other value, so a copy cannot say something the
    /// column does not - the same rule the details panel rests on.
    /// </summary>
    internal static string? Description(IReadOnlyList<EntryRow> rows) =>
        Joined(rows.Select(row => row["description"]));

    /// <summary>
    /// Everything the window knows about each of them.
    ///
    /// The text itself is built in <see cref="Details"/>, beside the decision about what the fields
    /// ARE, so the panel and the clipboard cannot drift apart.
    /// </summary>
    internal static string? Everything(IReadOnlyList<EntryRow> rows) =>
        rows.Count == 0 ? null : Details.AsText(rows.Select(row => row.Entry));

    /// <summary>
    /// One value per line, with nothing to say when there is nothing to say.
    ///
    /// <b>Null rather than an empty string, and the difference reaches a key press.</b> Ctrl+C over a
    /// list with nothing picked has to be handed back rather than swallowed, so that it can reach
    /// whatever is behind this window - and the caller tells those apart by this being null.
    ///
    /// <b>An entry with nothing to say is left out rather than pasted as a blank line</b>, so a copy of
    /// five entries where two have no description is three lines rather than five with gaps in it.
    ///
    /// <b>WHAT THIS DOES NOT DO, AND THE FIRST VERSION OF THIS COMMENT CLAIMED IT DID:</b> a value the
    /// tool was REFUSED goes on the clipboard as the same sentence a cell shows, because that is what
    /// the column catalogue rendered and this reads the catalogue. Telling those apart would mean
    /// matching the rendered text against the language file from inside the copy path, which is a copy
    /// of the language file in a second place - the drift this project has refused elsewhere for the
    /// same reason.
    ///
    /// <b>It is acceptable because the dangerous case cannot arise.</b> The line that ends up in a
    /// script is a service NAME, and identity is never unreadable - the manager gave us the name to
    /// have the entry at all. A description that says "no access" lands in a ticket, where it is
    /// informative rather than a command that fails.
    /// </summary>
    private static string? Joined(IEnumerable<string?> values)
    {
        var lines = values
            .Where(value => !string.IsNullOrEmpty(value))
            .ToList();

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }
}
