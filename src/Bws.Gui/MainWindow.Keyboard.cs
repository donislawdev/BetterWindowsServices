using System.Windows.Input;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The keyboard's half of this window: what a press means here, and the doing of it.
///
/// <b>Its own file since 2026-09-03, and the size ratchet is what asked - for the sixth time in
/// this window and the sixth time pointing at a real seam.</b> The rest of MainWindow is about the
/// window as a whole: what it is made of, when it may rearrange itself, when it asks the machine
/// anything. This is the one route into it that belongs to a person rather than to a timer, and
/// `docs/11` 9.1 says why that is not a convenience - an administrator works from the keyboard,
/// and WCAG 2.1.1 is the formal half of the same requirement.
///
/// <b>A partial class rather than a type beside the window, for the reason MainWindow.Carrying.cs
/// gives.</b> Two of the four members here are overrides of methods WPF declares on Window, so
/// they have to live on the window's own class. Pulling the other two out and leaving these behind
/// would put the decision in one file and the press that reaches it in another.
///
/// <b>The async void moved here with the code, and its entry in BackgroundWorkGuards moved with
/// it.</b> That list is keyed by BARE FILE NAME, so a seam that leaves the permission behind
/// leaves an argument being made about a file the code no longer lives in - which has already
/// happened in this project twice, to BroadCatchGuards, and was caught by the guard going red
/// rather than by anybody remembering.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// The keyboard, which is how an administrator works - `docs/11` 9.1, and WCAG 2.1.1 as the
    /// formal half of the same requirement.
    ///
    /// <b>Preview rather than bubble, and that is deliberate.</b> These three belong to the
    /// window rather than to whatever has focus, and a bubbling handler would never see Escape
    /// or Ctrl+F once a control decided to keep them. The cost of taking a key early is that it
    /// can be taken from something that needed it, which is why nothing here is marked handled
    /// unless it did something - an empty query box leaves Escape alone.
    ///
    /// What each key MEANS is decided in <see cref="Shortcuts"/> and can be tested. What is left
    /// here is the doing, which cannot be.
    /// </summary>
    protected override async void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Handled)
        {
            return;
        }

        // HANDLED IS DECIDED BEFORE THIS METHOD GIVES CONTROL BACK, AND UNTIL 2026-09-03 IT WAS
        // NOT - backlog 302. A routed event is over the moment its handler returns, and an async
        // method returns at its first await, so `e.Handled = await Act(...)` wrote the answer into
        // an argument WPF had already finished reading. Every synchronous branch got away with it,
        // because awaiting an already finished task never yields. The one branch that awaits
        // anything did not, and the next asynchronous branch anybody adds would have inherited the
        // fault without a word - which is why this is about the shape rather than about F5.
        e.Handled = Act(Wanted(e.Key, e.KeyboardDevice.Modifiers), out var work);

        await work.ConfigureAwait(true);
    }

    /// <summary>
    /// What a press means here, which is what it means anywhere except for the one key that
    /// belongs to the list.
    ///
    /// <b>The focus question is asked here rather than in <see cref="Shortcuts"/>, on purpose.</b>
    /// That class says it is deliberately ignorant of state, and where the keyboard is happens to
    /// be the one piece of state only a window can answer - the same split, and the same reason,
    /// as the letter that jumps to an entry.
    ///
    /// <b>Enter belongs to the list.</b> A preview handler sees the press before the query box
    /// does, so taking it unconditionally would mean somebody finishing a query gets a panel about
    /// whatever row happened to be selected - which is a window answering a question nobody asked.
    /// </summary>
    private Shortcut Wanted(Key key, ModifierKeys modifiers)
    {
        var wanted = Shortcuts.For(key, modifiers);

        var theListPress = wanted is Shortcut.OpenDetails or Shortcut.CopyRow;

        return theListPress && !Entries.IsKeyboardFocusWithin ? Shortcut.None : wanted;
    }

    /// <summary>
    /// Carries out one shortcut, and says - before it hands anything back - whether it did
    /// anything at all.
    ///
    /// <b>Apart from the handler so that it can be checked at all</b> - a handler the framework
    /// calls is reachable only by pressing a key, and the answer this returns is exactly the
    /// thing that is easy to get wrong: a key marked handled by something that decided to do
    /// nothing is a key that silently stops working for whatever needed it next.
    ///
    /// <b>THE ANSWER IS SYNCHRONOUS AND THE WORK TRAVELS BESIDE IT, since 2026-09-03. Until then
    /// this returned a task carrying both, so the answer arrived after the press had been
    /// routed</b> - backlog 302, and the handler above carries the mechanism.
    ///
    /// <b>ONE SWITCH RATHER THAN A QUESTION AND AN ANSWER, which is why this is an out parameter
    /// rather than a second method.</b> A "would this do anything" written beside a "do it" is two
    /// roads to one fact, and two roads only have to disagree once. This project was caught by
    /// exactly that shape on 2026-09-02, when a row and a cell reached one answer down separate
    /// paths, repairing the row left the cell alone, and a guard stayed green beside both.
    /// </summary>
    /// <param name="work">
    /// What is still going on when this returns, already started. An already finished task for
    /// every branch that does its work on the spot, so a caller may always await it.
    /// </param>
    internal bool Act(Shortcut shortcut, out Task work)
    {
        work = Task.CompletedTask;

        switch (shortcut)
        {
            case Shortcut.Refresh:
                // F5, because admins trust it more than they trust an automatic - `A10` says so
                // in as many words. It reads everything, including the configuration the
                // per-second reading deliberately does not watch.
                //
                // Handled whatever comes of it, and that is a statement about this branch rather
                // than a corner cut: a refresh always has something to do, so there is no result
                // to wait for before knowing the press was ours.
                work = _model.LoadAsync();

                return true;

            case Shortcut.FocusQuery:
                // Selected, not just focused. Ctrl+F in every other program starts a new search
                // rather than appending to the last one, and a person who wanted to keep the old
                // text still has it - one key press away, unselected by typing nothing.
                Search.Box.Focus();
                Search.Box.SelectAll();

                return true;

            case Shortcut.OpenDetails:
                // The grid's own selection rather than the model's, because the model is only told
                // at the moment somebody asks for something - see Copy, and the repair its comment
                // describes. This is that moment.
                _model.Chosen.Row = Entries.SelectedItem as EntryRow;

                // The two panels share a column, so opening one puts the other away. Arranged here
                // rather than by either of them, because neither has any business knowing the other
                // exists - the window is what owns the layout they compete for.
                _model.Planned.Hide();

                return _model.Chosen.Show();

            case Shortcut.CopyRow:
                // The same thing the menu's last item does, because two ways to one answer that
                // are written twice are two answers waiting to disagree.
                return Copy(Copying.Everything);

            case Shortcut.Back:
                // A PANEL FIRST, THE QUERY LAST, and the order is the decision rather than the
                // implementation - `docs/04` at Paczka 1. One press doing both at once takes
                // somebody's query away while they were reaching for the panel, and a query is the
                // more expensive of the two to type again.
                //
                // The plan panel joins the front of that queue in packet 2. Only one of the two can
                // be open at a time, so which comes first cannot change what happens - it is written
                // in this order because a plan is the more recent thing somebody opened, and if the
                // two ever could overlap that is the one they would mean.
                return _model.Planned.Hide() || _model.Chosen.Hide() || _model.ClearQuery();

            default:
                return false;
        }
    }

    /// <summary>
    /// The button over the list asking for the same thing F5 asks for.
    ///
    /// <b>Through <see cref="Act"/> rather than reaching for the model, so the bar and the key are
    /// one road to one answer.</b> Two ways to a refresh written twice are two refreshes waiting
    /// to disagree about what a refresh includes - and this one deliberately reads more than the
    /// once-a-second reading does.
    ///
    /// The answer is dropped here and only here: a button has nothing to hand a press back to.
    /// </summary>
    private Task Refreshing()
    {
        Act(Shortcut.Refresh, out var work);

        return work;
    }

    /// <summary>
    /// A character typed while the list has focus goes to the next entry beginning with it.
    ///
    /// <b>Text input rather than key down, and the reason is rule 3.</b> A key code names a
    /// position on the keyboard - on a keyboard that is not American the key where A sits produces
    /// something else, and a jump built on codes would land on the wrong entry while looking like
    /// it worked. This carries the character somebody actually typed.
    ///
    /// <b>Only while the list has focus.</b> Anywhere else the letter belongs to whatever is there,
    /// starting with the query box, and a window that swallows letters typed into a text field is
    /// a window nobody can search in.
    /// </summary>
    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPreviewTextInput(e);

        if (!e.Handled && Entries.IsKeyboardFocusWithin)
        {
            e.Handled = JumpTo(Shortcuts.JumpLetter(e.Text));
        }
    }
}
