namespace Bws.Gui.ViewModels;

/// <summary>
/// How two rows compare by one column, and what that answer costs.
///
/// <b>Its own file since 2026-08-31, and the size ratchet is what asked.</b> Keeping a key rather
/// than rebuilding it needed more lines of argument than <see cref="Columns"/> had left under its
/// ceiling, and the seam was already there: that file is the CATALOGUE of columns - what each one
/// is called, what it reads, how wide it starts - and this is how two rows are put in order by one
/// of them. The same seam <c>ListSorting</c> was cut along, one layer up.
///
/// <b>Nothing sorts as less than something, on purpose.</b> A row with no value in this column
/// compares below every row that has one, so reversing the direction moves the whole group from the
/// top to the bottom. That is exactly what the empty string already does in every column that says
/// nothing with one, and a column behaving differently would be a rule nobody could learn by using
/// the list.
/// </summary>
internal sealed class CellOrder(Column column, bool ascending) : System.Collections.IComparer
{
    /// <summary>
    /// What was worked out for a row last time, and at which version of that row.
    ///
    /// <b>Bounded by the rows this comparer is asked about while it is the sort</b>, which is the
    /// listing - a few hundred. A new comparer is built for every sort, so nothing here outlives
    /// the order it belongs to. It does hold its rows alive until then, and that is deliberate
    /// rather than overlooked: the rows are held anyway by <see cref="RowIndex"/>, which keeps one
    /// object per service for as long as the service exists.
    ///
    /// Not thread safe, and it does not need to be. A comparer handed to a ListCollectionView is
    /// only ever called from the thread that owns the view, which is the interface thread.
    /// </summary>
    private readonly Dictionary<EntryRow, (int Version, IComparable? Key)> _keys = [];

    public int Compare(object? left, object? right)
    {
        var order = Order(Key(left), Key(right));

        return ascending ? order : -order;
    }

    private static int Order(IComparable? left, IComparable? right) => (left, right) switch
    {
        (null, null) => 0,
        (null, _) => -1,
        (_, null) => 1,
        _ => left.CompareTo(right)
    };

    /// <summary>
    /// What this row sorts by, worked out once per row and then read.
    ///
    /// <b>WHY A KEY IS WORTH KEEPING AT ALL.</b> For twenty five of the twenty seven columns the
    /// sort key IS the cell, so asking for it runs <c>Reads</c> - a <c>string.Join</c> over a list
    /// of names for three of them, a Select plus a Distinct plus a Join for the triggers column, a
    /// several hundred character string for the descriptor. A comparer is asked O(n log n) times
    /// for one sort and about ten times for every insertion into an already sorted view, and a
    /// filter widening back out inserts hundreds of rows. Measured 2026-08-31 on 702 insertions
    /// into a sorted view: 117.5 ms with the key built per comparison against 66.6 ms with it
    /// already in hand.
    ///
    /// <b>AND WHY IT IS SAFE TO KEEP.</b> A row is mutable - the machine moves under it once a
    /// second - so a key kept without a way to notice that would sort the list by values that are
    /// no longer true, in an order that still looks plausible. <see cref="EntryRow.Version"/> moves
    /// in exactly the two places a row's cells change, so a key taken at a version that is no
    /// longer current is thrown away rather than trusted.
    /// </summary>
    private IComparable? Key(object? row)
    {
        if (row is not EntryRow entry)
        {
            return null;
        }

        if (_keys.TryGetValue(entry, out var kept) && kept.Version == entry.Version)
        {
            return kept.Key;
        }

        var key = column.SortKey(entry.Entry);

        _keys[entry] = (entry.Version, key);

        return key;
    }
}
