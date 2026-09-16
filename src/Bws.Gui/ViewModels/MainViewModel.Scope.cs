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
            Raise(nameof(SearchHint));
        }
    }

    /// <summary>
    /// What the readings call when the machine has been asked again.
    ///
    /// <b>The scope is recut here and NOT in <see cref="Apply"/>, and the split is the whole reason
    /// this method exists.</b> Apply runs on every keystroke against a 50 ms budget, and the scope
    /// cannot change while somebody types - so recutting it there would pay for a second pass over
    /// 812 entries per character, on exactly the path that was measured and fixed on 2026-08-19.
    /// It CAN change here, because an entry that arrived or left changes what the scope holds, and
    /// a list that skipped this would go on showing a service the machine no longer has.
    ///
    /// <b>In this file since 2026-09-15</b>, when MainViewModel.cs stood one line under the ceiling
    /// and the positions of the switch needed telling here that their counts moved - the counts
    /// are cut with the scope, so the reading that recuts is the reading that has to say so.
    /// </summary>
    private void Reread()
    {
        _scoping.Recut();

        foreach (var position in ScopePositions)
        {
            position.Rethink();
        }

        Apply();
    }

    /// <summary>
    /// The sentence behind the empty search box, naming what the box searches - which is the list
    /// somebody is standing on, not the whole machine.
    ///
    /// <b>Point 8(b) of the review in `docs/11` 2.14.</b> The hint said "Search services and
    /// drivers" on every scope, and the window opens on Services - a list from which, its own
    /// tooltip says, a search never reaches a driver. A hint that names something the box cannot
    /// find is the first sentence a new person reads and the first one that is not true.
    ///
    /// <b>One key per scope, each inside its own Texts.Of</b> - the shape PlanWords takes, for the
    /// reason written there: a key chosen into a variable is invisible to TextKeyGuards. An unnamed
    /// scope refuses rather than picking a sentence, as Scopes.QueryFor does one file over.
    /// </summary>
    public string SearchHint => Scope switch
    {
        EntryScope.Services => Texts.Of("gui.search.hint.services"),
        EntryScope.Drivers => Texts.Of("gui.search.hint.drivers"),
        EntryScope.Everything => Texts.Of("gui.search.hint.all"),
        _ => throw new InvalidOperationException($"No search hint is written for the scope {Scope}.")
    };
}
