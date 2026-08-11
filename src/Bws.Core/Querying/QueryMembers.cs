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

        var doomed = new List<Range>();

        for (var index = 0; index < members.Count; index++)
        {
            if (Is(text[spans[index]], field, value, negated))
            {
                doomed.Add(spans[index]);
            }
        }

        if (doomed.Count == 0)
        {
            return text;
        }

        // Backwards, so that cutting one span does not move the ones not yet cut.
        var left = text;

        for (var index = doomed.Count - 1; index >= 0; index--)
        {
            var (start, length) = doomed[index].GetOffsetAndLength(left.Length);
            var end = start + length;

            // THE CUT SWALLOWS ONE GAP AND ONLY THE ONE IT MADE. Removing just the member leaves
            // the space in front of it and the space behind it side by side, so the line grows a
            // double gap every time a chip is turned off. Taking the gap in front - or the one
            // behind, when the member was first - closes the hole and leaves every other
            // character where the person put it, tabs and all.
            //
            // The alternative was collapsing whitespace across the whole line afterwards, which
            // is two lines shorter and quietly reformats text somebody is looking at.
            while (start > 0 && char.IsWhiteSpace(left[start - 1]))
            {
                start--;
            }

            if (start == 0)
            {
                while (end < left.Length && char.IsWhiteSpace(left[end]))
                {
                    end++;
                }
            }

            left = left[..start] + left[end..];
        }

        return left;
    }

    /// <summary>Whether the text already carries this member, on this side.</summary>
    public static bool Carries(string? text, string field, string value, bool negated)
    {
        var parsed = QueryParser.Parse(text, QueryInput.BeingTyped);

        return parsed.Query?.Carries(field, value, negated) ?? false;
    }

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
