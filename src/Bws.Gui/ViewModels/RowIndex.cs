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
    /// Takes what the details panel read about ONE entry into that entry's row - UX-GUI-005.
    ///
    /// <b>Not <see cref="Absorb(IReadOnlyList{ScmEntry})"/> with a list of one</b>, which would
    /// remove every row it was not handed and leave the window a list of one. A row that has left
    /// the listing meanwhile is not here to take it, and nothing is written - the panel says the
    /// entry has gone.
    /// </summary>
    public void AbsorbOne(ScmEntry entry)
    {
        if (_byName.TryGetValue(entry.ServiceName, out var row))
        {
            row.Absorb(entry, _clock.Now);
        }
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
    /// Every entry the window is holding, as the machine last described it.
    ///
    /// <b>ALL of them, never the ones on screen, and that distinction is a correctness one rather
    /// than a convenience.</b> A plan asks the listing who a service is and who shares its process,
    /// so a plan built from the FILTERED rows would look those up in a set a query had narrowed - a
    /// cascade member hidden by the query would be found as nothing and quietly left out of the
    /// preview. A preview shorter than what will happen is the worst thing `ADR-11` can produce.
    ///
    /// <b>The entries here are fresher than the ones the last full reading handed over</b>, because
    /// the cheap tick writes the status and the process identifier back into each row. So this is the
    /// best answer the window has without going to the manager again.
    ///
    /// <b>What it still cannot be: current to the millisecond.</b> Configuration moves only on a full
    /// reading, so a start type up to one of those old can put a step in a plan for an entry already
    /// where it was asked to be - which the runner reports as "already there", honestly.
    /// </summary>
    internal IReadOnlyList<ScmEntry> Everything => [.. _order.Select(row => row.Entry)];

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
