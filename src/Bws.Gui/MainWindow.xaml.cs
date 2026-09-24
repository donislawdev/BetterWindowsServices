using System.Windows;
using System.Windows.Controls;
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
    /// How long the window waits after the last keystroke before it narrows the list.
    ///
    /// <b>MEASURED RATHER THAN CHOSEN, on the owner's own machine on 2026-08-19, with real keys at
    /// a real pace.</b> The first character of a search echoed in 105 ms, the second in 88 and the
    /// third in 61, dropping to 7-8 once the query left one row - so the cost is not the query, it
    /// is how many rows are LEFT, and the expensive characters are the first ones. Every search
    /// starts with them.
    ///
    /// <b>What this fixes is the ECHO, and that is the whole point rather than a side effect.</b>
    /// Until now the character could not appear until the filter and the grid had finished, because
    /// the binding pushed on every keystroke and the setter narrowed the list inside it. Nielsen
    /// puts typing under 50 ms to feel like direct manipulation and 100 ms as the line where a
    /// delay is felt - one of those keystrokes was over the second line and two more were over the
    /// first.
    ///
    /// <b>FOUR HUNDRED, AND THE FIRST ANSWER WAS A HUNDRED AND FIFTY - WHICH THE NEXT MEASUREMENT
    /// SHOWED WAS WORSE THAN NO ANSWER AT ALL FOR ONE KEYSTROKE.</b> A person typing at 200 ms a
    /// character and a delay set to 150 collide by arithmetic: the filter starts 150 ms after a
    /// character, and the NEXT one arrives 50 ms into it and queues behind it. Measured with that
    /// setting, the third character of a search echoed in 173 ms - worse than the 105 it was meant
    /// to fix. The block had not gone anywhere, it had moved from inside a keystroke to just before
    /// the next one.
    ///
    /// So the rule is not "short enough to feel live", it is <b>longer than the gap between two
    /// keystrokes</b>, or it fires in the middle of a burst by construction. Four hundred clears a
    /// 200 ms pace with room and stays far inside the second Nielsen gives for keeping somebody in
    /// the flow of what they are doing - it runs after they stopped, which is when nobody is
    /// waiting on a character.
    ///
    /// <b>THE DEFERRING IS Binding.Delay IN THE MARKUP, AND THE VERSION THAT DID IT HERE WAS A BUG
    /// THAT SHIPPED FOR AN HOUR.</b> That one used UpdateSourceTrigger=Explicit and pushed the box
    /// into the model from a timer - and pushing a value into a two way binding makes the binding
    /// write the SOURCE back to the TARGET, overwriting every character typed in the meantime.
    /// Measured on the owner's machine: three keystrokes out of 248 never reached the box at all,
    /// and the row counts beside them made no sense - "de" left 786 rows, "def" left 0, "defe"
    /// left 393. Binding.Delay is the framework's own answer and has none of that: it defers the
    /// SOURCE update and never touches what is in the box.
    ///
    /// <b>So this number and the one in the markup have to agree</b>, and what is left of the timer
    /// below is the one question a binding cannot answer - whether somebody is typing right now.
    /// </summary>
    private static readonly TimeSpan AfterTyping = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Whether somebody is typing right now, which is a question no binding can answer.
    ///
    /// Restarted by every keystroke and read by <see cref="Tick"/>, which will not go to the
    /// machine while it is running.
    /// </summary>
    private readonly DispatcherTimer _typing;

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

    /// <summary>
    /// The button offering the rights this session does not have, which belongs to the status row.
    ///
    /// <b>One line of forwarding since 2026-08-25, when that row moved into its own file</b> - the
    /// same shape as the line above it, and for the same reason: the tests that check whether this
    /// button is on screen and what it says are about the WINDOW rather than about a row, and a name
    /// that moved would make them look like tests of a control they were never about.
    /// </summary>
    internal Button ElevateButton => Status.Elevate;

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

        Arrange(preferences);

        HandTheColumnsOver();

        // WHAT THIS MACHINE LOOKS LIKE BEFORE ANYBODY ASKS IT ANYTHING - `G`. Its own method for the
        // reason Arrange has one: an analyser asked, and the seam is a subject rather than a line
        // count. MainWindow.Overview.cs.
        IntroduceTheOverview();

        // THE WAY OUT OF A SESSION WITHOUT RIGHTS, wired here rather than in the row that holds it -
        // 2026-08-25, when the status row moved into its own file. Pressing it starts a second copy
        // of this program, and Elevation.cs is one of the two files allowed to name a process at
        // all, held by a guard that reads the sources. A handler over there would be another file
        // reaching for that, reporting its failure through a model that row deliberately does not
        // hold.
        Status.Elevate.Click += RestartAsAdministrator;

        // The same way out on the plan sheet, beside the sentence that names it - the one at the
        // foot of the window is under the sheet's dimming while a plan is open. UX-GUI-004 (a).
        PlanPanel.Footer.Elevate.Click += RestartAsAdministrator;

        // And said before any plan, on the buttons that would open one - UX-GUI-004 (b). Once, from
        // the session's rights, which cannot change while the window is open.
        Actions.NeedsRights = _model.Says.NotElevated;

        // THE DONATE BUTTON, 2026-09-23, wired here for the reason the line above gives: pressing
        // it hands an address to the shell, which only ExternalLinks.cs may do, and a failure is a
        // sentence for the model this window holds and the row does not. An async lambda, the shape
        // the bar below uses, because the press waits for the shell off this thread.
        Scope.Donate.Click += async (_, _) => await OpenSupportPage().ConfigureAwait(true);

        // THE BAR OVER THE LIST, 2026-08-25. It asks and the window answers, which is the same
        // arrangement the plan panel uses for its own two buttons - a part of the window that
        // reaches into the model would be a second road to everything the model owns.
        Actions.PreviewRequest += async (_, asked) => await Preview(asked.Kind).ConfigureAwait(true);
        Actions.RefreshRequest += async (_, _) => await Refreshing().ConfigureAwait(true);
        Actions.ExportRequest += (_, _) => ExportWhatIsShown();

        Actions.StartTypeRequest += async (_, asked) =>
            await Preview(ActionKind.SetStartType, asked.Setting).ConfigureAwait(true);

        // TOLD WHEN A PERSON CHANGES THE SELECTION, rather than binding to it. A binding into a
        // list that reconciles itself once a second is another party in the middle of `A10`, and
        // SelectedItem bound two way is what broke the window journey on 2026-08-18. This fires on
        // the change and says one number - entries, not rows, and PickedEntries says why.
        Entries.SelectionChanged += (_, _) => Actions.Picked(PickedEntries(), OnlyDrivers(PickedRows()));

        // THE MENU ON A ROW, from a list rather than from the markup since 2026-09-15 - the
        // markup was on the size ratchet's ceiling. RowMenu says what is on it and why in that
        // order. The style is the theme's, like every other menu item this window builds in code.
        Entries.ContextMenu = RowMenu.Build(RowMenu.GroupsFor(this), (Style)FindResource("RowMenuItem"));

        // THE EXAMPLES MENU AND ITS BUTTON WENT ON 2026-08-13, owner's decision, and there is
        // nothing to wire in their place: the questions are in the search box's tooltip now, cut
        // to the list on screen and composed as one string in QueryExamples.cs. A tooltip bound on the box needs no handler,
        // no ItemsSource handed over and no Popup to reason about.

        // On the interface thread by design. The tick itself does nothing but start a reading
        // that runs elsewhere, and having it arrive here means nothing from a worker thread
        // ever touches what is on screen - `docs/06`, part 4.
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = AskEvery };
        _timer.Tick += async (_, _) => await Tick().ConfigureAwait(true);

        // THE BINDING IS EXPLICIT AND THIS IS WHAT MAKES IT MOVE. Input priority rather than
        // Background: the whole purpose is to run AFTER the character has been drawn and before
        // the person notices, and Background would put it behind whatever else the dispatcher is
        // holding - which on this window is a reading of eight hundred entries.
        _typing = new DispatcherTimer(DispatcherPriority.Input) { Interval = AfterTyping };

        // IT STOPS ITSELF AND THAT IS THE WHOLE HANDLER. A DispatcherTimer with nothing attached
        // runs for ever, so IsEnabled would stay true after the first character somebody ever
        // typed - and Tick reads exactly that to decide whether to go to the machine. The window
        // would have stopped refreshing itself permanently, quietly, from the first keystroke.
        _typing.Tick += (_, _) => _typing.Stop();

        _scrolling = HoldWhileScrolling();

        // Wired here rather than in the markup, and the reason CHANGED when the field moved out.
        // It used to be the markup ceiling - MainWindow.xaml stood exactly on it, so editing an
        // attribute cost nothing and adding one cost a seam. The seam has since been taken and the
        // ceiling is no longer the argument. What remains is the better one: this handler exists to
        // keep a once-a-second reading of the machine off somebody's keystrokes, which is a fact
        // about the WINDOW, and SearchRow knows nothing about readings. The whole argument,
        // including the bug the first version of this shipped, is at AfterTyping.
        Search.Box.TextChanged += QueryTyped;

        // The list under the same box, wired from here for the same reason - the keys it answers
        // to are decided beside every other shortcut of this window. MainWindow.Suggesting.cs.
        WatchTheBoxForSuggestions();

        // After the window is up, not before. Reading the manager takes about half a second
        // over 810 entries, and doing it in the constructor means the window appears already
        // late - the specification asks for a useful list inside a second, and part of that
        // second is spent showing that something is happening.
        Loaded += async (_, _) => await FirstLook().ConfigureAwait(true);

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

        ListenToThePlanSheet();

        Closed += (_, _) => NothingIsWatching();
    }

    /// <summary>
    /// The four things the plan sheet asks this window for.
    ///
    /// <b>A method of its own since 2026-09-07, and the length limit is what asked - MA0051, which
    /// is the size ratchet arriving as a compiler error rather than as a test.</b> The fourth event
    /// took this constructor to 125 lines against 120, and the seam it points at is a real one:
    /// everything above is what the window is MADE of, and these four are what one part of it asks
    /// the window to do on somebody's behalf.
    ///
    /// <b>Wired in one place rather than beside each thing it reaches.</b> Each of the four ends
    /// somewhere different - a run, a token, the clipboard, a new plan - and they are together here
    /// because what they have in common is the panel, not the destination.
    /// </summary>
    private void ListenToThePlanSheet()
    {
        // THE ONLY PLACE IN THIS WINDOW THAT LEADS TO A MACHINE CHANGING. An async lambda on an
        // event, which is the shape the constructor already uses twice - so the run is awaited by
        // something rather than started and dropped.
        PlanPanel.CarryOutRequest += async (_, _) => await CarryOut().ConfigureAwait(true);

        // Through a method rather than into the field, because everything else that touches the
        // run in flight lives in MainWindow.Carrying.cs and a field read from two files is a field
        // with two stories. The wiring stays here, where every other event of this panel is wired.
        PlanPanel.InterruptRequest += (_, _) => AskTheRunToStop();

        // The clipboard rather than the panel, for the reason beside CopyRequest over there: only
        // the window can say a copy was refused, because only the window owns the status line.
        PlanPanel.CopyRequest += (_, asked) => Put(asked.Command);

        // THE WAY OUT FROM UNDER A FAILURE, AND IT OPENS A PLAN RATHER THAN CARRYING ONE OUT. The
        // same shape as the first - an async lambda awaiting the work - because working a plan out
        // asks the manager and that moved off this thread on 2026-09-03.
        PlanPanel.ForceRequest += async (_, asked) => await Force(asked.Failure).ConfigureAwait(true);

        // THE OFFER UNDER "KEEPS RUNNING" - spec C4, the owner's variant O1 of 2026-09-24. The same
        // question asked again with the stop riding on it, so the sheet that answers is this one
        // with a second step rather than a second sheet somebody has to remember to open.
        PlanPanel.AlsoStopRequest += async (_, _) => await AlsoStop().ConfigureAwait(true);
    }

    /// <summary>
    /// The window has gone, so nothing goes on asking the machine anything for it.
    ///
    /// <b>Two halves, and until 2026-09-03 there was one</b> - backlog 300. Stopping the timer
    /// stops the NEXT reading being asked for and says nothing at all to the one already out,
    /// which then comes back and rebuilds rows, re-runs a query and moves a status line for a
    /// window nobody can see. What it still does not do is stop the work itself, and that is
    /// written out at <see cref="ViewModels.Readings.NoLongerWanted"/> rather than left here.
    /// </summary>
    private void NothingIsWatching()
    {
        _timer.Stop();
        _model.NoLongerWanted();
    }

    private async Task Tick()
    {
        // NOT WHILE SOMEBODY IS MID-WORD, AND THIS IS A MEASUREMENT RATHER THAN A COURTESY. On the
        // owner's machine on 2026-08-19 this tick queued the interface thread for 35 and 49 ms,
        // twice in twelve seconds, 959 ms apart - the signature of a once-a-second timer. It is
        // cheap most of the time and expensive when the machine actually moved, and a person
        // typing at five characters a second meets it constantly. A probe driven back to back
        // almost never does, which is why every number this project had said the window was fast.
        //
        // NOT DONE BY HOLDING THE LIST, WHICH WAS THE OBVIOUS VERSION AND IS WRONG. `A10`'s hold
        // runs through Holding.MayRearrange, and Apply asks the same question - so a window that
        // held while somebody typed would refuse the narrowing their own query asked for. The
        // hold is about rows moving under a pointer. This is about not reading a machine while
        // somebody is in the middle of a word.
        //
        // AND NOT MID-GESTURE EITHER, since 2026-08-25. The same sentence with a different noun:
        // a person dragging through eight hundred rows meets this tick constantly, and every one of
        // them recuts the scope and re-runs the query to build a rearrangement that `A10` then
        // refuses. The whole argument is in the constructor, beside _scrolling.
        if (_typing.IsEnabled || _scrolling.IsEnabled)
        {
            return;
        }

        // The fade runs on every tick rather than only when something moved, or the last thing
        // to change would stay lit until the next thing did.
        await _model.RefreshAsync().ConfigureAwait(true);
        _model.FadeHighlights();
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
    /// Somebody is using the list, so it stops rearranging itself underneath them.
    ///
    /// The mouse being over it counts, not just the keyboard focus: reaching for a row is
    /// exactly the moment when a row appearing above it would move the target.
    /// </summary>
    /// <summary>
    /// Somebody is typing, so the clock that decides when to narrow starts again from zero.
    ///
    /// <b>Restarted rather than left running, which is what makes it a burst rather than a
    /// metronome.</b> Seven characters typed quickly narrow the list once. Seven typed slowly
    /// narrow it seven times, and that is correct - each of those is a moment the person stopped
    /// and looked.
    /// </summary>
    private void QueryTyped(object sender, TextChangedEventArgs e)
    {
        _typing.Stop();
        _typing.Start();
    }

    /// <summary>
    /// Starts this program again with the rights this session does not have, and closes this one.
    ///
    /// <b>The order is the decision.</b> The new session is on its way before this one goes, so a
    /// person who answers no to the prompt still has the window they had - which is why the answer
    /// is read rather than assumed.
    ///
    /// <b>Nothing about starting a process is here</b>, and that is not tidiness: Elevation.cs is
    /// one of the two files in this product allowed to name one, held by a guard that reads the
    /// sources.
    /// </summary>
    /// <remarks>
    /// <b>Wired in the constructor since 2026-08-25, when the status row moved into its own
    /// file.</b> The markup over there cannot carry the click: pressing this starts a process, and
    /// <see cref="Elevation"/> is where that is allowed - so the handler stays here, where the model
    /// that reports its failure is.
    /// </remarks>
    private void RestartAsAdministrator(object sender, RoutedEventArgs e)
    {
        if (Elevation.Restart(HandOverNow().Encode()) is { } trouble)
        {
            _model.Says.CouldNotDo(trouble);
            return;
        }

        Close();
    }

    /// <summary>
    /// Hands the support page to a browser, or says in the status line why it did not and where
    /// the page is.
    ///
    /// <b>The window stays as it is either way</b> - nothing about the list, the query or a plan
    /// depends on a browser opening, so a failure here is a sentence rather than an interruption.
    /// It keeps answering while the shell is asked, too - ShellHandover says why that is not free.
    /// </summary>
    private async Task OpenSupportPage()
    {
        if (await ExternalLinks.OpenSupportAsync().ConfigureAwait(true) is { } trouble)
        {
            _model.Says.CouldNotDo(trouble);
        }
    }

    private void ListEngaged(object sender, RoutedEventArgs e) => _model.Interacting = true;

    private void ListReleased(object sender, RoutedEventArgs e) =>
        _model.Interacting = Entries.IsMouseOver || Entries.IsKeyboardFocusWithin;
}
