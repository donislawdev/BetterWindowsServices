using System.Globalization;
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
    internal OverviewLine(string label, int count, string countText, string query, bool leading)
    {
        Label = label;
        Count = count;
        CountText = countText;
        Query = query;
        Leading = leading;
    }

    /// <summary>What this number is about, in the language of whoever is reading it.</summary>
    public string Label { get; }

    /// <summary>How many entries answer to <see cref="Query"/>.</summary>
    public int Count { get; }

    /// <summary>
    /// The same number as something to put on a screen, or a placeholder while nothing has been
    /// read yet - backlog 263.
    ///
    /// <b>A second member rather than a converter on the number</b>, because the difference is not
    /// a formatting choice: it is whether there is an answer at all. A converter would have to be
    /// told the same fact through a second binding and would put the decision in the markup, where
    /// nothing in this project tests it.
    /// </summary>
    public string CountText { get; }

    /// <summary>
    /// Whether this line found nothing, as a question markup can ask.
    ///
    /// <b>A member rather than a trigger comparing the number to zero, and that is WPF rather than
    /// taste.</b> A DataTrigger read from markup keeps its Value as the string the file held, and
    /// whether it ever gets converted to the type on the other side of the binding depends on what
    /// that binding can report - this project has the same note beside <c>PlanLineTemplate</c> and
    /// paid for it once in a test that printed "Expected: True, Actual: True". Every trigger in the
    /// overview that demonstrably fires asks a bool, so this makes the zero a bool too and the
    /// question disappears rather than being answered.
    ///
    /// <b>Why a zero is worth drawing quieter at all:</b> this screen exists to show what is wrong
    /// with a machine, and "0 orphans" is the line where nothing is. Drawn at the same size and
    /// weight as "112 services running", it spent the loudest voice on the best news.
    /// </summary>
    public bool Nothing => Count == 0;

    /// <summary>The question, exactly as it would be typed. Clicking puts this in the box.</summary>
    public string Query { get; }

    /// <summary>
    /// What this line is called, for anything that has to say it in one string.
    ///
    /// <b>THE CARDS HAD NO NAME AT ALL UNTIL 2026-09-01 EVENING, and it was found by a probe that
    /// could not get hold of one.</b> A Button whose Content is a panel of TextBlocks gives WPF
    /// nothing to derive a name from, so all six questions on the first screen anybody sees arrived
    /// in the automation tree as empty strings - readable by eye, announced by a screen reader as
    /// six unnamed buttons. The tooltip carries the query, which is help rather than identity.
    ///
    /// <b>The number belongs in it.</b> "set to start automatically and did not come up" is the
    /// label, and what makes it a finding is the 1 in front - a name without it says what the line
    /// is about and not what it answers.
    /// </summary>
    public string Spoken => $"{CountText} {Label}";

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
/// One finding on the machine overview: a headline number and whatever has to be said about it.
///
/// <b>A SHAPE FOR THE SCREEN, DERIVED FROM THE ORDER RATHER THAN DECIDED A SECOND TIME.</b> The
/// lines are authored as a flat sequence in <see cref="Overview.Of"/>, where the reason for each one
/// lives beside it and where every guard over this screen reads them - so this is a PROJECTION of
/// that sequence and cannot disagree with it. Building the groups in <c>Of</c> instead would have
/// meant two places to look for "which questions does this screen ask", and this project has paid
/// for that shape before.
///
/// <b>Why the screen needed it at all.</b> Until 2026-09-01 the panel drew the flat list flat, and
/// a qualification was tied to the number it takes from by nothing but sitting under it, a smaller
/// size and a rule down its left edge. Photographed that day: the rules read as tick marks left over
/// from a table and the labels started at three different left edges. Containment says the same
/// thing without a mark - a qualification drawn INSIDE its finding cannot be read as a fourth one.
/// </summary>
public sealed class OverviewFinding
{
    internal OverviewFinding(OverviewLine headline, IReadOnlyList<OverviewLine> qualifications)
    {
        Headline = headline;
        Qualifications = qualifications;
    }

    /// <summary>The number, and what it counts.</summary>
    public OverviewLine Headline { get; }

    /// <summary>
    /// What has to be said about that number - a subtraction from it, or a widening of it. Empty for
    /// a finding that stands on its own, and the panel then draws nothing rather than a heading over
    /// an empty space, which is complaint 12.2 of <c>docs/11</c>.
    /// </summary>
    public IReadOnlyList<OverviewLine> Qualifications { get; }

    /// <summary>Whether there is anything to draw under the number, as a question markup can ask.</summary>
    public bool HasQualifications => Qualifications.Count > 0;
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
    /// <param name="counted">
    /// Whether a reading has finished. False while the first one is still out, and then every line
    /// shows a placeholder instead of the zero it would otherwise count over an empty listing -
    /// backlog 263.
    /// </param>
    internal static IReadOnlyList<OverviewLine> Of(IReadOnlyList<EntryRow> rows, bool counted)
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
            Line(Texts.Of("gui.overview.running"), $"status:running {Services}", rows, leading: true, counted),

            Line(Texts.Of("gui.overview.notUp"), $"{NotUp} peruser:no trigger:none", rows, leading: true, counted),

            // THE TWO EXCLUSIONS, EACH ONE CLICKABLE. Written as their own queries rather than as a
            // subtraction, because a number a person cannot ask for is a number they have to take
            // on trust.
            //
            // EVERY LABEL ON THIS SCREEN READS AS A SENTENCE STARTING FROM ITS OWN NUMBER, and that
            // is a rule rather than a preference - the owner caught these three breaking it on
            // 2026-09-01, minutes after the cards were built. All three began with "and", because
            // they were written to continue the sentence on the LINE ABOVE at a time when the screen
            // was one flat column. Standing beside their own count they read "3 and entries pointing
            // at a file that is not there", which is a sentence with nothing in front of the "and".
            //
            // The three headlines had always obeyed the rule by accident - "111 services running",
            // "1 set to start automatically and did not come up" - so the fault was invisible until
            // the layout stopped supplying the missing half. THE TEST FOR A NEW LABEL IS TO READ IT
            // OUT LOUD WITH A NUMBER IN FRONT and nothing else.
            Line(Texts.Of("gui.overview.notUp.template"), $"{NotUp} peruser:template", rows, leading: false, counted),
            Line(Texts.Of("gui.overview.notUp.trigger"), $"{NotUp} peruser:no !trigger:none", rows, leading: false, counted),

            // THE ORPHAN OF `C13`, WHICH IS A COMPOSITION RATHER THAN A WORD - `docs/07` settles
            // that: an automatic entry whose file is gone is written from two members that already
            // exist. Measured on this machine: zero.
            Line(Texts.Of("gui.overview.orphans"), $"file:missing start:auto {Services}", rows, leading: true, counted),

            // AND THE NUMBER BESIDE IT THAT IS NOT ZERO HERE - owner's decision, 2026-08-25. The
            // specification promises the orphan count and this machine has none, while four entries
            // do point at a file that is not there and none of them is automatic. A screen whose
            // number is always zero teaches that the screen is useless, so the promise is kept AND
            // the true finding is shown under it.
            //
            // THE REST OF THEM RATHER THAN ALL OF THEM, SINCE 2026-09-24 - UX-GUI-015 (b), owner's
            // decision. It counted every entry with a missing file, the orphans included, so the
            // card read "0 orphans" over "3 point at a file that is not there" and looked like a
            // contradiction at first glance. Now the two numbers split the missing files between
            // them, the way "4 more are per-user templates" sits beside the number it is not part
            // of. `start:auto` covers the delayed ones too (docs/07), so nothing falls between.
            Line(Texts.Of("gui.overview.fileGone"), $"file:missing !start:auto {Services}", rows, leading: false, counted)
        ];
    }

    /// <summary>
    /// The same lines, grouped the way the screen draws them - each headline holding the
    /// qualifications written under it.
    ///
    /// <b>ORDER IS THE ONLY INPUT, and that is what makes this projection rather than a second
    /// decision.</b> <see cref="Of"/> already promises that a qualification sits directly under the
    /// number it takes from - "and 4 more are per-user templates" means nothing three lines away
    /// from the number it is subtracting four from - so the sequence already carries the grouping
    /// and this reads it out.
    ///
    /// <b>A qualification with no headline in front of it throws, and the throw is a tripwire rather
    /// than the guarantee.</b> It can only happen by editing <see cref="Of"/> wrongly, which is a
    /// build rather than a session - and the note beside <see cref="Line"/> applies here word for
    /// word: WPF catches exceptions out of a binding source and turns them into a trace nobody
    /// reads, so on a running window a throw here is a blank screen. What holds the promise is
    /// <c>OverviewGuards</c>, which asks this directly.
    /// </summary>
    internal static IReadOnlyList<OverviewFinding> Findings(IReadOnlyList<OverviewLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var findings = new List<OverviewFinding>();

        // The group being filled, which is the one the last headline opened. Null until the first
        // headline arrives, and that null IS the check below - a qualification with nowhere to go.
        List<OverviewLine>? attached = null;

        foreach (var line in lines)
        {
            if (line.Leading)
            {
                attached = [];
                findings.Add(new OverviewFinding(line, attached));
                continue;
            }

            if (attached is null)
            {
                throw new InvalidOperationException(
                    $"The overview put a qualification before any number it could belong to: {line.Label}");
            }

            attached.Add(line);
        }

        return findings;
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
    /// age and asks, translated, for "explicit marking of a missing baseline for this build instead of
    /// quietly showing bad results". A missing baseline
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
    ///
    /// <b>AND THAT THROW IS QUIETER AT RUNTIME THAN THIS PARAGRAPH USED TO CLAIM - said out loud
    /// on 2026-08-26 rather than left to be discovered.</b> The only caller is a property the
    /// window binds to, and WPF CATCHES exceptions out of a binding source and turns them into a
    /// binding error in a trace nobody is reading. So on a running machine the loud failure is a
    /// blank screen, which is a milder version of the very thing this throw exists to prevent.
    ///
    /// <b>What makes the guarantee real is therefore a test, not this line.</b> These six queries
    /// are constants in this file - nobody types them - so the moment they can be wrong is a build,
    /// not a session, and <c>OverviewGuards.Every_question_this_screen_asks_parses</c> asks all six
    /// directly. The throw stays as the tripwire underneath it: cheap, and correct for any caller
    /// that is not a binding.
    ///
    /// <b>Not moved to startup, and that is a decision rather than an omission.</b> Checking them
    /// when the screen is switched on would put a second road to the same constants in the product,
    /// and would fail on a person's machine for a mistake that can only be made here.
    /// </summary>
    private static OverviewLine Line(
        string label, string query, IReadOnlyList<EntryRow> rows, bool leading, bool counted)
    {
        var parsed = QueryParser.Parse(query, input: QueryInput.Finished);

        if (!parsed.IsValid)
        {
            throw new InvalidOperationException(
                $"The overview asked a question this build cannot parse: {query}");
        }

        var count = Narrowing.Of(parsed.Query!, rows).Selected.Count;

        // ZERO IS A CLAIM AND THIS SCREEN MAKES IT BEFORE IT HAS READ ANYTHING. Backlog 263. The
        // window opens on this screen in its constructor and the first reading is still out for
        // 749-822 ms measured, over which every one of these lines counts an empty listing and says
        // nothing is running. That is the same fault this project keeps apart everywhere else: a
        // machine with no services legitimately shows zero, a machine nobody has read yet must not.
        //
        // The placeholder is a key rather than a character in this file - rule 13, no user visible
        // text as a literal - and the count is still carried beside it, because the line's OTHER
        // job is to be clickable and that does not depend on having a number to show.
        return new OverviewLine(
            label,
            count,
            counted ? count.ToString(CultureInfo.CurrentCulture) : Texts.Of("gui.overview.notCounted"),
            query,
            leading);
    }
}
