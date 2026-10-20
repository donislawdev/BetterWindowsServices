using System.Globalization;
using System.Text;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The change history as a file somebody can open in a spreadsheet - owner's decision AS-c of 2026-10-20
/// (`docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md` section 11), the comparison's export (DiffExporting)
/// with a history in it.
///
/// <b>THE SENTENCES FIRST, THEN THE TABLE, and the sentences are not optional.</b> A history read without
/// them claims more than the log knows: how far back it reaches, whether it was cleared or is losing its
/// oldest events, and that an installation names a display name rather than an entry. Then which lines
/// the table shows - the tick, the search, how many - so a file of four lines is never read as a machine
/// on which four things changed. Then an empty line, then the table.
///
/// <b>The table as it stands on screen</b> - the lines "Show" and the search leave, grouped or each on
/// its own the way the table has them, in the order somebody sorted them into. Two columns differ from
/// the screen, both on purpose. <b>Times is a number on every line</b>, where the screen leaves one
/// blank because "1" in a column of counts is noise: a spreadsheet sums and filters numbers, and a blank
/// is not one. <b>The account's SID is a column of its own</b>: an account's name is translated, or not
/// known at all, and the SID is what the account is (rule 3 of the project notes).
///
/// <b>Every protection the list export has, by calling the same code</b> - Exporting.Line, for the
/// reason DiffExporting gives: a display name is written by whoever installed the service.
/// </summary>
internal static class HistoryExporting
{
    /// <summary>The name the save dialog offers.</summary>
    internal static string FileName() => Texts.Of("gui.history.export.name") + ".csv";

    /// <summary>The whole history as it stands on screen, as comma separated values.</summary>
    /// <param name="rows">The lines the table shows, in the order it shows them.</param>
    internal static string AsCsv(HistorySheet sheet, IReadOnlyList<HistoryRow> rows, string separator)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(rows);

        var text = new StringBuilder();

        foreach (var sentence in Sentences(sheet))
        {
            Exporting.Line(text, [sentence], separator);
        }

        text.Append("\r\n");

        Exporting.Line(
            text,
            [
                Texts.Of("gui.history.column.when"),
                Texts.Of("gui.history.column.times"),
                Texts.Of("gui.history.column.since"),
                Texts.Of("gui.history.column.entry"),
                Texts.Of("gui.history.column.change"),
                Texts.Of("gui.history.column.by"),
                Texts.Of("gui.history.export.sid")
            ],
            separator);

        foreach (var row in rows)
        {
            Exporting.Line(
                text,
                [row.When, row.Count.ToString(CultureInfo.InvariantCulture), row.Since, row.Entry, row.Change, row.By, row.Group.Latest.By?.Sid ?? string.Empty],
                separator);
        }

        return text.ToString();
    }

    /// <summary>What the window says once the file is written - how many lines, and where.</summary>
    internal static string Done(int lines, string file) =>
        lines == 1
            ? Texts.Of("gui.history.export.done.one", lines, file)
            : Texts.Of("gui.history.export.done.many", lines, file);

    /// <summary>
    /// Everything the view says above the table, in the order it says it, then what narrows the table:
    /// the tick, the search when there is one, and the line beside them when it has something to say.
    /// </summary>
    private static IEnumerable<string> Sentences(HistorySheet sheet)
    {
        yield return Texts.Of("gui.history.title");

        foreach (var admission in sheet.Admissions)
        {
            yield return admission;
        }

        yield return sheet.Filter.Button;

        if (RowSearch.Words(sheet.Filter.SearchText) is { Length: > 0 } words)
        {
            yield return Texts.Of("gui.history.export.searched", RowSearch.Typed(words));
        }

        if (sheet.Filter.Showing is { Length: > 0 } showing)
        {
            yield return showing;
        }
    }
}
