using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The three states a person sees while a plan is being carried out: nothing done, doing it, done.
///
/// <b>Its own file since 2026-09-03, and the size ratchet is what asked.</b> The seam is a subject
/// rather than a line count: everything left in Planned.cs describes a plan - its title, its steps,
/// its warnings, the command line that would ask for the same thing - and all of it is true before
/// anybody presses anything. What is here is the only part that changes while a machine is being
/// changed, and it is the part a person watches.
///
/// <b>The two fields live here with the methods that move them</b>, which is the rule
/// MainWindow.Carrying.cs states in as many words: a field read from two files is a field with two
/// stories. Everything that raises or lowers <see cref="Busy"/> is in this file, and that is what
/// makes the list of ways out of it readable in one place - it is exactly three, and until
/// 2026-09-03 an exception nobody predicted was a fourth way IN with no way out.
/// </summary>
public sealed partial class Planned
{
    private bool _busy;
    private string _progress = string.Empty;

    /// <summary>
    /// What the step on screen was announced as, without the clock on the end of it.
    ///
    /// <b>Kept apart from <see cref="Progress"/> because the clock is rebuilt every second and the
    /// sentence is not.</b> Appending to Progress itself would grow the line once a second, which
    /// is the kind of fault that looks like a memory leak and reads like a stutter.
    /// </summary>
    private string _step = string.Empty;

    /// <summary>When the step on screen was announced, or nothing when none is running.</summary>
    private DateTimeOffset? _stepBegan;

    /// <summary>Whether a run is happening right now.</summary>
    public bool Busy
    {
        get => _busy;
        private set => Set(ref _busy, value);
    }

    /// <summary>
    /// Which step is happening, while it happens.
    ///
    /// <b>Empty except during a run.</b> A person watching a stop that takes half a minute has
    /// nothing else to tell them the window is alive - and this window has measured half a minute on
    /// a single step, so the line is not decoration.
    /// </summary>
    public string Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    /// <summary>
    /// A run has begun.
    ///
    /// <b>Told rather than started here</b> - the seam described at <see cref="Plan"/>. What this
    /// owns is that the button goes quiet and the sentence changes before the first step, not after
    /// it: a button still live while the manager is being asked is a second ask waiting to happen.
    /// </summary>
    internal void Starting()
    {
        Busy = true;
        Progress = string.Empty;
        _step = string.Empty;
        _stepBegan = null;

        // The button goes quiet here, so what it says about itself has to move with it - otherwise
        // a person resting on a greyed button mid-run reads the sentence describing what it would
        // do, which is the state it has just left.
        Raise(nameof(CarryOutTip));
        Raise(nameof(CanCarryOut));
        Raise(nameof(Notice));
    }

    /// <summary>
    /// A step is about to be attempted, with its place across the whole selection.
    ///
    /// <b>The number is the step's place in the plan, never a count of attempts</b>, and it arrives
    /// that way from the runner for a reason written there: steps get skipped, and a counter of
    /// attempts calls the sixth step the third one while somebody is trying to work out where a run
    /// has got to.
    /// </summary>
    internal void Announce(PlanStep step, int number)
    {
        _step = Texts.Of(
            "gui.plan.progress",
            number,
            _plan?.Steps.Count() ?? 0,
            PlanWords.Word(step.Operation),
            step.ServiceName);

        // THE CLOCK RESTARTS PER STEP, NOT PER RUN, because the ceiling is per step. A run of six
        // steps may take six minutes without any one of them being near its limit, and a single
        // number counting up towards sixty through all of it would be a number that means nothing.
        _stepBegan = _clock.Now;

        Tick();
    }

    /// <summary>
    /// Rebuilds the line on screen from the step and how long it has been going.
    ///
    /// <b>Called from a timer while a run is under way</b> - see <see cref="Watching"/>, which owns
    /// the timer and nothing else. Split from it so that everything decided here can be checked
    /// without one.
    /// </summary>
    internal void Tick() =>
        Progress = _stepBegan is not { } began
            ? _step
            : PlanWords.StillWaiting(_step, _clock.Now - began, Waiting);

    /// <summary>
    /// A run has ended, whether it finished or was interrupted.
    ///
    /// <b>The plan stays on screen beside it, which is the whole of `ADR-11`'s promise arriving in a
    /// window:</b> what was going to happen and what did, side by side, without anybody having to
    /// remember the first half.
    /// </summary>
    internal void Finished(BulkRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        _run = run;
        Busy = false;
        Progress = string.Empty;
        _stepBegan = null;

        Raise(nameof(CanCarryOut));
        Raise(nameof(Notice));

        // THE TITLE CHANGES TENSE HERE AND NOWHERE ELSE, so it has to be said here. Without this
        // line the panel reports a finished run under a heading asking what would happen - which is
        // exactly the sentence this raise exists to retire, still on screen because nothing asked
        // the binding to look again.
        Raise(nameof(Heading));
        Raise(nameof(Failures));
        Raise(nameof(WayBack));
        RaiseTheCounts();
    }

    /// <summary>
    /// Nothing is running any more, and nobody knows what came of what was.
    ///
    /// <b>BACKLOG 298, AND UNTIL 2026-09-03 THERE WAS NO WAY BACK OUT OF <see cref="Busy"/> AT
    /// ALL EXCEPT FINISHING.</b> The window raises it before the first step and lowers it in
    /// <see cref="Finished"/>, so any exception between the two left the panel saying a run was
    /// under way for as long as it stayed open: the button dead, the tooltip explaining that it is
    /// dead BECAUSE a run is under way, and the progress line naming a step that stopped ages ago.
    /// Three sentences on screen, all false, and the failure itself in the status line under them.
    ///
    /// <b>How bad that was is smaller than it looks, and saying so is the point of measuring
    /// rather than reasoning.</b> <see cref="Show"/> and <see cref="Hide"/> both lower the flag,
    /// so Escape or a second plan already brought the panel back - what nothing did was tell the
    /// person that, or stop the panel lying in the meantime.
    ///
    /// <b>NOT Finished with an empty run, which was the other candidate and is the worse one.</b>
    /// That is what the one named refusal does, and there it is honest: a plan with problems never
    /// reached a machine. Here nothing knows whether it did. Recording a run would put "already
    /// done" under the button, which replaces one false sentence with another and takes away the
    /// only sensible next move.
    ///
    /// <b>So the button comes back live, and that is a decision rather than a side effect.</b> The
    /// plan is still on screen and no result was recorded, so pressing again is the ordinary way to
    /// find out where the machine got to - and a second run is not a second machine change: the
    /// runner reads each entry before asking for anything and reports one already where it was
    /// asked to be as a skipped step. What it must not do is look untouched, which is why the
    /// failure reaches the status line through the window's own net rather than being swallowed
    /// here.
    /// </summary>
    internal void NoLongerRunning()
    {
        if (!Busy)
        {
            // Every ordinary path has already been through Finished, so this is the common case
            // rather than the exception - and a raise nobody needs redraws a panel for nothing.
            return;
        }

        Busy = false;
        Progress = string.Empty;
        _stepBegan = null;

        Raise(nameof(CanCarryOut));
        Raise(nameof(CarryOutTip));
        Raise(nameof(Notice));
    }
}
