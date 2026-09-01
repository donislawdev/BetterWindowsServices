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

            // A PASS RATHER THAN A REPAINT, because a sentence under the window depends on which
            // screen has the middle of it - backlog 263. The folded count is explained by a
            // sentence that means nothing while there are no rows, and the notice holding it is
            // composed inside the pass. Raising alone would leave that sentence standing over the
            // overview until the next tick, and missing from under the list for the same second on
            // the way back - AskEvery is one second.
            //
            // The same thing ShowingEveryInstance does one file over, and for the same reason: a
            // switch that changes what is DRAWN is a pass, not a repaint.
            //
            // NOT BEFORE THE WINDOW HAS READ ANYTHING, and that is a startup case rather than an
            // optimisation. This property is set from the kept file IN THE CONSTRUCTOR, before the
            // first reading - so an unguarded pass over an empty index would write "0 entries"
            // over the "reading" sentence for the whole of that read, measured at 749-822 ms, on
            // the very screen this exists for. FirstLook is the same fact the loading sentence is
            // decided by, asked here rather than invented a second time.
            if (!_readings.FirstLook)
            {
                Apply();
            }

            // The numbers are worked out when the screen appears rather than kept in step with the
            // machine. Measured: narrowing costs 0.42-2.25 ms over 810 entries and there are six of
            // them, so the whole screen is under 15 ms - and a screen recomputed once a second
            // would be six passes per tick for a panel nobody is looking at most of the time.
            Raise(nameof(Overview));
            Raise(nameof(OverviewFindings));
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
    /// <remarks>
    /// <c>counted</c> is what keeps this screen from claiming zero before it has read anything -
    /// backlog 263. The window opens on it in its constructor, so the first reading is still out
    /// while these lines are first asked for, and <see cref="LoadAsync"/> raises this again when it
    /// arrives - which is the same line that already existed to replace six zeroes with the real
    /// numbers, now also flipping the placeholder off.
    /// </remarks>
    public IReadOnlyList<OverviewLine> Overview =>
        _showingOverview ? ViewModels.Overview.Of(_index.Ordered, counted: !_readings.FirstLook) : [];

    /// <summary>
    /// The same lines as <see cref="Overview"/>, grouped the way the screen draws them - each
    /// headline number holding the qualifications written under it.
    ///
    /// <b>The panel binds to this and the guards read <see cref="Overview"/>, and that is on
    /// purpose.</b> This is a projection of that list rather than a second answer, so a test asking
    /// what the screen counts and a card drawing it cannot come apart - see
    /// <see cref="ViewModels.Overview.Findings"/>.
    ///
    /// <b>Raised beside Overview rather than instead of it.</b> Both are computed on demand, so the
    /// cost of the extra notification is one grouping pass over six lines on a screen somebody just
    /// switched to.
    /// </summary>
    public IReadOnlyList<OverviewFinding> OverviewFindings => ViewModels.Overview.Findings(Overview);

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
