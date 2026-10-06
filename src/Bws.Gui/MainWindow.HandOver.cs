using System.Windows;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// Restarting as administrator without starting again from nothing - UX-GUI-004 (c).
///
/// <b>Two halves in one file because they are one agreement.</b> The window without rights writes
/// down what it was showing just before it starts its replacement, and the replacement reads that
/// back after its first look at the machine. <see cref="HandOver"/> holds what crosses and why it is
/// read as hostile - this is where the window puts it and takes it from.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// The plan that is open, as it was asked for - remembered at the moment it opens, because the
    /// sheet itself keeps the answer and not the question.
    ///
    /// <b>Over is the entry the plan is about when that is NOT the picked rows</b> - a forcing sheet
    /// opened from a failure, which can be about a dependant of the row somebody picked. Backlog
    /// 504, owner's decision 2026-10-05: the hand-over carries that name, so the window after a
    /// restart asks the same question rather than the same kind of question about another entry.
    /// </summary>
    private (ActionKind Kind, StartSetting? To, bool AlsoStop, bool Dependents, IReadOnlyList<string>? Over)? _asked;

    /// <summary>
    /// The first reading, and everything that has to wait for it. Out of the constructor since
    /// 2026-09-23, when the hand-over joined it and the shape guard counted the constructor among
    /// the methods standing near the ceiling of length.
    /// </summary>
    private async Task FirstLook()
    {
        await _model.LoadAsync().ConfigureAwait(true);

        // AFTER THE FIRST READING AND NOT BEFORE IT. The order somebody left the list in is handed
        // to the view that holds the rows, and until this line has run there are no rows and no
        // view - so an order applied while the columns were being built would be dropped without a
        // word.
        SortAsKept();

        // And what the window this one replaced was showing, if it handed anything over - after the
        // first reading too, because the names it carries are matched against the rows.
        if (Application.Current is App { HandedOver: var handed })
        {
            await TakeOverAsync(handed.Carried, handed.Refused).ConfigureAwait(true);
        }

        // ONLY IF SOMEBODY CAN SEE IT - a window started minimised reads the machine once and waits
        // to be looked at, and coming back starts the timer with a tick (WatchWhileSeen).
        _looked = true;

        if (Seen)
        {
            _timer.Start();
        }
    }

    /// <summary>
    /// What this window would hand to the one that replaces it, read from the screen now. Internal
    /// for a guard, because the button that uses it may never be pressed in a test - a press is a
    /// UAC prompt and a closed host.
    /// </summary>
    internal HandOver HandOverNow()
    {
        // WHAT IS IN THE BOX RATHER THAN WHAT THE MODEL HAS HEARD SO FAR - G-9 of the external
        // stability report. The box hands its text over 400 ms after the last key, so a restart
        // pressed straight after typing would carry the query from before the last few characters.
        Search.Commit();

        var showing = _model.Planned.Showing;

        return new()
        {
            Scope = _model.Scope,
            Query = _model.QueryText,
            Picked = showing && _asked?.Over is { } over ? [.. over] : [.. PickedRows().Select(row => row.ServiceName)],
            Asked = showing ? _asked?.Kind : null,
            To = showing ? _asked?.To : null,
            AlsoStop = showing && _asked?.AlsoStop == true,
            Dependents = showing && _asked?.Dependents == true
        };
    }

    /// <summary>
    /// Puts back what the window this one replaced was showing, after the first reading - the rows
    /// to match names against do not exist before it.
    ///
    /// <b>The plan is asked for again, never carried</b>, and asked through the same door a press
    /// uses, so everything a press would be refused - drivers alone, a forcing ask over several - is
    /// refused here as well. Nothing is carried out: the person still presses, and a forcing plan
    /// still wants the name typed.
    ///
    /// <b>What could not come back is said</b> - a refused hand-over, a selection too large to carry,
    /// picked entries no longer in the list. A person who restarted expects their work, and a window
    /// quietly showing less would look like the whole of it (rule 8).
    /// </summary>
    internal async Task TakeOverAsync(HandOver? carried, bool refused)
    {
        if (refused)
        {
            _model.Says.AboutTheHandOver(Texts.Of("gui.handOver.refused"));
        }

        if (carried is null)
        {
            return;
        }

        // THE HOLD IS LET GO FOR THE LENGTH OF THE TAKE-OVER - G-7 of the external stability report.
        // A new window very often opens under the pointer, and the list then refuses to rearrange
        // itself (`A10`) - so the scope and the query below changed nothing on screen and the picks
        // were looked for among the rows of the list the window opened on. This is the window's
        // first content rather than a list moving under somebody's hand, which is what the hold is for.
        _model.Interacting = false;

        _model.ShowingOverview = false;
        _model.Scope = carried.Scope;
        _model.QueryText = carried.Query;

        if (carried.PickedLeftBehind)
        {
            _model.Says.AboutTheHandOver(Texts.Of("gui.handOver.pickedLeftBehind"));
        }

        if (carried.QueryLeftBehind)
        {
            _model.Says.AboutTheHandOver(Texts.Of("gui.handOver.queryLeftBehind"));
        }

        var wanted = new HashSet<string>(carried.Picked, StringComparer.OrdinalIgnoreCase);
        var found = PickAgain(wanted);
        var gone = wanted.Count - found;

        // And back to whatever the pointer and the keyboard say now, the way ListReleased asks.
        _model.Interacting = Entries.IsMouseOver || Entries.IsKeyboardFocusWithin;

        if (gone > 0)
        {
            _model.Says.AboutTheHandOver(gone == 1
                ? Texts.Of("gui.handOver.gone.one", gone)
                : Texts.Of("gui.handOver.gone.many", gone));
        }

        if (found > 0 && carried.Asked is { } kind)
        {
            await Preview(kind, carried.To, carried.AlsoStop, carried.Dependents).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Picks the rows whose manager's name was handed over and answers how many it found. Names and
    /// not display names, `ADR-14`, compared the way the manager compares them - without case.
    /// </summary>
    private int PickAgain(HashSet<string> wanted)
    {
        var found = 0;

        Entries.UnselectAll();

        foreach (var row in _model.Rows.Where(row => wanted.Contains(row.ServiceName)))
        {
            Entries.SelectedItems.Add(row);
            found++;
        }

        return found;
    }
}
