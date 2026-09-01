using System.Windows.Controls;
using System.Windows.Threading;

namespace Bws.Gui;

/// <summary>
/// Not reading the machine while somebody is moving the list.
///
/// <b>A file of its own because the class reached the length the ratchet allows</b> - 539 lines
/// against a ceiling of 536, and a third file over five hundred where two are allowed. That is the
/// same pressure that cut MainWindow.Columns.cs out of this class, and the seam is by THEME rather
/// than by line count: everything about holding off during a gesture is here, and nothing else is.
///
/// <b>WHY IT IS NOT `A10`, WHICH ALREADY HOLDS THE LIST.</b> That rule holds the ORDER of the rows
/// while a pointer is over them, and it holds it by working the rearrangement out and then refusing
/// to apply it - so every tick during a scroll recuts the scope, re-runs the query and compares up
/// to eight hundred rows in Holding, on the interface thread, in order to throw the answer away.
/// This skips the whole of that rather than paying for it and discarding it.
///
/// The cost of that is stated rather than hidden: cells stop being refreshed for the length of a
/// gesture and a quarter second after it. `A10` already accepts exactly this trade while somebody
/// is typing, for the same reason and in the same shape.
/// </summary>
public partial class MainWindow
{
    /// <summary>How long after the last scroll the list is left alone. Its argument is at _scrolling.</summary>
    private static readonly TimeSpan AfterScrolling = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Restarted by every scroll and read by <see cref="Tick"/>, which will not go to the machine
    /// while it runs. The argument for it is in the constructor, beside where it is built.
    /// </summary>
    private readonly DispatcherTimer _scrolling;

    /// <summary>
    /// Leave the machine alone while somebody is moving the list.
    ///
    /// <b>Its own method because the constructor reached the length the analyser allows</b>, which
    /// is the same pressure that cut MainWindow.Columns.cs out of this class - and the seam is by
    /// theme rather than by line count: everything about not reading mid-gesture is here.
    /// </summary>
    private DispatcherTimer HoldWhileScrolling()
    {
        // THE SAME SHAPE FOR SCROLLING, AND THE ARGUMENT IS NOT THE ONE ABOUT TYPING. `A10` already
        // holds the ORDER of the list while somebody is pointing at it - but it holds it by working
        // the rearrangement out and then refusing to apply it. So every tick during a scroll recuts
        // the scope, re-runs the query and compares up to eight hundred rows in `Holding`, on the
        // interface thread, in order to throw the answer away. Not reading the machine mid-gesture
        // skips the whole of that instead of paying for it and discarding it.
        //
        // 250 ms against typing's 400, because the two gestures end differently: keystrokes arrive
        // in a series with gaps of their own, and a wheel stops dead.
        var scrolling = new DispatcherTimer(DispatcherPriority.Input) { Interval = AfterScrolling };

        // It stops itself, for the reason spelled out above `_typing` - a DispatcherTimer with no
        // handler runs for ever, and this one is read to decide whether to go to the machine.
        scrolling.Tick += (_, _) => scrolling.Stop();

        // ScrollChanged rather than the wheel, because a wheel is one of four ways this list moves -
        // the bar, the keyboard and bringing a row into view are the others, and all of them arrive
        // here. Measured rather than assumed: ScrollViewer.ScrollChanged is a BUBBLING event, so one
        // handler on the grid sees the scroller inside it. ToolTipOpening, next door in CellTips, is
        // Direct and needed a different answer for exactly that reason.
        Entries.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler(ListScrolled),
            handledEventsToo: true);

        // A HANDLER ON ScrollBar.Scroll STOOD HERE FOR AN HOUR ON 2026-08-31 AND LEFT WITH THE
        // SETTING IT EXISTED FOR. Deferred scrolling silences ScrollChanged during a drag, so the
        // hold above needed a second road while that setting was on. The owner rejected deferring
        // by eye the same hour - a list somebody drags in order to look at it cannot stand still -
        // and a handler whose whole argument was a setting that is gone is machinery without a
        // reason. Backlog 253 carries both halves.
        return scrolling;
    }

    /// <summary>Whether the list is being left alone right now because somebody is moving it.</summary>
    internal bool HoldingForScroll => _scrolling.IsEnabled;

    /// <summary>
    /// The event plumbing, which is all this does. The judgement is in <see cref="Moved"/>.
    ///
    /// Split for the same reason CellTips splits its own: ScrollChangedEventArgs cannot be built by
    /// a test, so leaving the decision in here would leave it unguarded.
    /// </summary>
    private void ListScrolled(object sender, ScrollChangedEventArgs moved) =>
        Moved(moved.VerticalChange, moved.HorizontalChange);

    /// <summary>
    /// Somebody moved the list, so leave the machine alone until they stop.
    ///
    /// <b>A MOVE OF ZERO IS NOT A MOVE, AND THIS TEST IS THE WHOLE SAFETY OF THE FEATURE.</b>
    /// ScrollChanged also fires when the EXTENT changes under a list that has not moved - which is
    /// exactly what a refresh does when the machine gains or loses an entry. Without this, a refresh
    /// would start the timer that suppresses refreshes, and the list would stop updating itself for
    /// as long as it was on screen, silently, with every row still showing something true.
    /// </summary>
    internal void Moved(double vertical, double horizontal)
    {
        if (vertical == 0 && horizontal == 0)
        {
            return;
        }

        Hold();
    }

    /// <summary>
    /// Restarts the quarter second after which the machine may be read again.
    ///
    /// Stopped before started rather than started twice, because a running DispatcherTimer keeps its
    /// original deadline when Start is called on it again - so a second gesture would be measured
    /// from the first one and the hold would expire in the middle of it.
    /// </summary>
    private void Hold()
    {
        _scrolling.Stop();
        _scrolling.Start();
    }
}
