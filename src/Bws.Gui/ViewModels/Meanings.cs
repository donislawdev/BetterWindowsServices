using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>A word of the language and the key of the sentence that stands beside it in the list.</summary>
internal sealed record Meaning(string Word, string Key);

/// <summary>
/// The sentence beside each word the list can offer.
///
/// <b>Three sources, and which one is the decision rather than the mechanism.</b> A field gets a
/// sentence of its own, because a field name is what a stranger cannot guess and the four that
/// cost a second reading have to say so before somebody writes them - decision 11 of the design.
/// A reserved word gets one of three. A value gets the label of the chip that stands for it,
/// where a chip exists, and nothing where none does: the values are the words <c>sc.exe</c>
/// prints, and a sentence for each of forty-five would be forty-five more keys to keep true
/// (decision 3).
///
/// <b>THE KEY IS WRITTEN BESIDE THE WORD RATHER THAN ASSEMBLED FROM IT</b>, and the reason is
/// <c>TextKeyGuards</c>: a key put together at run time is a key nothing can find by reading, so
/// a sentence missing from the language file would reach the screen as the key itself -
/// <c>Texts.Of</c> answers a missing key with the key. Written here, the guard reads every one of
/// them, and <c>SuggestionGuards</c> holds that every field of the language has a row in this
/// table and every row names a field that exists.
/// </summary>
internal static class Meanings
{
    internal static readonly IReadOnlyList<Meaning> Fields =
    [
        new Meaning("name", "gui.suggest.field.name"),
        new Meaning("display", "gui.suggest.field.display"),
        new Meaning("description", "gui.suggest.field.description"),
        new Meaning("type", "gui.suggest.field.type"),
        new Meaning("peruser", "gui.suggest.field.peruser"),
        new Meaning("status", "gui.suggest.field.status"),
        new Meaning("start", "gui.suggest.field.start"),
        new Meaning("account", "gui.suggest.field.account"),
        new Meaning("pid", "gui.suggest.field.pid"),
        new Meaning("trigger", "gui.suggest.field.trigger"),
        new Meaning("path", "gui.suggest.field.path"),
        new Meaning("file", "gui.suggest.field.file"),
        new Meaning("signed", "gui.suggest.field.signed"),
        new Meaning("publisher", "gui.suggest.field.publisher"),
        new Meaning("privilege", "gui.suggest.field.privilege"),
        new Meaning("dependson", "gui.suggest.field.dependson"),
        new Meaning("requiredby", "gui.suggest.field.requiredby"),
        new Meaning("sidtype", "gui.suggest.field.sidtype"),
        new Meaning("sddl", "gui.suggest.field.sddl"),
        new Meaning("memory", "gui.suggest.field.memory"),
        new Meaning("mismatch", "gui.suggest.field.mismatch")
    ];

    internal static readonly IReadOnlyList<Meaning> Words =
    [
        new Meaning(QueryFields.Any, "gui.suggest.word.any"),
        new Meaning(QueryFields.None, "gui.suggest.word.none"),
        new Meaning(QueryFields.Unreadable, "gui.suggest.word.unreadable")
    ];

    /// <summary>The sentence for one completion, or nothing where nothing has one.</summary>
    internal static string Of(QueryCompletion completion, IReadOnlyList<FilterChip> chips) =>
        completion.Kind switch
        {
            QueryCompletionKind.Field => Sentence(Fields, completion.Word),
            QueryCompletionKind.Word => Sentence(Words, completion.Word),
            _ => chips
                .FirstOrDefault(chip => !chip.Negated
                    && string.Equals(chip.Field, completion.Field, StringComparison.Ordinal)
                    && string.Equals(chip.Value, completion.Word, StringComparison.Ordinal))
                ?.Label ?? string.Empty
        };

    private static string Sentence(IReadOnlyList<Meaning> table, string word)
    {
        var meaning = table.FirstOrDefault(entry => string.Equals(entry.Word, word, StringComparison.Ordinal));

        return meaning is null ? string.Empty : Texts.Of(meaning.Key);
    }
}
