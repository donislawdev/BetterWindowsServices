using System.Text;

namespace Bws.Core.Querying;

/// <summary>
/// A piece of query text together with a note, per character, of whether it still means
/// anything special.
///
/// This pairing is the whole trick of the scanner. A comma that arrived inside quotes and
/// a comma that arrived bare look identical once the quotes are gone, and one separates
/// values while the other is part of a display name. Carrying the mask alongside the text
/// is what lets every later step ask "was this one special" instead of guessing.
/// </summary>
internal sealed record ScannedText(string Text, bool[] Literal)
{
    internal int Length => Text.Length;

    /// <summary>True when the character at this position still carries its special meaning.</summary>
    internal bool IsSpecial(int index, char character) =>
        Text[index] == character && !Literal[index];

    internal int IndexOfSpecial(char character, int from = 0)
    {
        for (var index = from; index < Text.Length; index++)
        {
            if (IsSpecial(index, character))
            {
                return index;
            }
        }

        return -1;
    }

    internal bool HasSpecial(char character) => IndexOfSpecial(character) >= 0;

    internal bool StartsWithSpecial(char character) =>
        Text.Length > 0 && IsSpecial(0, character);

    internal ScannedText Slice(int start, int length) =>
        new(Text.Substring(start, length), Literal[start..(start + length)]);

    internal ScannedText Slice(int start) => Slice(start, Text.Length - start);

    /// <summary>True when nothing in this text was quoted or escaped.</summary>
    internal bool IsBare() => Array.TrueForAll(Literal, literal => !literal);

    /// <summary>Splits on one separator, ignoring the ones that were quoted or escaped.</summary>
    internal List<ScannedText> SplitOnSpecial(char separator)
    {
        var parts = new List<ScannedText>();
        var start = 0;

        for (var index = 0; index <= Text.Length; index++)
        {
            if (index != Text.Length && !IsSpecial(index, separator))
            {
                continue;
            }

            parts.Add(Slice(start, index - start));
            start = index + 1;
        }

        return parts;
    }
}

/// <summary>
/// Turns query text into members, keeping track of what was quoted and what was escaped.
///
/// The rules come from the query language document, and both of the odd ones are there
/// because of what the data in this domain looks like. A backslash takes the meaning away
/// from the next character only when that character has one, so a path can be typed the
/// way people type paths rather than with every separator doubled. Quotes protect
/// everything, because a service name with a space in it is an ordinary sight.
/// </summary>
internal static class QueryScanner
{
    /// <summary>
    /// The characters a backslash can disarm. A backslash in front of anything else stays
    /// an ordinary backslash, which is what keeps <c>C:\Windows\x.exe</c> readable.
    /// </summary>
    private const string Special = "\"\\,:!*?/= \t";

    /// <summary>
    /// Splits into members on unquoted whitespace. Returns false and points at the opening
    /// quote when one never closes.
    /// </summary>
    internal static bool TryScan(string query, out List<ScannedText> members, out int unclosedQuoteAt)
    {
        members = [];
        unclosedQuoteAt = -1;

        var text = new StringBuilder();
        var literal = new List<bool>();
        var quoting = false;
        var quoteStartedAt = -1;

        for (var index = 0; index < query.Length; index++)
        {
            var character = query[index];

            if (character == '"')
            {
                quoting = !quoting;
                quoteStartedAt = quoting ? index : -1;
                continue;
            }

            if (character == '\\'
                && index + 1 < query.Length
                && Special.Contains(query[index + 1], StringComparison.Ordinal))
            {
                text.Append(query[index + 1]);
                literal.Add(true);
                index++;
                continue;
            }

            if (!quoting && char.IsWhiteSpace(character))
            {
                Flush(members, text, literal);
                continue;
            }

            text.Append(character);
            literal.Add(quoting);
        }

        if (quoting)
        {
            unclosedQuoteAt = quoteStartedAt;
            return false;
        }

        Flush(members, text, literal);
        return true;
    }

    private static void Flush(List<ScannedText> members, StringBuilder text, List<bool> literal)
    {
        if (text.Length == 0)
        {
            return;
        }

        members.Add(new ScannedText(text.ToString(), [.. literal]));
        text.Clear();
        literal.Clear();
    }
}
