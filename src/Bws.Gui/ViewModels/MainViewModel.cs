using System.Collections.ObjectModel;
using Bws.Core;
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
    private const string HideDrivers = Sentences.HideDrivers;

    private const string DriverField = "type";
    private const string DriverValue = "driver";

    /// <summary>How long a row stays marked as having just moved. Lives with the rows it marks.</summary>
    internal static TimeSpan HighlightFor => RowIndex.HighlightFor;

    private readonly IScmCatalog _catalog;

    /// <summary>Whether the list may rearrange itself right now, and what it owes if it may not.</summary>
    private readonly Holding _holding = new();

    /// <summary>Every row that exists, kept in step with the machine.</summary>
    private readonly RowIndex _index;

    /// <summary>
    /// The last query that parsed, which is not always the last query that was typed.
    ///
    /// Holding the last good one is what makes "a query with a mistake in it filters nothing"
    /// mean in a window what `docs/07` says it means: the list stays as it was and the mistake
    /// is reported beside it. In a terminal the same sentence means the opposite - print
    /// nothing - because a terminal writes into pipes.
    /// </summary>
    private Query _query = QueryParser.Parse(null).Query!;

    private string _queryText = string.Empty;
    private bool _bareWordsAreExpressions;
    private string _status = Texts.Of("gui.status.reading");
    private string _notice = string.Empty;
    private string _problem = string.Empty;
    private string _refusal = string.Empty;
    private bool _incomplete;
    private bool _reading;

    public MainViewModel()
        : this(new WindowsScmCatalog(), new SystemClock())
    {
    }

    /// <summary>
    /// The manager and the clock are handed in so a test can decide what both say - including
    /// the cases a real machine will not produce on demand: a refusal, an empty machine, a
    /// service that stops between one second and the next, three seconds passing at once.
    /// </summary>
    public MainViewModel(IScmCatalog catalog, IClock clock)
    {
        _catalog = catalog;
        _index = new RowIndex(clock);
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
                _refusal = string.Empty;

                Apply();
            }
        }
    }

    /// <summary>
    /// The row the person has chosen, or nothing.
    ///
    /// <b>Handed in at the moment it is needed rather than bound to the list, and that is a
    /// repair.</b> It was a two way binding on SelectedItem for one afternoon, and the window
    /// journey turned flaky inside it - passages reporting a grid that disagreed with its own
    /// count line, with the window saying it was holding still. A binding into a list that
    /// reconciles itself once a second is another party in the middle of `A10`, and nothing here
    /// needs it: the selection is only ever read when somebody asks for a copy.
    ///
    /// What it is for is that <b>every question about the chosen entry has an answer that can be
    /// checked without opening a window</b> - which is why the two below live here rather than in
    /// the handler that copies them.
    /// </summary>
    public EntryRow? Selected { get; set; }

    /// <summary>What a copy of the name would put on the clipboard, or nothing when no row is chosen.</summary>
    public string? SelectedServiceName => Selected?.ServiceName;

    /// <summary>The same for the display name, which is the one a person recognises.</summary>
    public string? SelectedDisplayName => Selected?.DisplayName;

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
    /// Whether a member without a field reads as a regular expression. The regex switch of
    /// <c>A2</c>, and the same word the parser uses for it.
    ///
    /// Only bare words change. <c>name:spool*</c> keeps its own operators either way.
    /// </summary>
    public bool BareWordsAreExpressions
    {
        get => _bareWordsAreExpressions;
        set
        {
            if (Set(ref _bareWordsAreExpressions, value))
            {
                Apply();
            }
        }
    }

    /// <summary>
    /// Whether kernel drivers are in the list. <c>A7</c>, and it has no state of its own.
    ///
    /// Read from the query, so a person who types the exclusion by hand sees the switch move
    /// on its own - the round trip `docs/07` asks for, in the one shape a single switch needs
    /// it. Written by appending or removing the member at the end of the text.
    ///
    /// <b>Only at the end</b>, and that is a limit worth stating rather than a bug. Cutting a
    /// member out of the middle means finding it in text that may hold quotes, which is the
    /// scanner's job, and a second scanner living here would drift from the real one in
    /// silence. So the switch undoes what the switch could have written, and if somebody put
    /// the exclusion somewhere else by hand it stays where they put it and the switch says so
    /// by not moving.
    /// </summary>
    public bool ShowDrivers
    {
        get => !_query.Excludes(DriverField, DriverValue);

        set
        {
            QueryText = value ? Sentences.WithoutHiddenDrivers(_queryText) : Sentences.WithHiddenDrivers(_queryText);

            // Unconditional, because nothing was set. When the edit did not take - the member
            // was somewhere this cannot reach - the switch reads the query again and goes back
            // to where it was, which is the honest answer.
            Raise(nameof(ShowDrivers));
        }
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

    /// <summary>One line under the list. Never empty - "reading" is a state worth showing.</summary>
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>
    /// What this result has to admit about itself: entries judged on something nobody could
    /// read, entries never judged at all, questions about data this window has not read, and
    /// a list holding still because somebody is using it.
    ///
    /// Empty when there is nothing to admit, which is the ordinary case. Separate from
    /// <see cref="Problem"/> because these are facts about the answer and that one is a fact
    /// about the question.
    /// </summary>
    public string Notice
    {
        get => _notice;
        private set => Set(ref _notice, value);
    }

    /// <summary>
    /// What is wrong with what was asked for - a query that will not parse, or something the
    /// window tried and could not do. Empty while it reads.
    ///
    /// A mistake in a query leaves the list alone - `docs/07` again - so this is the only sign
    /// that the box and the list have stopped agreeing, and it has to be visible.
    ///
    /// <b>An action's refusal wins over a query's, and outlives a tick.</b> Both would otherwise
    /// be written by <see cref="Apply"/>, which runs whenever anything on the machine moves - so
    /// a copy that failed would announce itself and be gone within the second, on a busy machine
    /// before anybody read it. It clears when the person asks for something else.
    ///
    /// The right home for a finished action's result is a transient one, and WPF UI has a
    /// Snackbar for it - `docs/10` section 4. This line is where it goes until there is a slice
    /// that puts one in.
    /// </summary>
    public string Problem => _refusal.Length > 0 ? _refusal : _problem;

    /// <summary>
    /// Something the window tried on the person's behalf and could not do.
    ///
    /// Rule 8 in a place it is easy to think does not apply: an action that quietly did nothing
    /// leaves somebody believing it did. The clipboard is the live example - it belongs to
    /// whichever process grabbed it last, so copying genuinely fails on a working machine.
    /// </summary>
    public void CouldNotDo(string because)
    {
        _refusal = Texts.Of("gui.status.couldNotDo", because);

        Raise(nameof(Problem));
    }

    /// <summary>
    /// Whether the reading admitted to gaps. Shown, never swallowed.
    ///
    /// Rule 8 in the window: a listing that quietly dropped what it could not read looks
    /// complete, and looking complete is exactly what makes it dangerous.
    ///
    /// About the reading, not about the query. A result the query could only half answer says
    /// so in <see cref="Notice"/> - folding the two together would put "the machine refused"
    /// and "this filter had nothing to go on" behind one flag.
    /// </summary>
    public bool Incomplete
    {
        get => _incomplete;
        private set => Set(ref _incomplete, value);
    }

    /// <summary>
    /// Reads the machine in full and fills the list. The first reading, and whatever F5 asks
    /// for afterwards.
    ///
    /// A second call arriving while one is out is dropped rather than queued. Without that,
    /// two presses of F5 send two readings and the one that <b>finished later</b> wins rather
    /// than the one that <b>read later</b> - so the list can settle on the older of two
    /// answers and say nothing about it. Nothing here corrupts, because every continuation
    /// comes back to the interface thread, which is precisely why the hole was invisible: it
    /// is a question of ordering rather than of two threads touching one field.
    /// </summary>
    public async Task LoadAsync()
    {
        if (_reading)
        {
            return;
        }

        _reading = true;

        try
        {
            await LoadEverything().ConfigureAwait(true);
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>
    /// The reading itself, without the guard, because the tick already holds it when it finds
    /// out that it needs a full one.
    /// </summary>
    private async Task LoadEverything()
    {
        Status = Texts.Of("gui.status.reading");

        IReadOnlyList<ScmEntry> entries;

        try
        {
            entries = await Task.Run(_catalog.ReadAll).ConfigureAwait(true);
        }
#pragma warning disable CA1031
        // Broad, and it is the same argument as the entry point of the command line tool:
        // the failure reaches the person, in the line under the list, instead of taking the
        // window down with a dialog nobody can act on. The message is the system's, so it
        // carries its own number and its own language.
        catch (Exception failure)
        {
            Fail(failure);

            return;
        }
#pragma warning restore CA1031

        Incomplete = false;
        _index.Absorb(entries);
        Apply();
    }

    /// <summary>
    /// One tick of the live list: asks what is running and moves whatever moved.
    ///
    /// Driven from outside rather than by a loop in here, and that is a deliberate shape. A
    /// loop would need a thread, a cancellation and a rule about what happens when the window
    /// closes mid-read - three things to get wrong. A window that calls this on a timer needs
    /// none of them, and a test can call it whenever it likes instead of waiting for seconds
    /// to pass.
    ///
    /// The cheap reading measures 13-22 ms over 810 entries against 423-500 ms for a full one,
    /// which is what makes asking once a second reasonable rather than rude.
    /// </summary>
    public async Task RefreshAsync()
    {
        // A tick arriving while any reading is still out is dropped rather than queued. The
        // reading is short, so this only happens when the machine is busy - and answering a
        // late tick with a second reading would make it busier.
        //
        // One flag for both kinds of reading, not two. They rebuild the same state, so two
        // flags would let a tick and an F5 overlap and leave whichever finished last on
        // screen, which is not the same thing as whichever looked last.
        if (_reading)
        {
            return;
        }

        _reading = true;

        try
        {
            IReadOnlyList<ScmStatus> statuses;

            try
            {
                statuses = await Task.Run(_catalog.ReadStatuses).ConfigureAwait(true);
            }
#pragma warning disable CA1031
            // Same argument as above, and it earns its place here rather than inheriting it:
            // this runs unattended once a second, so an exception nobody caught would take the
            // window down while its owner was somewhere else entirely.
            catch (Exception failure)
            {
                Fail(failure);

                return;
            }
#pragma warning restore CA1031

            Incomplete = false;

            switch (_index.Absorb(statuses))
            {
                case Freshening.CompositionChanged:
                    // The unguarded one, because the guard above is already held. Calling the
                    // public entry point here would find its own flag raised and quietly do
                    // nothing, which is the sort of deadlock-by-politeness that looks like the
                    // machine simply never installing anything.
                    await LoadEverything().ConfigureAwait(true);
                    break;

                case Freshening.Moved:
                    Apply();
                    break;

                default:
                    break;
            }
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>
    /// Takes the highlight off the rows that have worn it long enough. Driven by the same tick
    /// that refreshes, because it has to keep happening while nothing is moving.
    /// </summary>
    public void FadeHighlights() => _index.Fade();

    /// <summary>What is wrong with the query, which may be nothing. Silent while an action's refusal stands.</summary>
    private void SayProblem(string problem)
    {
        if (_problem == problem)
        {
            return;
        }

        _problem = problem;

        Raise(nameof(Problem));
    }

    private void Fail(Exception failure)
    {
        Status = Texts.Of("gui.status.failed", failure.Message);
        Incomplete = true;
    }

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
            _queryText, bareWordsAreExpressions: _bareWordsAreExpressions, input: QueryInput.BeingTyped);

        if (!parsed.IsValid)
        {
            // Every complaint, not the first one. Two mistakes in one query is ordinary while
            // somebody is typing, and fixing one to be told about the next is a poor trade for
            // a shorter line.
            SayProblem(string.Join(" ", parsed.Problems.Select(QueryMessages.Of)));

            return;
        }

        SayProblem(string.Empty);
        _query = parsed.Query!;

        var everything = _index.Ordered;
        var narrowed = Narrowing.Of(_query, everything);

        Show(narrowed.Selected);

        Status = narrowed.Selected.Count == everything.Count
            ? Texts.Of("gui.status.read", everything.Count)
            : Texts.Of("gui.status.matched", narrowed.Selected.Count, everything.Count);

        Notice = Sentences.Admissions(_query, _holding.Pending, narrowed.Unreadable, narrowed.TooCostly);

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
}
