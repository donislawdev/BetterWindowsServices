namespace Bws.Gui.ViewModels;

/// <summary>
/// Which rows of a comparison the table shows - `D2` "filtrowalne", owner's decision W2-b of
/// 2026-10-15: a button with a menu, the way Columns opens its own.
///
/// <b>THE CHOICES ARE MADE FROM THIS COMPARISON, NOT FROM A LIST OF EVERY FIELD.</b> Each carries the
/// number of rows it would show, and only the ones that narrow anything are offered - a choice that
/// shows nothing teaches that the menu lies, and one that shows every row is "All rows" said twice.
/// Measured on the owner's machine 2026-10-14: 25 rows, of which FOUR were changes and twenty-one were
/// "seen on one side only" or "not compared", so "Differences only" is the choice this exists for more
/// than the field the spec gives as its example.
///
/// <b>NOTHING IS HIDDEN UNTIL SOMEBODY CHOOSES</b>, and while something is, <see cref="Showing"/> says
/// how much - rule 8: a table that lost rows without a word would read as the whole comparison. The
/// sentences above the table are not rows and no choice hides them.
///
/// <b>The collection stays and its contents are replaced in one notification</b> (<see cref="ShownRows{TRow}"/>).
/// Staying is what keeps the order somebody sorted the table into - the grid sorts a view over this
/// collection, and a collection swapped for another would take the sort with it. One notification
/// rather than one per row since a review of 2026-10-15: the first version removed and inserted row by
/// row, and the list measured a notification per row at about 285 ms for 810 rows (RowList), which a
/// comparison of two different machines reaches.
///
/// <b>One group of choices</b>, drawn by the same ShowRow as the change history's two - see
/// <see cref="IRowChoices"/>.
/// </summary>
public sealed class DiffFilter : Observable, IRowChoices
{
    private readonly IReadOnlyList<DiffLine> _all;
    private readonly Dictionary<RowChoice, Func<DiffLine, bool>> _takes = new();
    private RowChoice _chosen;

    internal DiffFilter(IReadOnlyList<DiffLine> all)
    {
        _all = all;
        Choices = ChoicesOf(all);
        Groups = [Choices];
        _chosen = Choices[0];
        _chosen.Mark(chosen: true);
        Rows = new ShownRows<DiffLine>(all);
    }

    /// <summary>What the menu under the button offers, "All rows" first.</summary>
    public IReadOnlyList<RowChoice> Choices { get; }

    /// <inheritdoc />
    public IReadOnlyList<IReadOnlyList<RowChoice>> Groups { get; }

    /// <summary>The rows the table shows - every row until somebody chooses otherwise.</summary>
    public ShownRows<DiffLine> Rows { get; }

    /// <summary>The words on the button, naming what is shown.</summary>
    public string Button => Texts.Of("gui.diff.show", _chosen.Name);

    /// <inheritdoc />
    public string Hint => Texts.Of("gui.diff.show.hint");

    /// <summary>A comparison that found nothing has no table under it, and no button over one.</summary>
    public bool Offered => _all.Count > 0;

    /// <summary>
    /// How many of the rows are on screen, or empty while all of them are. The noun counts the whole,
    /// so the pair is chosen by it - one row in all is never narrowed, but the language file cannot
    /// know that and CountedWordGuards asks for both halves.
    /// </summary>
    public string Showing =>
        Rows.Count == _all.Count ? string.Empty
        : _all.Count == 1 ? Texts.Of("gui.diff.showing.one", Rows.Count, _all.Count)
        : Texts.Of("gui.diff.showing.many", Rows.Count, _all.Count);

    /// <summary>
    /// The choice somebody ticked in the menu becomes what the table shows. Ticking the one already
    /// shown changes nothing - the menu has no "none", because a table with no choice is a table with
    /// no rows and no reason.
    /// </summary>
    internal void Choose(RowChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        if (!_takes.TryGetValue(choice, out var takes))
        {
            return;
        }

        _chosen.Mark(chosen: false);
        _chosen = choice;
        _chosen.Mark(chosen: true);

        // In the comparison's own order - the grid lays its sort over that, and without a sort this
        // is the order the command line reports in.
        Rows.ShowOnly(_all.Where(takes));

        Raise(nameof(Button));
        Raise(nameof(Showing));
    }

    /// <summary>
    /// The choices this comparison can offer: every row, the differences alone, each field that
    /// differs, the rows one side could not see, the fields one side holds no value for.
    ///
    /// <b>A field is named by the words its rows already say</b> - "Status (running state)" in the
    /// menu is what the table's What column says on each of those rows.
    /// </summary>
    private List<RowChoice> ChoicesOf(IReadOnlyList<DiffLine> all)
    {
        var offered = new List<RowChoice> { Made(Texts.Of("gui.diff.show.all"), all.Count, _ => true) };

        Offer(offered, Texts.Of("gui.diff.show.differences"), all, line => line.Kind is DiffLineKind.Added or DiffLineKind.Removed or DiffLineKind.Changed);

        foreach (var field in all.Where(line => line.Kind == DiffLineKind.Changed).GroupBy(line => line.Field))
        {
            Offer(offered, field.First().What, all, line => line.Kind == DiffLineKind.Changed && line.Field == field.Key);
        }

        Offer(offered, Texts.Of("gui.diff.show.uncertain"), all, line => line.Kind == DiffLineKind.Uncertain);
        Offer(offered, Texts.Of("gui.diff.show.notCompared"), all, line => line.Kind == DiffLineKind.NotCompared);

        return offered;
    }

    /// <summary>Offers a choice only when it shows something and less than everything.</summary>
    private void Offer(List<RowChoice> offered, string name, IReadOnlyList<DiffLine> all, Func<DiffLine, bool> takes)
    {
        var count = all.Count(takes);

        if (count > 0 && count < all.Count)
        {
            offered.Add(Made(name, count, takes));
        }
    }

    /// <summary>A choice with its count in the menu's words, and the rows it shows kept here.</summary>
    private RowChoice Made(string name, int count, Func<DiffLine, bool> takes)
    {
        var choice = new RowChoice(name, Texts.Of("gui.diff.show.counted", name, count), Choose);

        _takes[choice] = takes;

        return choice;
    }
}
