using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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

    private readonly MainViewModel _model = new();

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

    /// <summary>Whether the last right click landed on a row. Read by the menu, set by the click.</summary>
    private bool _pointedAtARow;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = _model;

        // BEFORE ANY ROW EXISTS, and the grid has no columns at all until this line runs. There
        // are seventeen of them and eleven are off, which is a list somebody chooses from rather
        // than a list written out - see ListColumns.
        ListColumns.Fill(Entries, _columns);

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

        // THE EXAMPLES, AND THE CLICK IS TAKEN ON THE MENU RATHER THAN ON EACH ITEM. A theme file
        // has no code-behind class, so an EventSetter in the item style is not available - and one
        // handler over the whole list is the better shape anyway: adding a seventh example needs no
        // wiring at all.
        if (ExamplesButton.ContextMenu is { } examples)
        {
            examples.ItemsSource = _model.Examples;

            examples.AddHandler(MenuItem.ClickEvent, new RoutedEventHandler((_, clicked) =>
            {
                if ((clicked.OriginalSource as MenuItem)?.DataContext is ViewModels.QueryExample example)
                {
                    // Into the box rather than into the filter, so what happens next is a query the
                    // person can read, edit and learn from - the same promise a chip makes.
                    _model.QueryText = example.Query;
                    QueryBox.Focus();
                    QueryBox.CaretIndex = QueryBox.Text.Length;
                }
            }));
        }

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
            e.Handled = await Act(Shortcuts.For(e.Key, e.KeyboardDevice.Modifiers)).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Tells the window manager that the bar above this window is a dark one.
    ///
    /// <b>Here rather than in the constructor, and that is the whole of why the first attempt at
    /// this did nothing.</b> The attribute is set against a window HANDLE, and a WPF window has no
    /// handle until its source is initialised - so the same call one step earlier is a call about
    /// a window that does not exist yet, and it fails in the quietest way there is: by succeeding.
    ///
    /// <b>Why it has to be asked for at all.</b> Merging the library's dark dictionaries styles
    /// everything INSIDE the window and says nothing about the frame around it, which belongs to
    /// Windows. Measured on screen 2026-08-10: the bar came out #4C4A48 against content at
    /// #202020 two rows below it, on a machine whose system theme is dark. Backlog 148.
    ///
    /// <b>Tried first and rejected with a measurement:</b> `ApplicationThemeManager.Apply(this)`
    /// from the control library, which changed the bar by nothing at all - #4C4A48 before and
    /// after. The library's only other lever is <c>WindowBackdrop</c>, and that is the door Mica
    /// and Acrylic come through, which this product refuses because a translucent background turns
    /// ClearType off across all eight hundred rows.
    ///
    /// A failure here is deliberately ignored. A pale title bar is a blemish, and taking the
    /// window down over one would be a far worse answer than the blemish.
    /// </summary>
    protected override unsafe void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        var window = new Windows.Win32.Foundation.HWND(handle);
        var dark = 1;

        _ = Windows.Win32.PInvoke.DwmSetWindowAttribute(
            window,
            Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
            &dark,
            sizeof(int));

        // AND THE COLOUR ITSELF, BECAUSE THE DARK MODE FLAG DOES NOT DELIVER WHAT THIS FILE SAID IT
        // DID. Measured 2026-08-12 on the release build, both states: the caption is #4C4A48 over
        // content at #202020, active and inactive alike. The note above claimed that flag closed
        // backlog 148 - it darkens the caption from the light default and stops well short of the
        // window's own colour, so the window still reads as two programs stacked.
        //
        // DWMWA_CAPTION_COLOR is Windows 11 only and a failure here is ignored for the same reason
        // as above: a pale title bar is a blemish, and taking the window down over one would be a
        // far worse answer than the blemish.
        //
        // THE COLOUR COMES FROM THE WINDOW'S OWN BACKGROUND RATHER THAN FROM A NUMBER HERE, which
        // is `ADR-23` reaching the one surface it could not otherwise reach: the frame belongs to
        // Windows, so it cannot be styled, but it can be handed the brush the theme already chose.
        // A literal here would be a second copy of the background, and the two would drift the
        // first time anybody changed the theme.
        if (Background is System.Windows.Media.SolidColorBrush brush)
        {
            // COLORREF is 0x00BBGGRR, which is the opposite order from every other colour in this
            // product - and getting it backwards produces a plausible wrong colour rather than an
            // error, so it is written out rather than packed in one expression.
            var colour = (uint)(brush.Color.R | (brush.Color.G << 8) | (brush.Color.B << 16));

            _ = Windows.Win32.PInvoke.DwmSetWindowAttribute(
                window,
                Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR,
                &colour,
                sizeof(uint));
        }
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

            case Shortcut.ClearQuery:
                return _model.ClearQuery();

            default:
                return false;
        }
    }

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

        if (row is not null)
        {
            row.IsSelected = true;
        }
    }

    /// <summary>
    /// A right click that landed on no row gets no menu.
    ///
    /// The alternative is the fault the handler above exists to prevent, arriving by the back
    /// door: click the header or the empty space under the last row, and the menu offers to copy
    /// whatever was selected some time earlier.
    ///
    /// <b>The keyboard route is left alone</b>, and it is told apart by the cursor position being
    /// negative, which is how WPF reports a menu opened from the menu key. There the focused row
    /// IS the selected row, so there is nothing to point at.
    /// </summary>
    private void OfferTheMenuOnlyOnARow(object sender, ContextMenuEventArgs e)
    {
        if (e.CursorLeft >= 0 && !_pointedAtARow)
        {
            e.Handled = true;
        }
    }

    private void ChooseColumns(object sender, RoutedEventArgs e) => OpenColumns();

    /// <summary>
    /// Opens the list of columns under the button that asks for it.
    ///
    /// <b>A context menu opened by a left click, which is unusual and is the point.</b> The menu
    /// is the surface - already themed, already used by this window - and the button is the
    /// discoverability, because a column chooser hidden behind a right click on a heading is one
    /// nobody finds. Placed under the button rather than at the pointer, so it reads as belonging
    /// to it rather than as a menu about whatever was clicked.
    ///
    /// <b>Apart from the handler for the same reason <see cref="Act"/> is</b> - a handler the
    /// framework calls is reachable only by clicking, and what this does is worth asserting: a
    /// button that opens nothing looks exactly like a feature that is not there.
    /// </summary>
    internal bool OpenColumns()
    {
        if (ColumnsButton.ContextMenu is not { } menu)
        {
            return false;
        }

        menu.PlacementTarget = ColumnsButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;

        return true;
    }

    private void ShowExamples(object sender, RoutedEventArgs e) => OpenExamples();

    /// <summary>
    /// Opens the list of example queries under the button that asks for it.
    ///
    /// The same arrangement as <see cref="OpenColumns"/>, and apart from its handler for the same
    /// reason: a button that opens nothing looks exactly like a feature that is not there, and a
    /// handler the framework calls can only be reached by clicking.
    /// </summary>
    internal bool OpenExamples()
    {
        if (ExamplesButton.ContextMenu is not { } menu)
        {
            return false;
        }

        menu.PlacementTarget = ExamplesButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;

        return true;
    }

    private void CopyServiceName(object sender, RoutedEventArgs e) => Copy(model => model.SelectedServiceName);

    private void CopyDisplayName(object sender, RoutedEventArgs e) => Copy(model => model.SelectedDisplayName);

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
    /// </summary>
    private void Copy(Func<MainViewModel, string?> field)
    {
        _model.Selected = Entries.SelectedItem as EntryRow;

        if (field(_model) is not string text)
        {
            return;
        }

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
