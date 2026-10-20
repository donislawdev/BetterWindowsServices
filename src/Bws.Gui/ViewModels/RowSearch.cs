namespace Bws.Gui.ViewModels;

/// <summary>
/// What the search over a table of the audit finds - the comparison's and the change history's, owner's
/// decisions AS-a and AS-b of 2026-10-20 (`docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md` section 11).
///
/// <b>PLAIN TEXT, NOT THE QUERY LANGUAGE.</b> The list's field speaks the language of `docs/07` because
/// its fields are a service's. A row of a comparison or of the history has its own few columns, and the
/// owner chose a field anybody can type into over a second language with its own fields to learn.
///
/// <b>EVERY WORD SOMEWHERE IN THE ROW, ANY CASE</b> - typed "bits system" finds BITS switched by SYSTEM.
/// Ordinal and case blind, the comparison a bare word on the list makes (QueryValues in the core): the
/// same row is found on every machine whatever its culture, which a culture's rules about case would
/// not promise (rule 26 of the project notes).
///
/// <b>A date is text too.</b> The history writes every time in one shape (HistoryWords.When), so
/// "2026-10-16" keeps that day and "2026-10" that month, with no field of dates beside the search.
/// </summary>
internal static class RowSearch
{
    /// <summary>The words somebody typed - none for an empty field, or for one holding only spaces.</summary>
    internal static string[] Words(string? typed) =>
        string.IsNullOrWhiteSpace(typed)
            ? []
            : typed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Whether every word stands somewhere in what a row says. No words find every row.</summary>
    /// <param name="said">What the row says, its values joined - <see cref="Said"/>.</param>
    internal static bool Finds(string said, IReadOnlyList<string> words)
    {
        foreach (var word in words)
        {
            if (!said.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The values of a row as one text to search, a line apart, so a word never matches across the edge
    /// of two values - "on" never finds the end of one and the start of the next.
    /// </summary>
    internal static string Said(params string?[] values) =>
        string.Join('\n', values.Where(value => !string.IsNullOrEmpty(value)));

    /// <summary>The words as the sentence about them says them - one space apart, whatever was pasted.</summary>
    internal static string Typed(IReadOnlyList<string> words) => string.Join(' ', words);
}
