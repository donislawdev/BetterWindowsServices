namespace Bws.Gui.ViewModels;

/// <summary>
/// Whether the visible list may be rearranged right now.
///
/// One policy, in one place, since 2026-08-05. It used to be a field, a property and a method
/// spread through <see cref="MainViewModel"/>, which is where the size ratchet found a seam and
/// named it - and the seam is real rather than convenient: <c>Interacting</c>, the memory of
/// what was held back and the decision itself are three parts of a single rule from `A10`.
///
/// <b>The rule holds an ORDER, not a list.</b> Cells keep changing while this says no, because a
/// row that quietly says the wrong thing is a far better failure than a row that disappears from
/// under a cursor reaching for it.
/// </summary>
internal sealed class Holding
{
    /// <summary>
    /// Whether somebody is using the list - pointing at it, or with the keyboard in it.
    ///
    /// Set from the window, because only a window knows about focus and a mouse.
    /// </summary>
    public bool Interacting { get; set; }

    /// <summary>
    /// Whether a rearrangement was refused and is still owed.
    ///
    /// It is what the window says out loud under the list. A list holding still and not saying so
    /// would be silently disagreeing with its own query, which is rule 8 arriving from the side it
    /// looks like a courtesy from.
    /// </summary>
    public bool Pending { get; private set; }

    /// <summary>
    /// Answers whether the shown list may become the selected one, and remembers a refusal.
    ///
    /// <b>An empty list is never held, and that half is not an optimisation.</b> A first fill is
    /// not a rearrangement of anything - there is nothing under the pointer to protect - and
    /// treating it as one meant a window opened under the mouse never filled at all while
    /// reporting the full count above the empty grid. Backlog 127, found by the one instrument a
    /// person has to run, and by none of the tests: they all load first and interact afterwards,
    /// which is a person's order rather than a script's.
    ///
    /// It is the same argument <see cref="RowList.Reconcile"/> already uses for its cheap path,
    /// and saying it twice in two shapes is how the two would drift apart.
    /// </summary>
    public bool MayRearrange(RowList shown, List<EntryRow> selected)
    {
        if (Interacting && shown.Count > 0)
        {
            // Only a difference is worth remembering. Refusing a rearrangement that would have
            // changed nothing and then announcing it would put a sentence under the list for as
            // long as the pointer rested there, saying something untrue about the list.
            Pending = shown.Count != selected.Count || !shown.SequenceEqual(selected);

            return false;
        }

        Pending = false;

        return true;
    }
}
