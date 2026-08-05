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
    private string _status;
    private string _statusShape;
    private string _startType;
    private string _startShape;
    private string _account;
    private string _processId;
    private bool _recentlyChanged;

    private EntryRow(ScmEntry entry)
    {
        var qualifies = StartQualifiers.Of(entry);

        _entry = entry;
        ServiceName = entry.ServiceName;
        _displayName = entry.DisplayName;
        _status = CellFaces.StatusLabel(entry.Status);
        _statusShape = CellFaces.StatusShape(entry.Status);
        _startType = CellFaces.StartLabel(entry, qualifies);
        _startShape = CellFaces.StartShape(entry, qualifies);
        _account = Describe(entry.Account, value => value);
        _processId = Describe(entry.ProcessId, Number);
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

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>
    /// Which shape the status wears, as a code the theme turns into a colour.
    ///
    /// Separate from <see cref="Status"/> rather than derived from it in the view, because the
    /// word is translated and the shape is not. A trigger comparing against "Running" would go
    /// quiet the day somebody adds a second language file, and go quiet is exactly what it
    /// would do - the row would simply lose its colour with nothing said.
    /// </summary>
    public string StatusShape
    {
        get => _statusShape;
        private set => Set(ref _statusShape, value);
    }

    public string StartType
    {
        get => _startType;
        private set => Set(ref _startType, value);
    }

    /// <summary>Which shape the start type wears. Same split as <see cref="StatusShape"/>.</summary>
    public string StartShape
    {
        get => _startShape;
        private set => Set(ref _startShape, value);
    }

    public string Account
    {
        get => _account;
        private set => Set(ref _account, value);
    }

    public string ProcessId
    {
        get => _processId;
        private set => Set(ref _processId, value);
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

    /// <summary>The entry behind this row, which is what a query is asked about.</summary>
    internal ScmEntry Entry => _entry;

    /// <summary>When this row last moved, for whoever is clearing the highlight.</summary>
    internal DateTimeOffset ChangedAt { get; private set; }

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

        Status = CellFaces.StatusLabel(_entry.Status);
        StatusShape = CellFaces.StatusShape(_entry.Status);
        ProcessId = Describe(_entry.ProcessId, Number);
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
            || Describe(entry.Account, value => value) != _account
            || _entry.DisplayName != entry.DisplayName;

        _entry = entry;

        DisplayName = entry.DisplayName;
        Status = CellFaces.StatusLabel(entry.Status);
        StatusShape = CellFaces.StatusShape(entry.Status);
        StartType = CellFaces.StartLabel(entry, qualifies);
        StartShape = CellFaces.StartShape(entry, qualifies);
        Account = Describe(entry.Account, value => value);
        ProcessId = Describe(entry.ProcessId, Number);

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

    private static string Number(int value) =>
        value.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// A reading as text, with each of the four states saying something different.
    ///
    /// The empty string is only ever used for "there is genuinely nothing", which is the one
    /// state where a blank cell tells the truth. The other two say so in words, because a
    /// person scanning a column has no other way to tell them from a value nobody has.
    /// </summary>
    private static string Describe<T>(Reading<T> reading, Func<T, string> text) => reading.Outcome switch
    {
        ReadOutcome.Present => text(reading.Value!),
        ReadOutcome.Absent => string.Empty,
        ReadOutcome.Denied => Texts.Of("gui.cell.noAccess"),
        _ => Texts.Of("gui.cell.unknown")
    };
}
