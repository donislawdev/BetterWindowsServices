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
public sealed partial class MainViewModel : Observable
{
    // THREE CONSTANTS STOOD HERE AND ALL THREE ARE GONE, 2026-08-11. HideDrivers was declared,
    // documented and referenced by nothing - the same shape as QueryValueReader.ReadExpressionValue
    // found the same day, and for the same reason: nothing points at an unused private constant,
    // so it survives every build and every test. DriverField and DriverValue followed the switch
    // into FilterChips, where the chip that stood for them lived.
    //
    // AND THE PARAGRAPH THAT STOOD ABOVE THIS ONE IS GONE TOO, 2026-08-19, BECAUSE IT HAD BECOME
    // FALSE. It said the drivers switch IS a member of the query rather than a state beside it -
    // owner's decision of 2026-08-02 - and that was true for seventeen days. The owner reversed it:
    // scope has a state of its own now, the box says only what somebody asked, and the field and
    // the value live in Scopes.cs. `docs/07` carries both decisions and which one is current.
    //
    // Kept as a note rather than deleted silently, because a comment describing a mechanism that
    // no longer exists is read as a description of one that does.

    /// <summary>How long a row stays marked as having just moved. Lives with the rows it marks.</summary>
    internal static TimeSpan HighlightFor => RowIndex.HighlightFor;


    /// <summary>Whether the list may rearrange itself right now, and what it owes if it may not.</summary>
    private readonly Holding _holding = new();

    /// <summary>Every row that exists, kept in step with the machine.</summary>
    private readonly RowIndex _index;


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

    /// <summary>
    /// What the box says before anybody types, which since 2026-08-19 is NOTHING.
    ///
    /// It opened with <c>!type:driver</c> in it for six days, because hiding drivers by default had
    /// to be a filter the window admitted to. The scope switch admits to it better - the position
    /// the window stands on says which list this is - so the box goes back to being only what
    /// somebody asked. <see cref="Scopes.Opening"/> carries the whole argument.
    /// </summary>
    private string _queryText = string.Empty;

    /// <summary>
    /// The controls standing for members of the query. Declared after the text they read,
    /// because they close over it.
    /// </summary>
    private readonly FilterBar _filters;

    /// <summary>Which list is showing, and that listing already cut down. Not a filter.</summary>
    private readonly Scoping _scoping;

    /// <summary>
    /// Whether the session copies stand on their own rather than under the template they came from.
    /// State beside the query rather than a member of it - <see cref="ShowingEveryInstance"/>.
    /// </summary>
    private bool _showingEveryInstance;


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
        // constructor has run - see the argument on Readings._look. The last argument is fetched
        // for the same reason and a second one: TWO things can ask the second phase for something
        // and one of them is the picker, which the window builds after this - MainViewModel.Asking.
        _readings = new Readings(
            catalog, _index, () => Says, Reread, TellTheList, inspector, reader, () => Asked);

        // Reading the field and writing the property, which is deliberate and is the difference
        // between a chip that filters and a chip that only edits text: the setter is what parses
        // the query, applies it and tells the list, so a chip goes through the same door a
        // keystroke does. Writing the field instead would change the box and leave the list
        // showing the answer to the previous question.
        _filters = new FilterBar(() => _queryText, text => QueryText = text);

        _scoping = new Scoping(() => _index.Ordered);

        // Reading the state and writing the PROPERTY, for the same reason the chips do it one line
        // above: the setter is what reapplies the query, lets go of the selection and tells the
        // list. Writing the scope directly would move the switch and leave the window showing the
        // other list.
        ScopePositions = Scopes.Positions(() => _scoping.Current, scope => Scope = scope);
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
    public Planned Planned { get; init; } = new();


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
    /// <b>Clearing no longer shows drivers, and that changed on 2026-08-19.</b> The exclusion used
    /// to live in this text, so emptying the box gave back the whole machine. It lives in
    /// <see cref="Scope"/> now, so Escape empties the question and leaves the window on the list it
    /// was on - which is what a person pressing it means, and the switch is the thing to press when
    /// they mean the other one.
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
    public async Task LoadAsync()
    {
        await _readings.LoadAsync().ConfigureAwait(true);

        // THE MACHINE OVERVIEW IS COUNTED FROM THE LISTING, AND THE LISTING ARRIVES AFTER THE WINDOW
        // DOES. Without this line every number on that screen is zero and stays zero: it is worked
        // out when the screen is asked for, the window asks for it in its constructor, and the first
        // reading is still out at that point. Found by opening the window rather than by reasoning -
        // six zeroes on a machine with 798 entries.
        //
        // ON A FULL READING RATHER THAN ON EVERY TICK, which is the same restraint `A10` puts on the
        // list itself. The cheap reading moves a status once a second, and rebuilding six buttons
        // under somebody's pointer to change one of them is the shape that rule exists to prevent.
        // This screen is what the machine looked like when it was asked - startup, F5, and after a
        // plan has been carried out, which are the three moments this method runs.
        if (_showingOverview)
        {
            Raise(nameof(Overview));
            Raise(nameof(OverviewFindings));
        }
    }

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
    ///
    /// <b>Three more facts since 2026-08-19, all of them about the scope, and all of them decided
    /// HERE rather than in the sentence.</b> How many entries the list holds tells an empty list
    /// apart from a query that matched nothing, which scope it is decides which of the two lists to
    /// name, and whether the query asks for what the scope leaves out is the one state the drivers
    /// switch could not reach while it was the query. <see cref="ListState"/> takes them already
    /// answered, which is the arrangement its own comment argues for at length.
    /// </summary>
    private void TellTheList() =>
        Says.AboutTheList(
            _readings.FirstLook,
            _readings.Failed,
            Rows.Count,
            _index.Ordered.Count,
            _scoping.InScope.Count,
            _scoping.Current,
            Scopes.AsksElsewhere(_scoping.Current, _queryText));

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

        // AGAINST THE WHOLE LISTING RATHER THAN AGAINST WHAT THE QUERY LEFT, which is why it is
        // asked here and not after the narrowing. A row leaves the visible list on almost every
        // keystroke, and an open panel calling that "this entry is gone" would be the window being
        // confidently wrong about a service that is running.
        //
        // THE WHOLE LISTING RATHER THAN THE SCOPE, and that stayed true when the scope arrived. A
        // panel open on a service is open on something that exists, whichever list is on screen -
        // asking this of the scope would have moving the switch declare the entry gone.
        Chosen.StillIn(_index.Ordered);

        // THE SCOPE FIRST AND THE QUESTION INSIDE IT, which is the whole shape of the change made
        // on 2026-08-19. Everything below counts against what the scope left rather than against
        // the machine, so a narrowed list of drivers reads "12 of 472 entries" - the second number
        // is the list somebody is looking at, and against the machine's 812 it would answer a
        // question nobody asked.
        var everything = _scoping.InScope;

        var narrowed = Narrowing.Of(_query, everything);

        // AFTER THE QUERY AND NEVER BEFORE IT, WHICH IS THE ONE ORDER THAT KEEPS BOTH HONEST. The
        // query decides which ENTRIES the answer holds and the fold decides how many ROWS that same
        // answer is drawn as. Folding first would hide a session's copy from `peruser:instance`,
        // which is the query that exists to find them - and the window's answer would stop being
        // the command line's answer to the same text, silently, in one interface out of two.
        var rolled = Folding.Of(narrowed.Selected, _showingEveryInstance);

        Show(rolled.Rows);

        // A SINGULAR BESIDE EVERY PLURAL, backlog 207. The noun follows the SECOND number rather
        // than the first - "1 of 810 entries" is right and "1 of 810 entry" is not - so both lines
        // below ask the same question about the size of the list being counted.
        //
        // WRITTEN OUT RATHER THAN PICKING A KEY INTO A VARIABLE, which is the arrangement
        // Sentences.Admissions carries the same note about: TextKeyGuards checks that every
        // declared sentence reaches a screen, and a key travelling as a variable is invisible to
        // it, so both halves of a pair would read as orphans.
        Says.Status = narrowed.Selected.Count == everything.Count
            ? everything.Count == 1
                ? Texts.Of("gui.status.read.one", everything.Count)
                : Texts.Of("gui.status.read.many", everything.Count)
            : everything.Count == 1
                ? Texts.Of("gui.status.matched.one", narrowed.Selected.Count, everything.Count)
                : Texts.Of("gui.status.matched.many", narrowed.Selected.Count, everything.Count);

        // The last argument is which screen has the middle of the window, and only one sentence
        // under there asks about it - backlog 263. Passed rather than read out of this class by
        // Sentences, because that class has never been allowed to know a window exists.
        Says.AboutTheAnswer(
            Asked, _holding.Pending, narrowed.Unreadable, narrowed.TooCostly,
            _readings.Have, _readings.Filling, rolled.Instances, !_showingOverview);

        TellTheList();

        // The controls read the query again, all of them - see FilterBar.Rethink for why every
        // one rather than the one that was clicked.
        _filters.Rethink();
    }

    /// <summary>
    /// Cuts the listing down to the scope, and keeps the result.
    ///
    /// <b>By asking the query language rather than by testing a type here.</b> What counts as a
    /// driver is decided once, in the language, and `docs/07` records that <c>type:driver</c>
    /// covers both kinds deliberately because Windows has two and no word for both. A check on
    /// <see cref="Bws.Core.EntryType"/> written here would be a second answer to that, in the
    /// window, drifting from the first in silence - see <see cref="Scopes"/>.
    ///
    /// <b>Everything skips the narrowing entirely rather than running an empty query over 812
    /// rows</b>, which is the one case where the answer is known without asking.
    /// </summary>
    /// <summary>
    /// What the readings call when the machine has been asked again.
    ///
    /// <b>The scope is recut here and NOT in <see cref="Apply"/>, and the split is the whole reason
    /// this method exists.</b> Apply runs on every keystroke against a 50 ms budget, and the scope
    /// cannot change while somebody types - so recutting it there would pay for a second pass over
    /// 812 entries per character, on exactly the path that was measured and fixed on 2026-08-19.
    /// It CAN change here, because an entry that arrived or left changes what the scope holds, and
    /// a list that skipped this would go on showing a service the machine no longer has.
    /// </summary>
    private void Reread()
    {
        _scoping.Recut();
        Apply();
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
}
