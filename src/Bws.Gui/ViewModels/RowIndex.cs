using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Every row that exists, kept in step with the machine.
///
/// The bookkeeping half of the window, split out of <see cref="MainViewModel"/> on 2026-08-05
/// when the size ratchet asked for a second seam. It answers one question - what does this
/// machine consist of right now - and knows nothing about queries, text or what is on screen.
///
/// <b>Rows are the same objects for as long as the service exists.</b> That is not an
/// optimisation either: replacing them would drop the selection and send the scroll back to the
/// top on every refresh, which is the first thing `A10` names.
/// </summary>
internal sealed class RowIndex
{
    /// <summary>
    /// How long a row stays marked as having just moved.
    ///
    /// Long enough to catch the eye of somebody looking at another part of the screen, short
    /// enough that a busy machine does not end up with half the list highlighted. `A10` asks
    /// for the behaviour and does not name a number, so this one is a judgement rather than a
    /// measurement and says so.
    /// </summary>
    internal static readonly TimeSpan HighlightFor = TimeSpan.FromSeconds(3);

    private readonly IClock _clock;

    /// <summary>Every row that exists, by service name, whether or not a query lets it through.</summary>
    private readonly Dictionary<string, EntryRow> _byName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every row in the order the manager hands them over.</summary>
    private List<EntryRow> _order = [];

    public RowIndex(IClock clock) => _clock = clock;

    /// <summary>Every row, in the manager's order. What a query is applied to.</summary>
    public IReadOnlyList<EntryRow> Ordered => _order;

    /// <summary>Rebuilds every row from a full reading, keeping the rows that already exist.</summary>
    public void Absorb(IReadOnlyList<ScmEntry> entries)
    {
        var now = _clock.Now;
        var order = new List<EntryRow>(entries.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            seen.Add(entry.ServiceName);

            if (_byName.TryGetValue(entry.ServiceName, out var row))
            {
                row.Absorb(entry, now);
            }
            else
            {
                row = EntryRow.Of(entry);
                _byName[entry.ServiceName] = row;
            }

            order.Add(row);
        }

        foreach (var gone in _byName.Keys.Where(name => !seen.Contains(name)).ToArray())
        {
            _byName.Remove(gone);
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
    /// <returns>What the caller now owes: nothing, a redraw, or a full reading.</returns>
    public Freshening Absorb(IReadOnlyList<ScmStatus> statuses)
    {
        if (statuses.Count != _byName.Count || statuses.Any(status => !_byName.ContainsKey(status.ServiceName)))
        {
            return Freshening.CompositionChanged;
        }

        var now = _clock.Now;
        var moved = false;

        foreach (var status in statuses)
        {
            moved |= _byName[status.ServiceName].Absorb(status, now);
        }

        Fade();

        // Only when something moved. Re-running the filter over 810 entries every second to
        // find out that nothing changed would be the one part of this that is genuinely
        // wasteful, and the answer is already known.
        return moved ? Freshening.Moved : Freshening.Unchanged;
    }

    /// <summary>
    /// Takes the highlight off the rows that have worn it long enough.
    ///
    /// One sweep rather than a timer per row. Called by the same tick that refreshes, and also
    /// when nothing is moving - otherwise the last thing to change stays lit until the next
    /// thing does.
    /// </summary>
    public void Fade()
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
}

/// <summary>
/// What a cheap reading found, and therefore what the window owes.
///
/// Three answers rather than a boolean, because the two that are not "nothing" are owed very
/// different things - a filter re-run costs milliseconds and a full reading costs half a second.
/// Returned rather than acted on, so the class that knows about queries stays the class that
/// decides what to do about them.
/// </summary>
internal enum Freshening
{
    /// <summary>Nothing moved. The list on screen is already right.</summary>
    Unchanged,

    /// <summary>Something moved within the entries already known, so the query is owed a re-run.</summary>
    Moved,

    /// <summary>An entry appeared or disappeared, which only a full reading can describe.</summary>
    CompositionChanged
}
