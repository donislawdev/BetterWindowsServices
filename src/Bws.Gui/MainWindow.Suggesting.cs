using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The window's half of the list under the search box: the events that open and close it, the
/// three presses that move through it, and the writing of a chosen row into the box.
///
/// <b>Point 9 of `docs/11` 2.14, backlog 15, built 2026-09-15 to
/// `docs/PROJEKT-PODPOWIEDZI-20260915.md`.</b> Everything that decides WHAT the list holds is in
/// <see cref="Suggesting"/> and has a test without a window. What is left here is the part only a
/// window can know - where the keyboard is, where the caret is, what the pointer is over - and each
/// handler is one call into the model.
///
/// <b>A partial rather than a handler in SearchRow, for the reason the box's typing timer is
/// wired from the window</b>: Ctrl+F, Escape and Enter are decided beside every other shortcut in
/// MainWindow.Keyboard.cs, and the list's presses are three more of them. Splitting the keys of
/// one list across two files would put one decision in two places.
///
/// <b>The closing is the window's, and there are six locks.</b> The popup says StaysOpen, because
/// a popup that closes itself on an outside click takes that click with it - and the click made
/// most often while the list is open is on the box it hangs off. So the list closes when the box
/// loses the keyboard, on Escape, on a row being taken, when nothing is left to offer, when the
/// window loses activation, and when the window moves: a popup does not travel with its window,
/// which is the thing the library's own AutoSuggestBox hooks the window procedure for. Closing is
/// cheaper and more honest than chasing.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// True for exactly as long as <see cref="GiveTheBoxTheKeyboard"/> is handing the box its
    /// keyboard, which is the one arrival WITH somewhere to come from that is still not a person.
    /// </summary>
    private bool _givingTheBoxTheKeyboard;

    /// <summary>
    /// Puts the keyboard in the search box when the list appears - at the window's start, and when
    /// the overview is put away - without offering anything under it. UX-GUI-010, owner's decision
    /// 2026-09-24: this is a tool for searching, and until that day typing after it opened went
    /// nowhere, because nothing held the keyboard but the window itself.
    ///
    /// <b>QUIET, AND THAT IS THE WHOLE DIFFICULTY.</b> The focus event below reads a focus with
    /// somewhere to come from as a person arriving, and opens the questions under an empty box. From
    /// the overview the keyboard comes from its "Show the list" button, so a plain Focus() here would
    /// open a list nobody asked for - which the audit and backlog 136 both rule out. The flag makes
    /// this one arrival read like the framework's own hand-back.
    ///
    /// <b>The box and not the grid</b>, because keyboard focus in the grid holds the list still
    /// (`A10`) and nobody opening a window has asked it to stop.
    ///
    /// <b>AT INPUT PRIORITY, AND THE FIRST BUILD WITHOUT IT WAS MEASURED DOING NOTHING.</b> Called
    /// the moment the overview goes, the box had been given its Visibility and not yet a layout pass,
    /// and the button holding the keyboard was being collapsed in the same breath - so
    /// tools/gui-probe/focus-at-start.ps1 found the keyboard on the window, exactly as before the
    /// change. PlanView.TakeTheKeyboard paid for the same lesson first: shown and arranged are two
    /// moments. Nothing waits for the focus to land, so the operation is discarded (MA0134).
    /// </summary>
    internal void GiveTheBoxTheKeyboard() =>
        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            () =>
            {
                if (_model.ShowingOverview)
                {
                    return;
                }

                _givingTheBoxTheKeyboard = true;

                try
                {
                    Search.Box.Focus();
                }
                finally
                {
                    _givingTheBoxTheKeyboard = false;
                }
            });

    /// <summary>
    /// Wires the box and the list to the model. Called once, from the constructor, beside the
    /// typing timer that watches the same box.
    ///
    /// <b>Two different focus events for two different things.</b> A focus that arrives with
    /// something to come FROM is a person: a click, Tab, Ctrl+F. A focus that arrives from nowhere
    /// is the framework handing the box its keyboard back - at the window's start, or on the return
    /// after Alt+Tab - and that is not a moment anybody asked for help. The model has a word for
    /// each and shows the questions that fit the list only on the first.
    ///
    /// <b>And a click in a box that already has the keyboard is an arrival too</b>, said through
    /// the mouse rather than through focus, because focus has nothing to report: somebody who
    /// closed the questions and clicks the empty box again is asking for them again.
    /// </summary>
    private void WatchTheBoxForSuggestions()
    {
        var box = Search.Box;
        var list = _model.Suggesting;

        box.GotKeyboardFocus += (_, e) =>
        {
            if (e.OldFocus is null || _givingTheBoxTheKeyboard)
            {
                list.Keyboard(present: true);
            }
            else
            {
                list.Arrived(box.Text);
            }
        };

        box.PreviewMouseLeftButtonUp += (_, _) => list.Arrived(box.Text);

        // And the keyboard starts here rather than on the window, since 2026-09-24 - the same
        // quiet hand-over as the one after the overview, GiveTheBoxTheKeyboard.
        Loaded += (_, _) => GiveTheBoxTheKeyboard();

        // The one lock that covers a chip, a tab, the plan sheet, the box's own context menu and
        // the closing of the window: all of them take the keyboard.
        box.LostKeyboardFocus += (_, _) => list.Left();

        // THE BOX'S TEXT, NOT THE MODEL'S. The binding to QueryText waits 400 ms so that the list
        // of entries does not narrow under every keystroke, and a list of completions that waited
        // with it would be a list answering the previous keystroke. The caret and the text arrive
        // as two events and both go to Follow with the current values, which tells typing from a
        // caret moving through unchanged text by the text itself - so their order is not relied on.
        box.TextChanged += (_, _) => FollowTheBox();
        box.SelectionChanged += (_, _) => FollowTheBox();

        // NOTHING IS WRITTEN OVER A CHARACTER STILL BEING COMPOSED - review of PR 22, and
        // Suggesting.CanTake says why. The start and the end of every text composition in the box,
        // handled or not, because an input method's own handling is exactly the case this is for.
        box.AddHandler(
            TextCompositionManager.PreviewTextInputStartEvent,
            new TextCompositionEventHandler((_, _) => list.Composing(open: true)),
            handledEventsToo: true);
        box.AddHandler(
            TextCompositionManager.PreviewTextInputEvent,
            new TextCompositionEventHandler((_, _) => list.Composing(open: false)),
            handledEventsToo: true);

        Search.List.PreviewMouseLeftButtonUp += (_, e) => TakeUnderThePointer(e);

        Deactivated += (_, _) => list.Close();
        LocationChanged += (_, _) => list.Close();
    }

    private void FollowTheBox() =>
        _model.Suggesting.Follow(Search.Box.Text, Search.Box.CaretIndex, Search.Box.SelectionLength);

    /// <summary>
    /// The three presses that belong to the list, and whether each did anything - Down opens a
    /// closed list and moves through an open one, Up moves back, Enter writes the chosen row.
    ///
    /// Through <see cref="Act"/> and never around it, so the keys and their answers stay one road:
    /// a press that did nothing is handed back, which for Up on a closed list and Enter on a
    /// closed list is exactly what the box needs.
    /// </summary>
    private bool Suggest(Shortcut shortcut)
    {
        var list = _model.Suggesting;
        var box = Search.Box;

        return shortcut switch
        {
            Shortcut.NextSuggestion => Announced(list.IsOpen ? list.Next() : list.Ask(box.Text, box.CaretIndex, box.SelectionLength)),
            Shortcut.PreviousSuggestion => Announced(list.Previous()),
            Shortcut.TakeSuggestion => Write(list.Take()),
            _ => false
        };
    }

    /// <summary>
    /// Tells a screen reader which row a press just chose, and hands the press's answer back.
    ///
    /// <b>Backlog 361: the list never has the keyboard, so nothing in it is ever announced on its
    /// own.</b> Measured 2026-09-16 with a listener subscribed as a reader is (tools/gui-probe/
    /// announce.ps1): no focus event ever names a row, and the only thing that arrives on Down is
    /// a selection event from the row's own peer - silent when the list opens, because the first
    /// row is chosen before its container exists. So the window says it itself, through the
    /// notification event Windows documents for a change somewhere other than the focus: raised on
    /// the BOX, which is where the keyboard is, carrying the sentence the model composes. Most
    /// recent wins, so holding Down does not queue up a reading of every row passed.
    ///
    /// <b>Only for a press, never for typing.</b> A list that replaces itself under every
    /// keystroke chooses a first row every time, and reading that row out while somebody types
    /// would talk over their own typing. What a reader is told about the list APPEARING is the
    /// count, and that is not built - written in `docs/08`.
    ///
    /// The peer is created on demand, because it exists only once a client has asked for it, and
    /// raising into an empty room costs nothing - the framework checks for listeners first.
    /// </summary>
    private bool Announced(bool pressDidSomething)
    {
        var list = _model.Suggesting;

        if (pressDidSomething && list.IsOpen && list.Chosen is not null
            && UIElementAutomationPeer.CreatePeerForElement(Search.Box) is { } peer)
        {
            peer.RaiseNotificationEvent(
                AutomationNotificationKind.Other,
                AutomationNotificationProcessing.MostRecent,
                list.Spoken,
                "suggestion");
        }

        return pressDidSomething;
    }

    /// <summary>
    /// A click on a row writes that row.
    ///
    /// <b>The row is found under the pointer rather than read from the selection, and that is not
    /// a choice.</b> Nothing in the popup can take focus - which is what keeps the keyboard in the
    /// box while the list is open - and a ListBoxItem only tells its list about a click after it has
    /// taken focus. So the click never becomes a selection on its own, and the handler asks which
    /// container the press landed in. Anywhere in the popup that is not a row does nothing.
    /// </summary>
    private void TakeUnderThePointer(MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.DependencyObject pressed
            || ItemsControl.ContainerFromElement(Search.List, pressed) is not ListBoxItem container
            || container.Content is not Suggestion row)
        {
            return;
        }

        _model.Suggesting.Chosen = row;
        e.Handled = Write(_model.Suggesting.Take());
    }

    /// <summary>
    /// Writes a row into the box, THROUGH THE SELECTION, and puts the caret after it.
    ///
    /// <b>Three calls where one assignment would do, and the one assignment is the wrong one -
    /// decision 13 of the design.</b> Setting Text replaces the whole line and takes the box's undo
    /// history with it, so a suggestion accepted by mistake could not be taken back with Ctrl+Z
    /// while every other edit in the same box could. Selecting the range and replacing the
    /// selection is an edit the box made itself, and it undoes like one.
    ///
    /// The box reports each of the three as a change, and the model recomputes the list at the
    /// new caret each time - which is how <c>start:</c> written from <c>sta</c> is followed at once
    /// by the eleven values that can go after it.
    /// </summary>
    private bool Write(Suggestion? taken)
    {
        if (taken is null)
        {
            return false;
        }

        var box = Search.Box;
        var start = taken.Replaces.Start.Value;
        var end = taken.Replaces.End.Value;

        // A range that no longer fits the text is a row offered against a line that has since
        // changed under it. Every change to the box recomputes the list, so this is not a state
        // the window reaches on its own - and a key press is the one place a stack trace can
        // never be the answer, so the row is dropped rather than written somewhere wrong.
        if (taken.Replaces.Start.IsFromEnd || taken.Replaces.End.IsFromEnd || start > end || end > box.Text.Length)
        {
            return false;
        }

        box.Select(start, end - start);
        box.SelectedText = taken.Written;
        box.CaretIndex = start + taken.Written.Length;

        // Followed as typing, because the last thing the box just reported is its caret moving
        // through unchanged text - which closes the list since 2026-09-25, and would take away the
        // values that belong after a field just written. Suggesting.Wrote says it in full.
        _model.Suggesting.Wrote(box.Text, box.CaretIndex);

        return true;
    }
}
