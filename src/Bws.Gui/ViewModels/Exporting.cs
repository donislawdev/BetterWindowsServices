using System.Globalization;
using System.Text;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the list shows, as a file somebody can open in a spreadsheet.
///
/// <b>WHAT THE LIST SHOWS, and every word of that is load bearing.</b> The rows are the ones on
/// screen - the scope somebody chose, narrowed by whatever they typed, in the order they sorted it
/// into. The columns are the ones they turned on, in the order they dragged them into. An export
/// that quietly held more than the window did would be a file nobody could check against what they
/// were looking at.
///
/// <b>The headings are the ones on screen too, which is the opposite of what the layout file
/// does.</b> A layout writes identifiers because it is read by this program on another machine,
/// where a translated word would name no column at all. This is read by a person and by whatever
/// they paste it into, so it says what they saw. <b>Somebody who needs stable field names has the
/// command line</b>, where <c>--json</c> writes the glossary's own spelling - and that difference
/// is worth stating rather than leaving somebody to find out.
///
/// <b>Nothing here knows what a DataGrid is</b>, so all of it can be checked without a window -
/// which columns are on and which rows are on screen are questions the window answers, and it hands
/// the answers in.
/// </summary>
internal static class Exporting
{
    /// <summary>
    /// The whole of what is on screen, as comma separated values.
    ///
    /// <b>RFC 4180 rather than something that looks like it.</b> A value is wrapped in quotes only
    /// when it needs to be - a comma, a quote, or a line break inside it - and a quote inside a
    /// value is doubled. Service descriptions on a real machine run to twelve hundred characters
    /// and contain all three, so this is the ordinary case rather than the careful one.
    ///
    /// <b>Lines end the way the standard says and the way Windows reads</b>, which is the same
    /// answer for once.
    ///
    /// <b>The separator is handed in, and the window hands in <see cref="Separator"/></b> - a
    /// comma until 2026-10-05, which a spreadsheet on a machine whose list separator is a semicolon
    /// opened as one column of whole lines.
    /// </summary>
    internal static string AsCsv(IReadOnlyList<string> columns, IReadOnlyList<EntryRow> rows, string separator)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        var text = new StringBuilder();

        Line(text, columns.Select(id => Columns.Of(id) is { } known ? Texts.Of(known.LabelKey) : id), separator);

        foreach (var row in rows)
        {
            Line(text, columns.Select(id => row[id]), separator);
        }

        return text.ToString();
    }

    /// <summary>
    /// The list separator this person's Windows uses - a semicolon on this project's own machine
    /// (pl-PL, measured 2026-09-30) and a comma under English settings. G-8 of the external stability
    /// report, owner's decision in round 2: the file is opened by a double click far more often than
    /// through an import dialog, and a double click splits by the separator Windows names.
    ///
    /// <b>A comma when the setting is something a file of this shape cannot carry</b> - nothing at
    /// all, or a quote or a line break, which are the three things quoting itself is made of.
    /// </summary>
    internal static string Separator(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var listed = culture.TextInfo.ListSeparator;

        return listed.Length == 0 || listed.Any(character => character is '"' or '\r' or '\n') ? "," : listed;
    }

    /// <summary>
    /// What the window says once the file is written, including the per-user copies that went in
    /// folded under their template's row - G-8 of the external stability report, owner's decision
    /// 2026-10-05. The file keeps one row per row on screen, and the sentence says what that row
    /// stood for, so the count here and the count under the list do not seem to disagree.
    /// </summary>
    internal static string Done(IReadOnlyList<EntryRow> rows, string file)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var wrote = Wrote(rows.Count, file);
        var folded = rows.Sum(row => row.Instances.Count);

        return folded switch
        {
            0 => wrote,
            1 => wrote + " " + Texts.Of("gui.export.folded.one", folded),
            _ => wrote + " " + Texts.Of("gui.export.folded.many", folded)
        };
    }

    /// <summary>
    /// The name the save dialog offers, after the list somebody is looking at - UX-GUI-016, owner's
    /// decision, 2026-09-24.
    ///
    /// <b>It was "services.csv" on every tab</b>, so a list of drivers saved without a second look
    /// arrived on disk named for what it did not hold. A file is named once and read many times, by
    /// people who never saw the window it came from.
    ///
    /// <b>Three calls with the key written in each rather than one call with the key chosen</b>,
    /// because TextKeyGuards sees a key only where it is written - the note at Overview.Of.
    /// </summary>
    internal static string FileName(EntryScope scope) => scope switch
    {
        EntryScope.Drivers => Texts.Of("gui.export.name.drivers"),
        EntryScope.Everything => Texts.Of("gui.export.name.all"),
        _ => Texts.Of("gui.export.name.services")
    } + ".csv";

    /// <summary>
    /// What the window says once the file is written - UX-GUI-016, and it reverses a decision that
    /// had the file speak for itself (MainWindow.Exporting.cs says why it no longer does).
    ///
    /// <b>Rows, counted as the list shows them.</b> A folded per-user family is one row in the file
    /// because it is one row on screen, so the number here is the number of rows the list showed.
    /// The name and not the path: the person chose the folder a second ago, and a path in a line
    /// this narrow would push the number out of sight.
    /// </summary>
    internal static string Wrote(int rows, string file) =>
        rows == 1
            ? Texts.Of("gui.export.done.one", rows, file)
            : Texts.Of("gui.export.done.many", rows, file);

    private static void Line(StringBuilder text, IEnumerable<string> values, string separator)
    {
        text.AppendJoin(separator, values.Select(value => Quoted(Inert(value), separator)));
        text.Append("\r\n");
    }

    /// <summary>
    /// One value, made unable to be a formula in the spreadsheet that opens this.
    ///
    /// <b>THE VALUES IN THIS FILE COME FROM THE THING BEING AUDITED, and that is the whole
    /// argument.</b> A display name, a launch path, an account and a description are written by
    /// whoever installed the service - not by us and not by the person exporting. A spreadsheet
    /// reads a cell beginning with one of these characters as a formula, so a service named
    /// <c>=cmd|'/c calc'!A1</c> arrives in an administrator's sheet as something to run rather than
    /// something to read. Quoting to RFC 4180 does not stop it: the quotes are removed on the way
    /// in, and what is left is the formula.
    ///
    /// <b>The apostrophe is what a spreadsheet reads as "this cell is text".</b> Owner's decision,
    /// 2026-08-26, with the cost said out loud rather than discovered: THIS CHANGES THE VALUE. A
    /// cell that held <c>-1</c> now holds <c>'-1</c>, and this file stops being a character for
    /// character copy of what was on screen. That was the trade taken, against the alternative of
    /// an audit tool that hands its findings over as executable content.
    ///
    /// <b>The five characters are the ones a spreadsheet acts on</b>, and the last two are there
    /// because a leading tab or carriage return is stripped before the first character is looked
    /// at - so they are a way of writing the first three with a step in between.
    ///
    /// <b>Only in the file, and only here.</b> The window shows what the machine said, and
    /// <c>--json</c> from the command line writes it too - neither is read by a spreadsheet, so
    /// neither needs this and neither gets it.
    /// </summary>
    private static string Inert(string value) =>
        value.Length > 0 && Dangerous.Contains(value[0])
            ? "'" + value
            : value;

    /// <summary>What a spreadsheet treats as the start of something to evaluate.</summary>
    private static readonly char[] Dangerous = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>
    /// One value, quoted when the format says it has to be.
    ///
    /// <b>Only when it has to be, rather than always.</b> Both are legal and the difference reaches
    /// a person: a file where every field is quoted is unreadable in a text editor, and this one is
    /// opened by hand at least as often as by a program.
    ///
    /// <b>The separator in use, not a comma</b>, since the separator stopped being one everywhere -
    /// a semicolon inside a description is the value cut in two on a Polish machine.
    /// </summary>
    private static string Quoted(string value, string separator)
    {
        if (!value.Contains(separator, StringComparison.Ordinal)
            && !value.Contains('"', StringComparison.Ordinal)
            && !value.Contains('\n', StringComparison.Ordinal)
            && !value.Contains('\r', StringComparison.Ordinal))
        {
            return value;
        }

        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}
