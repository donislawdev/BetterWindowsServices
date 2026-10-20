using System.Text;

namespace Bws.Gui.ViewModels;

/// <summary>
/// A comparison as a file somebody can open in a spreadsheet - `D2` "eksportowalne", owner's decision
/// W2-c of 2026-10-15.
///
/// <b>THE SENTENCES FIRST, THEN THE TABLE, and the sentences are not optional.</b> A comparison read
/// without its caveats claims more than it knows - "the two snapshots were taken at different privilege
/// levels" turns half of the removals into "cannot tell", and seventeen rows of "not compared" are not
/// seventeen fields that stayed the same. A file handed to somebody who never saw the window carries
/// what the window said above the table, one sentence a line, the way rule 8 asks of everything this
/// tool produces. Then an empty line, then the table.
///
/// <b>The table as it stands on screen</b> - the rows the filter shows, in the order somebody sorted
/// them into - the list export's own rule (Exporting), and for its reason: a file nobody can check
/// against what they were looking at is a file nobody can trust. When the filter hides rows, the file
/// says which choice and how many, so a file of four rows is never read as a comparison of four.
///
/// <b>Every protection the list export has, by calling the same code rather than a copy of it</b> -
/// the separator Windows names, quoting only where the format needs it, and the apostrophe before a
/// value a spreadsheet would run. The values here come from snapshot files, which may be somebody
/// else's, so that last one is the reason the whole of Exporting.Line is shared rather than imitated.
/// </summary>
internal static class DiffExporting
{
    /// <summary>The name the save dialog offers.</summary>
    internal static string FileName() => Texts.Of("gui.diff.export.name") + ".csv";

    /// <summary>The whole comparison as it stands on screen, as comma separated values.</summary>
    /// <param name="rows">The rows the table shows, in the order it shows them.</param>
    internal static string AsCsv(DiffSheet sheet, IReadOnlyList<DiffLine> rows, string separator)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(rows);

        var text = new StringBuilder();

        foreach (var sentence in Sentences(sheet, rows.Count))
        {
            Exporting.Line(text, [sentence], separator);
        }

        text.Append("\r\n");

        Exporting.Line(
            text,
            [Texts.Of("gui.diff.column.entry"), Texts.Of("gui.column.displayName"), Texts.Of("gui.diff.column.what"), sheet.BeforeHeading, sheet.AfterHeading],
            separator);

        foreach (var row in rows)
        {
            Exporting.Line(text, [row.ServiceName, row.DisplayName, row.What, row.Before, row.After], separator);
        }

        return text.ToString();
    }

    /// <summary>What the window says once the file is written - how many rows, and where.</summary>
    internal static string Done(int rows, string file) =>
        rows == 1
            ? Texts.Of("gui.diff.export.done.one", rows, file)
            : Texts.Of("gui.diff.export.done.many", rows, file);

    /// <summary>
    /// Everything the view says above the table, in the order it says it - and, when rows are hidden,
    /// which choice hides them. The sentence for "nothing differs" too, because a file with a heading
    /// row and nothing under it would otherwise say it.
    /// </summary>
    private static IEnumerable<string> Sentences(DiffSheet sheet, int shown)
    {
        yield return sheet.Title;

        foreach (var side in sheet.Sides)
        {
            yield return side.About;

            if (side.Note.Length > 0)
            {
                yield return side.Note;
            }
        }

        yield return sheet.Counts;

        foreach (var caveat in sheet.Caveats)
        {
            yield return caveat;
        }

        // The search too, since 2026-10-20 (AS-a) - "3 of 25 rows" under "All rows" would otherwise read
        // as a menu that lost count.
        if (RowSearch.Words(sheet.Filter.SearchText) is { Length: > 0 } words)
        {
            yield return Texts.Of("gui.diff.export.searched", RowSearch.Typed(words));
        }

        if (shown < sheet.Lines.Count)
        {
            yield return sheet.Lines.Count == 1
                ? Texts.Of("gui.diff.export.shown.one", sheet.Filter.Button, shown, sheet.Lines.Count)
                : Texts.Of("gui.diff.export.shown.many", sheet.Filter.Button, shown, sheet.Lines.Count);
        }

        if (sheet.Same.Length > 0)
        {
            yield return sheet.Same;
        }
    }
}
