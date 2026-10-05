namespace Bws.Core.Querying;

/// <summary>
/// Where the values of one member begin and end - the text after the colon, cut at its commas.
///
/// <b>One rule with two readers, and that is why it has a file.</b> The parser reads the values,
/// and <see cref="QueryMembers.Without"/> cuts one of them out when a chip is turned off. Until
/// 2026-10-05 the parser split on every bare comma and the chip cut the whole member - so the two
/// never had to agree on anything. Now the chip cuts a single value (stability report Q-5), and a
/// cut computed by a second copy of this rule would take the wrong characters the first time the
/// copies disagreed.
///
/// <b>A comma inside a pattern that has not closed yet belongs to the pattern.</b> Until
/// 2026-10-05 <c>name:/^w{2,}/</c> came apart into <c>/^w{2</c> and <c>}/</c>, neither of them a
/// pattern, and both were searched for as text - an empty answer with a code of success (stability
/// report Q-2, measured at 0 where <c>name:/^ww/</c> finds 1).
///
/// <b>What closes a pattern is a bare slash right before the comma</b>, so <c>name:/a/,/b/</c> is
/// still two patterns, which is what it has always meant. The price is a pattern with <c>/,</c>
/// in the middle of it, and it is written <c>\,</c> or <c>\/</c> - the same way out every other
/// special character has.
/// </summary>
internal static class QueryValueList
{
    /// <summary>The ranges of the values in <paramref name="values"/>, empty ones included.</summary>
    internal static List<Range> Of(ScannedText values)
    {
        var parts = new List<Range>();
        var start = 0;

        for (var index = 0; index < values.Length; index++)
        {
            if (!values.IsSpecial(index, ',') || StillOpen(values, start, index))
            {
                continue;
            }

            parts.Add(new Range(start, index));
            start = index + 1;
        }

        parts.Add(new Range(start, values.Length));

        return parts;
    }

    /// <summary>
    /// Whether the run from <paramref name="start"/> to <paramref name="end"/> is a pattern
    /// written whole - a bare slash at each end, and at least the two of them.
    /// </summary>
    internal static bool Closes(ScannedText text, int start, int end) =>
        end - start >= 2 && text.IsSpecial(start, '/') && text.IsSpecial(end - 1, '/');

    private static bool StillOpen(ScannedText values, int start, int comma) =>
        start < comma && values.IsSpecial(start, '/') && !Closes(values, start, comma);
}
