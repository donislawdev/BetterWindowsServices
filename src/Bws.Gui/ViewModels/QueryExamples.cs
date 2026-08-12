namespace Bws.Gui.ViewModels;

/// <summary>
/// One question somebody arrives with, written as a query they can then edit.
///
/// <b>The syntax stopped living in the placeholder on 2026-08-12.</b> It read
/// "Search, or write a query: start:auto !status:running" - which is a label, an example and the
/// documentation at once, and all three vanish permanently the moment somebody types a character.
/// The query language is this tool's best feature and it was discoverable for exactly as long as
/// the box was empty.
///
/// <b>Examples rather than a syntax card, and that is the same bet the chips made.</b> A chip
/// teaches by writing its member into the box; these teach by writing a whole question into it.
/// Somebody who clicks "Automatic but not running" and reads
/// <c>start:auto !status:running</c> has learnt the negation operator without being told about it,
/// and can edit it into the question they actually had.
///
/// <b>Every one of these has to parse, and a test says so.</b> An example that selects nothing
/// teaches the language wrong and blames the person for it.
/// </summary>
public sealed class QueryExample
{
    private readonly string _labelKey;

    internal QueryExample(string labelKey, string query)
    {
        _labelKey = labelKey;
        Query = query;
    }

    /// <summary>What the question is, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of(_labelKey);

    /// <summary>The query it writes into the box - shown as the tooltip, so it is read before it is used.</summary>
    public string Query { get; }
}

internal static class QueryExamples
{
    /// <summary>
    /// The questions, in the order they are offered.
    ///
    /// <b>Six rather than a catalogue.</b> These are a doorway, not a reference - somebody who
    /// needs the whole language has the field list in the error the window gives for a wrong one,
    /// and `docs/07` behind that. A list long enough to scroll would be a second column picker.
    ///
    /// <b>Chosen for the operator each one teaches, not only for the answer it gives.</b> The
    /// first carries negation, the second an alias, the third a value that is a group, the fourth
    /// two members narrowing each other, and the last two the reserved words every field has.
    ///
    /// <b>Nothing here needs a reading the window does not do.</b> A signature or memory example
    /// would compose a query the list cannot answer, which is the one thing an example must not
    /// do - `signed:` and `memory:` are second phase and the window has no second phase yet.
    /// </summary>
    internal static IReadOnlyList<QueryExample> All { get; } =
    [
        new QueryExample("gui.example.shouldBeRunning", "start:auto !status:running"),
        new QueryExample("gui.example.servicesOnly", "!type:driver"),
        new QueryExample("gui.example.disabledButRunning", "start:disabled status:running"),
        new QueryExample("gui.example.asLocalSystem", "account:LocalSystem"),
        new QueryExample("gui.example.missingFile", "file:missing"),
        new QueryExample("gui.example.waitingOnTrigger", "trigger:any status:stopped")
    ];
}
