using System.Collections.ObjectModel;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the window shows and how it got there.
///
/// Every reading runs off the interface thread, because a full one takes about half a second
/// over 810 entries and a window that stops answering for half a second looks broken.
/// `docs/06`, part 4: nothing from a worker thread touches the interface, so what comes back
/// is a plain list and everything the window binds to is built here.
///
/// Knows nothing about WPF beyond the notification interface. That is what makes it possible
/// to check what the window will show without opening one - including the parts that are
/// hardest to look at by hand, like what a list does to the row under somebody's cursor while
/// it refreshes itself.
/// </summary>
public sealed class MainViewModel : Observable
{
    /// <summary>
    /// The member the drivers switch stands for, as a person would write it.
    ///
    /// The switch <b>is</b> this member rather than a state beside the query - owner's
    /// decision, 2026-08-02, closing the open question at the end of `docs/07`. Turning it off
    /// writes these words into the box where they can be seen, which is the same promise `A5`
    /// makes about clickable filters: the query is what you are looking at.
    /// </summary>
    // THREE CONSTANTS STOOD HERE AND ALL THREE ARE GONE, 2026-08-11. HideDrivers was declared,
    // documented and referenced by nothing - the same shape as QueryValueReader.ReadExpressionValue
    // found the same day, and for the same reason: nothing points at an unused private constant,
    // so it survives every build and every test. DriverField and DriverValue followed the switch
    // into FilterChips, where the chip that stands for them lives.
    //
    // The member is still written into the box when the filter goes on. It is composed from the
    // field and the value rather than spelled anywhere, which is why there is nothing left here.

    /// <summary>How long a row stays marked as having just moved. Lives with the rows it marks.</summary>
    internal static TimeSpan HighlightFor => RowIndex.HighlightFor;


    /// <summary>Whether the list may rearrange itself right now, and what it owes if it may not.</summary>
    private readonly Holding _holding = new();

    /// <summary>Every row that exists, kept in step with the machine.</summary>
    private readonly RowIndex _index;

    /// <summary>
    /// The manager, kept because a plan asks it who breaks.
    ///
    /// <b>Held here as well as inside <see cref="Readings"/>, and that is one object with two
    /// readers rather than two copies of anything.</b> Reading the listing belongs to Readings.
    /// Working out what an operation would do is a different question asked at a different moment -
    /// when somebody opens a preview - and it is this class the window talks to.
    /// </summary>
    private readonly IScmCatalog _catalog;

    /// <summary>What the machine said, and whether anybody is still asking. Backlog 198.</summary>
    private readonly Readings _readings;

    /// <summary>
    /// The last query that parsed, which is not always the last query that was typed.
    ///
    /// Holding the last good one is what makes "a query with a mistake in it filters nothing"
    /// mean in a window what `docs/07` says it means: the list stays as it was and the mistake
    /// is reported beside it. In a terminal the same sentence means the opposite - print
    /// nothing - because a terminal writes into pipes.
    /// </summary>
    private Query _query = QueryParser.Parse(null).Query!;

    /// <summary>What the box says before anybody types - <see cref="FilterChips.OpeningQuery"/>.</summary>
    private string _queryText = FilterChips.OpeningQuery;

    /// <summary>
    /// The controls standing for members of the query. Declared after the text they read,
    /// because they close over it.
    /// </summary>
    private readonly FilterBar _filters;




    public MainViewModel()
        : this(
            new WindowsScmCatalog(),
            new SystemClock(),
            // Skip, exactly as the command line defaults, because a launch path on a share can
            // hang on a machine that cannot reach it - and this one runs on every full reading.
            new WindowsBinaryInspector(NetworkPaths.Skip),
            new WindowsProcessMemoryReader())
    {
    }

    /// <summary>
    /// The manager and the clock are handed in so a test can decide what both say - including
    /// the cases a real machine will not produce on demand: a refusal, an empty machine, a
    /// service that stops between one second and the next, three seconds passing at once.
    /// </summary>
    public MainViewModel(
        IScmCatalog catalog,
        IClock clock,
        IBinaryInspector? inspector = null,
        IProcessMemoryReader? reader = null)
    {
        _index = new RowIndex(clock);
        _catalog = catalog;

        // Says is fetched rather than handed over, because a caller may replace it after this
        // constructor has run - see the argument on Readings._look.
        _readings = new Readings(
            catalog, _index, () => Says, Apply, TellTheList, inspector, reader, () => _query.Needs);

        // Reading the field and writing the property, which is deliberate and is the difference
        // between a chip that filters and a chip that only edits text: the setter is what parses
        // the query, applies it and tells the list, so a chip goes through the same door a
        // keystroke does. Writing the field instead would change the box and leave the list
        // showing the answer to the previous question.
        _filters = new FilterBar(() => _queryText, text => QueryText = text);
    }

    /// <summary>
    /// What the list shows: the entries this query selected, in the manager's own order.
    ///
    /// One collection for the life of the window, changed in place. Replacing it would drop
    /// the selection and send the scroll back to the top on every refresh, which is the one
    /// thing `A10` names first.
    /// </summary>
    public RowList Rows { get; } = [];

    /// <summary>
    /// Everything the window says about this answer - the count, the two admissions, and the
    /// sentence in the middle of an empty list.
    ///
    /// Its own object since 2026-08-05, when the empty states arrived and the size ratchet asked
    /// for a fourth seam. Choosing which sentence applies is reasoning about words, and the rest
    /// of this class is about rows, queries and threads.
    /// </summary>
    public Says Says { get; init; } = new();

    /// <summary>
    /// What an operation over the selection would do, and whether that is on screen.
    ///
    /// <b>Beside <see cref="Chosen"/> rather than inside it, because they answer about different
    /// things on different schedules</b> - one entry somebody opened with Enter, and a selection
    /// somebody asked a question about. They share a column in the window and are mutually exclusive
    /// there, which the window arranges rather than either of them knowing about the other.
    /// </summary>
    public Planned Planned { get; } = new();


    /// <summary>
    /// The one field: free search, regular expressions and the query language, exactly as
    /// <c>A1</c> to <c>A3</c> ask for and exactly as the command line takes it.
    ///
    /// Filtering happens on the way in, on every keystroke. A member that is half typed is not
    /// a mistake and never reddens anything - the parser drops a member with nothing after the
    /// colon - because a box that shows an error for most of the time somebody is typing
    /// teaches them to ignore it.
    /// </summary>
    public string QueryText
    {
        get => _queryText;
        set
        {
            if (Set(ref _queryText, value ?? string.Empty))
            {
                // Asking for something else puts away what the last action could not do. It is
                // the only signal available that the person has moved on - and a refusal that
                // stayed while they typed would end up describing a list it no longer refers to.
                Says.Moved();

                Apply();
            }
        }
    }

    /// <summary>
    /// The entry the window is looking at, and everything it says about that one entry.
    ///
    /// <b>Its own object since 2026-08-13, and the size ratchet is what asked.</b> This class stood
    /// at exactly five hundred lines with every allowance for a long file spent, so the details
    /// panel could not add a property here - and the seam that found is real: this class is about
    /// the LIST, and none of the questions about one chosen row are.
    /// </summary>
    public Chosen Chosen { get; } = new();

    /// <summary>
    /// The next entry beginning with a character, after the one chosen now. Backlog 151.
    ///
    /// One line, because the search belongs to the collection and only the selection belongs here.
    /// <see cref="RowList.NextStartingWith"/> carries the reasoning.
    /// </summary>
    public EntryRow? NextStartingWith(char letter) => RowList.NextStartingWith(Rows, Chosen.Row, letter);

    /// <summary>
    /// Empties the query, which is what Escape asks for - `docs/11` 9.1.
    ///
    /// <b>Answers whether it did anything, and the window needs that answer rather than a
    /// courtesy.</b> A key press swallowed by something that decided to do nothing is a key that
    /// stops working further up, and Escape is the one key every dialog and every window in
    /// Windows already has an opinion about. So an empty box leaves the press alone.
    ///
    /// Clearing shows drivers again, because the exclusion lives in this text and nowhere else -
    /// which is <see cref="ShowDrivers"/> keeping its promise rather than a side effect.
    /// </summary>
    public bool ClearQuery()
    {
        if (_queryText.Length == 0)
        {
            return false;
        }

        QueryText = string.Empty;

        return true;
    }

    /// <summary>
    /// The controls standing for members of the query - the chips of `A5`, and the named drivers
    /// switch of `A7` which turned out to be one of them.
    ///
    /// <b>Moved out of this class on 2026-08-11 because the size ratchet asked, and the seam it
    /// found was real.</b> What was here was the chips, the switch, and the loop telling both to
    /// read the query again at the end of every <see cref="Apply"/> - and none of that is what
    /// this class is about. It is the fourth seam this file has given up the same way, after
    /// <see cref="Holding"/>, <see cref="RowIndex"/> and <see cref="Narrowing"/>.
    ///
    /// The two below forward rather than reimplement, so the window and the tests keep the names
    /// they had.
    /// </summary>
    public IReadOnlyList<FilterChip> Filters => _filters.Chips;

    /// <inheritdoc cref="FilterBar.Groups"/>
    public IReadOnlyList<FilterGroup> FilterGroups => _filters.Groups;

    /// <summary>Questions somebody can start from, each one a query they can then edit.</summary>
    public IReadOnlyList<QueryExample> Examples => QueryExamples.All;

    /// <summary>What the search box says to somebody pointing at it - <see cref="QueryExamples.Tip"/>.</summary>
    public string SearchTip => QueryExamples.Tip(Examples);

    /// <inheritdoc cref="FilterBar.ShowDrivers"/>
    public bool ShowDrivers
    {
        get => _filters.ShowDrivers;
        set => _filters.ShowDrivers = value;
    }

    /// <summary>
    /// Whether somebody is using the list right now - pointing at it, or with the keyboard in it.
    ///
    /// The window's door onto <see cref="Holding"/>, which carries the rule itself and why it has
    /// the shape it has. Set by the window, because only a window knows about focus and a mouse.
    /// </summary>
    public bool Interacting
    {
        get => _holding.Interacting;
        set
        {
            if (_holding.Interacting == value)
            {
                return;
            }

            _holding.Interacting = value;
            Raise(nameof(Interacting));

            if (!value && _holding.Pending)
            {
                // Whatever was held back happens now, in one go.
                Apply();
            }
        }
    }


    /// <summary>
    /// Reads the machine in full and fills the list. The first reading, and whatever F5 asks for.
    ///
    /// Handed straight on, because the window binds to this class and the state machine behind it
    /// is not something a window should have to know the name of.
    /// </summary>
    public Task LoadAsync() => _readings.LoadAsync();

    /// <summary>One tick of the live list: asks what is running and moves whatever moved.</summary>
    public Task RefreshAsync() => _readings.RefreshAsync();

    /// <summary>
    /// Takes the highlight off the rows that have worn it long enough. Driven by the same tick
    /// that refreshes, because it has to keep happening while nothing is moving.
    /// </summary>
    public void FadeHighlights() => _index.Fade();


    /// <summary>
    /// Tells the empty middle of the window what the list is doing now.
    ///
    /// It lives here rather than in <see cref="Readings"/> because it needs both halves: what the
    /// machine said, and how many rows came through the query onto the screen.
    /// </summary>
    private void TellTheList() =>
        Says.AboutTheList(_readings.FirstLook, _readings.Failed, Rows.Count, _index.Ordered.Count);

    /// <summary>
    /// Reads the query and narrows the list to what it selects.
    ///
    /// Runs on the interface thread on every keystroke, which is the whole reason the query
    /// language is compiled once and never reaches out to the system while it is being asked.
    /// The budget is 50 ms over the whole listing, from section 8.1 of the specification.
    /// </summary>
    private void Apply()
    {
        // BeingTyped, and the window is the only caller that asks for it. This runs on every
        // keystroke, so a member with nothing after its colon is somebody mid-word rather than
        // somebody making a mistake - and a box that shows an error through most of the typing
        // teaches people to ignore the box. The command line says Finished, because by the time
        // text reaches it there is no later.
        var parsed = QueryParser.Parse(
            _queryText, input: QueryInput.BeingTyped);

        if (!parsed.IsValid)
        {
            // Every complaint, not the first one. Two mistakes in one query is ordinary while
            // somebody is typing, and fixing one to be told about the next is a poor trade for
            // a shorter line.
            Says.AboutTheQuery(string.Join(" ", parsed.Problems.Select(QueryMessages.Of)));

            return;
        }

        Says.AboutTheQuery(string.Empty);
        _query = parsed.Query!;

        var everything = _index.Ordered;

        // AGAINST THE WHOLE LISTING RATHER THAN AGAINST WHAT THE QUERY LEFT, which is why it is
        // asked here and not after the narrowing. A row leaves the visible list on almost every
        // keystroke, and an open panel calling that "this entry is gone" would be the window being
        // confidently wrong about a service that is running.
        Chosen.StillIn(everything);

        var narrowed = Narrowing.Of(_query, everything);

        Show(narrowed.Selected);

        Says.Status = narrowed.Selected.Count == everything.Count
            ? Texts.Of("gui.status.read", everything.Count)
            : Texts.Of("gui.status.matched", narrowed.Selected.Count, everything.Count);

        Says.AboutTheAnswer(
            _query, _holding.Pending, narrowed.Unreadable, narrowed.TooCostly,
            _readings.Have, _readings.Filling);

        TellTheList();

        // The controls read the query again, all of them - see FilterBar.Rethink for why every
        // one rather than the one that was clicked. Raised here as well because the window binds
        // the switch through this class rather than through the bar.
        _filters.Rethink();
        Raise(nameof(ShowDrivers));
    }

    /// <summary>
    /// Makes the visible list match what the query selected, unless it is being held.
    ///
    /// Two collaborators and neither of them is this class, which is the point of the shape.
    /// <see cref="Holding"/> decides WHETHER, because that needs `A10` and what a window knows.
    /// <see cref="RowList.Reconcile"/> decides HOW, because that is a collection turning into
    /// another collection without losing the objects in it.
    /// </summary>
    private void Show(List<EntryRow> selected)
    {
        if (_holding.MayRearrange(Rows, selected))
        {
            Rows.Reconcile(selected);
        }
    }

    /// <summary>
    /// What an operation over a selection would do. Works it out and changes nothing.
    ///
    /// <b>On the calling thread, and that is a measurement rather than an oversight.</b> Building a
    /// plan asks the manager who depends on each entry, so the obvious worry is that a selection
    /// costs a question per entry per cascade member. Measured on this machine on 2026-08-18 through
    /// the command line, five runs each with the first discarded: a plan with a thirteen member
    /// cascade took 316-360 ms end to end and the same plan with no cascade at all took 325-366 ms.
    /// The spread is wider than the difference, so by this project's own rule there is no
    /// difference - the whole figure is process start and the reading of 810 entries.
    ///
    /// So no background work, no cancellation and no generation counter. That is the lesson of
    /// backlog 21 applied a second time: the machinery there turned out to answer a race that did
    /// not exist, and the limit was in a mechanism nobody had needed.
    ///
    /// <b>Every entry the window holds is handed in, never the rows on screen</b>, and the reason is
    /// at <see cref="RowIndex.Everything"/>: a plan looks entries up by name, so building one
    /// against a filtered set would silently shorten a cascade.
    /// </summary>
    internal BulkPlan Plan(BulkAction action) =>
        new BulkPlanBuilder(_index.Everything, _catalog).Build(action);
}
