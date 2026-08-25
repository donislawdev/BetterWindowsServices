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
    /// </summary>
    internal static string AsCsv(IReadOnlyList<string> columns, IReadOnlyList<EntryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        var text = new StringBuilder();

        Line(text, columns.Select(id => Columns.Of(id) is { } known ? Texts.Of(known.LabelKey) : id));

        foreach (var row in rows)
        {
            Line(text, columns.Select(id => row[id]));
        }

        return text.ToString();
    }

    private static void Line(StringBuilder text, IEnumerable<string> values)
    {
        text.AppendJoin(',', values.Select(Quoted));
        text.Append("\r\n");
    }

    /// <summary>
    /// One value, quoted when the format says it has to be.
    ///
    /// <b>Only when it has to be, rather than always.</b> Both are legal and the difference reaches
    /// a person: a file where every field is quoted is unreadable in a text editor, and this one is
    /// opened by hand at least as often as by a program.
    /// </summary>
    private static string Quoted(string value)
    {
        if (!value.Contains(',', StringComparison.Ordinal)
            && !value.Contains('"', StringComparison.Ordinal)
            && !value.Contains('\n', StringComparison.Ordinal)
            && !value.Contains('\r', StringComparison.Ordinal))
        {
            return value;
        }

        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}
