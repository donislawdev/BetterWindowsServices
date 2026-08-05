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
    private readonly DispatcherTimer _timer;

    /// <summary>Whether the last right click landed on a row. Read by the menu, set by the click.</summary>
    private bool _pointedAtARow;

    public MainWindow()
    {
        InitializeComponent();

        DataContext = _model;

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
            _model.CouldNotDo(refusal.Message);
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
