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
public sealed class ColumnBar
{
    public ColumnBar()
    {
        Choices = [.. Columns.All.Select(column => new ColumnChoice(column))];

        foreach (var choice in Choices)
        {
            choice.PropertyChanged += (_, changed) =>
            {
                if (changed.PropertyName == nameof(ColumnChoice.IsShown))
                {
                    Rethink();
                }
            };
        }

        Rethink();
    }

    /// <summary>
    /// Every column, in the order they are offered.
    ///
    /// <b>The catalogue's order, always, and never the order the grid is in.</b> This is a chooser
    /// rather than a readout - a list that rearranges itself under the pointer while somebody is
    /// ticking boxes is harder to use than a fixed one, and the order of the LIST is what dragging
    /// a heading is for.
    /// </summary>
    public IReadOnlyList<ColumnChoice> Choices { get; }

    /// <summary>The columns that are on, in the catalogue's order.</summary>
    public IEnumerable<ColumnChoice> Shown => Choices.Where(choice => choice.IsShown);

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
