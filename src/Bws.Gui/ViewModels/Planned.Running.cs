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
/// <b>The flag lives here with the methods that move it</b>, which is the rule
/// MainWindow.Carrying.cs states in as many words: a field read from two files is a field with two
/// stories. Everything that raises or lowers <see cref="Busy"/> is in this file, and that is what
/// makes the list of ways out of it readable in one place - it is exactly three, and until
/// 2026-09-03 an exception nobody predicted was a fourth way IN with no way out.
///
/// <b>What happens DURING a run moved to <see cref="ViewModels.Underway"/> on 2026-09-30</b> - the
/// progress line and, new that day, the two ways of asking a run to stop (backlog 497). This type
/// stood on the ceiling of methods, and the three methods below are where it hands over: each of
/// them begins or ends the run over there.
/// </summary>
public sealed partial class Planned
{
    private bool _busy;
    private Underway? _underway;

    /// <summary>Whether a run is happening right now.</summary>
    public bool Busy
    {
        get => _busy;
        private set
        {
            if (Set(ref _busy, value))
            {
                Raise(nameof(CanClose));
            }
        }
    }

    /// <summary>
    /// Whether the sheet may be put away - never while a run it carries is going on.
    ///
    /// <b>THE CLOSE MARK USED TO BE LIVE THROUGH A RUN, AND SO DID ESCAPE - G-1 OF THE EXTERNAL
    /// STABILITY REPORT, 2026-09-29.</b> Putting the sheet away dropped the plan and lowered
    /// <see cref="Busy"/> while the run went on underneath, so the button to carry out came back live
    /// and a second run could start beside the first. The window's close guard and Interrupt then
    /// followed whichever run had started last, and the first one's report landed under the second
    /// one's plan. Interrupt is the way out of a run, and it is on the sheet for as long as one lasts.
    /// </summary>
    public bool CanClose => !Busy;

    /// <summary>
    /// The run as it goes: the step on screen and the two ways of asking it to stop.
    ///
    /// <b>Made on first use rather than in an initialiser</b>, because it asks this sheet's clock, and
    /// <see cref="Clock"/> is set by an initialiser that runs after any field initialiser would. The
    /// clock is handed over as a question rather than a value for the same reason.
    /// </summary>
    public Underway Underway => _underway ??= new Underway(() => _clock, () => Raise(nameof(Notice)));

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

        // THE LIMIT IS READ HERE, AT THE PRESS, for the line that quotes it - the box it comes from
        // leaves the sheet in the line below, so nothing can change it for the rest of the run.
        Underway.Begin(_stepsInPlan, Waiting);

        // The button goes quiet here, so what it says about itself has to move with it - otherwise
        // a person resting on a greyed button mid-run reads the sentence describing what it would
        // do, which is the state it has just left.
        Raise(nameof(CarryOutTip));
        Raise(nameof(CanCarryOut));
        Raise(nameof(Notice));

        // AND THE BOX OF SECONDS GOES WITH IT, AND UNTIL 2026-09-15 NOTHING SAID SO. Waits reads
        // Busy, but Busy announces only itself, so the binding that hides the box during a run was
        // never told to look again - the value was right and the screen was not, which is `docs/08`
        // position 19 in this panel. The guard for it asks about the NOTIFICATION, not the value.
        Raise(nameof(Waits));
        RaiseTheProblem();

        // AND THE OFFERS UNDER A SENTENCE, THE SAME FAULT A THIRD TIME, found by review of PR #17.
        // Both lists offer only while nothing runs, but they were told to look again only when the
        // run ended - so "Also stop it" stayed live for the whole run, and pressing it rebuilt the
        // plan under a run still going. MainWindow.TakeTheOffer refuses on the same answer.
        Raise(nameof(Warnings));
        Raise(nameof(Problems));
    }

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
        Underway.End();

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
    /// rather than reasoning.</b> <see cref="Show"/> and <see cref="Hide"/> both lowered the flag,
    /// so Escape or a second plan already brought the panel back - what nothing did was tell the
    /// person that, or stop the panel lying in the meantime. <b>Since 2026-09-29 neither of them
    /// may touch a sheet whose run is going</b> (G-1, at <see cref="CanClose"/>), so this method is
    /// now the ONLY way back out of a run that ended without a result, and the window calls it from
    /// a finally for exactly that reason.
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
    ///
    /// <b>And the second way of stopping goes with it, since 2026-09-30</b> - a button offering to
    /// abandon a run that is no longer there would be a way to abandon nothing.
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
        Underway.End();

        Raise(nameof(CanCarryOut));
        Raise(nameof(CarryOutTip));
        Raise(nameof(Notice));

        // The sheet is a question again rather than a record, so the offers Starting took away
        // come back with the button.
        Raise(nameof(Warnings));
        Raise(nameof(Problems));
    }
}
