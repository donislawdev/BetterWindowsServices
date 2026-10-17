using System.Text;

namespace Bws.Gui.Tests;

/// <summary>
/// Comma separated values read back the way RFC 4180 says they are written - with the separator the
/// file was written with, which on this project's own machine is a semicolon.
///
/// <b>A reader in the tests rather than a package</b>, because the whole of the format is quotes,
/// doubled quotes, a separator and a line end, and a dependency would be a new name under `ADR-15` for
/// forty lines. <b>Strict where a spreadsheet is forgiving</b>: a quote inside a field that is not
/// quoted, a line break that is not inside quotes and a quote left open are refused rather than read
/// past, so a value the writer forgot to quote comes back as a refusal and not as two values.
/// </summary>
internal static class CsvText
{
    private const string LineEnd = "\r\n";

    /// <summary>Every record of <paramref name="text"/>, each a list of its fields.</summary>
    /// <exception cref="FormatException">The text is not what RFC 4180 writes.</exception>
    internal static List<List<string>> Read(string text, string separator)
    {
        var records = new List<List<string>>();
        var fields = new List<string>();
        var at = 0;

        while (at < text.Length)
        {
            fields.Add(text[at] == '"' ? Quoted(text, ref at) : Bare(text, separator, ref at));

            if (Next(text, at, separator))
            {
                at += separator.Length;
            }
            else if (Next(text, at, LineEnd))
            {
                at += LineEnd.Length;
                records.Add(fields);
                fields = [];
            }
            else
            {
                throw new FormatException($"A field ends at {at} with neither the separator nor a line end after it.");
            }
        }

        return fields.Count == 0 ? records : throw new FormatException("The text does not end with a line end.");
    }

    private static string Quoted(string text, ref int at)
    {
        var value = new StringBuilder();

        for (at++; ; at++)
        {
            if (at >= text.Length)
            {
                throw new FormatException("A quote is left open at the end of the text.");
            }

            if (text[at] == '"' && !Next(text, at + 1, "\""))
            {
                at++;

                return value.ToString();
            }

            // A doubled quote is one quote in the value, and the loop steps over its second half.
            at += text[at] == '"' ? 1 : 0;
            value.Append(text[at]);
        }
    }

    private static string Bare(string text, string separator, ref int at)
    {
        var start = at;

        while (at < text.Length && !Next(text, at, separator) && !Next(text, at, LineEnd))
        {
            if (text[at] is '"' or '\r' or '\n')
            {
                throw new FormatException($"A field that is not quoted holds a quote or a line break at {at}.");
            }

            at++;
        }

        return text[start..at];
    }

    private static bool Next(string text, int at, string expected) =>
        at <= text.Length && text.AsSpan(at).StartsWith(expected, StringComparison.Ordinal);
}
