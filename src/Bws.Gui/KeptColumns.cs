using System.Windows;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The layout the window was opened with, and the layout it leaves behind.
///
/// <b>Between the file and the grid rather than in either.</b> <see cref="PreferencesFile"/> knows
/// where a file lives and how to write one without losing what was there. <see cref="ColumnLayout"/>
/// knows what a layout is and how a stale one is reconciled with the columns this build has.
/// <c>ListColumns</c> knows what a width means. What is left - reading once at startup, saying in
/// one sentence what could not be honoured, and writing the same thing back - is this class, and it
/// is the only part that has to know all three exist.
///
/// <b>WHEN IT WRITES, AND WHY IT IS NOT ONLY AT CLOSING TIME.</b> Every failure this can meet is
/// ordinary - a roaming profile on a share that went away, a folder an administrator locked down,
/// a full disk - and a window being closed has nowhere to show a sentence. So the same write also
/// happens the moment somebody turns a column on or off, which is the one layout change that
/// happens while there is still a window to say it in. The cost of that choice is stated rather
/// than hidden: a width dragged and then a process killed is a width that was never written, and
/// only a tick or an orderly close puts it on disk.
/// </summary>
internal sealed class KeptColumns
{
    private readonly PreferencesFile _file;
    private readonly LayoutReading _reading;

    /// <summary>All three scopes' layouts, so that moving between them keeps what was not on screen.</summary>
    private ColumnLayouts _layouts;

    /// <summary>Which scope the grid is currently showing, so a write lands in the right section.</summary>
    private EntryScope _scope = Scopes.Opening;

    /// <summary>
    /// Whether the grid is half way through being handed another scope's layout.
    ///
    /// <b>Without this the switch writes a corrupted file, and it is not an obvious hazard.</b>
    /// Turning a column on or off raises <c>ColumnBar.Changed</c>, which is wired to write the file
    /// - so applying the new scope's visibility one column at a time would harvest the grid several
    /// times while it is neither the old layout nor the new one, and store each half-state. The
    /// write that matters happens once, after everything has been applied.
    /// </summary>
    private bool _switching;

    internal KeptColumns(PreferencesFile file)
    {
        _file = file;
        _reading = file.Read();
        _layouts = _reading.Layouts ?? ColumnLayouts.Default;

        Plan = ColumnPlan.Of(_reading.Layouts?.For(_scope), _scope);
    }

    /// <summary>What the window should show, whatever the file turned out to be.</summary>
    internal ColumnPlan Plan { get; private set; }

    /// <summary>
    /// Whether this profile has already put the machine overview away - `G`, schema 4.
    ///
    /// <b>Asked of what was READ rather than of the reconciled layouts</b>, and the difference is
    /// the whole answer on a first run: a file that is not there gives no layouts at all, so
    /// <see cref="_layouts"/> falls back to the defaults - and a default that said "seen" would
    /// hide this screen from exactly the person it was written for.
    /// </summary>
    internal bool OverviewSeen => _reading.Layouts?.OverviewSeen ?? false;

    /// <summary>
    /// Writes down that the overview has been put away, and says in the window what stopped it.
    ///
    /// <b>Written the moment it is dismissed rather than when the window closes, and that is the
    /// case this exists for.</b> The layout is saved on close, so a first run ended by a machine
    /// going down - or by the process being killed, which is how half this project's probes end -
    /// would show this screen again to somebody who had already read it.
    ///
    /// <b>It does not harvest the grid, unlike every other write here.</b> Nothing about the columns
    /// has changed, the grid may not even be visible, and harvesting a collapsed one would write a
    /// layout describing a control nobody is looking at.
    ///
    /// Does nothing when it is already written, so clicking through the numbers costs one write
    /// rather than one per click.
    /// </summary>
    internal void TheOverviewWasSeen(Says says) => KeepTheProfile(_layouts with { OverviewSeen = true }, says);

    /// <summary>
    /// Whether the filter chips were folded when this profile last left them - UX-GUI-007, schema 5.
    /// Asked of what was read, for the reason <see cref="OverviewSeen"/> gives: no file at all is a
    /// first run, and a first run opens with the chips showing.
    /// </summary>
    internal bool FiltersFolded => _reading.Layouts?.FiltersFolded ?? false;

    /// <summary>
    /// Writes down that the chips were folded or opened, at the press rather than at close - the
    /// reason <see cref="TheOverviewWasSeen"/> gives, a window killed rather than closed - and
    /// without harvesting the grid, for the reason given there too.
    /// </summary>
    internal void TheFiltersWere(bool folded, Says says) => KeepTheProfile(_layouts with { FiltersFolded = folded }, says);

    /// <summary>
    /// Writes one changed fact about the profile, or nothing when it is already written - so clicking
    /// through the numbers or the toggle costs one write rather than one per click.
    /// </summary>
    private void KeepTheProfile(ColumnLayouts changed, Says says)
    {
        ArgumentNullException.ThrowIfNull(says);

        if (changed == _layouts)
        {
            return;
        }

        _layouts = changed;

        if (_file.Write(_layouts) is { } trouble)
        {
            says.AboutTheLayout(Texts.Of("gui.layout.notKept", trouble));
        }
    }

    /// <summary>
    /// Moves the kept layout to another scope, and hands back what the grid should look like there.
    ///
    /// <b>The old scope is harvested FIRST, and that is the half that is easy to leave out.</b> The
    /// widths somebody dragged and the order they arranged exist only on the live columns until
    /// something reads them off - so moving away without harvesting would lose the arrangement of
    /// the list they were just looking at, and lose it silently.
    /// </summary>
    internal ColumnPlan MoveTo(EntryScope scope, DataGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        _layouts = _layouts.With(_scope, Harvested(grid));
        _scope = scope;

        Plan = ColumnPlan.Of(_layouts.For(scope), scope);

        return Plan;
    }

    /// <summary>
    /// Puts the layout of the list on screen back to what this build opens with, and writes it.
    ///
    /// <b>The same three moves as a scope change, in the same order, and that is deliberate.</b>
    /// Which columns are on goes through <see cref="ColumnBar.Follow"/>, because that is the one
    /// road from "this column is on" to a column being on. Width and order go through
    /// <c>ListColumns.Reapply</c>. Wrapped in <see cref="While"/> so the file is written once at
    /// the end rather than once per column turned off.
    ///
    /// <b>Only the scope on screen.</b> The other two layouts are left exactly as they are, for the
    /// reason <see cref="Keep"/> gives: somebody who arranged their drivers list should not lose it
    /// by putting the services list back.
    ///
    /// <b>A default layout carries no width at all</b>, which is what makes this a restore rather
    /// than a set of numbers written down twice - every column takes the width the theme names, so
    /// changing the theme still changes the list.
    ///
    /// <b>NOTHING TOUCHES THE KEPT ORDER HERE ANY MORE, AND THAT LINE WAS DELETED WITH A
    /// MEASUREMENT BEHIND IT ON 2026-09-03 - backlog 315.</b> There used to be one, seeding this
    /// scope's layout so that <see cref="Harvested"/> would have the right answer if the grid gave
    /// none. It was a second road to a fact that <c>ListColumns.Reapply</c> below already
    /// establishes, and the second road was the unguarded one.
    ///
    /// <b>Why it existed and why it stopped being needed.</b> Reapply hands the grid the default
    /// layout INCLUDING its order, and the harvest reads that order back - but ListSorting.By used
    /// to give up before marking anything when the grid had no collection view yet. Measured over
    /// twenty ways back: a window nobody had shown came out carrying no order SEVENTEEN times, a
    /// settled one none out of twenty. So the file was decided by a race, and the line was there
    /// to make the losing side harmless. By now marks the heading whether or not there is a view -
    /// the same probe reads twenty out of twenty - so the losing side is gone rather than padded.
    ///
    /// <b>What the way back must end up leaving is the order this build OPENS with, not nothing.</b>
    /// A section with no sort is applied as no sort rather than reconciled back to the default, so
    /// a file with none in it opens UNSORTED - which stopped being the list this build opens with
    /// on 2026-09-02, when the default gained displayName ascending on the owner's decision.
    /// </summary>
    internal void Defaults(DataGrid grid, ColumnBar bar, Says says)
    {
        ArgumentNullException.ThrowIfNull(bar);

        While(
            () =>
            {
                Plan = ColumnPlan.Of(null, _scope);

                bar.Follow(Plan);

                if (Trouble(ListColumns.Reapply(grid, Plan)) is { } trouble)
                {
                    says.AboutTheLayout(trouble);
                }
            },
            grid,
            says);
    }

    /// <summary>
    /// Holds off the writing while a scope is being applied, and writes once when it is done.
    ///
    /// A pair rather than a flag the window sets, because the two halves belong together and the
    /// second one is the whole point: the file is written at the end, from a grid that has finished
    /// becoming the thing it is supposed to be.
    /// </summary>
    internal void While(Action applying, DataGrid grid, Says says)
    {
        ArgumentNullException.ThrowIfNull(applying);

        _switching = true;

        try
        {
            applying();
        }
        finally
        {
            _switching = false;
        }

        Keep(grid, says);
    }

    /// <summary>
    /// Everything the file could not give the window, in one sentence, or nothing at all.
    ///
    /// <b>Rule 8 arriving somewhere it is easy to think it does not apply.</b> A layout quietly
    /// half applied is a window that is not the one somebody left, with no way to tell that from
    /// having imagined arranging it. Every branch here is a thing that was silently dropped in the
    /// first version of this class.
    ///
    /// The widths come in from outside because only the grid can say whether a saved width means
    /// anything - a width is text until something that knows what a width is has looked at it.
    /// </summary>
    internal string? Trouble(IReadOnlyList<string> widthsRefused)
    {
        ArgumentNullException.ThrowIfNull(widthsRefused);

        var said = new List<string>();

        if (_reading.OtherSchemaVersion is { } version)
        {
            said.Add(Texts.Of(
                "gui.layout.otherSchema", version, ColumnLayout.CurrentSchemaVersion));
        }

        // SAID RATHER THAN DONE QUIETLY. The file was understood and used, and the next change to a
        // column writes it back in the current shape - so the copy on their disk stops being
        // readable by the build they had yesterday. That is a change to somebody's file and rule 8
        // says a change nobody was told about is the one that costs.
        if (_reading.CarriedForwardFrom is { } older)
        {
            said.Add(Texts.Of(
                "gui.layout.carriedForward", older, ColumnLayout.CurrentSchemaVersion));
        }

        if (_reading.Unreadable is { } why)
        {
            said.Add(_reading.MovedAside is { } aside
                ? Texts.Of("gui.layout.unreadable", why, aside)
                : Texts.Of("gui.layout.unreadableStays", why));
        }

        if (Plan.Ignored.Count > 0)
        {
            said.Add(Texts.Of("gui.layout.ignored", Listed(Plan.Ignored)));
        }

        if (Plan.NoneWasShown)
        {
            said.Add(Texts.Of("gui.layout.noneShown"));
        }

        if (widthsRefused.Count > 0)
        {
            said.Add(Texts.Of("gui.layout.widths", Listed(widthsRefused)));
        }

        return said.Count == 0 ? null : string.Join(" ", said);
    }

    /// <summary>
    /// Ties the layout to the window, so that it follows what somebody does to it.
    ///
    /// <b>Here rather than in the window, and that is the same split every other decision in this
    /// product makes.</b> The window's job is introducing the parties. WHEN a layout is worth
    /// writing down is a decision, it has an argument behind it - the one this class opens with -
    /// and it belongs beside the writing rather than beside the event handlers.
    /// </summary>
    internal void Watch(Window window, DataGrid grid, ColumnBar bar, Says says)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(bar);

        bar.Changed += (_, _) => Keep(grid, says);

        // Closing rather than Closed: the columns still belong to a live window here, and their
        // widths and their order are read off them. This is also the write that has nowhere to
        // report a failure, which is the whole reason the line above exists.
        window.Closing += (_, _) => Keep(grid, says);
    }

    /// <summary>
    /// Writes down what the grid looks like now, and says in the window what stopped it.
    ///
    /// <b>The whole layout every time, rather than the part that moved.</b> Working out what
    /// changed would need a second copy of the state to compare against, and a file of eighteen
    /// short lines costs less to write than that copy costs to keep correct.
    /// </summary>
    private void Keep(DataGrid grid, Says says)
    {
        if (_switching)
        {
            return;
        }

        // THE SCOPE ON SCREEN, AND THE OTHER TWO EXACTLY AS THEY WERE. Writing only what the grid
        // shows would empty the other two sections on the first change anybody made, so somebody
        // who arranged their drivers list would lose it by touching a column while looking at
        // services.
        _layouts = _layouts.With(_scope, Harvested(grid));

        if (_file.Write(_layouts) is { } trouble)
        {
            says.AboutTheLayout(Texts.Of("gui.layout.notKept", trouble));
        }
    }

    /// <summary>
    /// What the grid looks like now, keeping the order the file holds when the grid carries none.
    ///
    /// <b>MEASURED 2026-09-01, AND IT COST SOMEBODY THEIR SORTED LIST WITHOUT A WORD.</b> A window
    /// opened on a layout and closed again before the first reading arrived came back with the
    /// order gone from the file. <c>ListSorting.By</c> runs inside the Loaded handler, AFTER
    /// <c>LoadAsync</c> - a grid with no rows has no view to hand a comparer to - while the write
    /// on Closing runs whenever the window shuts. So the harvest read the order off headings that
    /// had not been given it yet, found none, and wrote none.
    ///
    /// <b>The second way in is not a race at all and was a promise nothing kept.</b> Turning OFF
    /// the column a list is sorted by leaves the grid with no direction anywhere - by design, an
    /// order nobody can see cannot be read - and <c>ListSorting.By</c> says in as many words that
    /// "the kept sort stays in the file, so turning the column back on brings the order back with
    /// it". Until this method existed the very next write erased it.
    ///
    /// <b>Safe because the grid has no third state.</b> A click always leaves a direction on some
    /// heading - <c>WhenAHeadingIsClicked</c> sets ascending or descending and never nothing - so
    /// an empty answer from the grid never means "this person took the order off".
    ///
    /// <b>The way back used to be the exception to that and no longer is, since 2026-09-03.</b>
    /// It goes through <c>ListColumns.Reapply</c>, which asks <c>ListSorting.By</c> for the default
    /// order - and By marks the heading whether or not the rows have arrived, so the grid always
    /// has an answer to give and this fallback is not consulted on that path at all. Backlog 315
    /// carries the measurement that settled it.
    /// </summary>
    private ColumnLayout Harvested(DataGrid grid)
    {
        var harvested = ListColumns.Harvest(grid);

        return harvested.Sort is null
            ? harvested with { Sort = _layouts.For(_scope).Sort }
            : harvested;
    }

    /// <summary>
    /// Identifiers as a person sees them in the file, which is the point of naming them at all.
    ///
    /// Not translated, and that is deliberate: the sentence is about a line in a file they can
    /// open, so it has to use the words that are in it. A heading would send somebody looking for
    /// "Display name" in a file that says <c>displayName</c>.
    /// </summary>
    private static string Listed(IReadOnlyList<string> identifiers) =>
        string.Join(", ", identifiers);
}
