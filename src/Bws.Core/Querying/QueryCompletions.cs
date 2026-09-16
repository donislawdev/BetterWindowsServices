namespace Bws.Core.Querying;

/// <summary>What one completion writes: a field name, a value of a field, or a reserved word.</summary>
public enum QueryCompletionKind
{
    /// <summary>A field name - with a colon after it when the member had none yet.</summary>
    Field,

    /// <summary>A value of an enumeration field, as the language spells it.</summary>
    Value,

    /// <summary>One of the three words every field takes: any, none, and the question mark.</summary>
    Word
}

/// <summary>Text after an edit, and where the caret stands in it.</summary>
public readonly record struct QueryEdit(string Text, int Caret);

/// <summary>
/// One thing that can be written where the caret stands.
///
/// <b>It carries the RANGE it stands in for and the TEXT that goes there, rather than the finished
/// line</b> - and that is for the window's sake rather than for elegance. A text box can undo an
/// edit made through its selection and cannot undo a replacement of its whole text, so a caller
/// holding these two can write the completion in a way Ctrl+Z takes back. The finished line is
/// one call away for everybody else - <see cref="Apply"/>.
/// </summary>
/// <param name="Kind">What is being written.</param>
/// <param name="Word">The word as the language spells it - "status", "running", "any".</param>
/// <param name="Field">For a value or a reserved word, the field it belongs to. Null for a field name.</param>
/// <param name="Replaces">The range of the text it stands in for - the part typed so far, whole.</param>
/// <param name="Written">
/// What goes in: the word, a colon after a field name that has none yet, a space after a value
/// that ends the line - so that the next keystroke starts the next member rather than lengthening
/// this one.
/// </param>
public sealed record QueryCompletion(
    QueryCompletionKind Kind, string Word, string? Field, Range Replaces, string Written)
{
    /// <summary>
    /// The text with this written into it, and the caret just after what was written.
    ///
    /// For the text this completion was offered on. A range is a position in one particular
    /// string, so handing this a different one is a caller fault and is answered the way a range
    /// outside a string always is - not quietly.
    /// </summary>
    public QueryEdit Apply(string? text)
    {
        text ??= string.Empty;

        var (start, length) = Replaces.GetOffsetAndLength(text.Length);

        return new QueryEdit(text[..start] + Written + text[(start + length)..], start + Written.Length);
    }
}

/// <summary>
/// What can be written where the caret stands in a query somebody is typing.
///
/// <b>In the language rather than in the window, for the two reasons <see cref="QueryMembers"/>
/// already gave for editing by member.</b> Which member the caret is in, where that member's field
/// ends and its value begins, and what the language would accept there are all questions about the
/// language - the scanner reports spans precisely so that nobody has to write a second scanner to
/// answer them. And `E1` of the specification promises completion in PowerShell one day, which is
/// the same question asked from a terminal. Everything here can be asked without a window.
///
/// <b>What is offered is what the LANGUAGE knows, not what the MACHINE holds.</b> Field names, the
/// spellings of enumeration values and the three reserved words are closed lists frozen in
/// `docs/07`. Service names, accounts and paths are open sets that differ on every machine, and
/// a list of eight hundred names under a search box is a second list of services - so a text
/// field offers the reserved words and nothing else. The day <c>name:</c> is to offer names from
/// the machine, the candidates for a field come out of one private method below, and a provider
/// goes in there rather than a second algorithm in the window.
///
/// <b>Two ways of asking, and they differ in exactly two places.</b> <see cref="WhileTyping"/> is
/// asked on every keystroke and stays quiet where a list would be noise: on an empty field prefix,
/// where every one of twenty-one names fits, and on a candidate already written in full - or the
/// list would open again with <c>running</c> alone the moment <c>running</c> was accepted.
/// <see cref="OnRequest"/> is the same question asked on purpose, so it offers both.
///
/// <b>Bare members only.</b> A member in quotes, one with an escape in it, and a query whose quote
/// never closes get nothing: the scanner's spans cover the member as WRITTEN, and only for a bare
/// member is that the same string as the member as READ, which is what makes a range into the
/// person's text safe to replace.
///
/// <b>Never throws.</b> A null text is empty, a caret outside the text is moved to its nearest
/// end, and a property test asks this of random text and random carets - because this runs on
/// every keystroke and every caret movement in a search box, and a stack trace from moving the
/// caret is the one failure nobody can work around.
/// </summary>
public static class QueryCompletions
{
    /// <summary>
    /// What can be written where the caret stands, offered while somebody types. Empty when
    /// nothing is worth offering - including when the only candidate is already written in full.
    /// </summary>
    public static IReadOnlyList<QueryCompletion> WhileTyping(string? text, int caret) =>
        Offer(text, caret, asked: false);

    /// <summary>
    /// The same question asked on purpose - Down on a closed list. Offers everything that fits,
    /// an empty prefix and a word already written in full included.
    /// </summary>
    public static IReadOnlyList<QueryCompletion> OnRequest(string? text, int caret) =>
        Offer(text, caret, asked: true);

    private static IReadOnlyList<QueryCompletion> Offer(string? text, int caret, bool asked)
    {
        text ??= string.Empty;
        caret = Math.Clamp(caret, 0, text.Length);

        if (!QueryScanner.TryScan(text, out var members, out var spans, out _))
        {
            return [];
        }

        var at = MemberAt(spans, caret);

        if (at < 0)
        {
            // The caret stands in whitespace, so the member under it is one nobody has started:
            // a field name goes here, and it replaces nothing.
            return Fields(typed: string.Empty, replaces: caret..caret, colonFollows: false, asked);
        }

        var (start, length) = spans[at].GetOffsetAndLength(text.Length);
        var end = start + length;
        var written = text[start..end];

        // The member as READ has to be the member as WRITTEN, character for character - otherwise
        // a range into the written text is not a range into what the language saw. Only a bare
        // member reads as it is written: a quote and an escape both leave characters out. Asked
        // of the two strings rather than of the scanner's own IsBare, because an empty pair of
        // quotes is bare by that definition - it left no characters at all - and is still two
        // characters somebody typed.
        if (!string.Equals(members[at].Text, written, StringComparison.Ordinal))
        {
            return [];
        }

        // A leading exclamation mark is skipped the way the parser skips it, and it stays where
        // it is: the completion replaces the body, so `!sta` becomes `!start:` rather than
        // `start:`. Only the first one - the parser reads `!!x` as a bare word `!x`, and so does
        // this by offering nothing for it.
        var body = written.StartsWith('!') ? start + 1 : start;

        // The colon that names a field. One at the very start of the body names nothing - `:foo`
        // is a bare word to the parser - so it is not a colon here either.
        var colon = text.IndexOf(':', body, end - body);

        if (colon <= body)
        {
            return Fields(text[body..Math.Max(caret, body)], body..end, colonFollows: false, asked);
        }

        if (caret <= colon)
        {
            return Fields(text[body..Math.Max(caret, body)], body..colon, colonFollows: true, asked);
        }

        return ValuesOf(text, field: text[body..colon], from: colon + 1, caret, end, asked);
    }

    /// <summary>
    /// The member whose span holds the caret, or ends exactly at it. Minus one when the caret is
    /// in whitespace between members, or in an empty text.
    /// </summary>
    private static int MemberAt(List<Range> spans, int caret)
    {
        for (var index = 0; index < spans.Count; index++)
        {
            if (spans[index].Start.Value <= caret && caret <= spans[index].End.Value)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// The field names that begin with what was typed, in the order the language declares them.
    ///
    /// <b>A prefix, and only a prefix.</b> A member with no colon in it is usually a word somebody
    /// is searching for, and <c>on</c> sits inside <c>dependson</c> and <c>description</c> - so a
    /// containment match would put a list under every free search. Aliases are accepted by the
    /// parser and not offered here: the list teaches one spelling per field.
    /// </summary>
    private static List<QueryCompletion> Fields(string typed, Range replaces, bool colonFollows, bool asked)
    {
        var prefix = QuerySpelling.Normalise(typed);
        var offered = new List<QueryCompletion>();

        // Every name fits an empty prefix, and twenty-one rows under a box somebody has just
        // clicked into is noise - unless they asked for it.
        if (prefix.Length == 0 && !asked)
        {
            return offered;
        }

        foreach (var name in QueryFields.Names)
        {
            var spelled = QuerySpelling.Normalise(name);

            if (!spelled.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!asked && spelled.Length == prefix.Length)
            {
                // Written in full already. Offering it back would open a one-row list saying what
                // the box already says.
                continue;
            }

            offered.Add(new QueryCompletion(
                QueryCompletionKind.Field, name, Field: null, replaces, colonFollows ? name : name + ":"));
        }

        return offered;
    }

    /// <summary>
    /// The values of the field a member names, for the one value the caret stands in - the part
    /// of the member between the commas around the caret.
    /// </summary>
    private static IReadOnlyList<QueryCompletion> ValuesOf(
        string text, string field, int from, int caret, int end, bool asked)
    {
        var known = QueryFields.Find(field);

        if (known is null)
        {
            // The window's error line is already saying the field is unknown. A list of values
            // for a field that does not exist would be a second, contradictory answer.
            return [];
        }

        // The value under the caret runs from the last comma before it to the first comma at or
        // after it, and a comma is always a separator here: the member is bare.
        var lastComma = caret > from ? text.LastIndexOf(',', caret - 1, caret - from) : -1;
        var valueStart = lastComma < 0 ? from : lastComma + 1;
        var nextComma = text.IndexOf(',', caret, end - caret);
        var valueEnd = nextComma < 0 ? end : nextComma;

        return Values(
            known,
            typed: text[valueStart..caret],
            replaces: valueStart..valueEnd,
            closesTheLine: valueEnd == text.Length,
            asked);
    }

    /// <summary>
    /// The values that match what was typed: those beginning with it first, then those with a
    /// later part of their name beginning with it.
    ///
    /// <b>Value names are compound and field names are not, which is the whole reason the rule
    /// differs from the one for fields.</b> Somebody who types <c>pend</c> after <c>status:</c>
    /// is choosing from a closed list, and <c>startPending</c> is what they are reaching for as
    /// much as <c>pending</c> is - so <c>pending</c> comes first and the four that END in it come
    /// after. The second match is on the START OF A LATER PART, not on containment: a contained
    /// <c>r</c> would drag <c>startPending</c> under <c>status:r</c>, and a contained <c>a</c>
    /// would put <c>manual</c> and <c>disabled</c> beside <c>automatic</c>, which is exactly the
    /// noise the prefix rule for fields exists to prevent. A part begins at a capital letter,
    /// which is how every compound value in the language is spelled.
    /// </summary>
    private static List<QueryCompletion> Values(
        QueryField field, string typed, Range replaces, bool closesTheLine, bool asked)
    {
        var prefix = QuerySpelling.Normalise(typed);
        var offered = new List<QueryCompletion>();
        var byLaterPart = new List<QueryCompletion>();

        foreach (var (word, kind) in CandidatesFor(field))
        {
            var spelled = QuerySpelling.Normalise(word);

            // A value ending the line gets a space after it, so that the next keystroke begins
            // the next member. A value with more of the line after it gets nothing: that text is
            // the person's, and it already knows where it stands.
            var written = closesTheLine ? word + " " : word;

            if (spelled.StartsWith(prefix, StringComparison.Ordinal))
            {
                if (!asked && spelled.Length == prefix.Length)
                {
                    // Already there in full - the case that would otherwise reopen the list with
                    // one row the moment a row was accepted.
                    continue;
                }

                offered.Add(new QueryCompletion(kind, word, field.Name, replaces, written));
            }
            else if (prefix.Length > 0 && BeginsALaterPart(word, prefix))
            {
                byLaterPart.Add(new QueryCompletion(kind, word, field.Name, replaces, written));
            }
        }

        offered.AddRange(byLaterPart);

        return offered;
    }

    /// <summary>
    /// Whether some part of a compound word after its first one begins with the prefix - the
    /// <c>Pending</c> of <c>startPending</c>, the <c>Driver</c> of <c>kernelDriver</c>.
    /// </summary>
    private static bool BeginsALaterPart(string word, string prefix)
    {
        for (var index = 1; index < word.Length; index++)
        {
            if (char.IsUpper(word[index])
                && QuerySpelling.Normalise(word[index..]).StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Everything that can follow the colon of one field, in the order it is offered: the field's
    /// own spellings, then the three words every field takes.
    ///
    /// <b>THE ONE PLACE TO EXTEND, and it is named rather than built.</b> A field whose values
    /// come from the machine - names for <c>name:</c>, accounts for <c>account:</c> - is a
    /// provider handed in here, so that the window never grows a second list of what a field
    /// accepts. Today no field has one, on purpose: `docs/PROJEKT-PODPOWIEDZI-20260915.md`
    /// section 8 says what this list deliberately does not offer.
    /// </summary>
    private static IEnumerable<(string Word, QueryCompletionKind Kind)> CandidatesFor(QueryField field) =>
        field.Values
            .Select(value => (value.Text, QueryCompletionKind.Value))
            .Concat(QueryFields.ReservedWords.Select(word => (word, QueryCompletionKind.Word)));
}
