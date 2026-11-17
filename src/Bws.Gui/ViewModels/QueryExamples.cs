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
/// teaches by writing its member into the box - these teach by writing a whole question into it.
/// Somebody who clicks "Services only" and reads <c>!type:driver</c> has learnt the negation
/// operator without being told about it, and can edit it into the question they actually had.
///
/// <b>Every one of these has to parse, and a test says so.</b> An example that selects nothing
/// teaches the language wrong and blames the person for it.
/// </summary>
public sealed class QueryExample
{
    private readonly string _labelKey;
    private readonly IReadOnlyCollection<EntryScope> _scopes;

    internal QueryExample(string labelKey, string query, params EntryScope[] scopes)
    {
        _labelKey = labelKey;
        Query = query;
        _scopes = scopes;
    }

    /// <summary>What the question is, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of(_labelKey);

    /// <summary>The query it writes into the box - shown as the tooltip, so it is read before it is used.</summary>
    public string Query { get; }

    /// <summary>
    /// Whether this question makes sense of the list somebody is standing on.
    ///
    /// <b>Declared per example rather than worked out, because what makes an example senseless is
    /// not one rule.</b> <c>account:LocalSystem</c> can never select a driver - a driver has no
    /// account, the manager reads nothing there on 461 of 464 on this machine - and that is a fact
    /// about the field. <c>!type:driver</c> selects nothing on Drivers and EVERYTHING on Services,
    /// and the second is as useless as the first: an example that lights up the whole list teaches
    /// no operator and answers no question. Backlog 358 found three of the six selecting nothing on
    /// Drivers, in a tooltip nobody pointed at.
    /// </summary>
    public bool Fits(EntryScope scope) => _scopes.Contains(scope);
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
    /// first carries a field the tool works out rather than reads, the second negation over a value
    /// that is a group (<c>driver</c> is both kinds of driver), the third two members narrowing each
    /// other, the fourth a plain value, and the last two the reserved words every field has.
    ///
    /// <b>THE FIRST ONE TAUGHT NEGATION UNTIL 2026-09-23 AND GAVE IT UP FOR THE RIGHT ANSWER -
    /// UX-GUI-008.</b> It wrote <c>start:auto !status:running</c>, and the chip beside the box asks
    /// the same question as <c>mismatch:stopped</c>. On the machine the audit was taken on the two
    /// answered 7 services and 1: the literal query also catches per-user templates and services a
    /// trigger starts, which are stopped because that is where they are meant to sit - the noise
    /// spec G already refused for the start screen. One question now has one answer wherever it is
    /// asked from. The price is that negation is taught only by "Services only", which is offered
    /// on the whole machine and nowhere else.
    ///
    /// <b>Nothing here needs a reading the window does not do.</b> A signature or memory example
    /// would compose a query the list cannot answer, which is the one thing an example must not
    /// do - `signed:` and `memory:` are second phase and the window has no second phase yet.
    ///
    /// <b>Each one names the scopes it is shown on, and the reasons were measured on 2026-09-16
    /// rather than reasoned</b> - `bws list --query` on this machine, 336 services and 464 drivers.
    /// Drivers keep three: they have a start type and a status (two auto-start drivers were not
    /// running), a disabled one can still be loaded, and a driver's file can be missing (one was).
    /// They lose the account, the trigger - fields the manager does not read for a driver - and the
    /// example whose whole point is to leave them out. Services lose only that last one, which
    /// there selects the entire list.
    /// </summary>
    internal static IReadOnlyList<QueryExample> All { get; } =
    [
        new QueryExample("gui.example.shouldBeRunning", "mismatch:stopped", EntryScope.Services, EntryScope.Drivers, EntryScope.Everything),
        new QueryExample("gui.example.servicesOnly", "!type:driver", EntryScope.Everything),
        new QueryExample("gui.example.disabledButRunning", "start:disabled status:running", EntryScope.Services, EntryScope.Drivers, EntryScope.Everything),
        new QueryExample("gui.example.asLocalSystem", "account:LocalSystem", EntryScope.Services, EntryScope.Everything),
        new QueryExample("gui.example.missingFile", "file:missing", EntryScope.Services, EntryScope.Drivers, EntryScope.Everything),
        new QueryExample("gui.example.waitingOnTrigger", "trigger:any status:stopped", EntryScope.Services, EntryScope.Everything)
    ];

    /// <summary>
    /// The questions that fit one list, in the same order. Cut once per scope rather than on
    /// every read, because the tooltip and the list under the box both ask on every visit.
    /// </summary>
    private static readonly IReadOnlyDictionary<EntryScope, IReadOnlyList<QueryExample>> ByScope =
        Enum.GetValues<EntryScope>().ToDictionary(
            scope => scope,
            scope => (IReadOnlyList<QueryExample>)[.. All.Where(example => example.Fits(scope))]);

    /// <summary>
    /// The questions shown on one list - what the list under an empty box offers, so it never
    /// offers a question the list on screen cannot answer. The box's tooltip listed them too until
    /// 2026-11-17, when the guide under the ? took over explaining the language (MainViewModel.SearchTip).
    /// </summary>
    internal static IReadOnlyList<QueryExample> For(EntryScope scope) => ByScope[scope];
}
