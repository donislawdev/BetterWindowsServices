using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The menu on a row: which entries it acts on, and what each of its items does.
///
/// <b>Its own file since 2026-08-18, and the size ratchet is what asked - for the third time in this
/// window and the third time pointing at a real seam.</b> The rest of MainWindow is about the window
/// as a whole: what it is made of, where the keyboard goes, when the list may rearrange itself. This
/// is about one thing a person does to a set of rows, and it grew by half when the menu learned to
/// open a plan.
///
/// <b>A partial class rather than a separate type, and the reason is the markup.</b> Every item here
/// is wired by a Click attribute in MainWindow.xaml, which the compiler resolves against the window's
/// own class. The alternative is AddHandler in the constructor - this window already does that twice
/// for menus opened under a button - and it would trade a file boundary for seven lookups by name,
/// which is more mechanism rather than less.
///
/// <b>What is deliberately NOT here:</b> which rows are picked, which is read from the grid at the
/// moment somebody asks and never kept, and what a copy or a plan SAYS, which is decided in the view
/// models where it can be checked without opening a window.
/// </summary>
public partial class MainWindow
{
    /// <summary>Whether the last right click landed on a row. Read by the menu, set by the click.</summary>
    private bool _pointedAtARow;


    /// <summary>
    /// Puts the pointer's own row under the menu before the menu opens.
    ///
    /// Without it the two copy items would copy whatever was selected EARLIER, so right
    /// clicking one row and getting another row's name - which is a wrong answer delivered
    /// confidently, the worst kind this product can give.
    ///
    /// <c>ContainerFromElement</c> rather than a hand written walk up the visual tree, and that
    /// is not only shorter: the thing under a pointer can be a content element rather than a
    /// visual one, and <c>VisualTreeHelper.GetParent</c> throws on those.
    /// </summary>
    private void PointAtRowBeforeMenu(object sender, MouseButtonEventArgs e)
    {
        var row = e.OriginalSource is DependencyObject source
            ? ItemsControl.ContainerFromElement(Entries, source) as DataGridRow
            : null;

        _pointedAtARow = row is not null;

        if (row?.Item is EntryRow entry)
        {
            PointAt(entry);
        }
    }

    /// <summary>
    /// Puts one entry under the menu, and leaves a selection it is already part of alone.
    ///
    /// <b>A ROW ALREADY PICKED KEEPS THE WHOLE SELECTION, AND ONE OUTSIDE IT REPLACES IT.</b> Since
    /// the grid started taking more than one row this became the difference between a menu that acts
    /// on what somebody pointed at and one that acts on that PLUS whatever was highlighted earlier -
    /// the confidently wrong answer the handler above exists to prevent, arriving by the back door.
    /// Marking a row selected only ever ADDS.
    ///
    /// Right clicking inside a selection has to keep it, or there would be no way to reach the menu
    /// for five rows at once, which is what the selection is for. Every list in Windows works this
    /// way round.
    ///
    /// <b>Apart from the handler so that it can be checked at all, and on the ENTRY rather than on
    /// the row container.</b> A handler the framework calls is reachable only by clicking, and a
    /// container exists only for rows the grid has realised - so a test written against containers
    /// would be a test about scrolling.
    /// </summary>
    internal void PointAt(EntryRow entry)
    {
        if (Entries.SelectedItems.Contains(entry))
        {
            return;
        }

        // ONE LINE, AND THE SECOND WAS MEASURED AWAY RATHER THAN REASONED AWAY. This began as an
        // UnselectAll followed by this, on the assumption that marking one row selected would only add
        // to the others. The mutation registry reported that UnselectAll as MISSED - taking it out
        // broke nothing - because assigning SelectedItem on a grid in Extended mode already clears
        // what else was picked. A line nothing can break is a line that does nothing.
        Entries.SelectedItem = entry;
    }

    /// <summary>
    /// A right click that landed on no row gets no ROW menu - and on a heading it gets the
    /// heading's own.
    ///
    /// The alternative is the fault the handler above exists to prevent, arriving by the back
    /// door: click the header or the empty space under the last row, and the menu offers to copy
    /// whatever was selected some time earlier.
    ///
    /// <b>The keyboard route is left alone</b>, and it is told apart by the cursor position being
    /// negative, which is how WPF reports a menu opened from the menu key. There the focused row
    /// IS the selected row, so there is nothing to point at.
    ///
    /// <b>THE HEADING GOT A MENU OF ITS OWN ON 2026-09-05 - the owner's ask.</b> That space was
    /// free precisely because of the line below: a right click on a heading was suppressed here
    /// and did nothing at all. It is still suppressed - what opens instead is built for the
    /// column that was clicked, by <see cref="OfferTheColumnMenu"/>, which answers false when the
    /// pointer was on neither a row nor a heading and then nothing opens, exactly as before.
    /// </summary>
    private void OfferTheMenuOnlyOnARow(object sender, ContextMenuEventArgs e)
    {
        if (e.CursorLeft < 0 || _pointedAtARow)
        {
            return;
        }

        e.Handled = true;

        OfferTheColumnMenu(e.OriginalSource);
    }

    /// <summary>
    /// The one menu this window opens under a button: which columns the list shows.
    ///
    /// <b>Apart from its handler for the same reason <see cref="Act"/> is</b> - a handler the
    /// framework calls is reachable only by clicking, and what it does is worth asserting. How a
    /// menu opens under a button lives in <see cref="ButtonMenu"/>, which is where it went while
    /// there were two of these and the reasoning was written twice. The second was the examples
    /// button, and it went on 2026-08-13 - its six questions are in the search box's tooltip now.
    /// </summary>
    internal bool OpenColumns() => Filters.OpenColumns();

    private void CopyServiceName(object sender, RoutedEventArgs e) => Copy(Copying.Name);

    private void CopyDisplayName(object sender, RoutedEventArgs e) => Copy(Copying.DisplayName);

    private void CopyDescription(object sender, RoutedEventArgs e) => Copy(Copying.Description);

    private void CopyEverything(object sender, RoutedEventArgs e) => Copy(Copying.Everything);

    private async void PreviewStop(object sender, RoutedEventArgs e) =>
        await Preview(ActionKind.Stop).ConfigureAwait(true);

    private async void PreviewStart(object sender, RoutedEventArgs e) =>
        await Preview(ActionKind.Start).ConfigureAwait(true);

    private async void PreviewRestart(object sender, RoutedEventArgs e) =>
        await Preview(ActionKind.Restart).ConfigureAwait(true);

    /// <summary>
    /// Which preview was asked for most recently, so an older one cannot land on top of it.
    ///
    /// <b>A counter arrived with the background work of backlog 301 and would have been machinery
    /// answering nothing before it.</b> While the plan was worked out on the window's own thread,
    /// two asks could not overlap - the menu could not even open again until the first had
    /// finished. A quarter of a second is long enough for somebody to ask for Stop, change their
    /// mind and ask for Start, and two builds that finish in the order they please would put the
    /// first answer over the second with nothing on screen to say so.
    ///
    /// <b>Read and written on the window's thread only</b>, which is what makes an ordinary field
    /// enough: both the increment and the comparison happen before and after an await that comes
    /// back here.
    /// </summary>
    private int _previews;

    /// <summary>
    /// Works out what an operation over the picked rows would do, and puts it on screen.
    ///
    /// <b>Apart from the three handlers so that it can be checked at all</b> - a handler the framework
    /// calls is reachable only by clicking, and the answer is the part worth asserting: a menu item
    /// that opens nothing looks exactly like a feature that is not there.
    ///
    /// <b>The selection is read here, at the moment somebody asks, and never kept</b> - the repair
    /// described at Copy. Internal names rather than rows, because that is what identity is: `ADR-14`,
    /// and a plan built from display names would work on one machine and not the next.
    ///
    /// <b>The cascade is NOT asked for, which matches the command line's own default and is a safety
    /// property rather than a preference.</b> Asking to stop one service is not asking to stop seven.
    /// What the plan does instead is name the ones in the way, in a warning, so `C2`'s promise about
    /// saying what would be dragged in is kept without quietly widening what was asked. There is no
    /// control for turning it on yet and that is open rather than decided.
    /// </summary>
    /// <param name="kind">What was asked for, in the words a person used.</param>
    /// <param name="to">
    /// The start type to write, and nothing at all for the other three asks. It travels from the
    /// menu that offered it rather than being worked out here, for the reason every other value in
    /// this file travels the same way: the control knows what it offered, and the model knows what
    /// that means.
    /// </param>
    internal async Task<bool> Preview(ActionKind kind, StartType? to = null)
    {
        var picked = Entries.SelectedItems.OfType<EntryRow>().ToList();
        var names = Everything(picked);

        if (names.Count == 0)
        {
            return false;
        }

        // Mutually exclusive with the details panel, for the reason at OpenDetails above. Done
        // before the waiting rather than after it, so the window answers the press at once even
        // though the plan behind it takes a moment.
        _model.Chosen.Hide();

        var asked = ++_previews;

        // OFF THE WINDOW'S THREAD SINCE 2026-09-03 - backlog 301, and the numbers are at
        // MainViewModel.PlanAsync. The whole listing selected and asked to stop was measured at
        // 224-240 ms, all of it round trips to the manager, and the owner's decision was that a
        // window not answering for that long is too much.
        var plan = await _model.PlanAsync(new BulkAction(kind, names, To: to)).ConfigureAwait(true);

        if (asked != _previews)
        {
            // Somebody asked for something else while this was being worked out. Dropped rather
            // than shown: a preview that arrives after a later one would put an answer about Stop
            // under a heading somebody opened for Start, and the panel has no way to say which of
            // the two it is looking at.
            return false;
        }

        // THE LABEL TRAVELS BESIDE THE NAMES RATHER THAN INSTEAD OF THEM, and only when there is
        // one row: the plan is built from internal names because that is what identity is, and the
        // display name is what the title calls it so that somebody reads "Performance Logs and
        // Alerts" rather than "pla" before changing a machine. Both end up on screen - the panel
        // puts the internal name under the title, exactly as the details panel does.
        var shown = _model.Planned.Show(plan, picked.Count == 1 ? picked[0].DisplayName : null);

        if (shown)
        {
            PlanPanel.TakeTheKeyboard();
        }

        return shown;
    }

    /// <summary>
    /// Takes up the offer under a failure: closes the sheet reporting it and opens a new one
    /// holding a plan that ends the process.
    ///
    /// <b>A NEW SHEET RATHER THAN A SECOND QUESTION ON THIS ONE, and that is the design's first
    /// named collision.</b> A sheet that has been carried out is a RECORD, and `docs/11` 3.12 says
    /// a sheet asks one question and is answered once. Growing a second ask onto a record would put
    /// a report of what happened and a proposal about what could still happen on one surface, with
    /// one button underneath and nothing saying which of the two it belongs to.
    ///
    /// <b>The reason travels across and the plan does not.</b> What opens here is worked out
    /// against the machine as it is at this moment - the entry may have finished stopping while
    /// somebody read the failure, in which case the core refuses to build anything and says so.
    /// Carrying the old plan over would have been carrying an answer about a machine that has
    /// moved.
    ///
    /// <b>It goes through the same counter every other preview goes through</b>, so an offer taken
    /// up while a menu preview is still being worked out cannot be overtaken by it. The two are the
    /// same kind of event and the window has no way to tell a person which of two answers it is
    /// showing.
    /// </summary>
    internal async Task<bool> Force(PlanFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (failure.Offer is not { } offer)
        {
            return false;
        }

        // THE OLD SHEET GOES FIRST, BEFORE THE WAITING RATHER THAN AFTER IT. Working the new plan
        // out means asking the manager, which takes a moment - and a record of a failed run left
        // standing under a press somebody has already made reads as a button that did nothing.
        _model.Planned.Hide();

        var asked = ++_previews;

        var plan = await _model
            .PlanAsync(new BulkAction(offer.Kind, [offer.ServiceName]))
            .ConfigureAwait(true);

        if (asked != _previews)
        {
            return false;
        }

        // THE NAME A PERSON RECOGNISES IS LOOKED UP AGAIN RATHER THAN CARRIED, because the offer
        // travels with the manager's own name - `ADR-14`, the only thing identity may be made of -
        // and the row it belongs to is the one thing that knows what to call it. A row that has
        // gone from the listing since gives nothing, and the title falls back to the internal name,
        // which is exactly what it does for every plan not built by this window.
        var shownAs = _model.Rows
            .FirstOrDefault(row => string.Equals(
                row.ServiceName, offer.ServiceName, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName;

        var shown = _model.Planned.Show(plan, shownAs, offer.Because);

        if (shown)
        {
            // WHERE THE KEYBOARD LANDS IS PART OF THIS SLICE RATHER THAN A COURTESY. This is the
            // one sheet in the window whose main button ends a process, so Enter arriving on it
            // with nothing to say where focus is would be an instruction nobody gave.
            PlanPanel.TakeTheKeyboard();
        }

        return shown;
    }

    /// <summary>
    /// Every entry the picked rows stand for, which for a folded row is a whole per-user family.
    ///
    /// <b>THE HALF OF `A11` THE SPECIFICATION WARNS ABOUT IN ITS OWN WORDS</b> - "inaczej admin
    /// kliknie stop na jednym wierszu i zatrzyma cztery uslugi, nie wiedzac o tym". A row standing
    /// for four session copies has to put all four into the plan, or the preview is shorter than
    /// the run and `ADR-11` has produced the one thing it exists to prevent.
    ///
    /// <b>THE TEMPLATE GOES IN BESIDE ITS INSTANCES, AND THAT IS A MEASUREMENT RATHER THAN A
    /// READING OF THE SENTENCE ABOVE.</b> The glossary says an action on a template concerns all
    /// its instances, which could be read as concerning only them. Measured on this machine on
    /// 2026-08-25 over the whole family: the template and its instance agree on the start type 23
    /// times out of 23, and the template is what a new session's copy is made from. So setting a
    /// folded row to Disabled without the template would disable the copies that exist and leave
    /// the next login making an automatic one - the change would evaporate at the next logon, with
    /// nothing on screen having been wrong. A silently reversible write is worse than a refused
    /// one.
    ///
    /// <b>What that costs, said rather than hidden: a stop over a folded row carries a step for an
    /// entry that is already stopped.</b> A template never runs - 0 of 23 on this machine, against
    /// 8 of its 23 instances - so the runner reads it, finds it where it was asked to be and
    /// reports "already there", which is the truth. <see cref="Bws.Core.Planning.BulkPlan"/> argues
    /// at length that a step like that is named rather than tidied away, and a refusal lands beside
    /// its own entry without holding up the rest of the selection.
    ///
    /// <b>It reads what the rows stand for NOW rather than recomputing the fold</b>, which is what
    /// keeps the plan and the screen the same answer. The fold is recomputed on every pass and
    /// moving the switch is a pass, so a row can never be standing for something different from
    /// what the person pressing the button is looking at.
    /// </summary>
    private static List<string> Everything(List<EntryRow> picked)
    {
        var names = new List<string>(picked.Count);

        foreach (var row in picked)
        {
            // Internal names rather than display names, because that is what identity is -
            // `ADR-14`. A session's copy carries no display name of its own at all: measured on
            // this machine, all 23 of them answer with their own service name where the template
            // answers with a translated sentence.
            names.Add(row.ServiceName);

            names.AddRange(row.Instances.Select(instance => instance.ServiceName));
        }

        return names;
    }

    /// <summary>
    /// Puts one field of the chosen row on the clipboard, or says why it could not.
    ///
    /// <b>The selection is handed over here rather than bound, and that is a repair rather than a
    /// preference.</b> SelectedItem was bound two way for one afternoon and the window journey
    /// turned flaky inside it: passages reporting a grid that disagreed with its own count line,
    /// with the window saying it was holding still. A two way binding into a list that reconciles
    /// itself once a second is another party in the middle of `A10`, and this needs none of it -
    /// the selection is only ever read at the moment somebody asks for a copy.
    ///
    /// <b>A refusal is reported rather than swallowed</b>, which is rule 8 arriving somewhere it
    /// is easy to think it does not apply. The clipboard belongs to whatever process grabbed it
    /// last, so this genuinely fails on a working machine - and a copy that quietly did nothing
    /// leaves somebody pasting the previous thing they copied into a command that stops a
    /// service.
    ///
    /// Which row and which field is decided by the view model, where it can be checked.
    ///
    /// <b>It answers whether it did anything, since 2026-08-13, because a key press asks.</b> Ctrl+C
    /// over a list with nothing chosen has to be handed back rather than swallowed - the same rule
    /// every other shortcut in this window follows, and the reason it returns a value at all.
    /// </summary>
    private bool Copy(Func<IReadOnlyList<EntryRow>, string?> field)
    {
        // EVERY ROW THAT IS PICKED, SINCE THE GRID STARTED TAKING MORE THAN ONE. A menu opened over
        // five highlighted rows and acting on one of them is the same confidently wrong answer that
        // PointAtRowBeforeMenu exists to prevent, and it would arrive with nothing on screen to say
        // which of the five had been used.
        //
        // Read here rather than kept, which is the repair described above. Filtered by type rather
        // than cast, so a grid holding something unexpected drops it instead of throwing over a
        // clipboard operation.
        var rows = Entries.SelectedItems.OfType<EntryRow>().ToList();

        if (field(rows) is not string text || text.Length == 0)
        {
            return false;
        }

        Put(text);

        // The press did something either way: it either copied, or it said out loud that it could
        // not. Handing it back after saying so would let it reach whatever is behind this window.
        return true;
    }

    /// <summary>
    /// Puts one string on the clipboard, or says why it could not.
    ///
    /// <b>Apart from the method above since 2026-09-02, and the reason is a second caller rather
    /// than tidiness.</b> The plan sheet grew a copy button under each terminal command, and that
    /// button has no row and no field - it has a string. Everything below it was already right and
    /// had to stop being reachable only through a row.
    ///
    /// <b>A refusal is reported rather than swallowed</b>, which is rule 8 arriving somewhere it is
    /// easy to think it does not apply. The clipboard belongs to whatever process grabbed it last,
    /// so this genuinely fails on a working machine - and a copy that quietly did nothing leaves
    /// somebody pasting the previous thing they copied into a command that stops a service.
    /// </summary>
    internal void Put(string text)
    {
        try
        {
            // The flushing overload, so the text outlives this process. Copying a service name
            // and closing the window is an ordinary thing to do.
            Clipboard.SetDataObject(text, copy: true);
        }
        catch (System.Runtime.InteropServices.ExternalException refusal)
        {
            _model.Says.CouldNotDo(refusal.Message);
        }
    }
}
