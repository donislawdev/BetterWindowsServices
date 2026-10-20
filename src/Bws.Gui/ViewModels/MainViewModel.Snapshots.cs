using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Snapshots from the window - taking one, and comparing one with this machine (`D1`, `D2`, since
/// 2026-10-14).
///
/// <b>A partial holding what the list's model needs to know about them, which is little.</b> The work
/// and its state are <see cref="ViewModels.Snapshotting"/>, which knows nothing about the list, the
/// query or the reading - and this class, which is about the list, stood on the size ratchet's
/// allowance for a long file. Made in the constructor from the same manager, clock and inspector the
/// list reads through, so a test hands one fake machine to both.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Taking and comparing snapshots, and the comparison when one is on screen.</summary>
    public Snapshotting Snapshotting { get; }

    /// <summary>
    /// Whether the list is what the middle of the window shows - neither the overview nor a comparison
    /// is standing in front of it.
    ///
    /// <b>One question where there used to be "is the overview up"</b>, asked in three places that were
    /// all really asking this: whether the line under the window may speak about the list, whether
    /// Ctrl+F may open the suggestions under a box nobody can see, and whether the box may take the
    /// keyboard. A comparison hides the list exactly as the overview does, so each of the three would
    /// otherwise have answered wrongly while one is up. The form before a snapshot (W2-d) hides it too,
    /// and <see cref="Snapshotting.InTheMiddle"/> answers for both. The change history (J2) hides it
    /// the same way, and <see cref="HistoryReading.InTheMiddle"/> answers for it.
    /// </summary>
    public bool ListOnScreen => !_showingOverview && !Snapshotting.InTheMiddle && !History.InTheMiddle;

    /// <summary>
    /// Escape's middle step: a comparison or the change history standing where the list was goes first,
    /// then the panel about one entry - true when any of them went. Only one view ever stands where the
    /// list was, so the order of the first two decides nothing.
    ///
    /// <b>Here rather than as two more links in the window's chain</b>, because the method holding that
    /// chain counts every <c>||</c> against the shape guard's ceiling and stood one under the line it
    /// calls near. The order is the same argument the chain makes: the more recent thing somebody
    /// opened goes first, and a comparison replaces the list the panel sits beside.
    ///
    /// <b>THE SEARCH OVER THE VIEW'S TABLE GOES BEFORE THE VIEW, since 2026-10-20 (AS-a).</b> On the list
    /// the query is the last thing Escape takes, because it outlives the panel. A view's search does not
    /// outlive the view - the sheet goes with it - so the cheaper loss comes first: the search, then on
    /// the next press the view.
    /// </summary>
    internal bool PutAwayAView() => MiddleSearchEmptied() || Snapshotting.PutAway() || History.PutAway() || Chosen.Hide();

    /// <summary>
    /// The choices over the table standing where the list was - the comparison's or the change history's -
    /// or nothing while the list, the overview or the form before a snapshot is there. Only one view ever
    /// stands there, so at most one of the two answers.
    /// </summary>
    internal IRowChoices? MiddleChoices => (IRowChoices?)Snapshotting.Shown?.Filter ?? History.Shown?.Filter;

    /// <summary>
    /// Empties the search over the table in the middle - true when there was one to empty.
    ///
    /// <b>Only a search somebody can see</b>, since a review of #82: a history read again and found
    /// empty keeps what was typed, while the row holding the box is gone (<see cref="IRowChoices.Offered"/>).
    /// Emptying it there spent a press on nothing visible, and the view went only on the second. The
    /// condition is Ctrl+F's own (Finding.Begin).
    /// </summary>
    private bool MiddleSearchEmptied()
    {
        if (MiddleChoices is not { Offered: true, SearchText.Length: > 0 } choices)
        {
            return false;
        }

        choices.SearchText = string.Empty;

        return true;
    }

    /// <summary>
    /// The comparison's state, wired to the line under the window: when a comparison comes or goes, the
    /// sentence about the list is said again, because whether it may speak has just changed.
    /// </summary>
    private Snapshotting Comparing(IScmCatalog catalog, IBinaryInspector? inspector, IClock clock)
    {
        var snapshotting = new Snapshotting(catalog, inspector, clock, () => Says);

        snapshotting.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName is nameof(ViewModels.Snapshotting.Shown) or nameof(ViewModels.Snapshotting.Form))
            {
                SayTheAnswer();
            }
        };

        return snapshotting;
    }
}
