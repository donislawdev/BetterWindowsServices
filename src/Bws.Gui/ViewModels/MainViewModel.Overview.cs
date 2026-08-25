namespace Bws.Gui.ViewModels;

/// <summary>
/// The machine overview: whether it is on screen, what it says, and what leaving it means - `G`.
///
/// <b>A partial for the same reason <see cref="MainViewModel.Scope"/> and
/// <see cref="MainViewModel.ShowingEveryInstance"/> are ones</b> - the rule itself is already a
/// class that knows nothing about a window, <see cref="Overview"/>, and what is here is what has to
/// happen to the REST of the model when this state moves.
///
/// <b>IT REPLACES THE LIST RATHER THAN SITTING OVER IT - owner's decision, 2026-08-25.</b> The
/// specification asks for "nie surowa liste alfabetyczna, tylko krotkie podsumowanie", and a band
/// above the list is still a list as the first thing on screen. The list is behind it and comes
/// back the moment anybody asks a question.
///
/// <b>AND THERE IS A WAY BACK, WHICH THE SPECIFICATION DOES NOT MENTION AND WHICH IT NEEDS.</b> The
/// preferences file is written when the window closes, so without one this screen would appear once
/// in the life of a profile - and the person it is written for is an administrator opening the tool
/// on their twentieth unknown server, who wants it every time.
/// </summary>
public sealed partial class MainViewModel : Observable
{
    private bool _showingOverview;

    /// <summary>
    /// Whether the overview is on screen instead of the list.
    ///
    /// <b>Set by the window from the kept file at startup, and by a person before and after.</b> It
    /// is not read from anywhere inside this class, because "has this profile seen it" is a fact
    /// about a file and this class has never been allowed to know about one.
    /// </summary>
    public bool ShowingOverview
    {
        get => _showingOverview;

        set
        {
            if (_showingOverview == value)
            {
                return;
            }

            _showingOverview = value;

            // The numbers are worked out when the screen appears rather than kept in step with the
            // machine. Measured: narrowing costs 0.42-2.25 ms over 810 entries and there are six of
            // them, so the whole screen is under 15 ms - and a screen recomputed once a second
            // would be six passes per tick for a panel nobody is looking at most of the time.
            Raise(nameof(Overview));
            Raise(nameof(ShowingOverview));
        }
    }

    /// <summary>
    /// What the overview says right now, worked out when it is asked for.
    ///
    /// <b>Against every row the window holds rather than against the scope</b>, and each line
    /// carries its own <c>!type:driver</c> instead - see <see cref="ViewModels.Overview.Of"/>. The
    /// scope is a state somebody can have moved before opening this, and a summary of the machine
    /// that changed depending on which tab was last clicked would be a summary of a tab.
    /// </summary>
    public IReadOnlyList<OverviewLine> Overview =>
        _showingOverview ? ViewModels.Overview.Of(_index.Ordered) : [];

    /// <summary>The number `G` promises and this build cannot count - <see cref="ViewModels.Overview.Missing"/>.</summary>
    public string OverviewMissing => ViewModels.Overview.Missing;

    /// <summary>
    /// Asks the question one of the numbers stands for, and puts the overview away.
    ///
    /// <b>The order is the decision.</b> The query goes in first, so the list behind the overview is
    /// already the answer by the time it becomes visible - a person clicking "1 service did not come
    /// up" never sees the whole machine flash past on the way to one row.
    ///
    /// <b>It writes the QUERY rather than filtering by some other route</b>, which is `A5`'s promise
    /// applied to a screen that is not made of chips: the filter you clicked is a query you can
    /// read, edit and learn from.
    /// </summary>
    public void Ask(OverviewLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        QueryText = line.Query;

        ShowingOverview = false;
    }
}
