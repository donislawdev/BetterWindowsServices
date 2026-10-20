namespace Bws.Gui.ViewModels;

/// <summary>
/// The bar of tabs across the top of the window: which page is up - one of the three lists, or the Tools
/// page - and what moving between them does. `docs/PROJEKT-TOOLS-20261020.md`, owner's decisions T-a and
/// T-e of 2026-10-20.
///
/// <b>ITS OWN CLASS BECAUSE "WHICH PAGE" IS NOT "WHICH ROWS".</b> MainViewModel is the list's model: what the
/// window shows and how it got there. Until the Tools tab, the bar only ever chose a list, so its positions
/// lived there as a property. A tab that is not a list is a second subject, and the size ratchet said so the
/// same day - the list's model stood at the edge of the band for state and for members, and the tab would
/// have taken it over both.
///
/// <b>It owns the marks and the Tools page, and the list's model owns what happens to the list.</b> What leaving
/// a page does to the overview, the views of the audit and the panel is handed in as one action, and moving
/// the scope as another, so this class never reaches into the list - and a test can build it without one.
///
/// <b>A TAB, NOT A VIEW IN THE MIDDLE.</b> The overview and the views of the audit stand where the list was
/// and belong to it, so the list's tab stays marked over them. The Tools tab is a page of its own: the search,
/// the filters and the actions go (the window arranges that), the query and the scope wait untouched, and no
/// list tab is marked while it is up.
/// </summary>
public sealed class Tabs : Observable
{
    private readonly Action _clearTheMiddle;
    private readonly Action<EntryScope> _scope;
    private readonly Action _moved;
    private bool _showingTools;

    /// <param name="tools">What the Tools page holds.</param>
    /// <param name="current">The scope the list is on.</param>
    /// <param name="count">How many entries a scope's list holds, for the number beside its tab.</param>
    /// <param name="scope">Moves the list to a scope, through the list's own setter.</param>
    /// <param name="clearTheMiddle">
    /// Puts away whatever stands where the list was - the overview, a comparison, the form before a
    /// snapshot, the change history - and the panel about one entry. Asked when a page arrives, because the
    /// newer thing somebody asked for wins, the rule MainViewModel.OpenHistory follows.
    /// </param>
    /// <param name="moved">
    /// Tells the list's model the page moved, so the line under the window says again whether it may speak
    /// about a list.
    /// </param>
    internal Tabs(
        ToolsPage tools,
        Func<EntryScope> current,
        Func<EntryScope, int> count,
        Action<EntryScope> scope,
        Action clearTheMiddle,
        Action moved)
    {
        Tools = tools;
        _scope = scope;
        _clearTheMiddle = clearTheMiddle;
        _moved = moved;

        // No position is the page while the Tools page is - which is what the null says. A press on a
        // position goes through ShowTheList, so a marked tab over the overview is a way back to the list.
        Positions = Scopes.Positions(() => _showingTools ? null : current(), ShowTheList, count);
    }

    /// <summary>The list tabs, in the order they are shown.</summary>
    public IReadOnlyList<ScopeChoice> Positions { get; }

    /// <summary>What the Tools page holds.</summary>
    public ToolsPage Tools { get; }

    /// <summary>
    /// Whether the Tools page is up. Arriving puts away whatever stands where the list was, so leaving lands
    /// on the list itself rather than on a screen nobody asked to come back to.
    /// </summary>
    public bool ShowingTools
    {
        get => _showingTools;

        set
        {
            if (_showingTools == value)
            {
                return;
            }

            // THE FLAG FIRST, THEN THE MIDDLE CLEARED - a review of #83. Putting the overview or a view of
            // the audit away raises notifications, and the window arranges itself and the line under it
            // speaks on each of them. With the flag still down at that moment the list was "on screen"
            // for the length of the clearing - its controls drawn and its sentence said on the way to a
            // page that has neither.
            _showingTools = value;

            if (value)
            {
                _clearTheMiddle();
                Tools.Rethink();
            }

            // The list tabs read this to know they are not the page any more.
            foreach (var position in Positions)
            {
                position.Rethink();
            }

            Raise(nameof(ShowingTools));
            Raise(nameof(ToolsTabOn));
            _moved();
        }
    }

    /// <summary>
    /// The Tools tab's mark, which is what the tab binds.
    ///
    /// <b>The setter ignores false, for the reason ScopeChoice.IsOn ignores it</b>: the tabs are one group of
    /// radio buttons, and the group tells the tab being left that it is off BEFORE it tells the new one it is
    /// on. Acting on that false would leave the Tools page for a moment on the way to a list tab whose own
    /// press then leaves it again - two moves for one click. The list tab's arrival does the leaving.
    /// </summary>
    public bool ToolsTabOn
    {
        get => _showingTools;

        set
        {
            if (value)
            {
                ShowingTools = true;
            }

            Raise(nameof(ToolsTabOn));
        }
    }

    /// <summary>
    /// Escape's step on the Tools page, and Ctrl+F's first move there: back to the list - true when the page
    /// was up. MainViewModel.PutAwayAView holds it among the other steps that put a page away.
    /// </summary>
    internal bool LeaveTheTools()
    {
        if (!_showingTools)
        {
            return false;
        }

        ShowingTools = false;

        return true;
    }

    /// <summary>
    /// Asks the Tools page's cards again whether they can be pressed - after a reading starts, ends or fails -
    /// and only while the page is up, because the page asks them itself when it comes.
    /// </summary>
    internal void Rethink()
    {
        if (_showingTools)
        {
            Tools.Rethink();
        }
    }

    /// <summary>
    /// What pressing a list tab does since the tabs stopped hiding (T-e): that list, with the Tools page, the
    /// overview and any view of the audit put away. A press on a tab that is already marked is the only way
    /// to ask for that from the overview, so the bar hears it (ScopeBar.PositionPressed).
    /// </summary>
    private void ShowTheList(EntryScope scope)
    {
        ShowingTools = false;
        _clearTheMiddle();
        _scope(scope);
    }
}
