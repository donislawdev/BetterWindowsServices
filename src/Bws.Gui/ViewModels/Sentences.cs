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
    internal static string Admissions(
        Query query, bool held, int unreadable, int tooCostly, bool elevated,
        ExtraRead have, bool filling)
    {
        var needs = query.Needs;
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

        // Never silent about holding still. A list that quietly stopped matching its own query
        // while somebody leant on it would be the same silence rule 8 forbids, arriving from
        // the one direction where it looks like politeness.
        if (held)
        {
            notes.Add(Texts.Of("gui.status.holding"));
        }

        return string.Join(" ", notes);
    }

}
