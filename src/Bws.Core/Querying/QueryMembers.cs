namespace Bws.Core.Querying;

/// <summary>
/// Adding a member to a query somebody typed, and taking one back out.
///
/// <b>This is what makes a clickable filter possible without a second copy of the language.</b>
/// `A5` promises chips that compose into the same query the person could have typed - and the
/// point of that promise is teaching: somebody clicks "stopped" and watches `status:stopped`
/// appear in the box, so the language gets learned by using the thing rather than by reading
/// about it. That only works if the text stays theirs, which is what this file is careful about.
///
/// <b>The switch beside the search box did this by hand until now, and it does not generalise.</b>
/// <c>Sentences.WithoutHiddenDrivers</c> takes <c>!type:driver</c> off the END of the line and
/// leaves it alone otherwise, which is exactly right for one control and breaks on the second:
/// click "stopped" then "hide drivers", and the first chip is no longer last, so a tail cut can
/// never find it again. Eight chip families need to remove a member from the middle.
///
/// <b>What it will not do: reprint the query.</b> Turning terms back into text is easy and
/// wrong - it loses the person's spacing, their quoting and their case, so the box would rewrite
/// itself under their hands every time they clicked anything. Instead a member is cut out by the
/// span it occupied, which is why <see cref="QueryScanner"/> learned to report spans on the same
/// day this arrived. Everything outside the cut is untouched, byte for byte.
///
/// <b>Meaning decides what matches, not spelling.</b> A member is found by parsing it and asking
/// the resulting query whether it carries the field and value - so <c>!TYPE:Driver</c> is found
/// by a chip written <c>!type:driver</c>, and <c>"status:stopped"</c> in quotes is NOT, because
/// quoting takes the colon's meaning off and leaves a search for that text. Comparing the written
/// characters would get the first wrong. Comparing them after unquoting would get the second
/// wrong.
/// </summary>
public static class QueryMembers
{
    /// <summary>
    /// The query text with this member in it, added at the end if it was not there already.
    ///
    /// Appending rather than inserting, because the end is where somebody watching the box
    /// expects a thing they just clicked to appear. Idempotent on purpose: a chip that is
    /// already on cannot add a second copy, which is the state that would then need two clicks
    /// to turn off.
    /// </summary>
    public static string With(string? text, string field, string value, bool negated)
    {
        var member = Member(field, value, negated);

        if (Carries(text, field, value, negated))
        {
            return text ?? string.Empty;
        }

        var trimmed = (text ?? string.Empty).TrimEnd();

        return trimmed.Length == 0 ? member : trimmed + " " + member;
    }

    /// <summary>
    /// The query text without this member, wherever it sat, with everything else exactly as it
    /// was typed.
    ///
    /// <b>Every occurrence goes, not the first.</b> Somebody who typed <c>status:stopped</c> and
    /// then clicked the chip for it has the member twice through no fault of their own, and a
    /// chip that turned off while leaving one behind would be a control that visibly does not
    /// work.
    ///
    /// <b>One value out of a list, not the list, since 2026-10-05.</b> Somebody who typed
    /// <c>status:running,stopped</c> sees both chips lit, and turning Stopped off used to cut the
    /// whole member - so Running went dark with it and the line was empty (stability report Q-5).
    /// Now only the values asked about leave, each with one comma, and the rest of the member
    /// stays character for character. The whole member still goes when nothing in it would be
    /// left to say.
    /// </summary>
    public static string Without(string? text, string field, string value, bool negated)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (!QueryScanner.TryScan(text, out var members, out var spans, out _))
        {
            // An unclosed quote means the line cannot be taken apart, so there is nothing safe
            // to cut. Handing it back whole is the honest answer: the person is mid-typing and
            // the chip they clicked will work on the next keystroke that closes the quote.
            return text;
        }

        var cuts = new List<Cut>();

        for (var index = 0; index < members.Count; index++)
        {
            if (!Is(text[spans[index]], field, value, negated))
            {
                continue;
            }

            var (start, length) = spans[index].GetOffsetAndLength(text.Length);
            var whole = new Cut(start, start + length, Whole: true);

            cuts.AddRange(ValuesOut(members[index], start + length, value) ?? [whole]);
        }

        // Backwards, so that cutting one span does not move the ones not yet cut. The cuts come
        // in the order they stand in the line and never overlap - ValuesOut says why.
        var left = text;

        for (var index = cuts.Count - 1; index >= 0; index--)
        {
            left = Remove(left, cuts[index]);
        }

        return left;
    }

    /// <summary>
    /// The cuts that take this value out of a member's list, or null when the member has nothing
    /// left without it and goes whole.
    ///
    /// <b>Each value leaves with the comma after it, and the ones after the last value that stays
    /// leave with the comma before it</b> - otherwise <c>status:a,b</c> without <c>b</c> would keep
    /// a trailing comma, and two neighbours leaving together would both claim the comma between
    /// them. Read through <see cref="QueryValueList"/>, the same cut the parser makes, so a comma
    /// inside a pattern is never mistaken for one between values.
    /// </summary>
    private static List<Cut>? ValuesOut(ScannedText member, int end, string value)
    {
        var body = member.StartsWithSpecial('!') ? member.Slice(1) : member;
        var colon = body.IndexOfSpecial(':');

        if (colon <= 0)
        {
            return null;
        }

        var list = body.Slice(colon + 1);
        var parts = QueryValueList.Of(list);
        var spelling = QuerySpelling.Normalise(value);
        var going = parts.Select(part => QuerySpelling.Normalise(list.Slice(part).Text) == spelling).ToArray();

        if (!parts.Where((part, at) => !going[at]).Any(part => part.End.Value > part.Start.Value))
        {
            return null;
        }

        // Where each value begins in the line as written, and one more past the end - so the
        // value at `at` runs to starts[at + 1] - 1, the comma after it or the end of the member.
        int[] starts =
        [
            .. parts.Select((part, at) => 1 + (at == 0 ? body.WrittenAt[colon] : list.WrittenAt[part.Start.Value - 1])),
            end + 1
        ];

        var lastKept = Array.FindLastIndex(going, gone => !gone);
        var cuts = new List<Cut>();

        for (var at = 0; at < going.Length; at++)
        {
            if (going[at])
            {
                cuts.Add(at < lastKept
                    ? new Cut(starts[at], starts[at + 1], Whole: false)
                    : new Cut(starts[at] - 1, starts[at + 1] - 1, Whole: false));
            }
        }

        return cuts;
    }

    /// <summary>
    /// The line without one cut.
    ///
    /// <b>A WHOLE MEMBER SWALLOWS ONE GAP AND ONLY THE ONE IT MADE.</b> Removing just the member
    /// leaves the space in front of it and the space behind it side by side, so the line grows a
    /// double gap every time a chip is turned off. Taking the gap in front - or the one behind,
    /// when the member was first - closes the hole and leaves every other character where the
    /// person put it, tabs and all. A value cut out of a list makes no gap, so it takes none.
    ///
    /// The alternative was collapsing whitespace across the whole line afterwards, which is two
    /// lines shorter and quietly reformats text somebody is looking at.
    /// </summary>
    private static string Remove(string left, Cut cut)
    {
        var (start, end) = (cut.Start, cut.End);

        while (cut.Whole && start > 0 && char.IsWhiteSpace(left[start - 1]))
        {
            start--;
        }

        while (cut.Whole && start == 0 && end < left.Length && char.IsWhiteSpace(left[end]))
        {
            end++;
        }

        return left[..start] + left[end..];
    }

    /// <summary>Characters to take out of the line, and whether they were a whole member.</summary>
    private readonly record struct Cut(int Start, int End, bool Whole);

    /// <summary>
    /// Whether the text already carries this member, on this side - for one question about one
    /// text. Anything asking several questions of the same text keeps a <see cref="QueryAsTyped"/>
    /// instead, which is where the answer and its argument live.
    /// </summary>
    public static bool Carries(string? text, string field, string value, bool negated) =>
        QueryAsTyped.Of(text).Carries(field, value, negated);

    /// <summary>How this member is written when a chip puts it there.</summary>
    public static string Member(string field, string value, bool negated) =>
        (negated ? "!" : string.Empty) + field + ":" + value;

    /// <summary>
    /// Whether one member, on its own, is the one being looked for.
    ///
    /// Parsed rather than compared as text, which is the whole of why quoting cannot fool it -
    /// see the note at the top of this file.
    /// </summary>
    private static bool Is(string member, string field, string value, bool negated)
    {
        var parsed = QueryParser.Parse(member, QueryInput.BeingTyped);

        return parsed.Query?.Carries(field, value, negated) ?? false;
    }
}
