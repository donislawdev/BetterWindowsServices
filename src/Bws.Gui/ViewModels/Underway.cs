using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// A run as somebody watches it while it goes: which step it is on, and the two ways of asking it
/// to stop.
///
/// <b>ITS OWN TYPE SINCE 2026-09-30, AND THE SHAPE GUARD IS WHAT ASKED.</b> <see cref="Planned"/>
/// stood on the ceiling of methods a type may have, 78 of 78, on the day the window learned the
/// second way of stopping a run (backlog 497), so nothing could be added to it. The seam is a
/// subject rather than a count: everything left in Planned is true of a plan before, during and
/// after a run, and everything here exists only while one is going. The progress line came across
/// with the two asks, which is what took Planned from 78 to 75.
///
/// <b>THE TWO LEVELS ARE THE COMMAND LINE'S TWO PRESSES OF Ctrl+C</b> (Execution.Carry in Bws.Cli).
/// The first stops going forward and still puts back what the run took down. The second stops
/// watching and puts nothing back - the expensive ask, and the reason it exists at all is an entry
/// that reports progress forever: since the step limit counts time without progress (stability
/// report W-1), such an entry is watched forever, and until this type the window had no way out of
/// that short of the task manager.
///
/// <b>THE RULE THAT GUARDS THE SECOND LEVEL LIVES HERE, NOT IN THE WINDOW, and it has two halves.</b>
/// The second level is offered only after the first was asked, and only once <see cref="ArmsAfter"/>
/// has passed with the run still going. The window holds the tokens - its test seam hands it a run
/// with the sheet idle, and the close guard has to reach the first token whatever this says - but it
/// cancels the second one only when <see cref="Abandon"/> agrees. The button follows the same answer,
/// so the door and the button cannot disagree.
/// </summary>
public sealed class Underway : Observable
{
    /// <summary>
    /// How long the first level has to have been asked, with the run still going, before the second
    /// is offered.
    ///
    /// <b>A judgement rather than a measurement, and said so in `docs/PROJEKT-PRZERWANIE-I-ZALEZNI-20260930.md`
    /// where the owner chose it.</b> A double click cannot reach the second button - it stands in
    /// another rectangle and the first one greys - and a second key press cannot either, because
    /// greying the focused button hands the keyboard to the window rather than to the next control
    /// (measured in pwsh on 2026-09-30). What the wait adds is that the button giving up on putting
    /// things back does not appear at all in the ordinary case, where the step in flight ends a moment
    /// after the press, and does appear where a run really hangs. Three seconds is more than Windows'
    /// default double-click time of 500 ms (learn.microsoft.com, SetDoubleClickTime) and nothing
    /// against a step watched for minutes.
    /// </summary>
    internal static readonly TimeSpan ArmsAfter = TimeSpan.FromSeconds(3);

    private readonly Func<IClock> _clock;
    private readonly Action _noticeMoved;

    private bool _running;
    private bool _interrupted;
    private bool _abandoned;
    private bool _armed;
    private bool _closing;
    private TimeSpan _interruptedAt;
    private int _steps;
    private int _waiting;
    private string _progress = string.Empty;

    /// <summary>
    /// What the step on screen was announced as, without the clock on the end of it - kept apart from
    /// <see cref="Progress"/> because the clock is rebuilt every second and the sentence is not.
    /// Appending to Progress itself would grow the line once a second.
    /// </summary>
    private string _step = string.Empty;

    /// <summary>When the step on screen was announced, or nothing when none is running.</summary>
    private DateTimeOffset? _stepBegan;

    /// <param name="clock">
    /// Asked every time rather than kept, because <see cref="Planned.Clock"/> is set by an
    /// initialiser that may run after this is made.
    /// </param>
    /// <param name="noticeMoved">
    /// Told when the sentence at the top of the sheet has something new to say - that sentence
    /// belongs to Planned, which says the plan's state before and after a run as well as during one.
    /// </param>
    internal Underway(Func<IClock> clock, Action noticeMoved)
    {
        _clock = clock;
        _noticeMoved = noticeMoved;
    }

    /// <summary>
    /// Which step is happening, while it happens. Empty except during a run.
    ///
    /// <b>A person watching a stop that takes half a minute has nothing else to tell them the window
    /// is alive</b> - and this window has measured half a minute on a single step.
    /// </summary>
    public string Progress
    {
        get => _progress;
        private set
        {
            if (Set(ref _progress, value))
            {
                Raise(nameof(HasProgress));
            }
        }
    }

    /// <summary>
    /// Whether there is a step to report at all - what puts the line on the sheet and takes it off.
    ///
    /// <b>The line goes when it has nothing to say.</b> Until 2026-09-16 it stood on top of the box of
    /// seconds, empty, and caught every click meant for the box - the owner could not type a number
    /// into it. It stands in the row under the buttons since 2026-09-30, but a line that goes when it
    /// is empty is the rule this sheet keeps everywhere (backlog 203).
    /// </summary>
    public bool HasProgress => _progress.Length > 0;

    /// <summary>Whether the first level can still be asked - a run is going and nobody has asked it yet.</summary>
    public bool CanInterrupt => _running && !_interrupted;

    /// <summary>
    /// Whether the second button is on the sheet at all - from the moment it is offered until the run
    /// ends, grey once pressed, so nothing appears or disappears under a hand twice.
    /// </summary>
    public bool OffersAbandoning => _running && _interrupted && (_armed || _abandoned);

    /// <summary>Whether the second level can be asked now. <see cref="Abandon"/> asks the same question.</summary>
    public bool CanAbandon => _running && _interrupted && _armed && !_abandoned;

    /// <summary>
    /// What the first button says about itself. Once it has been pressed it is grey, and a grey
    /// button saying what it WOULD do describes the state it has just left.
    /// </summary>
    public string InterruptTip => _interrupted
        ? Texts.Of("gui.plan.interrupt.asked")
        : Texts.Of("gui.plan.interrupt.hint");

    /// <summary>
    /// The sentence at the top of the sheet while a run is going - which of the two asks it is under,
    /// and whether the window is waiting to close.
    /// </summary>
    internal string Notice
    {
        get
        {
            var state = _abandoned ? Texts.Of("gui.plan.notice.abandoning")
                : _interrupted ? Texts.Of("gui.plan.notice.interrupting")
                : Texts.Of("gui.plan.notice.running");

            return _closing
                ? Texts.Of("gui.plan.notice.because", state, Texts.Of("gui.plan.notice.closing"))
                : state;
        }
    }

    /// <summary>
    /// A run has begun: nothing asked of it yet, and nothing announced.
    /// </summary>
    /// <param name="steps">How many steps the plan has, for "step 2 of 5".</param>
    /// <param name="waiting">
    /// The limit the run was given, read at the press. The box it comes from is off the sheet for the
    /// whole run, so it cannot change under the line that quotes it.
    /// </param>
    internal void Begin(int steps, int waiting)
    {
        _steps = steps;
        _waiting = waiting;
        Reset(running: true);
    }

    /// <summary>
    /// The run is over, whichever way it ended - and every way out of a run comes here: Finished,
    /// NoLongerRunning, and a sheet shown or put away. A second button left standing on a sheet with
    /// no run under it would be a way to abandon nothing.
    /// </summary>
    internal void End() => Reset(running: false);

    /// <summary>
    /// A step is about to be attempted, with its place across the whole selection - the step's place
    /// in the plan, never a count of attempts, because steps get skipped.
    /// </summary>
    internal void Announce(PlanStep step, int number)
    {
        _step = Texts.Of("gui.plan.progress", number, _steps, PlanWords.Word(step.Operation), step.ServiceName);

        // THE CLOCK RESTARTS PER STEP, NOT PER RUN, because the limit is per step. A run of six steps
        // may take six minutes without any one of them being near its limit.
        _stepBegan = _clock().Now;

        Tick();
    }

    /// <summary>
    /// Rebuilds the line from the step and how long it has been going, and offers the second level
    /// once it is due. Called once a second by the window's timer while a run is going.
    /// </summary>
    internal void Tick()
    {
        Progress = _stepBegan is not { } began
            ? _step
            : PlanWords.StillWaiting(_step, _clock().Now - began, _waiting);

        // ELAPSED RATHER THAN NOW, because Now is the wall clock and jumps when a virtual machine
        // resumes (IClock.Elapsed, backlog 299). Across such a jump a wait measured on Now would
        // arm at once, or never.
        if (_running && _interrupted && !_armed && _clock().Elapsed - _interruptedAt >= ArmsAfter)
        {
            _armed = true;
            RaiseTheAsks();
        }
    }

    /// <summary>
    /// The first level was asked - the button, or somebody reaching for the corner of the window.
    ///
    /// <b>Only the first ask counts.</b> A second press - a double click, a close after an interrupt -
    /// changes nothing, and in particular does not start the wait for the second level again.
    /// </summary>
    /// <param name="closing">Whether the window asked, and will close once the run has ended.</param>
    internal void Interrupt(bool closing)
    {
        if (!_running)
        {
            return;
        }

        _closing |= closing;

        if (!_interrupted)
        {
            _interrupted = true;
            _interruptedAt = _clock().Elapsed;
        }

        RaiseTheAsks();
        _noticeMoved();
    }

    /// <summary>
    /// The second level was asked. True when it may be - and only then does the window cancel the
    /// second token, so a press arriving early, twice, or with no run under it does nothing.
    /// </summary>
    internal bool Abandon()
    {
        if (!CanAbandon)
        {
            return false;
        }

        _abandoned = true;
        RaiseTheAsks();
        _noticeMoved();

        return true;
    }

    /// <summary>
    /// What the report says about how a finished run was stopped, or nothing when it was not.
    ///
    /// <b>READ OFF THE STEPS, NEVER OFF THIS TYPE'S OWN STATE.</b> The report is evidence and outlives
    /// the run, and the run already writes down which of the two asks it met: one flag for both on
    /// the run (PlanRunner.Run says why), and a step skipped as cancelled for each thing either ask
    /// held back. A step going forward held back is the first level. A step putting something back
    /// held back is the second - the first level never holds those.
    ///
    /// <b>An interrupt that arrived after the last step held nothing back</b>, and says nothing here -
    /// the run's own flag would say "interrupted" about a run that did everything it was asked.
    /// </summary>
    /// <param name="arrived">How many of the entries ended where they were asked to be.</param>
    internal static string Afterwards(BulkRun run, int arrived)
    {
        ArgumentNullException.ThrowIfNull(run);

        var held = run.Runs
            .SelectMany(one => one.Results)
            .Where(result => result.SkippedBecause == SkipReason.Cancelled)
            .ToList();

        if (held.Count == 0)
        {
            return string.Empty;
        }

        var nothingPutBack = held.Any(result => result.Step.Reason == StepReason.Restore);

        return (nothingPutBack, run.Runs.Count == 1) switch
        {
            (true, true) => Texts.Of("gui.plan.notice.leftAsItWas.one"),
            (true, false) => Texts.Of("gui.plan.notice.leftAsItWas.many", arrived, run.Runs.Count),
            (false, true) => Texts.Of("gui.plan.notice.interrupted.one"),
            (false, false) => Texts.Of("gui.plan.notice.interrupted.many", arrived, run.Runs.Count)
        };
    }

    private void Reset(bool running)
    {
        _running = running;
        _interrupted = false;
        _abandoned = false;
        _armed = false;
        _closing = false;
        _interruptedAt = TimeSpan.Zero;
        _step = string.Empty;
        _stepBegan = null;
        Progress = string.Empty;

        RaiseTheAsks();
        _noticeMoved();
    }

    private void RaiseTheAsks()
    {
        Raise(nameof(CanInterrupt));
        Raise(nameof(OffersAbandoning));
        Raise(nameof(CanAbandon));
        Raise(nameof(InterruptTip));
    }
}
