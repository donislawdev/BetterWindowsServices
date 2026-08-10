using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The list the window is bound to, with one way of changing it that an ordinary observable
/// collection does not offer: replacing the whole contents and saying so once.
///
/// <b>Why this type exists, measured rather than assumed.</b> The first time the window fills,
/// it puts 810 rows into an empty list. Through <see cref="ObservableCollection{T}"/> that is
/// 810 separate notifications, and a DataGrid answers every one of them - so filling the list
/// cost about 285 ms of the 833-889 the window took from launch to a row on the screen, and
/// almost none of that was building the rows.
///
/// <b>What it does NOT change, because that is the whole of `A10`.</b> The incremental path is
/// still there and still the default: a refresh that moves a handful of rows inserts and
/// removes them one at a time, the row objects survive, and with them the selection and the
/// scroll position. This type is only used where there is nothing to preserve - see
/// <see cref="ResetTo"/> for the one condition, which is checked rather than assumed.
/// </summary>
public sealed class RowList : ObservableCollection<EntryRow>
{
    /// <summary>
    /// The next entry whose name begins with a character, starting after the one given.
    ///
    /// <b>The behaviour admins already have in <c>services.msc</c></b>, asked for by name by the
    /// owner - backlog 151. Press a letter on the list and land on the nearest entry that starts
    /// with it.
    ///
    /// <b>Matched against the name rather than the display name, because the name is the column
    /// the eye is on</b> - it is the first one in this window, where <c>services.msc</c> puts the
    /// display name first. Somebody pressing a letter is looking at a column, not recalling a
    /// convention.
    ///
    /// <b>Wraps, and starts AFTER the row given rather than at it.</b> Both halves are what makes
    /// repeated presses walk through the entries beginning with that letter instead of standing on
    /// the first one - and wrapping means the walk never dead-ends at the bottom of the list with
    /// nothing to say for itself.
    ///
    /// Ordinal and case insensitive. Not the machine's culture: this compares an internal name,
    /// which is `ADR-14` territory - those are not words in any language, and a Turkish culture
    /// would famously disagree about what an upper case i is.
    /// </summary>
    /// <b>STATIC, AND TAKING THE ROWS RATHER THAN READING ITS OWN.</b> The moment the columns
    /// became sortable, this list stopped being the order on screen - the grid keeps its own view
    /// over these rows and sorts that. A jump that walked this collection would send somebody to a
    /// row that is nowhere near where they are looking, and it would do it only after they had
    /// sorted something, which is the worst kind of intermittent.
    ///
    /// <param name="rows">The order to walk, which is whatever the person is looking at.</param>
    /// <param name="after">Where to start looking, exclusive. Null starts at the top.</param>
    /// <param name="letter">What the name has to begin with.</param>
    public static EntryRow? NextStartingWith(IReadOnlyList<EntryRow> rows, EntryRow? after, char letter)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return null;
        }

        var prefix = letter.ToString();
        // Walked rather than asked, because IReadOnlyList has no IndexOf and materialising the
        // sequence into something that does would copy 810 references on every key press.
        var from = -1;

        for (var index = 0; after is not null && index < rows.Count; index++)
        {
            if (ReferenceEquals(rows[index], after))
            {
                from = index;

                break;
            }
        }

        for (var step = 1; step <= rows.Count; step++)
        {
            // The second modulus is not decoration: a null starting row gives -1, and a row that
            // is no longer in the list gives -1 as well, which is the case a reconciliation can
            // produce between one press and the next.
            var row = rows[((from + step) % rows.Count + rows.Count) % rows.Count];

            if (row.ServiceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>
    /// Replaces everything and raises a single reset.
    ///
    /// <b>A reset is exactly what a DataGrid needs to throw away the selection and the scroll
    /// position</b>, which is why this refuses to run when there is a selection to throw away.
    /// The caller has to have established that first, and the parameter is named for what it
    /// asserts rather than for what it does.
    /// </summary>
    /// <param name="rows">The new contents, in order.</param>
    /// <param name="nothingToPreserve">
    /// Whether the list currently holds anything a person would notice losing. Passing true
    /// when it does is the one way to misuse this type, so it is a parameter rather than a
    /// check inside - the caller knows about focus and selection, and this collection does not.
    /// </param>
    public void ResetTo(IReadOnlyList<EntryRow> rows, bool nothingToPreserve)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (!nothingToPreserve)
        {
            throw new InvalidOperationException(
                "A reset drops the selection and the scroll position, which A10 forbids while " +
                "there is either to drop. Use the ordinary insert and remove path instead.");
        }

        // The base type's own list, changed directly, so the notifications below are the only
        // ones that leave here. CheckReentrancy first, because that is the protection the base
        // type gives against a handler changing the list while it is being told about a change,
        // and reaching into Items skips everything else it would otherwise do.
        CheckReentrancy();

        Items.Clear();

        foreach (var row in rows)
        {
            Items.Add(row);
        }

        // Three notifications rather than one, and all three are required. WPF listens to the
        // count and the indexer to know the list changed at all, and to the reset to know it
        // cannot work out how - leaving any of them out produces a list that is right in memory
        // and stale on the screen, which is this project's least favourite kind of wrong.
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Turns this list into the given one, keeping every row object that appears in both.
    ///
    /// <b>Moved here from the view model on 2026-08-03</b>, when the size ratchet asked for a
    /// seam and this was the honest one: a collection that knows how to become another
    /// collection. What stays behind decides WHETHER to change, which needs focus, a mouse and a
    /// query - none of which a list has any business knowing about.
    ///
    /// Three passes and each one is load-bearing.
    /// </summary>
    public void Reconcile(IReadOnlyList<EntryRow> wanted)
    {
        ArgumentNullException.ThrowIfNull(wanted);

        // Filling an empty list one row at a time is 810 notifications, and a DataGrid answers
        // every one of them. Measured 2026-08-02: about 285 ms of the time between the window
        // appearing and a row being on the screen, against roughly 80 for the reading that
        // produced the rows.
        //
        // Only when this list is empty, and that condition is doing real work rather than being
        // cautious. A reset is how a DataGrid is told it cannot work out what moved, so it throws
        // away the selection and the scroll position - the two things `A10` names first. An empty
        // list has neither, so this is the one moment where the cheap path costs nothing.
        if (Count == 0 && wanted.Count > 0)
        {
            ResetTo(wanted, nothingToPreserve: true);

            return;
        }

        var keeping = new HashSet<EntryRow>(wanted);

        for (var index = Count - 1; index >= 0; index--)
        {
            if (!keeping.Contains(this[index]))
            {
                RemoveAt(index);
            }
        }

        // A ROW ALREADY HERE IS MOVED, NEVER INSERTED A SECOND TIME.
        //
        // This pass used to insert whenever the object at a position was not the one wanted
        // there, which is correct only while what survived the pass above is in the same relative
        // order as what is wanted - an assumption nothing stated and nothing checked. Two rows
        // that swap places break it outright: [A, B] against a wanted [B, A] inserted B at the
        // front and left the old B where it was, so the list ended [B, A, B] and the same service
        // was on screen twice.
        //
        // It is reachable. The order comes from the service control manager, and Snapshot says in
        // as many words that the manager's order is in no contract and has been seen to move -
        // and a full reading happens on F5 and whenever a service is installed or removed.
        //
        // The invariant guarding this list did not see it either, because it asks whether the
        // list holds a row the query rejected, and in a swap both rows are wanted.
        for (var index = 0; index < wanted.Count; index++)
        {
            if (index < Count && ReferenceEquals(this[index], wanted[index]))
            {
                continue;
            }

            var already = IndexOf(wanted[index]);

            // Moved rather than removed and added, because a move keeps the row object - and the
            // selection and the scroll position ride on the row objects being the same ones.
            if (already >= 0)
            {
                Move(already, index);
            }
            else
            {
                Insert(index, wanted[index]);
            }
        }
    }
}
