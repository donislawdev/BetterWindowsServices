using System.Windows;
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
    /// F5, because admins trust it more than they trust an automatic - `A10` says so in as
    /// many words. It reads everything, including the configuration the per-second reading
    /// deliberately does not watch.
    /// </summary>
    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key != Key.F5)
        {
            return;
        }

        e.Handled = true;

        await _model.LoadAsync().ConfigureAwait(true);
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
