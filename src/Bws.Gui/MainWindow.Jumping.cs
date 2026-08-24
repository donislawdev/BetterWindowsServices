using System.Collections;
using System.Windows.Controls;

namespace Bws.Gui;

/// <summary>
/// Finding a row by the letter it starts with - `C4`, and how an administrator reaches a service
/// in a list of eight hundred without touching the search box.
///
/// <b>A file of its own because the class crossed the length the ratchet allows</b>, the same
/// pressure that cut MainWindow.Columns.cs and MainWindow.Scrolling.cs out of it, and the same kind
/// of seam: everything about jumping to a row is here and nothing else is.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Moves the selection to the next entry beginning with a character, and says whether it moved.
    ///
    /// Apart from the handler for the same reason <see cref="Act"/> is: the half that can be
    /// checked should not live inside the half that cannot. What it returns is the part that is
    /// easy to get wrong - a press marked handled by something that did nothing is a press that
    /// silently stops working for whatever needed it next.
    /// </summary>
    internal bool JumpTo(char? letter)
    {
        if (letter is null)
        {
            return false;
        }

        // The grid's own order, not the model's. Once a column can be sorted they are two
        // different sequences, and the one somebody is looking at is this one.
        var row = ViewModels.RowList.NextStartingWith(
            new Displayed(Entries.Items),
            Entries.SelectedItem as ViewModels.EntryRow,
            letter.Value);

        if (row is null)
        {
            return false;
        }

        // The grid first, then the model, then the view. Setting the grid's selection is what
        // raises the change that hands the row to the model everywhere else in this window, so
        // doing it here keeps one path rather than two that can disagree.
        Entries.SelectedItem = row;
        Entries.ScrollIntoView(row);

        return true;
    }

    /// <summary>
    /// The rows the grid is showing, read in place rather than copied.
    ///
    /// <b>JumpTo used to build an array of every row on every keystroke</b> - eight hundred
    /// references copied so that one letter could find the next match, and thrown away before the
    /// next character arrived. ItemCollection is already indexable, so this is one small object
    /// instead of one large one, and the search reads exactly the same sequence.
    ///
    /// It stays a view of the grid rather than a snapshot on purpose: the order somebody is looking
    /// at is the grid's, which is not the model's once a column has been sorted.
    /// </summary>
    private sealed class Displayed(ItemCollection shown) : IReadOnlyList<ViewModels.EntryRow>
    {
        public int Count => shown.Count;

        public ViewModels.EntryRow this[int index] => (ViewModels.EntryRow)shown[index]!;

        public IEnumerator<ViewModels.EntryRow> GetEnumerator()
        {
            for (var index = 0; index < shown.Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
