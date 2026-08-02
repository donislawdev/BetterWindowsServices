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

    /// <summary>
    /// How long a row stays marked as having just moved.
    ///
    /// Long enough to catch the eye of somebody looking at another part of the screen, short
    /// enough that a busy machine does not end up with half the list highlighted. `A10` asks
    /// for the behaviour and does not name a number, so this one is a judgement rather than a
    /// measurement and says so.
    /// </summary>
    internal static readonly TimeSpan HighlightFor = TimeSpan.FromSeconds(3);

    private readonly IScmCatalog _catalog;
    private readonly IClock _clock;

    /// <summary>Every row that exists, by service name, whether or not the query lets it through.</summary>
    private readonly Dictionary<string, EntryRow> _rows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every row in the order the manager hands them over.</summary>
    private List<EntryRow> _order = [];

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
    private bool _incomplete;
    private bool _interacting;
    private bool _reading;
    private bool _held;

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
        _clock = clock;
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
                Apply();
            }
        }
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
    /// Whether somebody is using the list right now - pointing at it, or typing into it.
    ///
    /// Set by the window, because only a window knows about focus and a mouse. While it is
    /// true, rows keep updating and <b>nothing joins or leaves the list</b>: `A10` says the
    /// order freezes for the duration of an interaction, and a row appearing above the one
    /// somebody is aiming at moves their target while they are reaching for it.
    ///
    /// Cells still move, and that is the deliberate half of the compromise. A row that says
    /// Stopped under a query about running services is visibly odd and highlighted, which is a
    /// far better failure than a row that silently disappears from under a cursor.
    /// </summary>
    public bool Interacting
    {
        get => _interacting;
        set
        {
            if (Set(ref _interacting, value) && !value && _held)
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
    /// What is wrong with the query as typed. Empty while it reads.
    ///
    /// A mistake here leaves the list alone - `docs/07` again - so this is the only sign that
    /// the box and the list have stopped agreeing, and it has to be visible.
    /// </summary>
    public string Problem
    {
        get => _problem;
        private set => Set(ref _problem, value);
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
        Absorb(entries);
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

            if (Freshen(statuses))
            {
                // The unguarded one, because the guard above is already held. Calling the
                // public entry point here would find its own flag raised and quietly do
                // nothing, which is the sort of deadlock-by-politeness that looks like the
                // machine simply never installing anything.
                await LoadEverything().ConfigureAwait(true);
            }
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>
    /// Takes the highlight off the rows that have worn it long enough.
    ///
    /// One sweep rather than a timer per row, and called by the same tick that refreshes.
    /// Separate from the reading because it has to keep happening while nothing is moving -
    /// otherwise the last thing to change stays lit until the next thing does.
    /// </summary>
    public void FadeHighlights()
    {
        var now = _clock.Now;

        foreach (var row in _order)
        {
            if (row.RecentlyChanged && now - row.ChangedAt >= HighlightFor)
            {
                row.RecentlyChanged = false;
            }
        }
    }

    /// <summary>Rebuilds every row from a full reading, keeping the rows that already exist.</summary>
    private void Absorb(IReadOnlyList<ScmEntry> entries)
    {
        var now = _clock.Now;
        var order = new List<EntryRow>(entries.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            seen.Add(entry.ServiceName);

            if (_rows.TryGetValue(entry.ServiceName, out var row))
            {
                row.Absorb(entry, now);
            }
            else
            {
                row = EntryRow.Of(entry);
                _rows[entry.ServiceName] = row;
            }

            order.Add(row);
        }

        foreach (var gone in _rows.Keys.Where(name => !seen.Contains(name)).ToArray())
        {
            _rows.Remove(gone);
        }

        _order = order;
    }

    /// <summary>
    /// Takes a cheap reading and moves what moved.
    ///
    /// A name nobody knows means an entry was installed, and a name that stopped coming means
    /// one was removed. Neither can be filled in from this reading - everything else a row
    /// shows is configuration - so it asks for a full one instead. That is rare enough to be
    /// worth the half second, and pretending otherwise would put a row on screen with two
    /// columns saying "unknown" for no reason a person could work out.
    /// </summary>
    /// <returns>Whether the composition changed, so a full reading is owed.</returns>
    private bool Freshen(IReadOnlyList<ScmStatus> statuses)
    {
        if (statuses.Count != _rows.Count || statuses.Any(status => !_rows.ContainsKey(status.ServiceName)))
        {
            return true;
        }

        var now = _clock.Now;
        var moved = false;

        foreach (var status in statuses)
        {
            moved |= _rows[status.ServiceName].Absorb(status, now);
        }

        FadeHighlights();

        // Only when something moved. Re-running the filter over 810 entries every second to
        // find out that nothing changed would be the one part of this that is genuinely
        // wasteful, and the answer is already known.
        if (moved)
        {
            Apply();
        }

        return false;
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
        var parsed = QueryParser.Parse(_queryText, bareWordsAreExpressions: _bareWordsAreExpressions);

        if (!parsed.IsValid)
        {
            // Every complaint, not the first one. Two mistakes in one query is ordinary while
            // somebody is typing, and fixing one to be told about the next is a poor trade for
            // a shorter line.
            Problem = string.Join(" ", parsed.Problems.Select(QueryMessages.Of));

            return;
        }

        Problem = string.Empty;
        _query = parsed.Query!;

        var selected = new List<EntryRow>(_order.Count);
        var unreadable = 0;
        var tooCostly = 0;

        foreach (var row in _order)
        {
            var match = _query.Match(row.Entry);

            if (match.Matched)
            {
                selected.Add(row);
            }

            if (match.Unreadable)
            {
                unreadable++;
            }

            if (match.TooCostly)
            {
                tooCostly++;
            }
        }

        Show(selected);

        Status = selected.Count == _order.Count
            ? Texts.Of("gui.status.read", _order.Count)
            : Texts.Of("gui.status.matched", selected.Count, _order.Count);

        Notice = Sentences.Admissions(_query, _held, unreadable, tooCostly);

        Raise(nameof(ShowDrivers));
    }

    /// <summary>
    /// Makes the visible list match what the query selected, moving as little as possible.
    ///
    /// Two passes over one collection: drop what is no longer wanted, then put the missing
    /// ones where they belong. After the first pass what remains is a subsequence of what is
    /// wanted, so the second can walk both in step. The rows themselves are the same objects
    /// throughout, which is what lets the selection and the scroll position survive.
    ///
    /// Held back entirely while somebody is using the list. Cells keep moving underneath -
    /// see <see cref="Interacting"/> for why that half is not held back with it.
    /// </summary>
    private void Show(List<EntryRow> selected)
    {
        if (_interacting)
        {
            _held = Rows.Count != selected.Count || !Rows.SequenceEqual(selected);

            return;
        }

        _held = false;

        // Filling an empty list one row at a time is 810 notifications, and a DataGrid answers
        // every one of them. Measured 2026-08-02: it was about 285 ms of the time between the
        // window appearing and a row being on the screen, against roughly 80 for the reading
        // that produced the rows.
        //
        // Only when the list is empty, and that condition is doing real work rather than being
        // cautious. A reset is how a DataGrid is told it cannot work out what moved, so it
        // throws away the selection and the scroll position - the two things `A10` names first.
        // An empty list has neither, so this is the one moment where the cheap path costs
        // nothing. Every refresh after it goes through the loop below, row object by row
        // object, exactly as before.
        if (Rows.Count == 0 && selected.Count > 0)
        {
            Rows.ResetTo(selected, nothingToPreserve: true);

            return;
        }

        var wanted = new HashSet<EntryRow>(selected);

        for (var index = Rows.Count - 1; index >= 0; index--)
        {
            if (!wanted.Contains(Rows[index]))
            {
                Rows.RemoveAt(index);
            }
        }

        for (var index = 0; index < selected.Count; index++)
        {
            if (index >= Rows.Count || !ReferenceEquals(Rows[index], selected[index]))
            {
                Rows.Insert(index, selected[index]);
            }
        }
    }

}
