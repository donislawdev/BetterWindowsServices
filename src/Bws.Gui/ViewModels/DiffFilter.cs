using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

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
/// <b>The collection stays and its contents are replaced in one notification</b> (<see cref="DiffRows"/>).
/// Staying is what keeps the order somebody sorted the table into - the grid sorts a view over this
/// collection, and a collection swapped for another would take the sort with it. One notification
/// rather than one per row since a review of 2026-10-15: the first version removed and inserted row by
/// row, and the list measured a notification per row at about 285 ms for 810 rows (RowList), which a
/// comparison of two different machines reaches.
/// </summary>
public sealed class DiffFilter : Observable
{
    private readonly IReadOnlyList<DiffLine> _all;
    private DiffChoice _chosen;

    internal DiffFilter(IReadOnlyList<DiffLine> all)
    {
        _all = all;
        Choices = Offered(all);
        _chosen = Choices[0];
        _chosen.Mark(chosen: true);
        Rows = new DiffRows(all);
    }

    /// <summary>What the menu under the button offers, "All rows" first.</summary>
    public IReadOnlyList<DiffChoice> Choices { get; }

    /// <summary>The rows the table shows - every row until somebody chooses otherwise.</summary>
    public DiffRows Rows { get; }

    /// <summary>The words on the button, naming what is shown.</summary>
    public string Button => Texts.Of("gui.diff.show", _chosen.Name);

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
    internal void Choose(DiffChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        if (!Choices.Contains(choice))
        {
            return;
        }

        _chosen.Mark(chosen: false);
        _chosen = choice;
        _chosen.Mark(chosen: true);

        // In the comparison's own order - the grid lays its sort over that, and without a sort this
        // is the order the command line reports in.
        Rows.ShowOnly(_all.Where(choice.Takes));

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
    private List<DiffChoice> Offered(IReadOnlyList<DiffLine> all)
    {
        var offered = new List<DiffChoice> { new(this, Texts.Of("gui.diff.show.all"), all.Count, _ => true) };

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
    private void Offer(List<DiffChoice> offered, string name, IReadOnlyList<DiffLine> all, Func<DiffLine, bool> takes)
    {
        var count = all.Count(takes);

        if (count > 0 && count < all.Count)
        {
            offered.Add(new DiffChoice(this, name, count, takes));
        }
    }
}

/// <summary>
/// One item of the menu under "Show": a name, how many rows it shows, and whether it is what the
/// table shows now.
///
/// <b>A tick that cannot be taken off.</b> The menu item is bound two way the way a column choice is,
/// and ticking another choice is what moves the tick - so a click on the ticked one says "this" again
/// rather than "nothing", and the tick is put straight back.
/// </summary>
public sealed class DiffChoice : Observable
{
    private readonly DiffFilter _filter;
    private bool _chosen;

    internal DiffChoice(DiffFilter filter, string name, int count, Func<DiffLine, bool> takes)
    {
        _filter = filter;
        Name = name;
        Label = Texts.Of("gui.diff.show.counted", name, count);
        Takes = takes;
    }

    /// <summary>What is shown, the way the button says it.</summary>
    public string Name { get; }

    /// <summary>What is shown and how many rows that is, the way the menu says it.</summary>
    public string Label { get; }

    /// <summary>Whether this is what the table shows now.</summary>
    public bool IsChosen
    {
        get => _chosen;
        set
        {
            if (value)
            {
                _filter.Choose(this);
            }
            else
            {
                Raise(nameof(IsChosen));
            }
        }
    }

    /// <summary>Whether a row is one this choice shows.</summary>
    internal Func<DiffLine, bool> Takes { get; }

    internal void Mark(bool chosen) => Set(ref _chosen, chosen, nameof(IsChosen));
}

/// <summary>
/// The rows a comparison's table shows, replaced all at once when "Show" changes.
///
/// <b>RowList.ResetTo's mechanism, and its measurement is the reason</b>: a notification per row is
/// a notification the grid answers per row. The second place this is done, so a small type rather than
/// a shared base - the list's version carries a contract about the selection and the scroll position
/// (`A10`) that a comparison's table does not have.
/// </summary>
public sealed class DiffRows : ObservableCollection<DiffLine>
{
    internal DiffRows(IEnumerable<DiffLine> rows)
        : base(rows)
    {
    }

    /// <summary>
    /// Replaces every row with these, saying so once. The three notifications are the three WPF needs
    /// - RowList.ResetTo says why each one.
    /// </summary>
    internal void ShowOnly(IEnumerable<DiffLine> rows)
    {
        CheckReentrancy();

        Items.Clear();

        foreach (var row in rows)
        {
            Items.Add(row);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
