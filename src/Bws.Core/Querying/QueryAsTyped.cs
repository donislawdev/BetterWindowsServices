namespace Bws.Core.Querying;

/// <summary>
/// A query as it stands in the box, parsed once and then asked as often as anybody likes.
///
/// <b>Why it exists - G-4 of the external performance report of 2026-09-28.</b> One keystroke in the
/// window's search box used to parse the same line eighteen times: once to narrow the list, once to
/// ask whether it names what the scope leaves out, and once for each of the sixteen filter chips
/// asking whether it is lit. Each parse of a line holding a pattern builds a new regular expression,
/// and a pattern the engine refuses throws and is caught on every one of them (S-10 of the same
/// report). Everything that asks about one text now asks this.
///
/// <b>It is the TEXT and the answer together, never the answer alone</b>, and that is what keeps
/// it from being the second copy of the query the filter chips are forbidden to hold - the rule is
/// at FilterChip in the window. Whoever keeps one of these keeps it next to the text it was made
/// from and replaces it the moment the text differs, so it cannot disagree with the box.
///
/// <b>Parsed as being typed</b> (<see cref="QueryInput.BeingTyped"/>), because the box is the one
/// place a line is read while it is still being written. The command line reads a finished query
/// and has no use for this.
///
/// <b>Not safe to share between threads</b> - the members of a line that does not parse are worked
/// out on the first question and kept. The window asks from its own thread only.
/// </summary>
public sealed class QueryAsTyped
{
    private IReadOnlyList<Query?>? _members;

    private QueryAsTyped(string text, QueryParseResult parsed)
    {
        Text = text;
        Parsed = parsed;
    }

    /// <summary>The text, exactly as it was typed - an absent text is an empty one.</summary>
    public string Text { get; }

    /// <summary>The whole line parsed, once.</summary>
    public QueryParseResult Parsed { get; }

    /// <summary>Parses the text once.</summary>
    public static QueryAsTyped Of(string? text) =>
        new(text ?? string.Empty, QueryParser.Parse(text, QueryInput.BeingTyped));

    /// <summary>
    /// Whether the text already carries this member, on this side.
    ///
    /// <b>MEMBER BY MEMBER WHEN THE WHOLE LINE DOES NOT PARSE - UX-GUI-002, 2026-09-23.</b> Until
    /// then a mistake anywhere in the line meant no member counted, so in
    /// <c>status:running pid:abc</c> the chip for Running went dark over its own member, and
    /// clicking it wrote a second copy on the end. The whole line is still asked first, because
    /// that is the reading that folds repeated fields - and a line that cannot be taken apart at
    /// all, an unclosed quote, carries nothing, for the reason <see cref="QueryMembers.Without"/>
    /// gives. The members are parsed once for every question, not once per question.
    /// </summary>
    public bool Carries(string field, string value, bool negated)
    {
        if (Parsed.IsValid)
        {
            return Parsed.Query!.Carries(field, value, negated);
        }

        return Members().Any(member => member?.Carries(field, value, negated) ?? false);
    }

    private IReadOnlyList<Query?> Members() => _members ??= Scanned(Text);

    /// <summary>
    /// Each member of the line on its own, parsed rather than compared as text - which is the whole
    /// of why quoting cannot fool a chip (the note at the top of <see cref="QueryMembers"/>).
    /// </summary>
    private static IReadOnlyList<Query?> Scanned(string text)
    {
        if (text.Length == 0 || !QueryScanner.TryScan(text, out _, out var spans, out _))
        {
            return [];
        }

        return [.. spans.Select(span => QueryParser.Parse(text[span], QueryInput.BeingTyped).Query)];
    }
}
