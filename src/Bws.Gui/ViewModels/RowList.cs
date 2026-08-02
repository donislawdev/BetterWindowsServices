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
}
