using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What this window asks the machine for, and when it stops asking.
///
/// <b>Its own file since 2026-09-05, and the size ratchet is what asked - for the fourth time in
/// this class and the fourth time pointing at a subject rather than a line count.</b> The rest of
/// MainViewModel is about the ANSWER: which entries the query selected, how they are counted,
/// folded, ordered and described. What is here is the other direction - the standing request the
/// reading keeps consulting, the tick that renews it, and the moment nobody wants it any more.
///
/// <b>The seam was drawn by a defect rather than by a ratchet, which is why it is a real one.</b>
/// Until that day only the query could ask the second phase of `ADR-13` for anything, so five
/// columns fed by that phase printed "unknown" on every row for ever - the owner reported it about
/// the memory column and it was never about memory. The repair was to give the picker a voice, and
/// the two voices then had to be summed in exactly one place, because what the window FETCHES and
/// what it SAYS it is fetching are read from that sum separately. Column.Needs carries the whole
/// account.
///
/// <b>A partial rather than a class of its own</b>, for the reason MainViewModel.Folding.cs and
/// MainViewModel.Overview.cs are: every member here reads a field of the view model - the query,
/// the reading - and a separate type would take them as arguments and be this class wearing a
/// different name.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>
    /// What the columns on screen need read, asked of whoever owns the picker.
    ///
    /// <b>A question rather than a value, and set rather than handed to the constructor.</b> The
    /// picker belongs to the window and the answer changes every time somebody ticks a box, so a
    /// value taken once would be the state of the layout at startup and nothing after it. Left
    /// unset - which is every test that does not care and every caller that has no picker - it
    /// asks for nothing, and the query is then the only thing that speaks, exactly as before.
    ///
    /// <b>Nothing subscribes to it.</b> The reading asks this on its own tick, so turning a column
    /// on is picked up within the second without an event, a subscription or an order of wiring
    /// that has to be right - see <see cref="ColumnBar.Needs"/>.
    /// </summary>
    public Func<ExtraRead>? ColumnsNeed { get; set; }

    /// <summary>
    /// Everything being asked of the second phase right now, from both directions at once.
    ///
    /// <b>One definition, read in two places that must never disagree.</b> One decides what the
    /// window goes and reads, the other decides what it SAYS about what it has - and a window that
    /// fetches a family without admitting it is fetching it stops the list for seconds with no
    /// sentence under it. That is rule 8, and keeping the two on one expression is what makes it
    /// impossible rather than remembered.
    /// </summary>
    private ExtraRead Asked => _query.Needs | (ColumnsNeed?.Invoke() ?? ExtraRead.None);

    /// <summary>One tick of the live list: asks what is running and moves whatever moved.</summary>
    public Task RefreshAsync() => _readings.RefreshAsync();

    /// <summary>
    /// Nobody is looking at this any more, so a reading still out there writes nothing.
    ///
    /// Backlog 300. What it does and, more importantly, what it does NOT do is written at
    /// <see cref="Readings.NoLongerWanted"/> - the short of it is that the work carries on and
    /// only its answer is dropped.
    /// </summary>
    public void NoLongerWanted() => _readings.NoLongerWanted();
}
