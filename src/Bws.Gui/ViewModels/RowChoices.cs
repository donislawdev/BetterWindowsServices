using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What a "Show" button over a table offers: the words on the button, how much of the table is on
/// screen, and the choices under it in groups - ShowRow.xaml draws any of these.
///
/// <b>ONE COMPONENT FOR EVERY TABLE THAT CAN BE NARROWED, since 2026-10-20</b> - GUI rules 2 and 5 of
/// the project notes. It was the comparison's own (DiffFilterRow, W2-b of 2026-10-15) until the change
/// history needed the same button (J2-b), and a second copy of the file would have been a second place
/// to change the look of one control.
///
/// <b>GROUPS, BECAUSE A TABLE CAN BE NARROWED ALONG MORE THAN ONE LINE.</b> A comparison has one group
/// - which rows. The change history has two - identical changes together or each on its own, and whose
/// changes. One tick per group, and the menu draws a line between groups so the two ticks read as two
/// answers to two questions.
/// </summary>
public interface IRowChoices
{
    /// <summary>The words on the button, naming what is shown.</summary>
    string Button { get; }

    /// <summary>What the button does, for its tooltip.</summary>
    string Hint { get; }

    /// <summary>
    /// How much of the table is on screen, or empty while all of it is - rule 8: a table that lost rows
    /// without a word would read as the whole answer.
    /// </summary>
    string Showing { get; }

    /// <summary>The choices under the button, a group at a time, each group with one tick.</summary>
    IReadOnlyList<IReadOnlyList<RowChoice>> Groups { get; }

    /// <summary>
    /// Whether there is anything to narrow at all. A table with no rows has no button over it - a menu
    /// whose every choice shows nothing would teach that the menu lies.
    /// </summary>
    bool Offered { get; }
}

/// <summary>
/// One item of the menu under "Show": a name, the words the menu says it in, and whether it is what
/// the table shows now.
///
/// <b>A tick that cannot be taken off.</b> The menu item is bound two way the way a column choice is,
/// and ticking another choice of the same group is what moves the tick - so a click on the ticked one
/// says "this" again rather than "nothing", and the tick is put straight back. What a choice does is
/// its owner's, handed in as the one thing it calls.
/// </summary>
public sealed class RowChoice : Observable
{
    private readonly Action<RowChoice> _chosen;
    private bool _isChosen;

    internal RowChoice(string name, string label, Action<RowChoice> chosen)
    {
        Name = name;
        Label = label;
        _chosen = chosen;
    }

    /// <summary>What is shown, the way the button says it.</summary>
    public string Name { get; }

    /// <summary>What is shown and how many rows that is, the way the menu says it.</summary>
    public string Label { get; }

    /// <summary>Whether this is what the table shows now.</summary>
    public bool IsChosen
    {
        get => _isChosen;
        set
        {
            if (value)
            {
                _chosen(this);
            }
            else
            {
                Raise(nameof(IsChosen));
            }
        }
    }

    internal void Mark(bool chosen) => Set(ref _isChosen, chosen, nameof(IsChosen));
}

/// <summary>
/// One group of choices that narrow a table to the rows a rule takes, and the rule both tables offer
/// them by: every row first, then each narrower choice only when it shows something AND less than
/// everything - a choice that shows nothing teaches that the menu lies, and one that shows everything is
/// "every row" said twice (W2-b).
///
/// <b>One copy of that rule, since a review of 2026-10-20 (#81)</b>: the comparison's filter and the
/// change history's had it line for line, and a rule in two places is two places for it to drift - the
/// mutation registry had to hold both. Each filter keeps only what is its own: the rules that narrow,
/// the words they are called, and what happens when one is ticked.
///
/// <b>The counted label comes in as a function rather than a key</b>, so each key stays whole at its own
/// call to Texts.Of - the shape TextKeyGuards finds a key in.
/// </summary>
/// <typeparam name="TRow">What one row of the table is.</typeparam>
internal sealed class RowChoiceSet<TRow>
{
    private readonly Dictionary<RowChoice, Func<TRow, bool>> _takes = new();
    private readonly Func<string, int, string> _counted;
    private readonly Action<RowChoice> _chosen;

    /// <param name="counted">The words for a choice and how many rows it shows.</param>
    /// <param name="chosen">What ticking a choice of this group does.</param>
    internal RowChoiceSet(Func<string, int, string> counted, Action<RowChoice> chosen)
    {
        _counted = counted;
        _chosen = chosen;
    }

    /// <summary>
    /// The choices this table can offer now - every row, then each narrower rule that leaves something
    /// out and keeps something in. Made again from nothing each time, because the counts are this
    /// table's.
    /// </summary>
    internal List<RowChoice> Offered(
        IReadOnlyList<TRow> all, string everything, IEnumerable<(string Name, Func<TRow, bool> Takes)> narrower)
    {
        _takes.Clear();

        var offered = new List<RowChoice> { Made(everything, all.Count, _ => true) };

        foreach (var (name, takes) in narrower)
        {
            var count = all.Count(takes);

            if (count > 0 && count < all.Count)
            {
                offered.Add(Made(name, count, takes));
            }
        }

        return offered;
    }

    /// <summary>The rule behind a choice of this group, or false for a choice that is not one of them.</summary>
    internal bool TryTakes(RowChoice choice, [MaybeNullWhen(false)] out Func<TRow, bool> takes) =>
        _takes.TryGetValue(choice, out takes);

    private RowChoice Made(string name, int count, Func<TRow, bool> takes)
    {
        var choice = new RowChoice(name, _counted(name, count), _chosen);

        _takes[choice] = takes;

        return choice;
    }
}

/// <summary>
/// The rows a narrowed table shows, replaced all at once when "Show" changes - the comparison's table
/// and the change history's.
///
/// <b>RowList.ResetTo's mechanism, and its measurement is the reason</b>: a notification per row is
/// a notification the grid answers per row, about 285 ms for 810 rows. It was the comparison's own
/// DiffRows until the change history became the third place doing this (2026-10-20), and the third time
/// is when GUI rule 2 of the project notes makes a thing a component. Still apart from the list's own
/// version, which carries a contract about the selection and the scroll position (`A10`) that a table
/// read once does not have.
/// </summary>
/// <typeparam name="TRow">What one row of the table is.</typeparam>
public sealed class ShownRows<TRow> : ObservableCollection<TRow>
{
    internal ShownRows(IEnumerable<TRow> rows)
        : base(rows)
    {
    }

    /// <summary>
    /// Replaces every row with these, saying so once. The three notifications are the three WPF needs
    /// - RowList.ResetTo says why each one.
    /// </summary>
    internal void ShowOnly(IEnumerable<TRow> rows)
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
