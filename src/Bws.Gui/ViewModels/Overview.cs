using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One line of the machine overview: what it counts, how many there are, and the question that
/// produced the number.
///
/// <b>The query travels with the count because clicking the number has to ASK IT AGAIN.</b> `G`
/// promises that one click from every number leads to the filtered list, and the only honest way to
/// keep that is for the click to put this exact text in the box - so a person can read what was
/// counted, edit it, and learn the language from it. A click that filtered by some other route
/// would be a second answer to the same question, drifting from the first in silence.
/// </summary>
public sealed class OverviewLine
{
    internal OverviewLine(string label, int count, string query, bool leading)
    {
        Label = label;
        Count = count;
        Query = query;
        Leading = leading;
    }

    /// <summary>What this number is about, in the language of whoever is reading it.</summary>
    public string Label { get; }

    /// <summary>How many entries answer to <see cref="Query"/>.</summary>
    public int Count { get; }

    /// <summary>The question, exactly as it would be typed. Clicking puts this in the box.</summary>
    public string Query { get; }

    /// <summary>
    /// Whether this is one of the headline numbers or one of the qualifications under it.
    ///
    /// <b>A property rather than two collections, because the panel draws them differently and the
    /// ORDER matters.</b> A qualification has to sit directly under the number it qualifies - "and
    /// 4 more are per-user templates whose copy is running" means nothing three lines away from the
    /// number it is taking four off.
    /// </summary>
    public bool Leading { get; }
}

/// <summary>
/// What the window says about a machine before anybody has asked it anything - `G`.
///
/// <b>THE SCREEN EXISTS BECAUSE OF ONE SENTENCE IN THE SPECIFICATION AND IT IS WORTH QUOTING:</b>
/// an administrator opens this on an unknown server and sees several hundred rows, and that is the
/// moment they either understand what the tool is for or close it. So the first thing shown is not
/// an alphabetical list but a handful of numbers, each of which is a question the machine has
/// already been asked.
///
/// <b>EVERY NUMBER IS A QUERY AND NONE OF THEM IS A PREDICATE WRITTEN HERE</b>, which is the same
/// rule <see cref="Scopes"/> follows and for the same reason: what counts as a driver, as an
/// automatic start, or as a missing file is decided once, in the language, and a second answer
/// living in this file would drift from the first without a sound.
///
/// <b>AND THE NUMBERS ARE COUNTED INSIDE THE LIST THE WINDOW WILL SHOW, not over the whole
/// machine.</b> Clicking one lands on the services list, so a number counted over 798 entries and a
/// click that lands on a list of 335 would be the screen contradicting itself in the one gesture it
/// promises. Measured on this machine 2026-08-25: 798 entries, 335 of them services.
///
/// Pure, so what a person will read can be checked without opening a window - the property
/// <see cref="Narrowing"/>, <see cref="ListState"/> and <see cref="Folding"/> are all built for.
/// </summary>
internal static class Overview
{
    /// <summary>
    /// Everything the overview shows, in the order it is read.
    ///
    /// <b>THE SECOND NUMBER IS THE ONE THIS SCREEN LIVES OR DIES BY, AND IT IS NOT THE LITERAL
    /// ANSWER TO ACCEPTANCE SCENARIO 1 - owner's decision, 2026-08-25.</b> That scenario asks for
    /// "all auto-start services that are not running", and on this machine the literal query answers
    /// 7 or 8 depending on the minute, of which SIX OR SEVEN ARE FALSE ALARMS: per-user templates,
    /// which never run because their session copy runs instead, and entries with a trigger, which
    /// are stopped because nobody has asked for them. The true content is one entry.
    ///
    /// <b>Nothing is hidden by that, and this is the half that makes it honest.</b> Both exclusions
    /// are on screen, under the number, with their own counts and their own queries - so the person
    /// can click either one and see exactly what was taken out. A headline of 8 would be a window
    /// being confidently wrong, and a headline of 1 with nothing under it would be a window being
    /// quietly incomplete.
    ///
    /// <b>THE FOURTH NUMBER THE SPECIFICATION PROMISES IS NOT HERE, AND IT IS NAMED RATHER THAN
    /// MISSING</b> - see <see cref="Missing"/>.
    /// </summary>
    /// <param name="rows">Every row the window holds, before any query.</param>
    internal static IReadOnlyList<OverviewLine> Of(IReadOnlyList<EntryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        // The list the window opens on, so that clicking a number lands where the number was
        // counted. Every query below carries it.
        const string Services = "!type:driver";

        const string NotUp = "start:auto !status:running !type:driver";

        // EVERY KEY IS WRITTEN OUT AT ITS OWN CALL, WHICH LOOKS REPETITIVE AND IS NOT OPTIONAL. A
        // key travelling as a variable is invisible to TextKeyGuards, the check that every declared
        // sentence reaches a screen - so a table of key and query, walked in a loop, reads to that
        // guard as six orphaned strings. The first version of this file was exactly that table and
        // the guard said so. Sentences.Admissions and ListState.Say carry the same note.
        return
        [
            Line(Texts.Of("gui.overview.running"), $"status:running {Services}", rows, leading: true),

            Line(Texts.Of("gui.overview.notUp"), $"{NotUp} peruser:no trigger:none", rows, leading: true),

            // THE TWO EXCLUSIONS, EACH ONE CLICKABLE. Written as their own queries rather than as a
            // subtraction, because a number a person cannot ask for is a number they have to take
            // on trust.
            Line(Texts.Of("gui.overview.notUp.template"), $"{NotUp} peruser:template", rows, leading: false),
            Line(Texts.Of("gui.overview.notUp.trigger"), $"{NotUp} peruser:no !trigger:none", rows, leading: false),

            // THE ORPHAN OF `C13`, WHICH IS A COMPOSITION RATHER THAN A WORD - `docs/07` settles
            // that: an automatic entry whose file is gone is written from two members that already
            // exist. Measured on this machine: zero.
            Line(Texts.Of("gui.overview.orphans"), $"file:missing start:auto {Services}", rows, leading: true),

            // AND THE NUMBER BESIDE IT THAT IS NOT ZERO HERE - owner's decision, 2026-08-25. The
            // specification promises the orphan count and this machine has none, while four entries
            // do point at a file that is not there and none of them is automatic. A screen whose
            // number is always zero teaches that the screen is useless, so the promise is kept AND
            // the true finding is shown under it.
            Line(Texts.Of("gui.overview.fileGone"), $"file:missing {Services}", rows, leading: false)
        ];
    }

    /// <summary>
    /// The number `G` promises that this build cannot count, said out loud rather than left off.
    ///
    /// <b>"How many entries are not from a stock Windows" needs `D6`, the stock baseline, and `D6`
    /// is Phase 3</b> - `docs/01` puts it there twice, in the phase bundle and in acceptance
    /// scenario 6. So section `G` promises, on the first screen of Phase 1, a number the product
    /// cannot produce before Phase 3.
    ///
    /// <b>Naming it is the specification's own answer to its own risk.</b> R3 says the baseline will
    /// age and asks for "jawne oznaczanie brak baseline'u dla tego builda zamiast cichego pokazywania
    /// zlych wynikow" - explicit marking rather than quietly showing bad results. A missing baseline
    /// and an ageing one are the same problem at different times, and this is the same answer.
    ///
    /// Owner's decision, 2026-08-25: three numbers now, the fourth with `D6`.
    /// </summary>
    internal static string Missing => Texts.Of("gui.overview.notYet");

    /// <summary>
    /// One number, asked of the query language.
    ///
    /// <b>Finished rather than BeingTyped, because this text is ours and complete by construction.</b>
    /// A complaint here would be a fault in this file rather than in something somebody typed - the
    /// same reason <see cref="Scoping.Recut"/> parses its own scope that way.
    ///
    /// <b>A query this file cannot parse throws rather than counting zero.</b> A screen quietly
    /// showing zero for a question it failed to ask is the exact shape rule 8 forbids, and it would
    /// look like a clean machine.
    /// </summary>
    private static OverviewLine Line(string label, string query, IReadOnlyList<EntryRow> rows, bool leading)
    {
        var parsed = QueryParser.Parse(query, input: QueryInput.Finished);

        if (!parsed.IsValid)
        {
            throw new InvalidOperationException(
                $"The overview asked a question this build cannot parse: {query}");
        }

        return new OverviewLine(label, Narrowing.Of(parsed.Query!, rows).Selected.Count, query, leading);
    }
}
