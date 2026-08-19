using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The window itself, which does as little as a window can.
///
/// Everything it shows is decided by <see cref="MainViewModel"/>, so what the list will
/// contain can be checked without opening one. What is left here is what only a window knows:
/// when it is on screen, when somebody is pointing at the list, and what a key press means.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// How often the list asks what is running.
    ///
    /// Measured before choosing it: the cheap reading costs 13-22 ms over 810 entries against
    /// 423-500 ms for a full one, so asking every second spends under two per cent of one
    /// processor. A slower tick would be cheaper and would also be a window that tells you
    /// about a service three seconds after you stopped it, which is the habit `A10` exists to
    /// break.
    /// </summary>
    private static readonly TimeSpan AskEvery = TimeSpan.FromSeconds(1);

    private readonly MainViewModel _model;

    /// <summary>
    /// Which columns the list is showing - `A8`, and a view model of its own.
    ///
    /// <b>Not hung off <see cref="MainViewModel"/>, and the reasoning is at
    /// <see cref="ColumnBar"/>.</b> The short of it: that class is about what the list CONTAINS,
    /// and this is about what the rows are shown through. The window is the only thing that needs
    /// both, so the window is where they meet.
    /// </summary>
    private readonly ColumnBar _columns = new();

    private readonly DispatcherTimer _timer;

    /// <summary>
    /// The button that opens the list of columns, which belongs to the row of filters.
    ///
    /// <b>A field again since 2026-08-13, and losing this line was what the theme cost.</b> While
    /// the filters row was a style carrying a template, this button had to be found by name at
    /// construction and refused with an exception when the part was missing - because a theme file
    /// has no code-behind class. Backlog 185 chose the UserControl instead, so the button is a
    /// field on the control that owns it and this is one line of forwarding.
    /// </summary>
    internal Button ColumnsButton => Filters.Picker;

    public MainWindow()
        : this(new PreferencesFile())
    {
    }

    /// <summary>
    /// A window that keeps its layout somewhere else, which is how anything but a person gets one.
    ///
    /// A seam, because every test and every probe here builds this window: without it they would
    /// all read and then overwrite the layout of whoever is logged in. <see cref="KeptColumns"/>
    /// carries the rest of that argument.
    /// </summary>
    internal MainWindow(PreferencesFile preferences)
        : this(preferences, new MainViewModel())
    {
    }

    /// <summary>
    /// A window looking at a machine somebody else chose. The second seam, and the same argument.
    ///
    /// <b>Added 2026-08-18 because a test that replaces DataContext afterwards is a test about
    /// itself.</b> This window kept its model in a field AND as its DataContext, so a test handing it
    /// a different one moved the bindings and left every handler talking to the first - a plan shown
    /// on one model while the panel watched another, with nothing in the build to say so. The panel
    /// stayed empty and read exactly like a feature that had not been wired up.
    ///
    /// One object, two readers, and no way for them to disagree. The public constructor still builds
    /// its own, so nothing about running the program changed.
    /// </summary>
    internal MainWindow(PreferencesFile preferences, MainViewModel model)
    {
        _model = model;

        InitializeComponent();

        DataContext = _model;

        // THE KEPT LAYOUT, BEFORE THE GRID HAS A SINGLE COLUMN - which columns are on, in what
        // order, how wide. A file that is unreadable, stale or from another build is reconciled
        // into something usable first, and whatever could not be honoured is said out loud.
        var kept = new KeptColumns(preferences);

        _columns.Follow(kept.Plan);

        // BEFORE ANY ROW EXISTS, and the grid has no columns at all until this line runs. There
        // are eighteen of them and twelve are off, which is a list somebody chooses from rather
        // than a list written out - see ListColumns.
        if (kept.Trouble(ListColumns.Fill(Entries, _columns, kept.Plan)) is { } trouble)
        {
            _model.Says.AboutTheLayout(trouble);
        }

        kept.Watch(this, Entries, _columns, _model.Says);

        // SET RATHER THAN BOUND, and that is the same trap the column headers fell into: a menu
        // hangs off a Popup, which is not in the visual tree, so what it inherits is a question
        // with an answer nobody should have to know. Handing it the choices costs one line and
        // has no such question.
        if (ColumnsButton.ContextMenu is { } menu)
        {
            // HEADINGS AS ITEMS, NOT AS GROUPS, AND THAT IS A REPAIR RATHER THAN A PREFERENCE. The
            // first version of this used GroupStyle with a grouped collection view. It looked right
            // and it took the whole menu out of the automation tree: with it open, the window
            // offered twelve togglable elements - all of them filter chips - and none of the
            // seventeen columns. WPF puts a GroupItem between a menu and its items and the menu's
            // peer does not reach through it, so a screen reader sees what the probe saw.
            //
            // A flat list of headings and choices keeps every entry a real MenuItem with a peer of
            // its own. Which style each one wears is decided by the selector below.
            menu.ItemsSource = _columns.Entries;
            menu.ItemContainerStyleSelector = new ColumnEntryStyles();
        }

        // THE EXAMPLES MENU AND ITS BUTTON WENT ON 2026-08-13, owner's decision, and there is
        // nothing to wire in their place: the six questions are in the search box's tooltip now,
        // composed as one string in QueryExamples.cs. A tooltip bound on the box needs no handler,
        // no ItemsSource handed over and no Popup to reason about.

        // On the interface thread by design. The tick itself does nothing but start a reading
        // that runs elsewhere, and having it arrive here means nothing from a worker thread
        // ever touches what is on screen - `docs/06`, part 4.
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = AskEvery };
        _timer.Tick += async (_, _) => await Tick().ConfigureAwait(true);

        // After the window is up, not before. Reading the manager takes about half a second
        // over 810 entries, and doing it in the constructor means the window appears already
        // late - the specification asks for a useful list inside a second, and part of that
        // second is spent showing that something is happening.
        Loaded += async (_, _) =>
        {
            await _model.LoadAsync().ConfigureAwait(true);
            _timer.Start();
        };

        // Nothing to refresh when nobody can see it. A window minimised for an afternoon has
        // no business asking the manager anything, and the first tick after it comes back
        // catches up in one go.
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        };

        // THE ONLY PLACE IN THIS WINDOW THAT LEADS TO A MACHINE CHANGING. An async lambda on an
        // event, which is the shape this constructor already uses twice above - so the run is
        // awaited by something rather than started and dropped.
        PlanPanel.CarryOutRequest += async (_, _) => await CarryOut().ConfigureAwait(true);

        // Through a method rather than into the field, because everything else that touches the
        // run in flight lives in MainWindow.Carrying.cs and a field read from two files is a field
        // with two stories. The wiring stays here, where every other event of this panel is wired.
        PlanPanel.InterruptRequest += (_, _) => AskTheRunToStop();

        Closed += (_, _) => _timer.Stop();
    }

    private async Task Tick()
    {
        // The fade runs on every tick rather than only when something moved, or the last thing
        // to change would stay lit until the next thing did.
        await _model.RefreshAsync().ConfigureAwait(true);
        _model.FadeHighlights();
    }

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

        if (!e.Handled)
        {
            e.Handled = await Act(Wanted(e.Key, e.KeyboardDevice.Modifiers)).ConfigureAwait(true);
        }
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
    /// Hands the bar above this window over to <see cref="TitleBar"/>, once there is a handle.
    ///
    /// Here rather than in the constructor because the attribute is set against a window HANDLE,
    /// and a WPF window has none until its source is initialised - the same call one step earlier
    /// fails in the quietest way there is, by succeeding.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        TitleBar.Darken(this);
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

    /// <summary>
    /// Moves the selection to the next entry beginning with a character, and says whether it moved.
    ///
    /// Apart from the handler for the same reason <see cref="Act"/> is: the half that can be
    /// checked should not live inside the half that cannot. What it returns is the part that is
    /// easy to get wrong - a press marked handled by something that did nothing is a press that
    /// silently stops working for whatever needed it next.
    /// </summary>
    internal bool JumpTo(char? letter)
    {
        if (letter is null)
        {
            return false;
        }

        // The grid's own order, not the model's. Once a column can be sorted they are two
        // different sequences, and the one somebody is looking at is this one.
        var row = ViewModels.RowList.NextStartingWith(
            [.. Entries.Items.Cast<ViewModels.EntryRow>()],
            Entries.SelectedItem as ViewModels.EntryRow,
            letter.Value);

        if (row is null)
        {
            return false;
        }

        // The grid first, then the model, then the view. Setting the grid's selection is what
        // raises the change that hands the row to the model everywhere else in this window, so
        // doing it here keeps one path rather than two that can disagree.
        Entries.SelectedItem = row;
        Entries.ScrollIntoView(row);

        return true;
    }

    /// <summary>
    /// Carries out one shortcut, and says whether it did anything.
    ///
    /// <b>Apart from the handler so that it can be checked at all</b> - a handler the framework
    /// calls is reachable only by pressing a key, and the answer this returns is exactly the
    /// thing that is easy to get wrong: a key marked handled by something that decided to do
    /// nothing is a key that silently stops working for whatever needed it next.
    /// </summary>
    internal async Task<bool> Act(Shortcut shortcut)
    {
        switch (shortcut)
        {
            case Shortcut.Refresh:
                // F5, because admins trust it more than they trust an automatic - `A10` says so
                // in as many words. It reads everything, including the configuration the
                // per-second reading deliberately does not watch.
                await _model.LoadAsync().ConfigureAwait(true);

                return true;

            case Shortcut.FocusQuery:
                // Selected, not just focused. Ctrl+F in every other program starts a new search
                // rather than appending to the last one, and a person who wanted to keep the old
                // text still has it - one key press away, unselected by typing nothing.
                QueryBox.Focus();
                QueryBox.SelectAll();

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
    /// Somebody is using the list, so it stops rearranging itself underneath them.
    ///
    /// The mouse being over it counts, not just the keyboard focus: reaching for a row is
    /// exactly the moment when a row appearing above it would move the target.
    /// </summary>
    private void ListEngaged(object sender, RoutedEventArgs e) => _model.Interacting = true;

    private void ListReleased(object sender, RoutedEventArgs e) =>
        _model.Interacting = Entries.IsMouseOver || Entries.IsKeyboardFocusWithin;
}
