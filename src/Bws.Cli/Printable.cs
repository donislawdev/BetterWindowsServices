using System.Buffers;
using System.Globalization;
using System.Text;

namespace Bws.Cli;

/// <summary>
/// A value from the machine or from a file, made safe to put in front of a terminal.
///
/// <b>Stability report C-4, owner's decision 2026-10-05.</b> Names, paths, descriptions and the
/// values inside a snapshot reached the terminal exactly as they were. A tab or a line break in
/// one of them breaks a row of the listing, and an escape sequence in somebody else's snapshot
/// file - the case worth worrying about, since <c>snapshot diff</c> reads files from other machines
/// - recolours or rewrites what the terminal shows next. Measured the same day on this machine: not
/// one such character in 15 325 strings of <c>bws list --json</c>, so this guards other machines and
/// other people's files, not the one it was built on.
///
/// <b>Each one becomes <c>&lt;U+XXXX&gt;</c></b>, which says which character it was, cannot be
/// mistaken for part of a Windows path the way <c>\n</c> could, and keeps a table row one row.
/// The set is the C0 controls, DEL and the C1 controls, plus the characters that reorder text
/// around them (left to right and right to left marks, embeddings, overrides and isolates) - the
/// ones a file name can use to show one extension and carry another.
///
/// <b>Text only.</b> A JSON document escapes all of these anyway, through AsciiJson. And it is
/// applied to VALUES, never to sentences: five templates of this tool break lines on purpose.
/// </summary>
internal static class Printable
{
    private static readonly SearchValues<char> Unprintable = SearchValues.Create(Characters());

    /// <summary>The value with every character from the set above written out as its code point.</summary>
    internal static string Of(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var first = value.AsSpan().IndexOfAny(Unprintable);

        if (first < 0)
        {
            return value;
        }

        var shown = new StringBuilder(value.Length + 16);
        shown.Append(value, 0, first);

        foreach (var character in value.AsSpan(first))
        {
            if (Unprintable.Contains(character))
            {
                shown.Append(CultureInfo.InvariantCulture, $"<U+{(int)character:X4}>");
            }
            else
            {
                shown.Append(character);
            }
        }

        return shown.ToString();
    }

    private static char[] Characters() =>
    [
        .. Range(0x0000, 0x001F),
        .. Range(0x007F, 0x009F),
        .. Range(0x200E, 0x200F),
        .. Range(0x202A, 0x202E),
        .. Range(0x2066, 0x2069)
    ];

    private static IEnumerable<char> Range(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(code => (char)code);
}
