using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// How long one step of this plan may be watched for, and what tells the panel the time.
///
/// <b>Its own file since 2026-09-09, and the size ratchet is what asked - for the seventh time in
/// this window and the seventh time pointing at a real seam.</b> The rest of <see cref="Planned"/>
/// describes a plan: its title, its steps, its warnings, what stands in the way. This is the one
/// number on the sheet a person can CHANGE before pressing, which is a different kind of thing
/// entirely, and the clock that makes it checkable travels with it.
///
/// <b>Backlog 330, and the pre-release audit's RELEASE-008.</b> The ceiling was a constant nobody
/// could reach from the window, so "wait longer" was an answer the terminal had and this did not.
/// </summary>
public sealed partial class Planned
{
    /// <summary>
    /// What tells this panel the time, so a test does not have to wait for one.
    ///
    /// <b>The same seam <see cref="Elevated"/> takes and for the same reason</b>: the working
    /// answer is the one a person gets, and a test asserting how a waiting line reads after twelve
    /// seconds cannot be asked to spend twelve seconds proving it.
    /// </summary>
    internal IClock Clock
    {
        get => _clock;
        init => _clock = value;
    }

    private IClock _clock = new SystemClock();

    /// <summary>
    /// How long one step may be watched for before the run gives up on it.
    ///
    /// <b>UNTIL 2026-09-09 THIS WAS A CONSTANT AND THE WINDOW HAD NO WAY TO CHANGE IT</b> - backlog
    /// 330, and the pre-release audit's RELEASE-008. The terminal has had <c>--timeout</c> since it
    /// could stop anything, so "wait longer" was an answer available to one of two interfaces, and
    /// somebody in the window whose service needed ninety seconds had no move except to watch it be
    /// abandoned at sixty.
    ///
    /// <b>Whole seconds and at least one, which is the terminal's own rule rather than a new one.</b>
    /// <c>Arguments.Seconds</c> refuses anything else there, and two interfaces disagreeing about
    /// what a timeout may be is the kind of difference nobody discovers until a runbook is being
    /// translated from one to the other.
    ///
    /// <b>Not kept between runs, deliberately.</b> It is a decision about the plan in front of
    /// somebody - a service they know is slow - rather than a preference about how they like the
    /// window, and section H of the specification is where preferences live. A number that quietly
    /// stayed at ten from a fortnight ago would be worse than one that starts where the terminal
    /// starts every time.
    /// </summary>
    public int Waiting
    {
        get => _waiting;
        set
        {
            if (value < 1)
            {
                return;
            }

            Set(ref _waiting, value);
        }
    }

    private int _waiting = (int)Carrying.Ceiling.TotalSeconds;

    /// <summary>
    /// Whether this plan has anything to wait for, which decides whether the box above is offered.
    ///
    /// <b>A start type change has nothing</b>: it writes a setting and moves no service, so there
    /// is no state to arrive at and no reason to watch for one. The terminal says exactly this in
    /// its help - "neither --dependents nor --timeout applies here" - and a box offering to change
    /// a number that does nothing is the silence the belonging table exists to end, said the other
    /// way round.
    /// </summary>
    /// <b>AND NOT WHILE A RUN IS UNDER WAY, which the first build of this got wrong and a
    /// screenshot caught.</b> The ceiling is read at the press - see MainWindow.CarryOut - so a box
    /// still editable during the run is a control that accepts a number and does nothing with it.
    /// The strip it stands in says what the run is doing instead, which is the more useful sentence
    /// at that moment and the reason the two share a line.
    public bool Waits =>
        Showing
        && !Busy
        && _run is null
        && _plan is { } plan
        && plan.Plans.Any(one => one.Steps.Any(step => step.Operation != StepOperation.SetStartType));
}
