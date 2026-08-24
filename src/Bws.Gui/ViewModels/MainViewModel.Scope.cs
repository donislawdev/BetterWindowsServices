namespace Bws.Gui.ViewModels;

/// <summary>
/// Which of the two lists the window is showing, and what moving between them costs.
///
/// <b>Its own file since 2026-08-19, and the size ratchet is what asked</b> - the same answer
/// Planned.Sections.cs got when that class grew past its own ceiling. The subject is real: what
/// the rest of MainViewModel does is turn a question into rows, and this decides WHICH rows the
/// question is asked of.
///
/// <b>A partial rather than a class of its own, and the boundary is where the state stops.</b>
/// The state itself - which scope, and the listing cut down to it - already IS a class of its own,
/// <see cref="Scoping"/>, which knows nothing about a window. What is here is what has to happen
/// to the REST of the model when that state moves: a selection let go of, a query reapplied, three
/// positions told to look again. All three need MainViewModel internals, so a separate class would
/// take them as arguments and be this class wearing a different name.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The positions of the scope switch, in the order they are shown.</summary>
    public IReadOnlyList<ScopeChoice> ScopePositions { get; }

    /// <summary>
    /// Which list the window is showing - <see cref="EntryScope"/>.
    ///
    /// <b>Changing it clears the selection, and that is a safety decision rather than a
    /// convenience.</b> Everything that changes a machine acts on what is selected, and `A7` asks
    /// for drivers to be "clearly separated visually and warned about when operated on" while
    /// `docs/01` asks for a heavier confirmation path for them. A selection that survived a move
    /// from Services to Drivers would put five services under a button on a screen that says
    /// Drivers - the plan would be right and the sentence above it would be a lie.
    ///
    /// Setting it to what it already is does nothing at all, so a radio button re-announcing itself
    /// cannot throw somebody's selection away.
    /// </summary>
    public EntryScope Scope
    {
        get => _scoping.Current;

        set
        {
            if (!_scoping.MoveTo(value))
            {
                return;
            }

            Chosen.Row = null;

            Apply();

            foreach (var position in ScopePositions)
            {
                position.Rethink();
            }

            Raise(nameof(Scope));
        }
    }
}
