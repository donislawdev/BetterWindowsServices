using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The sentences the window says about an answer, and the surgery on the query text that the
/// drivers switch performs.
///
/// <b>Moved out of MainViewModel on 2026-08-02 because the size ratchet said so, for the third
/// time that day.</b> Filling the list in one go rather than 810 notifications pushed that file
/// eighteen lines past a ceiling that may only ever go down, and the ceiling exists so that
/// adding to the longest file starts with looking for a seam.
///
/// This is the seam. Everything here turns state into words or words into words: what an answer
/// admits about itself, and how a checkbox writes a member into a query somebody can read. None
/// of it touches the manager, the rows, the clock or the interface thread, which is what the
/// rest of the view model is about.
/// </summary>
internal static class Sentences
{
    /// <summary>
    /// The member the drivers checkbox writes into the query, spelled once. It lives here
    /// rather than in the view model because the two methods below are the only code that
    /// looks for it in somebody typed text.
    /// </summary>
    // HideDrivers STOOD HERE UNTIL 2026-08-11 AND WENT WITH THE TWO METHODS THAT USED IT.
    // WithHiddenDrivers and WithoutHiddenDrivers edited the query text by hand, the second one
    // only at the end of the line, and both were replaced by QueryMembers in the core - where the
    // scanner already lives and where a member can be found wherever somebody put it. The words
    // are still written into the box, composed now from the field and the value in MainViewModel
    // rather than spelled a second time here.
    //
    // The member is the same and so is the promise: turning the filter off writes what it stands
    // for into the text, which is what `A5` asks of every chip.

    /// <summary>
    /// Everything this answer is not, in sentences.
    ///
    /// The order is deliberate: what was never read comes first, because it is the sentence
    /// that explains an empty list, and somebody staring at one should not have to read past
    /// anything to find out why.
    /// </summary>
    /// <param name="needs">
    /// What the window is being asked to read - the second phase families, from every direction
    /// somebody can ask from.
    ///
    /// <b>A Query stood here until 2026-09-05 and only its Needs was ever read.</b> That was
    /// harmless while the query was the one thing that could ask, and it stopped being harmless
    /// the day a shown column could ask too: this method would have gone on describing the box
    /// above the list while the list itself was waiting on a pass nobody had explained. Rule 8,
    /// arriving through a parameter that was more specific than the question.
    /// </param>
    /// <param name="folded">
    /// How many session copies were drawn under a template rather than as rows of their own.
    /// </param>
    /// <param name="listOnScreen">
    /// Whether the list is the thing in the middle of the window. False while the machine overview
    /// has it, which REPLACES the list rather than sitting over it - so there are no rows at all.
    /// </param>
    internal static Admitted Admissions(
        ExtraRead needs, bool held, int unreadable, int tooCostly, bool elevated,
        ExtraRead have, bool filling, int folded, bool listOnScreen)
    {
        var notes = new List<string>();

        // FIRST, BECAUSE IT IS A FACT ABOUT THE WHOLE LIST RATHER THAN ABOUT THIS QUERY.
        //
        // <b>The sentence existed in the language file from the beginning and reached no screen
        // until 2026-08-05</b>, which made it the shape rule 8 forbids: the window knew the list
        // was short and said nothing. Measured on this machine: without elevation the manager
        // enumerates 807 entries where an elevated session sees 810, and five more refuse their
        // security descriptor. Somebody reading a count has to know that before anything else,
        // because every other sentence here is about a list they think is complete.
        if (!elevated)
        {
            notes.Add(Texts.Of("gui.status.notElevated"));
        }

        // THREE ANSWERS RATHER THAN TWO, SINCE 2026-08-18 - backlog 21, the second half of
        // `ADR-13`. "Nobody has looked" and "this is being looked at right now" are not the same
        // apology, and once the pass has run neither of them is true and the window should say
        // nothing at all. A window that went on apologising after it had the answer would teach
        // people to distrust a line that is usually right.
        //
        // WRITTEN OUT TWICE RATHER THAN DRIVEN FROM A TABLE OF KEYS, and the guard is what settled
        // that. The first version paired each family with its two keys and looked up the one it
        // wanted, which reddened TextKeyGuards immediately: a key travelling as a variable is
        // invisible to the check that every declared sentence reaches a screen, so both new
        // sentences read as orphans. ListState.Say carries the same note for the same reason.
        if (needs.HasFlag(ExtraRead.Signatures) && !have.HasFlag(ExtraRead.Signatures))
        {
            notes.Add(filling
                ? Texts.Of("gui.query.readingSignatures")
                : Texts.Of("gui.query.unreadSignatures"));
        }

        if (needs.HasFlag(ExtraRead.Memory) && !have.HasFlag(ExtraRead.Memory))
        {
            notes.Add(filling
                ? Texts.Of("gui.query.readingMemory")
                : Texts.Of("gui.query.unreadMemory"));
        }

        // Suppressed when the query asked about something nobody has read, and this is a
        // choice rather than an oversight. Both cases arrive as one count, and the sentence
        // below says the machine refused - which for an unread family would turn "nobody
        // looked" into "you were not allowed", the one distinction this project spends most of
        // its rules keeping apart. The sentence above already says what happened.
        // A SINGULAR BESIDE EACH PLURAL, backlog 207, and these two are the pair a person really
        // meets: one entry judged on a field nobody could read is an ordinary answer on a machine
        // where one service refuses its configuration. Written out rather than picking a key into a
        // variable, for the reason given three paragraphs above about TextKeyGuards.
        if (unreadable > 0 && needs == ExtraRead.None)
        {
            notes.Add(unreadable == 1
                ? Texts.Of("gui.status.partial.one", unreadable)
                : Texts.Of("gui.status.partial.many", unreadable));
        }

        if (tooCostly > 0)
        {
            notes.Add(tooCostly == 1
                ? Texts.Of("gui.status.tooCostly.one", tooCostly)
                : Texts.Of("gui.status.tooCostly.many", tooCostly));
        }

        // THE COUNT ABOVE THE LIST AND THE NUMBER OF ROWS IN IT NO LONGER AGREE, AND THIS IS THE
        // ONLY THING THAT SAYS SO. `A11` folds a session's copy under the template it came from,
        // so an answer of 326 entries can be drawn as 303 rows - and a list shorter than the number
        // over it, with nothing explaining the difference, is rule 8 broken in the first place a
        // person looks. Measured on this machine 2026-08-25: 23 of 798 entries fold, and a machine
        // with several people logged on folds that many times over.
        //
        // AFTER THE READING NOTES, BECAUSE A FOLD NEVER EMPTIES A LIST. The head of this method
        // says the unread sentences come first because they are what explains an empty list, and
        // folding cannot produce one - an instance only ever folds under a template that is in the
        // same answer, so the row it went under is still there.
        //
        // The way out is named by its own label rather than by a direction, because the row that
        // holds the switch can be folded away itself and "the switch above" would then point at
        // nothing. Written out twice rather than picked into a variable - TextKeyGuards.
        //
        // AND ONLY WHILE THE LIST IS THE THING ON SCREEN - backlog 263, owner's decision 2026-09-01.
        // The machine overview REPLACES the list rather than sitting over it, so on that screen
        // there are no rows at all. This sentence exists to explain a difference between a count
        // and a NUMBER OF ROWS, and with no rows there is no difference to explain - it goes
        // further than being idle, because it names a switch and a list that are not there to be
        // looked at.
        //
        // THE SAME REASONING ALREADY LIVES IN MainWindow.ArrangeTheMiddle, which collapses the
        // empty state on that screen for exactly this reason - in its words, it would otherwise
        // leave "a sentence about a list nobody can see floating across the overview". It reached
        // one of the three sentences and not this one.
        //
        // NOT THE ELEVATION SENTENCE ABOVE, and the line between them is what each is a fact
        // about. Elevation is a fact about the MACHINE - it is why the numbers ON THIS SCREEN are
        // short too - and backlog 16 put it first deliberately. This one is a fact about rows.
        // THE ONE NOTE WITH A LINK IN IT, SINCE 2026-09-15 - point 8(d) of `docs/11` 2.14. The
        // sentence used to NAME the switch ("Show every instance lists them on their own") and
        // now the name is the switch: whoever reads the sentence can press the words. The line
        // stays ONE string for everything that reads it as one - the tests, the automation tree,
        // the count of what was said - and is handed out in three pieces for the view, cut from
        // the same words, so the two cannot drift. The link's words are the switch's own key, so
        // the sentence and the button can never call it two things.
        var fold = Folded(folded, listOnScreen);

        // Never silent about holding still. A list that quietly stopped matching its own query
        // while somebody leant on it would be the same silence rule 8 forbids, arriving from
        // the one direction where it looks like politeness.
        var afterwards = held ? Texts.Of("gui.status.holding") : string.Empty;

        if (fold.Link.Length == 0)
        {
            return new Admitted(string.Join(" ", notes.Append(afterwards).Where(note => note.Length > 0)), string.Empty, string.Empty);
        }

        return new Admitted(
            string.Join(" ", notes.Append(fold.Head)) + " ",
            fold.Link,
            " " + string.Join(" ", new[] { fold.Tail, afterwards }.Where(note => note.Length > 0)));
    }

    /// <summary>
    /// The folded sentence in its three pieces, or three empty strings when nothing is folded or
    /// no list is on screen. Written out twice rather than picked into a variable - TextKeyGuards,
    /// as at the head of <see cref="Admissions"/>.
    /// </summary>
    private static (string Head, string Link, string Tail) Folded(int folded, bool listOnScreen) =>
        folded > 0 && listOnScreen
            ? (
                folded == 1
                    ? Texts.Of("gui.status.folded.one", folded)
                    : Texts.Of("gui.status.folded.many", folded),
                Texts.Of("gui.instances.toggle"),
                folded == 1
                    ? Texts.Of("gui.status.folded.one.after")
                    : Texts.Of("gui.status.folded.many.after"))
            : (string.Empty, string.Empty, string.Empty);
}

/// <summary>
/// What the window admits about an answer, as one line and as the three pieces the view draws it
/// in - the words before the link, the link, and the words after it.
///
/// <b>One record rather than a string and three properties beside it</b>, because the line and its
/// pieces are the same words and a reader of either must get the other for free. <see cref="Notice"/>
/// is the pieces joined, never a fourth string.
/// </summary>
/// <param name="BeforeLink">Every note up to and including the first half of the folded sentence, or the whole line when nothing folds.</param>
/// <param name="Link">The words that press the switch - empty when nothing folds, and then nothing is drawn as a link.</param>
/// <param name="AfterLink">The second half of the folded sentence and whatever note follows it.</param>
internal sealed record Admitted(string BeforeLink, string Link, string AfterLink)
{
    internal static readonly Admitted Nothing = new(string.Empty, string.Empty, string.Empty);

    /// <summary>The whole line, exactly as a TextBlock drawing the three pieces would read it back.</summary>
    public string Notice => BeforeLink + Link + AfterLink;

    /// <summary>Whether there is anything to press - what enables the link, so an empty one is never a Tab stop.</summary>
    public bool HasLink => Link.Length > 0;
}
