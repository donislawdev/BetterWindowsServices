using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One column, as something a person can turn on and off.
///
/// <b>`A8`, and the promise is the opposite of the chips' one.</b> A chip teaches the query
/// language by writing into a box somebody can read. This teaches nothing and is not supposed to:
/// it answers "what else does this tool know about a service", which is a question the window
/// could not be asked at all before, because eleven of the seventeen answers had nowhere to go.
///
/// <b>State of its own, which is where this differs from <see cref="FilterChip"/>.</b> A chip
/// holds none because the query text is the one true copy and the chip is a view onto it. There is
/// no such text here - the layout IS this, until S6d3 writes it to a file - so the flag lives here
/// and the grid follows it.
/// </summary>
public sealed class ColumnChoice : Observable
{
    private readonly string _labelKey;
    private bool _shown;
    private bool _mayHide = true;
    private bool _yielding;

    internal ColumnChoice(Column column)
    {
        Column = column;
        _labelKey = column.LabelKey;
        _shown = column.ShownAtFirst;
    }

    /// <summary>Which column this stands for.</summary>
    internal Column Column { get; }

    /// <summary>
    /// What it is called, in the language of whoever is reading it - and, while the details panel
    /// has taken it off the list, where it went. A tick beside a column that is not on the list
    /// would be the picker contradicting the screen, so the label says why rather than the tick
    /// lying or going out.
    /// </summary>
    public string Label => _yielding && _shown
        ? Texts.Of("gui.columns.yielded", Texts.Of(_labelKey))
        : Texts.Of(_labelKey);

    /// <summary>
    /// The heading this one sits under in the picker.
    ///
    /// Seventeen ticks in one column is a list somebody scans rather than reads, and eleven of them
    /// are off - so the ones a person has never seen are exactly the ones hardest to find. Which
    /// heading each column belongs to is decided in <see cref="Columns"/>, where the whole grouping
    /// can be looked at at once.
    /// </summary>
    public string Group => Columns.GroupOf(Column.Id) is { } key ? Texts.Of(key) : string.Empty;

    /// <summary>
    /// Whether somebody chose this column - which is what the layout file keeps and what the tick
    /// in the picker shows. Not the same as being on the list since 2026-09-24: see
    /// <see cref="IsOnList"/>.
    /// </summary>
    public bool IsShown
    {
        get => _shown;
        set
        {
            if (Set(ref _shown, value))
            {
                Raise(nameof(IsOnList));
                Raise(nameof(Label));
            }
        }
    }

    /// <summary>
    /// Whether the column is on the list right now: chosen, and not stepped aside for the details
    /// panel. The grid follows THIS, and the layout file follows <see cref="IsShown"/> - the two
    /// answers were one until UX-GUI-012, and a panel that hid a column through the only answer
    /// there was would have written "turned off" into somebody's profile on every close.
    /// </summary>
    public bool IsOnList => _shown && !_yielding;

    /// <summary>Whether the open details panel has this column off the list. Set by <see cref="ColumnBar"/>.</summary>
    internal bool Yielding
    {
        get => _yielding;
        set
        {
            if (_yielding != value)
            {
                _yielding = value;
                Raise(nameof(IsOnList));
                Raise(nameof(Label));
            }
        }
    }

    /// <summary>
    /// Whether it may be turned off, which is false for the last one still on.
    ///
    /// <b>Refused by the control being unavailable rather than by the click doing nothing.</b> A
    /// list with no columns at all is 809 rows of nothing above a count line still saying 809 -
    /// which is the empty rectangle `docs/11` complaint 9 is about, arriving by a door nobody
    /// thought to close. A tick box that quietly springs back is the other way to prevent it and
    /// is worse: the person is told after acting rather than before, and rule 8 of the project
    /// notes is about exactly that difference.
    /// </summary>
    public bool MayHide
    {
        get => _mayHide;
        internal set => Set(ref _mayHide, value);
    }
}

/// <summary>
/// Which columns the list is showing, and which it could show.
///
/// <b>It belongs to the window and to nothing else, which is the owner's decision of
/// 2026-08-11.</b> Section H of the specification says the window and the command line share one
/// configuration - and `ListingTable.cs` carries the opposite decision, written down and argued:
/// there, a column is present because there is data behind it and never because a switch was
/// passed. A shared layout would have to overrule one of those two, so the layout stays here and
/// what is shared stays the Smart Views and the tags.
///
/// <b>Its own view model rather than a property on <see cref="MainViewModel"/>, and the size
/// ratchet is what made somebody ask.</b> That file had six lines of room, which is the ratchet
/// doing its job - and the honest answer to its question was that this is not that class's
/// business. MainViewModel is about what the list CONTAINS: the machine, the query, the rows and
/// what the window says about them. Which columns those rows are shown through is a different
/// subject, and the window is the only thing that needs both.
/// </summary>
/// <summary>
/// One heading in the column picker and the columns under it, as a SUBMENU.
///
/// <b>MEASURED 2026-09-05, WHICH IS WHY THIS IS NOT A FLAT LIST ANY MORE.</b> The picker handed
/// the menu 32 items - 27 columns, four headings and the way back. The menu is capped at 420
/// units, which on this machine at 150% is 630 device pixels, and an item measures 51 of them.
/// Counted from outside with the menu open: <b>13 items on screen and 19 below the fold</b>,
/// starting at Publisher - so the whole of "About the entry" and the whole of "Advanced" were
/// reachable only by scrolling a menu most people never scroll. The owner reported it as the list
/// looking small. It was not small, it was cut off.
///
/// <b>Raising the ceiling was the other way and it does not work.</b> Thirty-two items is about
/// 1630 device pixels of menu, which is taller than the screen it opens on. Four items and a way
/// back is 255, and the longest group inside is nine, which is 459 - so nothing scrolls anywhere.
/// Owner's decision, 2026-09-05.
///
/// <b>A SUBMENU IS NOT A GROUP, AND THE DIFFERENCE IS THE WHOLE REASON THIS SHAPE IS ALLOWED.</b>
/// The picker was grouped with <c>GroupStyle</c> on 2026-08-12, looked right on screen, and from
/// outside offered twelve togglable elements - all of them filter chips - and not one of the
/// seventeen columns. WPF builds a <c>GroupItem</c> between the menu and its items and the menu's
/// peer does not reach through it. A submenu has no such thing in the middle: it is a
/// <c>MenuItem</c> whose children are <c>MenuItem</c>s, each with a peer of its own. Verified from
/// outside after the change rather than reasoned about, because that is how the first attempt was
/// caught.
///
/// <b>What it costs, said rather than left to be found.</b> Which columns are on can no longer be
/// read at a glance - it takes four openings instead of one scroll. That is the trade for every
/// group being reachable at all.
/// </summary>
public sealed class ColumnGroup
{
    private readonly string _labelKey;

    internal ColumnGroup(string labelKey, IReadOnlyList<ColumnChoice> choices)
    {
        _labelKey = labelKey;
        Choices = choices;
    }

    /// <summary>What this heading says, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of(_labelKey);

    /// <summary>The columns under it, in the catalogue's order.</summary>
    public IReadOnlyList<ColumnChoice> Choices { get; }
}

/// <summary>
/// The last item in the column picker: put the list back the way this window opens.
///
/// <b>It exists because what somebody chooses here is kept on disk.</b> A layout is written the
/// moment a column goes on or off and comes back on the next start, so a person who turned on the
/// description and the five signature columns to look at something once has no way back except
/// turning them off one at a time - and the catalogue holds twenty-six.
///
/// <b>A third kind of entry rather than a flag on a choice</b>, because it is not a column: it has
/// no width, no order and nothing to be shown or hidden. The style selector beside the window tells
/// the three apart, which is the same mechanism the headings already use and the reason they are
/// items rather than a group.
///
/// Nothing here does the restoring. What a default layout IS belongs to
/// <see cref="ColumnLayout.DefaultFor"/>, and WHEN one is worth writing down belongs to
/// <c>KeptColumns</c> - this is a label and a place to click.
/// </summary>
public sealed class ColumnReset
{
    /// <summary>What the item says, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of("gui.columns.restore");
}

public sealed class ColumnBar
{
    public ColumnBar()
    {
        Choices = [.. Columns.All.Select(column => new ColumnChoice(column))];

        // WHAT THE MENU IS HANDED: one item per heading, each carrying the columns under it, and
        // the way back at the end. Five items instead of thirty-two, which is the difference
        // between a menu that fits and one where nineteen items sat below the fold - see
        // ColumnGroup for the measurement.
        //
        // Built once, in the catalogue's order, because the picker is a chooser rather than a
        // readout and a list that rearranges itself under the pointer is harder to use.
        var entries = new List<object>();

        foreach (var group in Choices.GroupBy(choice => choice.Group, StringComparer.Ordinal))
        {
            var first = group.First().Column.Id;

            // A COLUMN UNDER NO HEADING IS A FAILED TEST RATHER THAN A DEFAULT - Columns.Groups
            // says so where the map lives, and putting one quietly under whichever heading was
            // least wrong is exactly what it refuses. Nothing here changes that.
            //
            // What changed on 2026-08-26 is what it looks like when it happens. The bang stood
            // here, so a column missing from the map handed null to Texts.Of, which threw
            // ArgumentNullException about a key - inside the constructor of this class, which is
            // inside the constructor of the window. Six tests already go red when the map is
            // incomplete, and none of them named the column. This one does.
            entries.Add(new ColumnGroup(
                Columns.GroupOf(first)
                    ?? throw new InvalidOperationException(
                        $"The column '{first}' is under no heading. Columns.Groups has to name every "
                        + "column in the catalogue - see the argument written at that map."),
                [.. group]));
        }

        // LAST, AFTER EVERY GROUP, because it is about the whole list rather than about one column
        // and because a person reaches for it after reading what is there rather than before.
        entries.Add(new ColumnReset());

        Entries = entries;

        foreach (var choice in Choices)
        {
            choice.PropertyChanged += (_, changed) =>
            {
                if (changed.PropertyName == nameof(ColumnChoice.IsShown))
                {
                    Rethink();
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            };
        }

        Rethink();
    }

    /// <summary>
    /// Somebody has turned a column on or off - the one layout change that happens through this
    /// class rather than through the grid.
    ///
    /// <b>Raised from the subscription rather than from <see cref="Rethink"/>, which also runs
    /// once while this object is being built.</b> A change announced from a constructor is a
    /// change nobody could have subscribed to in time, and the listener that matters here writes a
    /// file - so it would have been either a write of the defaults on every startup or a handler
    /// that has to know it is being lied to.
    /// </summary>
    internal event EventHandler? Changed;

    /// <summary>
    /// Every column, in the order they are offered.
    ///
    /// <b>The catalogue's order, always, and never the order the grid is in.</b> This is a chooser
    /// rather than a readout - a list that rearranges itself under the pointer while somebody is
    /// ticking boxes is harder to use than a fixed one, and the order of the LIST is what dragging
    /// a heading is for.
    /// </summary>
    public IReadOnlyList<ColumnChoice> Choices { get; }

    /// <summary>The same choices with their headings between them, which is what the menu shows.</summary>
    public IReadOnlyList<object> Entries { get; }

    /// <summary>
    /// Whether the details panel is open beside the list, which takes the prose columns off it -
    /// <see cref="Column.YieldsToPanel"/>. Told by the window, because the window is what holds
    /// both the panel and this bar.
    /// </summary>
    internal bool PanelOpen
    {
        get => _panelOpen;
        set
        {
            _panelOpen = value;
            Rethink();
        }
    }

    private bool _panelOpen;

    /// <summary>The columns that are on, in the catalogue's order.</summary>
    public IEnumerable<ColumnChoice> Shown => Choices.Where(choice => choice.IsShown);

    /// <summary>
    /// What the columns on screen need the window to go and read - the picker's half of the
    /// question <c>Query.Needs</c> answers for the box above the list.
    ///
    /// <b>Read off what is SHOWN rather than remembered, exactly like <see cref="FilterChip.IsOn"/>
    /// reads the query text.</b> A field holding this would be a second copy of an answer the
    /// ticks already carry, and the two would disagree the first time a kept layout was applied
    /// through <see cref="Follow"/> - which happens before anybody has clicked anything.
    ///
    /// <b>Asked once a second rather than pushed on change, and that is why it is a property.</b>
    /// The window's tick already asks whether the question on screen has outrun what was read, so
    /// a column turned on is picked up by the next tick with nothing to subscribe to and no order
    /// of wiring to get right. It costs one pass over 27 flags.
    /// </summary>
    internal ExtraRead Needs =>
        Shown.Aggregate(ExtraRead.None, (needs, choice) => needs | choice.Column.Needs);

    /// <summary>
    /// Turns on what a kept layout had on, and turns the rest off.
    ///
    /// <b>Only the shown flags, because they are the only part of a layout that lives here.</b>
    /// The order somebody dragged a heading into and the width they dragged an edge to are held by
    /// the grid and nowhere else, so the same plan is applied in two places rather than copied
    /// into a third.
    ///
    /// <b>It expects a plan rather than a file</b>, and that is what makes it total: a plan names
    /// every column this build has, exactly once, with at least one of them shown. A column the
    /// plan somehow does not mention keeps what it already had rather than being turned off, which
    /// is the second lock on a door <see cref="ColumnPlan.Of"/> already closes.
    /// </summary>
    internal void Follow(ColumnPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var kept = plan.Layout.Columns.ToDictionary(column => column.Id, StringComparer.Ordinal);

        foreach (var choice in Choices)
        {
            if (kept.TryGetValue(choice.Column.Id, out var column))
            {
                choice.IsShown = column.Shown;
            }
        }
    }

    /// <summary>
    /// Works out which choices may still be turned off.
    ///
    /// Runs after every change rather than only after the one that was clicked, for the reason
    /// <see cref="FilterBar.Rethink"/> gives about the chips: one edit moves several of them.
    /// Turning a second column on releases the first, and nothing about that click mentions the
    /// first.
    /// </summary>
    private void Rethink()
    {
        // WHICH COLUMNS STEP ASIDE FOR THE PANEL, AND NEVER THE LAST ONE ON THE LIST. A list whose
        // only chosen column is the description would otherwise go blank the moment the panel
        // opened - `docs/11` complaint 9 arriving by a door nobody thought to close, the same one
        // MayHide shuts below.
        var others = Choices.Any(choice => choice.IsShown && !choice.Column.YieldsToPanel);

        foreach (var choice in Choices)
        {
            choice.Yielding = _panelOpen && others && choice.Column.YieldsToPanel;
        }

        // COUNTED ON THE LIST RATHER THAN IN THE CHOICES since 2026-09-24. With the description
        // away for the panel, a list of the name and the description is a list of one column, and
        // counting choices would let that one go too.
        var alone = Choices.Count(choice => choice.IsOnList) <= 1;

        foreach (var choice in Choices)
        {
            choice.MayHide = !(alone && choice.IsOnList);
        }
    }
}
