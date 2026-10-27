using Bws.Core.History;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Which lines of the change history the table shows - owner's decision J2-b of 2026-10-20, and the
/// window's answer to backlog 578.
///
/// <b>TWO QUESTIONS, TWO GROUPS IN ONE MENU.</b> The first is how: identical changes together on one
/// line, or each change on its own. The second is whose and what: every change, the ones not made by
/// SYSTEM, installations, start type changes. Measured on the owner's machine that day: 328 changes, 262
/// of them Windows switching three services by itself, 56 one driver installed again by a Python tool.
/// Together they make 21 lines, and without SYSTEM fewer still.
///
/// <b>IDENTICAL CHANGES TOGETHER BY DEFAULT, AND EVERY ACCOUNT.</b> Grouping hides no change - a line
/// says how many it stands for and between when - so it is not the "nothing hidden until somebody
/// chooses" rule of the comparison (W2-b) being broken. Leaving SYSTEM out by default would be, and the
/// owner chose against it. While lines stand for more changes than there are lines, or a choice leaves
/// changes out, <see cref="Showing"/> says so - rule 8.
///
/// <b>"Not by SYSTEM" is literal</b>: the account S-1-5-18, the one Windows changes itself under. Not
/// "not by Windows", because an installation SYSTEM carried out may well have been somebody's
/// installer, and a filter promising more than the log says would be a guess.
///
/// <b>Filtered first - the tick, then the search - and grouped last</b>, so a line never counts changes
/// the table is not showing. The counts in the menu are the whole reading's: a choice says how many
/// changes it stands for, not how many of them the search has left.
/// </summary>
public sealed class HistoryFilter : Observable, IRowChoices
{
    /// <summary>The account Windows changes itself under - well known, the same on every machine.</summary>
    internal const string SystemSid = "S-1-5-18";

    private readonly RowChoiceSet<ServiceChange> _whose;
    private readonly RowChoice _together;
    private IReadOnlyList<ServiceChange> _all;
    private IReadOnlyList<IReadOnlyList<RowChoice>> _groups;
    private RowChoice _arranged;
    private RowChoice _chosen;
    private int _shown;
    private string _searchText = string.Empty;
    private string[] _words = [];

    /// <summary>
    /// What each change says, in the order of <see cref="_all"/> - worked out once per reading rather
    /// than on every keystroke, because the words of a change are the same until the log is read again.
    /// </summary>
    private string[] _said;

    internal HistoryFilter(IReadOnlyList<ServiceChange> all)
    {
        ArgumentNullException.ThrowIfNull(all);

        _all = all;
        _said = Said(all);
        _whose = new RowChoiceSet<ServiceChange>((name, count) => Texts.Of("gui.history.show.counted", name, count), Choose);
        _together = new RowChoice(Texts.Of("gui.history.show.together"), Texts.Of("gui.history.show.together"), Arrange);

        var each = new RowChoice(Texts.Of("gui.history.show.each"), Texts.Of("gui.history.show.each"), Arrange);
        var whose = WhoseOf(all);

        _groups = [[_together, each], whose];

        _arranged = _together;
        _arranged.Mark(chosen: true);
        _chosen = whose[0];
        _chosen.Mark(chosen: true);

        Rows = new ShownRows<HistoryRow>(Made());
    }

    /// <inheritdoc />
    public IReadOnlyList<IReadOnlyList<RowChoice>> Groups
    {
        get => _groups;
        private set => Set(ref _groups, value);
    }

    /// <summary>The lines the table shows.</summary>
    public ShownRows<HistoryRow> Rows { get; }

    /// <inheritdoc />
    public string Button => Texts.Of("gui.history.show", _chosen.Name);

    /// <inheritdoc />
    public string Hint => Texts.Of("gui.history.show.hint");

    /// <summary>A log with no change in it has no table, and no button over one.</summary>
    public bool Offered => _all.Count > 0;

    /// <inheritdoc />
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? string.Empty))
            {
                return;
            }

            _words = RowSearch.Words(_searchText);

            Show();
        }
    }

    /// <inheritdoc />
    public string SearchHint => Texts.Of("gui.history.search.hint");

    /// <inheritdoc />
    public string SearchTip => Texts.Of("gui.history.search.tip");

    /// <inheritdoc />
    public string Showing
    {
        get
        {
            // A SEARCH THAT FINDS NOTHING IS A SENTENCE, the only way this table can come out empty - a
            // tick in the menu always leaves something (RowChoiceSet), so `docs/11` 3.8 asks it here.
            // Never over an empty log: the sentences above the table already say there is nothing.
            if (_words.Length > 0 && _shown == 0 && _all.Count > 0)
            {
                return Texts.Of("gui.history.search.nothing", RowSearch.Typed(_words));
            }

            var narrowed = _shown < _all.Count;
            var gathered = Rows.Count < _shown;
            var oneLine = Rows.Count == 1;

            // Each key whole at its own call - TextKeyGuards finds a key by the call it stands in, and
            // CountedWordGuards asks for both halves of every pair, so both are written out.
            if (narrowed && gathered)
            {
                return oneLine
                    ? Texts.Of("gui.history.showing.narrowedGrouped.one", _shown, _all.Count, Rows.Count)
                    : Texts.Of("gui.history.showing.narrowedGrouped.many", _shown, _all.Count, Rows.Count);
            }

            if (narrowed)
            {
                return _all.Count == 1
                    ? Texts.Of("gui.history.showing.narrowed.one", _shown, _all.Count)
                    : Texts.Of("gui.history.showing.narrowed.many", _shown, _all.Count);
            }

            if (!gathered)
            {
                return string.Empty;
            }

            return oneLine
                ? Texts.Of("gui.history.showing.grouped.one", _shown, Rows.Count)
                : Texts.Of("gui.history.showing.grouped.many", _shown, Rows.Count);
        }
    }

    /// <summary>
    /// The log read again - F5, or the view opened again - with the table and both ticks kept.
    ///
    /// <b>The same collection, so the order somebody sorted the table into stays</b> - GUI rule 3 of the
    /// project notes: nothing resets on a refresh. The second group is made again, because its counts
    /// are this reading's, and the tick goes back to the choice of the same name - or to every change,
    /// when this reading no longer offers it.
    /// </summary>
    internal void Renew(IReadOnlyList<ServiceChange> all)
    {
        ArgumentNullException.ThrowIfNull(all);

        var name = _chosen.Name;

        _all = all;
        _said = Said(all);

        var whose = WhoseOf(all);

        _chosen = whose.FirstOrDefault(choice => string.Equals(choice.Name, name, StringComparison.Ordinal)) ?? whose[0];
        _chosen.Mark(chosen: true);

        Groups = [Groups[0], whose];

        Show();
        Raise(nameof(Offered));
    }

    /// <summary>Identical changes together, or each on its own - the first group's tick.</summary>
    private void Arrange(RowChoice choice)
    {
        if (!Groups[0].Contains(choice))
        {
            return;
        }

        _arranged.Mark(chosen: false);
        _arranged = choice;
        _arranged.Mark(chosen: true);

        Show();
    }

    /// <summary>Whose and what - the second group's tick.</summary>
    private void Choose(RowChoice choice)
    {
        if (!_whose.TryTakes(choice, out _))
        {
            return;
        }

        _chosen.Mark(chosen: false);
        _chosen = choice;
        _chosen.Mark(chosen: true);

        Show();
    }

    private void Show()
    {
        Rows.ShowOnly(Made());

        Raise(nameof(Button));
        Raise(nameof(Showing));
    }

    /// <summary>The lines for the two ticks and the search as they stand, in the history's own order - newest first.</summary>
    private List<HistoryRow> Made()
    {
        var takes = _whose.TryTakes(_chosen, out var rule) ? rule : (_ => true);
        var shown = new List<ServiceChange>();

        for (var i = 0; i < _all.Count; i++)
        {
            if (takes(_all[i]) && RowSearch.Finds(_said[i], _words))
            {
                shown.Add(_all[i]);
            }
        }

        _shown = shown.Count;

        var groups = ReferenceEquals(_arranged, _together) ? ChangeGroup.Of(shown) : ChangeGroup.Each(shown);

        return [.. groups.Select(group => new HistoryRow(group))];
    }

    /// <summary>
    /// What a change says as the search reads it: the time, the entry and its display name, the change,
    /// the account by name and by SID - the words of the table, and the two names the table leaves out.
    /// </summary>
    private static string[] Said(IReadOnlyList<ServiceChange> all) =>
    [
        .. all.Select(change => RowSearch.Said(
            HistoryWords.When(change.At),
            HistoryWords.Entry(change),
            change.DisplayName,
            HistoryWords.What(change),
            HistoryWords.Who(change.By),
            change.By?.Sid))
    ];

    /// <summary>
    /// Whose and what, after every change: the changes SYSTEM did not make, installations, startup type
    /// changes. Which of them are offered is <see cref="RowChoiceSet{TRow}"/>'s rule, the comparison's too.
    ///
    /// <b>"Not by SYSTEM" takes only a change whose account the log names</b> - since 2026-10-27, logic
    /// report H03. A change the log records without an account may have been SYSTEM's, so leaving it in
    /// would make a missing name read as a finding about who did it. It stays under every change, and the
    /// line over the table says the choice left changes out.
    /// </summary>
    private List<RowChoice> WhoseOf(IReadOnlyList<ServiceChange> all) => _whose.Offered(
        all,
        Texts.Of("gui.history.show.all"),
        [
            (Texts.Of("gui.history.show.notSystem"), change => change.By is { } by && !string.Equals(by.Sid, SystemSid, StringComparison.Ordinal)),
            (Texts.Of("gui.history.show.installed"), change => change.Kind == ChangeKind.Installed),
            (Texts.Of("gui.history.show.startType"), change => change.Kind == ChangeKind.StartTypeChanged)
        ]);
}
