using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One entry as a row on screen.
///
/// A separate type from <see cref="ScmEntry"/> rather than binding to it directly, and the
/// reason is the one this project spends most of its rules on: an entry carries four read
/// states per field, and a row carries text. Turning the first into the second is a decision
/// with a wrong answer - "not read" rendered as an empty cell reads as "there is none", and
/// those two say opposite things about a service.
///
/// <b>It changes rather than being replaced, and that is what makes a live list possible.</b>
/// A window that swapped in new row objects every second would reset the scroll position and
/// drop the selection every time, which is precisely what `A10` forbids. So the row keeps its
/// identity - the internal service name, never the display name (`ADR-14`) - and its cells
/// move underneath it.
/// </summary>
public sealed class EntryRow : Observable
{
    private ScmEntry _entry;
    private string _displayName;
    private string _statusShape;
    private string _startShape;
    private string _againstShape;
    private bool _recentlyChanged;
    private string _standsFor = string.Empty;

    /// <summary>
    /// The two cells whose text decides whether the row MOVED, kept because the decision is made
    /// against what was SHOWN rather than against the entry behind it.
    ///
    /// <b>Not a cache and not a leftover.</b> The four cells that used to be properties here are
    /// gone, because the columns read them through the indexer now. These two survive as fields
    /// for one job: the shown form is the one carrying the qualifiers, so a file going missing
    /// under a service counts as the row moving, and comparing the raw fields would miss it.
    /// </summary>
    private string _startType;

    private string _account;

    private EntryRow(ScmEntry entry)
    {
        var qualifies = StartQualifiers.Of(entry);

        _entry = entry;
        ServiceName = entry.ServiceName;

        // Through the rule rather than straight off the entry, because two entries on an ordinary
        // machine carry an indirection nobody resolved instead of a label - and since the list
        // gained a default order those two were its first two rows, an at sign sorting before every
        // letter. ServiceDisplayName carries the whole argument, including why a snapshot must go
        // on storing what the manager said.
        _displayName = ServiceDisplayName.Of(entry.DisplayName, entry.ServiceName);
        _statusShape = CellFaces.StatusShape(entry.Status);
        _startShape = CellFaces.StartShape(entry, qualifies);
        _againstShape = CellFaces.AgainstShape(entry.RunsAgainstItsStartType);
        _startType = CellFaces.StartLabel(entry, qualifies);
        _account = SystemAccounts.Shown(entry.Account);
    }

    /// <summary>
    /// Which entry this row is. Never changes, because it is the identity rather than a
    /// property - `ADR-14`. Windows treats it as case insensitive while keeping the spelling,
    /// so anything matching rows to entries has to do the same.
    /// </summary>
    public string ServiceName { get; }

    public string DisplayName
    {
        get => _displayName;
        private set => Set(ref _displayName, value);
    }

    /// <summary>
    /// Which shape the status wears, as a code the theme turns into a colour.
    ///
    /// Separate from the word rather than derived from it in the view, because the word is
    /// translated and the shape is not. A trigger comparing against "Running" would go quiet the
    /// day somebody adds a second language file, and go quiet is exactly what it would do - the
    /// row would simply lose its colour with nothing said.
    ///
    /// <b>A property rather than a column, unlike every word on this row.</b> The mark is not a
    /// cell - it sits beside one, inside a template - so nothing would ever ask a column for it,
    /// and it notifies precisely where the cells notify in a batch.
    /// </summary>
    public string StatusShape
    {
        get => _statusShape;
        private set => Set(ref _statusShape, value);
    }

    /// <summary>Which shape the start type wears. Same split as <see cref="StatusShape"/>.</summary>
    public string StartShape
    {
        get => _startShape;
        private set => Set(ref _startShape, value);
    }

    /// <summary>
    /// Which shape the disagreement between the run state and the start type wears - backlog 165.
    ///
    /// <b>It is worked out from BOTH, so it moves when either does - and one of them moves every
    /// second.</b> That is the trap this property carries: a service starting turns this from
    /// nothing into a mark without any configuration changing, so the cheap reading has to
    /// recompute it. A version that only did so on a full reading would draw a mark that was true
    /// when the window opened and quietly false afterwards, which is worse than no mark at all.
    /// </summary>
    public string AgainstShape
    {
        get => _againstShape;
        private set => Set(ref _againstShape, value);
    }

    /// <summary>
    /// Whether this row moved a moment ago.
    ///
    /// `A10` rule three: a change has to be visible rather than stealthy. A list that quietly
    /// corrects itself is a list where somebody looking away misses the one thing they were
    /// waiting for, and then distrusts the whole window.
    ///
    /// Cleared by whoever set it, on a clock, rather than by a timer per row. Eight hundred
    /// timers to make a highlight fade would be a lot of machinery for a coloured background.
    /// </summary>
    public bool RecentlyChanged
    {
        get => _recentlyChanged;
        internal set => Set(ref _recentlyChanged, value);
    }

    /// <summary>
    /// What this row says beside its name when it stands for a whole per-user family, and nothing
    /// at all when it stands only for itself - <see cref="Folding.Badge"/> carries the wording.
    ///
    /// <b>A property rather than a column, exactly like <see cref="StatusShape"/> and for the same
    /// reason.</b> It is not a cell - it sits beside one, inside a template - so no column would
    /// ever be asked for it, and it notifies where the cells notify rather than through the
    /// indexer.
    ///
    /// <b>It moves when an instance moves, which is once a second on a busy machine.</b> The text
    /// carries how many of the folded instances are running, so a family coming up has to change
    /// this without anything about the template itself changing. That is what makes it a string
    /// recomputed on every pass rather than a count worked out when the fold is built.
    /// </summary>
    public string StandsFor
    {
        get => _standsFor;
        private set => Set(ref _standsFor, value);
    }

    /// <summary>
    /// The rows folded under this one, which is empty for almost every row on the machine.
    ///
    /// <b>Read by whatever turns a selection into a plan, and that is the whole reason it is kept
    /// rather than being a number.</b> The specification's own warning about `A11` is that
    /// selecting a folded row means acting on everything under it, so the plan has to be able to
    /// name them - "inaczej admin kliknie stop na jednym wierszu i zatrzyma cztery uslugi".
    /// A count would show the badge and leave the plan short, which is the one failure `ADR-11`
    /// exists to prevent.
    /// </summary>
    internal IReadOnlyList<EntryRow> Instances { get; private set; } = [];

    /// <summary>
    /// Tells this row which instances are folded under it, which is usually none.
    ///
    /// <b>Called about every row on every pass, including the rows that gained nothing.</b> A
    /// template standing for four of them a keystroke ago has to lose the badge saying so, and the
    /// only cheap way to be right about that is to say it about all of them -
    /// <see cref="Folding.Of"/> carries the argument.
    ///
    /// The list is taken as given rather than copied. It is built once per pass by the fold and
    /// handed to exactly one row, and copying 23 short lists per keystroke would buy an
    /// immutability nothing in this window is trying to break.
    /// </summary>
    internal void StandsAlsoFor(IReadOnlyList<EntryRow> instances)
    {
        Instances = instances;

        // Set rather than assigned, so a family whose run state has not moved says nothing at all.
        // This runs on every keystroke, and a row announcing an unchanged string 810 times a
        // second is the shape of waste `A10` was measured to remove.
        StandsFor = Folding.Badge(instances);
    }

    /// <summary>The entry behind this row, which is what a query is asked about.</summary>
    internal ScmEntry Entry => _entry;

    /// <summary>
    /// What this row says in one column, asked for by that column's own identifier.
    ///
    /// <b>An indexer rather than seventeen properties, and the reason is measured rather than
    /// stylistic.</b> Eleven of the seventeen columns of `A8` are off by default and most of them
    /// stay off - a security descriptor is several hundred characters and a privilege list runs to
    /// twenty-eight names on this machine. Holding all of that as text on every one of 810 rows
    /// would spend the whole of the 1.5 MB the row list is allowed by MemoryBudgetTests on cells
    /// nobody has turned on. Computed here, only the cells a virtualised grid has realised are
    /// ever worked out - about thirty rows of them.
    ///
    /// <b>Keyed by identifier and never by the heading.</b> The heading is translated and the
    /// identifier is not, so a layout keyed on words would name different columns on a machine set
    /// to a different language. Rule 3 of the project notes, the same one that makes the marks
    /// compare against a code.
    ///
    /// An identifier nothing knows comes back as itself, which is what <see cref="Texts.Of"/> does
    /// with a key nothing declares and for the same reason: a column that has gone away should
    /// look wrong on screen rather than render as an empty cell, which is the one thing an empty
    /// cell must never mean.
    /// </summary>
    public string this[string column] =>
        Columns.Of(column) is { } known ? known.Reads(_entry) : column;

    /// <summary>
    /// The name WPF listens for when a binding goes through an indexer.
    ///
    /// <b>Raising this is the whole of what makes the cells above live, and nothing else does
    /// it.</b> No cell has a property of its own any more, so nothing else says a word on this row
    /// changed - a row whose launch path was rewritten would keep showing the old one, correctly,
    /// forever, with every property on it telling the truth. That is the shape this window has
    /// been caught by four times, and it is why the guard over this claims a NOTIFICATION rather
    /// than a value: a computed cell answers correctly whenever it is asked, so asking it proves
    /// nothing about whether anybody was told to ask.
    /// </summary>
    private const string EveryCell = "Item[]";

    /// <summary>When this row last moved, for whoever is clearing the highlight.</summary>
    internal DateTimeOffset ChangedAt { get; private set; }

    /// <summary>
    /// How many times what this row SAYS has changed, for anything keeping an answer about it.
    ///
    /// <b>It exists for one reader and the reason is measured.</b> Sorting hands the view a
    /// comparer, and a comparer is asked O(n log n) times for a sort and about ten times for every
    /// single insertion into an already sorted view - which is every row arriving when a filter
    /// widens. Working the key out on each of those calls means rebuilding the same string
    /// thousands of times: for the list columns that is a <c>string.Join</c>, and for the triggers
    /// column a Select, a Distinct and a Join. Measured on 702 insertions into a sorted view, with
    /// the key built per comparison against a key already in hand: 117.5 ms against 66.6.
    ///
    /// <b>A counter rather than a flag, because the reader has to notice a change it did not
    /// watch.</b> Whoever keeps a key alongside the number it was taken at can tell in one
    /// comparison whether it still describes this row, and no subscription is needed to do it.
    ///
    /// <b>It moves exactly where <c>Item[]</c> is raised and nowhere else</b>, which is the same
    /// promise: those are the two places this row's cells can change. A version that moved without
    /// the cells moving would be a cache thrown away for nothing, and one that stood still while
    /// they moved would sort the list by yesterday's values - silently, and in an order that looks
    /// plausible.
    /// </summary>
    internal int Version { get; private set; }

    public static EntryRow Of(ScmEntry entry) => new(entry);

    /// <summary>
    /// Takes what a cheap reading found, and says whether anything actually moved.
    ///
    /// The entry behind the row is updated too, not only the cells. Leaving it stale would
    /// mean the list showed a service as stopped while a query about running services still
    /// counted it - the row and the filter disagreeing about the same fact.
    /// </summary>
    internal bool Absorb(ScmStatus status, DateTimeOffset now)
    {
        if (_entry.Status == status.Status && SameProcess(_entry.ProcessId, status.ProcessId))
        {
            return false;
        }

        _entry = _entry with { Status = status.Status, ProcessId = status.ProcessId };

        StatusShape = CellFaces.StatusShape(_entry.Status);

        // AND THE DISAGREEMENT, WHICH IS THE HALF THAT IS EASY TO LEAVE OUT. Nothing about the
        // configuration changed here - the start type is the same as it was - but whether the entry
        // is doing the opposite of it depends on the state that just moved. A service starting is
        // exactly how a row goes from ordinary to marked, and it arrives through this path.
        AgainstShape = CellFaces.AgainstShape(_entry.RunsAgainstItsStartType);

        // The status and the process identifier as WORDS, which are cells and therefore say so
        // here rather than one property at a time.
        //
        // AND THE VERSION GOES WITH IT, in the same statement pair, because the two say the same
        // thing to two different readers - see Version.
        Version++;
        Raise(EveryCell);

        ChangedAt = now;
        RecentlyChanged = true;

        return true;
    }

    /// <summary>
    /// Takes a full reading of the same entry. Configuration moves when somebody changes it,
    /// which is rarer than a service starting and is not what the cheap reading watches.
    /// </summary>
    internal bool Absorb(ScmEntry entry, DateTimeOffset now)
    {
        var qualifies = StartQualifiers.Of(entry);

        // Compared against what the row currently SHOWS rather than against the entry behind
        // it. The two are the same thing said twice, and the shown form is the one that now
        // carries the qualifiers - so a file going missing under a service counts as the row
        // moving, which it is.
        var moved = _entry.Status != entry.Status
            || !SameProcess(_entry.ProcessId, entry.ProcessId)
            || CellFaces.StartLabel(entry, qualifies) != _startType
            || SystemAccounts.Shown(entry.Account) != _account
            || _entry.DisplayName != entry.DisplayName;

        _entry = entry;

        DisplayName = ServiceDisplayName.Of(entry.DisplayName, entry.ServiceName);
        StatusShape = CellFaces.StatusShape(entry.Status);
        StartShape = CellFaces.StartShape(entry, qualifies);
        AgainstShape = CellFaces.AgainstShape(entry.RunsAgainstItsStartType);

        _startType = CellFaces.StartLabel(entry, qualifies);
        _account = SystemAccounts.Shown(entry.Account);

        // UNCONDITIONAL, AND THAT IS NOT LAZINESS - IT IS THE ONLY HONEST ANSWER HERE. The five
        // comparisons above decide whether the row MOVED, which is a question about what a person
        // should be shown a highlight for. Whether a CELL changed is a different question over a
        // different set of fields: eleven of the seventeen columns read parts of the entry nothing
        // here compares, so a dependency list gaining a name, a launch path being rewritten or a
        // descriptor changing would all leave their cells showing yesterday's answer with nothing
        // anywhere reporting it.
        //
        // Comparing the whole entry instead was the other option and it does not work: ScmEntry is
        // a record, so == is memberwise, and several of its members are lists compared by
        // reference - two readings of an unchanged machine are already unequal. It would cost a
        // comparison to arrive at the same answer this line gives for nothing.
        Version++;
        Raise(EveryCell);

        if (moved)
        {
            ChangedAt = now;
            RecentlyChanged = true;
        }

        return moved;
    }

    /// <summary>
    /// Two readings of a process, compared including their outcome.
    ///
    /// Absent and present-with-a-value are different answers even when neither carries a
    /// number, and comparing only the numbers would miss a service stopping if the manager
    /// happened to have refused the reading before.
    /// </summary>
    private static bool SameProcess(Reading<int> left, Reading<int> right) =>
        left.Outcome == right.Outcome && left.Value == right.Value;

    // A Describe helper stood here until 2026-09-15, wrapping CellFaces.Say for the one caller
    // left - the account. The account now goes through SystemAccounts.Shown, which is Say with
    // the known names in front of it, so the helper had no caller and went the way its own
    // comment said a dead helper should: out, rather than surviving every build unread.
}
