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
/// <param name="Blanks">
/// Where an empty pair of quotes sat, as positions in <paramref name="Text"/>.
///
/// <b>The one thing a quote leaves behind after producing no characters</b>, and without it the
/// language cannot tell two different sentences apart. <c>status:</c> is what a search box holds
/// between the colon and the value, so it is tolerated and dropped. <c>status:""</c> is finished -
/// somebody wrote quotes and closed them - and it asks for entries whose status is the empty
/// string, which is nothing, ever.
///
/// Until 2026-08-03 both were the same absence, so <c>bws list --query '""'</c> and
/// <c>--query 'name:""'</c> each came back with all 810 entries and a code of success. Backlog
/// item 66, and it is the same fault as the lone exclamation mark that was fixed the day before:
/// a script with a typo in its query gets the whole machine and a green light.
///
/// A position rather than a flag, because a member can hold both shapes - <c>name:""x</c> has an
/// empty pair of quotes and a value that is not empty, and only a position can say that the two
/// are in different places.
/// </param>
internal sealed record ScannedText(string Text, bool[] Literal, IReadOnlyList<int>? Blanks = null)
{
    /// <summary>
    /// Which characters arrived through a backslash, as opposed to through quotes or bare.
    ///
    /// <b>Added 2026-10-05 for one reader, the pattern between slashes</b>, and the reason is that
    /// a pattern speaks a second language with its own escapes. <c>name:/a\?/</c> means a literal
    /// question mark to anybody who has written a regular expression - and the scanner, which
    /// takes the backslash away before the value is read, turned it into <c>a?</c>, which matches
    /// every entry on the machine. <c>path:/C:\\Windows/</c> went the other way: the doubled
    /// backslash became one, and <c>\W</c> is a character class. Stability report Q-1, measured
    /// at 800 of 800 and 0 of 307.
    ///
    /// <b>Literal cannot answer this</b>, because it is also true of every quoted character, and a
    /// quote inside a pattern only protects a space from the scanner - <c>display:/"Print Spooler"/</c>
    /// has never meant a pattern with quotes in it.
    /// </summary>
    internal bool[] Escaped { get; init; } = new bool[Text.Length];

    /// <summary>
    /// Where each character stood in the text that was scanned - for an escaped one, the character
    /// after the backslash.
    ///
    /// <b>Added 2026-10-05 so that one value can be taken out of a list without reprinting it.</b>
    /// Turning a chip off in <c>status:running,stopped</c> used to cut the whole member, so the
    /// chip still lit for running went dark too (stability report Q-5). Cutting one value needs
    /// the positions of the colon and the commas in the text as WRITTEN, and a second scanner in
    /// QueryMembers finding them again is the duplicated rule this file exists to avoid.
    ///
    /// The default is a text that stands where it was written, which is the honest answer for one
    /// nobody scanned.
    /// </summary>
    internal int[] WrittenAt { get; init; } = [.. Enumerable.Range(0, Text.Length)];

    internal int Length => Text.Length;

    /// <summary>
    /// True when this is nothing, and was written as nothing on purpose.
    ///
    /// The difference between a value somebody has not finished typing and one they finished by
    /// closing a pair of quotes around nothing.
    /// </summary>
    internal bool IsExplicitlyEmpty => Text.Length == 0 && Blanks is { Count: > 0 };

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

    /// <summary>
    /// A run of this text, with the marks that belong to it.
    ///
    /// The end of the range is inclusive for the blanks and exclusive for the characters, and
    /// that is not a slip: a blank sits BETWEEN characters, so one at the very end of a slice
    /// belongs to it. That is the whole case this exists for - the value of <c>name:""</c> is a
    /// slice of length zero whose blank is at its start, which is also its end.
    /// </summary>
    internal ScannedText Slice(int start, int length) =>
        new(
            Text.Substring(start, length),
            Literal[start..(start + length)],
            Blanks?.Where(at => at >= start && at <= start + length).Select(at => at - start).ToArray())
        {
            Escaped = Escaped[start..(start + length)],
            WrittenAt = WrittenAt[start..(start + length)]
        };

    internal ScannedText Slice(int start) => Slice(start, Text.Length - start);

    internal ScannedText Slice(Range range)
    {
        var (start, length) = range.GetOffsetAndLength(Text.Length);

        return Slice(start, length);
    }

    /// <summary>True when nothing in this text was quoted or escaped.</summary>
    internal bool IsBare() => Array.TrueForAll(Literal, literal => !literal);

    /// <summary>
    /// A run of this text with every backslash an escape took away put back in front of its
    /// character - what a pattern between slashes has to be compiled from. Quotes are not put
    /// back, because inside a pattern they only ever kept a space away from the scanner.
    /// </summary>
    internal string WithEscapes(int start, int length)
    {
        var written = new StringBuilder(length + 4);

        for (var index = start; index < start + length; index++)
        {
            if (Escaped[index])
            {
                written.Append('\\');
            }

            written.Append(Text[index]);
        }

        return written.ToString();
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
    internal static bool TryScan(string query, out List<ScannedText> members, out int unclosedQuoteAt) =>
        TryScan(query, out members, out _, out unclosedQuoteAt);

    /// <summary>
    /// The same scan, and where each member sat in the text it came from.
    ///
    /// <b>The spans are here rather than on <see cref="ScannedText"/>, and that is the whole
    /// design decision.</b> A ScannedText is also produced by <see cref="ScannedText.Slice(int, int)"/>
    /// when a member is cut at its colon or split on its commas, and those pieces have no
    /// position in the original query - only in their parent. A Start property on the record
    /// would be right for members and quietly wrong for every slice, which is a worse thing to
    /// own than no property at all.
    ///
    /// <b>What needs them:</b> editing a query by member. Adding a filter is appending text,
    /// but REMOVING one means cutting exactly the characters it occupied and leaving the rest
    /// of the line as the person typed it - their spacing, their quoting, their case. Without a
    /// span the only options are re-printing the query from its parts, which loses all three,
    /// or a second scanner living in the interface, which is the duplicated rule this project
    /// keeps paying for.
    ///
    /// A span covers the member as WRITTEN, including its quotes and its escapes, so
    /// <c>query[span]</c> is the text that would have to go.
    /// </summary>
    internal static bool TryScan(
        string query, out List<ScannedText> members, out List<Range> spans, out int unclosedQuoteAt)
    {
        members = [];
        spans = [];
        unclosedQuoteAt = -1;

        var text = new Collected();
        var quoting = false;
        var quoteStartedAt = -1;
        var quotedFrom = 0;
        var startedAt = -1;

        for (var index = 0; index < query.Length; index++)
        {
            var character = query[index];

            // Where this member began, which is the first character that belongs to it - the
            // opening quote and the backslash of an escape included, because those are
            // characters somebody typed and a cut that left them behind would leave debris.
            if (startedAt < 0 && (quoting || !char.IsWhiteSpace(character)))
            {
                startedAt = index;
            }

            if (character == '"')
            {
                if (quoting)
                {
                    // A pair that produced no characters. Noted, because after this line there
                    // is nothing left to show it ever happened - and "somebody wrote nothing on
                    // purpose" is a different sentence from "somebody has not typed it yet".
                    if (text.Length == quotedFrom)
                    {
                        text.Blanks.Add(text.Length);
                    }
                }
                else
                {
                    quotedFrom = text.Length;
                }

                quoting = !quoting;
                quoteStartedAt = quoting ? index : -1;
                continue;
            }

            if (character == '\\'
                && index + 1 < query.Length
                && Special.Contains(query[index + 1], StringComparison.Ordinal))
            {
                text.Append(query[index + 1], literal: true, escaped: true, at: index + 1);
                index++;
                continue;
            }

            if (!quoting && char.IsWhiteSpace(character))
            {
                Flush(members, spans, text, startedAt, index);
                startedAt = -1;
                quotedFrom = 0;
                continue;
            }

            text.Append(character, literal: quoting, escaped: false, at: index);
        }

        if (quoting)
        {
            unclosedQuoteAt = quoteStartedAt;
            return false;
        }

        Flush(members, spans, text, startedAt, query.Length);
        return true;
    }

    private static void Flush(
        List<ScannedText> members, List<Range> spans, Collected text, int startedAt, int endedAt)
    {
        // Nothing typed, so there is no member. An empty pair of quotes is not nothing typed:
        // it is a member that was finished and says nothing, and dropping it here is how
        // `bws list --query '""'` used to answer with the whole machine and a code of success.
        if (text.Length == 0 && text.Blanks.Count == 0)
        {
            return;
        }

        members.Add(text.ToScanned());

        // One span per member and in the same order, because the two lists are read by index.
        // A member that got here without a start would be a member made of no characters, which
        // the guard above has already sent back.
        spans.Add(new Range(startedAt, endedAt));

        text.Clear();
    }

    /// <summary>
    /// The member being read, a character at a time, with what the scanner knows about each one.
    ///
    /// A type of its own since 2026-10-05, when two more facts per character arrived - whether it
    /// came through a backslash and where it stood. Five parallel lists handed to Flush one by
    /// one would have been a signature as wide as the facts, and a sixth fact would widen it again.
    /// </summary>
    private sealed class Collected
    {
        private readonly StringBuilder _text = new();
        private readonly List<bool> _literal = [];
        private readonly List<bool> _escaped = [];
        private readonly List<int> _writtenAt = [];

        internal List<int> Blanks { get; } = [];

        internal int Length => _text.Length;

        internal void Append(char character, bool literal, bool escaped, int at)
        {
            _text.Append(character);
            _literal.Add(literal);
            _escaped.Add(escaped);
            _writtenAt.Add(at);
        }

        internal ScannedText ToScanned() =>
            new(_text.ToString(), [.. _literal], Blanks.Count == 0 ? null : [.. Blanks])
            {
                Escaped = [.. _escaped],
                WrittenAt = [.. _writtenAt]
            };

        internal void Clear()
        {
            _text.Clear();
            _literal.Clear();
            _escaped.Clear();
            _writtenAt.Clear();
            Blanks.Clear();
        }
    }
}
