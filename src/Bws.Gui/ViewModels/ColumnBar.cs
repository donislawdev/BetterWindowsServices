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

    internal ColumnChoice(Column column)
    {
        Column = column;
        _labelKey = column.LabelKey;
        _shown = column.ShownAtFirst;
    }

    /// <summary>Which column this stands for.</summary>
    internal Column Column { get; }

    /// <summary>What it is called, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of(_labelKey);

    /// <summary>
    /// The heading this one sits under in the picker.
    ///
    /// Seventeen ticks in one column is a list somebody scans rather than reads, and eleven of them
    /// are off - so the ones a person has never seen are exactly the ones hardest to find. Which
    /// heading each column belongs to is decided in <see cref="Columns"/>, where the whole grouping
    /// can be looked at at once.
    /// </summary>
    public string Group => Columns.GroupOf(Column.Id) is { } key ? Texts.Of(key) : string.Empty;

    /// <summary>Whether the column is in the list right now.</summary>
    public bool IsShown
    {
        get => _shown;
        set => Set(ref _shown, value);
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
/// A heading in the column picker, as an ITEM rather than as a group.
///
/// <b>It is an item because grouping a menu takes its contents out of the automation tree, and
/// that was measured rather than feared.</b> The picker was grouped with <c>GroupStyle</c> on
/// 2026-08-12 and looked right on screen - and from outside, a window whose menu was open offered
/// twelve togglable elements, all of them filter chips, and not one of the seventeen columns. WPF
/// builds a <c>GroupItem</c> between the menu and its items, and the menu's peer does not reach
/// through it. A screen reader sees what the probe saw.
///
/// So the headings are items in the same flat list, told apart by a style selector: every entry
/// stays a real <c>MenuItem</c> with a peer of its own, and the grouping is drawn rather than
/// structural.
/// </summary>
public sealed class ColumnHeading
{
    private readonly string _labelKey;

    internal ColumnHeading(string labelKey) => _labelKey = labelKey;

    /// <summary>What this heading says, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of(_labelKey);
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

        // THE FLAT LIST THE MENU IS HANDED: a heading, then the columns under it, then the next.
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
            entries.Add(new ColumnHeading(Columns.GroupOf(first)
                ?? throw new InvalidOperationException(
                    $"The column '{first}' is under no heading. Columns.Groups has to name every "
                    + "column in the catalogue - see the argument written at that map.")));

            entries.AddRange(group);
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

    /// <summary>The columns that are on, in the catalogue's order.</summary>
    public IEnumerable<ColumnChoice> Shown => Choices.Where(choice => choice.IsShown);

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
        var alone = Choices.Count(choice => choice.IsShown) <= 1;

        foreach (var choice in Choices)
        {
            choice.MayHide = !(alone && choice.IsShown);
        }
    }
}
