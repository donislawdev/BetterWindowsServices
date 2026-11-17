namespace Bws.Gui.ViewModels;

/// <summary>A way to write a value, as the guide shows it: what is typed, and what it does.</summary>
/// <param name="Written">The text, as it would stand in the box.</param>
/// <param name="Meaning">What it finds, in the reader's language.</param>
public sealed record GuideLine(string Written, string Meaning);

/// <summary>
/// What the guide to the language says over the questions to start from - the owner's second
/// choice of 2026-11-16: a button at the end of the box opening a card with the fields and the
/// examples, the examples written into the box by a click. Until then the syntax stood in the box's
/// tooltip alone.
///
/// <b>Data rather than markup</b> (GUI rule 8): eight ways to write a value and twenty one fields,
/// drawn by SearchGuide.xaml from these lists. Every example below reads - SuggestingHelpTests parses
/// each one, because a guide teaching a spelling the parser refuses is worse than no guide.
///
/// <b>The fields are the ones the list already offers on Down</b>, with the same sentence beside
/// each (<see cref="Meanings.Fields"/>), so a field has one sentence wherever it is explained.
/// </summary>
public static class QueryGuide
{
    /// <summary>
    /// The ways to write a value, from a plain word to the reserved words - one example each, because
    /// each is a row of the list a click writes into the box, and a row writing two examples at once
    /// would be a query nobody asked for.
    /// </summary>
    public static IReadOnlyList<GuideLine> Lines { get; } =
    [
        new("spooler", Texts.Of("gui.guide.word")),
        new("status:running", Texts.Of("gui.guide.field")),
        new("!start:disabled", Texts.Of("gui.guide.not")),
        new("status:running,paused", Texts.Of("gui.guide.either")),
        new("name:win*", Texts.Of("gui.guide.wildcard")),
        new("name:/^sql/", Texts.Of("gui.guide.pattern")),
        new("display:\"Print Spooler\"", Texts.Of("gui.guide.quotes")),
        new("memory:>100MB", Texts.Of("gui.guide.range")),
        new("trigger:any", Texts.Of("gui.guide.reserved"))
    ];

    /// <summary>The ways to write a value as rows of the list, each written at the end of whatever the box holds.</summary>
    internal static IReadOnlyList<Suggestion> Rows(string text)
    {
        var gap = text.Length == 0 || char.IsWhiteSpace(text[^1]) ? string.Empty : " ";

        return [.. Lines.Select(line => new Suggestion(line.Written, line.Meaning, text.Length..text.Length, gap + line.Written))];
    }

    /// <summary>
    /// Every field of the language, as a row the guide can write into the box: the field and its
    /// colon, so the list of its values opens at once.
    /// </summary>
    public static IReadOnlyList<Suggestion> Fields { get; } =
        [.. Meanings.Fields.Select(field => new Suggestion(field.Word, Texts.Of(field.Key), default, field.Word + ":"))];
}
