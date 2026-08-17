namespace Bws.Gui.ViewModels;

/// <summary>
/// Which of the five things the list is doing, and the words for it.
///
/// <b>Complaint 9 of the eleven in `docs/11`, and it is the one the status line could not
/// answer.</b> That document asks for four screens rather than one - full, empty, loading,
/// error - and says why in a sentence worth keeping: an empty rectangle does not tell anybody
/// whether nothing matched or something broke. A line of grey text under the grid is read after
/// the grid, if at all.
///
/// <b>Five rather than four, and the fifth is a degenerate case that is perfectly legal.</b> A
/// machine that hands over no entries at all is not a query that matched nothing, and telling
/// somebody to clear their query when their query is empty would be the window being confidently
/// wrong. It is rare and it is cheap to say properly.
///
/// Pure, so the words a person will read can be checked without opening a window - which is the
/// same reason <see cref="Sentences"/> exists.
/// </summary>
internal enum ListFace
{
    /// <summary>There are rows. The ordinary case, and the only one with nothing to say.</summary>
    Rows,

    /// <summary>The first reading is still out. Later readings keep the rows they had.</summary>
    Loading,

    /// <summary>The query let nothing through. The filter works and nothing matched it.</summary>
    NothingMatched,

    /// <summary>The manager handed over no entries at all, which is not the same thing.</summary>
    NothingToShow,

    /// <summary>The reading failed outright. What went wrong is in the line under the list.</summary>
    Failed
}

/// <summary>What the middle of the window says when it has no rows to show, and the way out of it.</summary>
internal readonly record struct ListState
{
    /// <summary>Which of the five. <see cref="ListFace.Rows"/> means say nothing at all.</summary>
    public required ListFace Face { get; init; }

    /// <summary>The sentence, or empty when there are rows.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// One way out, or empty when there is none to offer.
    ///
    /// <b>Nielsen's third heuristic, which `docs/11` 9.2 names as this product's weakest.</b> It
    /// is also the only place in the window that says the two shortcuts exist: Escape empties the
    /// query and F5 reads again, both added the day before this, both invisible.
    /// </summary>
    public required string WayOut { get; init; }

    /// <summary>
    /// Works out which face the list is wearing.
    ///
    /// The order of the questions is the order of certainty, and it matters. A failed reading is
    /// asked about first because it is a fact about the machine rather than about the query - a
    /// window that answered "nothing matched" after the manager refused to open would be blaming
    /// the person for the machine.
    /// </summary>
    public static ListState Of(bool reading, bool failed, int shown, int everything)
    {
        if (shown > 0)
        {
            return new ListState { Face = ListFace.Rows, Message = string.Empty, WayOut = string.Empty };
        }

        if (failed)
        {
            return Say(ListFace.Failed, Texts.Of("gui.empty.failed"), Texts.Of("gui.empty.failedWayOut"));
        }

        // ONLY WHILE NOTHING HAS ARRIVED YET, AND THE SECOND HALF OF THAT WAS MISSING UNTIL
        // 2026-08-13 - owner's report. The comment here always said a reading over a list that has
        // already been read must not take the answer off the screen, and the condition did not do
        // it: the guard above only catches a list with rows ON SCREEN, which is exactly what a
        // query matching nothing does not have.
        //
        // What it looked like: type something that matches nothing, and the sentence saying so was
        // replaced by "reading the manager" and back again ONCE A SECOND, for as long as it stood
        // there. `A10` reads the machine every second, and every one of those readings raised and
        // lowered this flag.
        //
        // `everything` is what tells the two apart - it is how many entries this window has ever
        // seen, so a reading with entries already behind it is a refresh rather than the first
        // look. Nothing else here can distinguish them.
        if (reading && everything == 0)
        {
            return Say(ListFace.Loading, Texts.Of("gui.empty.loading"), string.Empty);
        }

        // The machine, not the query. Offering to clear an empty query would be the window
        // telling somebody to undo something they never did.
        if (everything == 0)
        {
            return Say(ListFace.NothingToShow, Texts.Of("gui.empty.nothingToShow"), string.Empty);
        }

        return Say(
            ListFace.NothingMatched,
            Texts.Of("gui.empty.nothingMatched"),
            Texts.Of("gui.empty.nothingMatchedWayOut"));
    }

    /// <summary>
    /// Builds the answer. Takes the words rather than their keys, and that is not a detail: a key
    /// travelling as a variable is invisible to the guard that checks every declared string is
    /// said somewhere, and to a person reading this method for what it shows.
    /// </summary>
    private static ListState Say(ListFace face, string message, string wayOut) => new()
    {
        Face = face,
        Message = message,
        WayOut = wayOut
    };
}
