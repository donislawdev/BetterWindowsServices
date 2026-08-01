using Bws.Core;
using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the window shows and how it got there.
///
/// The reading runs off the interface thread, because it takes about half a second over 810
/// entries and a window that stops answering for half a second at startup is a window that
/// looks broken. `docs/06`, part 4: nothing from a worker thread touches the interface, so
/// what comes back is a plain list and everything the window binds to is built here.
///
/// Knows nothing about WPF beyond the notification interface. That is what makes it possible
/// to check what the window will show without opening one - and the thing this slice is
/// judged on is whether the same query text picks the same entries here as in the command
/// line, which is a question about this file rather than about the markup.
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
    private const string HideDrivers = "!type:driver";

    private const string DriverField = "type";
    private const string DriverValue = "driver";

    private readonly Func<IReadOnlyList<ScmEntry>> _read;

    /// <summary>Everything the manager handed over. Kept, because every keystroke filters it again.</summary>
    private IReadOnlyList<ScmEntry> _entries = [];

    /// <summary>
    /// The last query that parsed, which is not always the last query that was typed.
    ///
    /// Holding the last good one is what makes "a query with a mistake in it filters nothing"
    /// mean in a window what `docs/07` says it means: the list stays as it was and the mistake
    /// is reported beside it. In a terminal the same sentence means the opposite - print
    /// nothing - because a terminal writes into pipes.
    /// </summary>
    private Query _query = QueryParser.Parse(null).Query!;

    private IReadOnlyList<EntryRow> _rows = [];
    private string _queryText = string.Empty;
    private bool _bareWordsAreExpressions;
    private string _status = Texts.Of("gui.status.reading");
    private string _notice = string.Empty;
    private string _problem = string.Empty;
    private bool _incomplete;

    public MainViewModel()
        : this(() => new WindowsScmCatalog().ReadAll())
    {
    }

    /// <summary>
    /// The reading is handed in so a test can decide what the manager says, including the
    /// cases a real machine will not produce on demand - a refusal, an empty machine, a
    /// service whose configuration could not be read.
    /// </summary>
    public MainViewModel(Func<IReadOnlyList<ScmEntry>> read) => _read = read;

    /// <summary>What the list shows: the entries this query selected, as rows.</summary>
    public IReadOnlyList<EntryRow> Rows
    {
        get => _rows;
        private set => Set(ref _rows, value);
    }

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
            QueryText = value ? WithoutHiddenDrivers(_queryText) : WithHiddenDrivers(_queryText);

            // Unconditional, because nothing was set. When the edit did not take - the member
            // was somewhere this cannot reach - the switch reads the query again and goes back
            // to where it was, which is the honest answer.
            Raise(nameof(ShowDrivers));
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
    /// read, entries never judged at all, and questions about data this window has not read.
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
    /// Reads the machine and fills the list.
    ///
    /// Returns the rows as well as setting them, so a test can look at what was produced
    /// without watching a property it did not set.
    /// </summary>
    public async Task<IReadOnlyList<EntryRow>> LoadAsync()
    {
        Status = Texts.Of("gui.status.reading");

        try
        {
            // Off the interface thread, and only the reading. Turning entries into rows now
            // belongs with the filtering, because the filter decides which of them there are
            // to turn - and it runs again on every keystroke either way.
            _entries = await Task.Run(_read).ConfigureAwait(true);
        }
#pragma warning disable CA1031
        // Broad, and it is the same argument as the entry point of the command line tool:
        // the failure reaches the person, in the line under the list, instead of taking the
        // window down with a dialog nobody can act on. The message is the system's, so it
        // carries its own number and its own language.
        catch (Exception failure)
        {
            Status = Texts.Of("gui.status.failed", failure.Message);
            Incomplete = true;

            return [];
        }
#pragma warning restore CA1031

        Apply();

        return Rows;
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

        var result = _query.Filter(_entries);

        Rows = [.. result.Entries.Select(EntryRow.Of)];

        Status = result.Entries.Count == _entries.Count
            ? Texts.Of("gui.status.read", _entries.Count)
            : Texts.Of("gui.status.matched", result.Entries.Count, _entries.Count);

        Notice = Admissions(result);

        Raise(nameof(ShowDrivers));
    }

    /// <summary>
    /// Everything this answer is not, in sentences.
    ///
    /// The order is deliberate: what was never read comes first, because it is the sentence
    /// that explains an empty list, and somebody staring at one should not have to read past
    /// anything to find out why.
    /// </summary>
    private string Admissions(QueryResult result)
    {
        var needs = _query.Needs;
        var notes = new List<string>();

        // This window reads what a listing reads and no more. The command line answers a
        // question about signatures by going and verifying them, measured at 1100-1245 ms over
        // 810 entries and 544 files - a price a listing pays once and a search box cannot pay
        // on every keystroke. Doing it in the background is A10's work and belongs to S6c.
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
        if (result.Unreadable > 0 && needs == ExtraRead.None)
        {
            notes.Add(Texts.Of("gui.status.partial", result.Unreadable));
        }

        if (result.TooCostly > 0)
        {
            notes.Add(Texts.Of("gui.status.tooCostly", result.TooCostly));
        }

        return string.Join(" ", notes);
    }

    private static string WithHiddenDrivers(string text)
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
    private static string WithoutHiddenDrivers(string text)
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
