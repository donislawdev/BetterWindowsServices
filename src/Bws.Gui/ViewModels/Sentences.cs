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
    internal const string HideDrivers = "!type:driver";

    /// <summary>
    /// Everything this answer is not, in sentences.
    ///
    /// The order is deliberate: what was never read comes first, because it is the sentence
    /// that explains an empty list, and somebody staring at one should not have to read past
    /// anything to find out why.
    /// </summary>
    internal static string Admissions(Query query, bool held, int unreadable, int tooCostly)
    {
        var needs = query.Needs;
        var notes = new List<string>();

        // This window reads what a listing reads and no more. The command line answers a
        // question about signatures by going and verifying them, measured at 1100-1245 ms over
        // 810 entries and 544 files - a price a listing pays once and a search box cannot pay
        // on every keystroke. Doing it in the background is its own slice after S6c.
        if (needs.HasFlag(ExtraRead.Signatures))
        {
            notes.Add(Texts.Of("gui.query.unreadSignatures"));
        }

        if (needs.HasFlag(ExtraRead.Memory))
        {
            notes.Add(Texts.Of("gui.query.unreadMemory"));
        }

        // Suppressed when the query asked about something nobody has read, and this is a
        // choice rather than an oversight. Both cases arrive as one count, and the sentence
        // below says the machine refused - which for an unread family would turn "nobody
        // looked" into "you were not allowed", the one distinction this project spends most of
        // its rules keeping apart. The sentence above already says what happened.
        if (unreadable > 0 && needs == ExtraRead.None)
        {
            notes.Add(Texts.Of("gui.status.partial", unreadable));
        }

        if (tooCostly > 0)
        {
            notes.Add(Texts.Of("gui.status.tooCostly", tooCostly));
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

    internal static string WithHiddenDrivers(string text)
    {
        var trimmed = text.TrimEnd();

        return trimmed.Length == 0 ? HideDrivers : trimmed + " " + HideDrivers;
    }

    /// <summary>
    /// Takes the exclusion off the end, and leaves everything else exactly as it was typed.
    ///
    /// Cut at whitespace and nowhere else, so a quoted value earlier in the line is not so
    /// much as looked at. Case is folded because Windows folds it everywhere else in this
    /// language - the spelling this recognises is the one the switch itself writes.
    /// </summary>
    internal static string WithoutHiddenDrivers(string text)
    {
        var trimmed = text.TrimEnd();
        var lastGap = trimmed.LastIndexOfAny([' ', '\t', '\n', '\r']);
        var tail = trimmed[(lastGap + 1)..];

        if (!string.Equals(tail, HideDrivers, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        return lastGap < 0 ? string.Empty : trimmed[..lastGap].TrimEnd();
    }
}
