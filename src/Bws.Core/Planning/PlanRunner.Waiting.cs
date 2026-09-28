namespace Bws.Core.Planning;

/// <summary>
/// The half of <see cref="PlanRunner"/> that watches ONE step on its way to where it was sent - how
/// often the entry is asked, and when it is given up on.
///
/// <b>Its own file since 2026-09-29</b>, when the pause between two questions stopped being one
/// number and became a ramp with a rule about promises beside it. The rest of the class walks the
/// plan and never waits for anything, so the two halves change for different reasons.
/// </summary>
public sealed partial class PlanRunner
{
    /// <summary>
    /// The longest pause between two questions about where the entry has got to.
    ///
    /// Our choice, not the system's, and it carries no correctness: the deadline comes from
    /// the entry's own wait hint, this only decides how soon we notice. Short because
    /// somebody is watching a terminal, and asking is cheap - five calls to the manager (open
    /// it, open the entry, close the manager, query, close the entry - WindowsScmControl.Read),
    /// 0.19-0.23 ms the lot, measured unelevated on 2026-09-28 with tools/plan-probe/read-cost.ps1.
    ///
    /// <b>Until 2026-09-29 this was the ONLY pause, and it was most of every wait.</b> Measured on
    /// the throwaway machine on 2026-09-28: Spooler and W32Time changed state in 2-50 ms and each
    /// step reported 260-289 ms, because the first question goes straight after the request, is
    /// almost always too early, and the next one came a whole cadence later. Backlog 466, S-6 of
    /// the external performance report. The pauses now start at <see cref="FirstLook"/> and
    /// double up to this.
    /// </summary>
    internal static readonly TimeSpan Cadence = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The first pause after the question that goes straight after the request. Each pause after
    /// it is twice the one before, until <see cref="Cadence"/>.
    ///
    /// <b>Why doubling, rather than a list of pauses:</b> it keeps one promise that a list would
    /// have to be checked for. The looks land at 0, 10, 30, 70, 150 and 310 ms, so an entry that
    /// arrives inside that stretch is never left waiting longer than it took to get there, plus
    /// this pause. A step that took 40 ms is reported at 70, not at 250. After that the pauses stay
    /// at the cadence, so a wait of any length asks the manager at most four more times than it
    /// did.
    ///
    /// <b>Windows sleeps in whole ticks of its clock, about 15.6 ms by default</b>, so on a real
    /// machine the first pauses come out nearer 16, 31 and 47 ms. That only moves the looks a
    /// little later and changes none of the above.
    /// </summary>
    internal static readonly TimeSpan FirstLook = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Watches an entry on its way, and decides when it has stopped going anywhere.
    ///
    /// Two deadlines, and the entry's own comes first. Win32 documents the promise: before
    /// its wait hint elapses a service will either raise its check point or change state.
    /// Keeping that promise buys it a fresh wait hint, so an entry that genuinely needs a
    /// minute gets one. Breaking it is the documented signal that something has gone wrong.
    /// The cap is ours and only stops a plan hanging a terminal on an entry that reports
    /// progress it never finishes.
    /// </summary>
    private StepResult WaitFor(PlanStep step, EntryStatus target, TimeSpan timeout, TimeSpan started)
    {
        var giveUpAt = started + timeout;
        var pause = FirstLook;
        var status = EntryStatus.Unknown;

        // Carried alongside the status rather than read again at the end, because the point of it
        // is the step that gives up: by then the entry is exactly where nobody can act on it, and
        // the last process seen holding it is the only handle a person has on what to do next.
        var processId = Reading<int>.NotRead();
        uint? checkPoint = null;
        TimeSpan? promisedBy = null;

        while (true)
        {
            var answer = control.Read(step.ServiceName);

            if (!answer.Worked)
            {
                return Refused(step, answer, status, processId, started);
            }

            var progress = answer.Progress!.Value;
            var moved = checkPoint is null || progress.CheckPoint > checkPoint || progress.Status != status;

            status = progress.Status;
            processId = Holding(answer);

            if (status == target)
            {
                return Result(step, StepOutcome.Succeeded, status, processId, Elapsed(started));
            }

            var now = clock.Elapsed;

            if (moved)
            {
                checkPoint = progress.CheckPoint;

                // A wait hint of zero is what an entry reports when it has nothing pending,
                // so it is not a promise to hold anybody to. Then only our own cap applies.
                promisedBy = progress.WaitHint > TimeSpan.Zero ? now + Honoured(progress.WaitHint) : null;
            }

            if (now >= giveUpAt || (promisedBy is not null && now >= promisedBy))
            {
                return Result(step, StepOutcome.TimedOut, status, processId, Elapsed(started));
            }

            clock.Wait(pause);
            pause = pause * 2 < Cadence ? pause * 2 : Cadence;
        }
    }

    /// <summary>
    /// How long an entry's promise is held open: its wait hint, rounded UP to a whole
    /// <see cref="Cadence"/>.
    ///
    /// <b>This is the line that keeps looking sooner from turning into giving up sooner</b>, and it
    /// is new with the doubling pauses, 2026-09-29. Until then the entry was only looked at once a
    /// cadence, so a promise was in effect honoured to the next whole cadence - an entry that said
    /// 50 ms had 250 to keep its word, and one that said 300 had 500. Services exist that promise
    /// less than they need, and to the person reading it a step reported as timed out that then
    /// finished is a failure on a machine that is fine. The costly mistake is that one, and waiting
    /// a fraction of a second longer on an entry that really has hung is the cheap one. So the
    /// deadline stays exactly where it always was, and only how often it is checked changed.
    ///
    /// <b>Not applied to our own cap</b>, which is not the entry's promise. It is still checked at
    /// the first look after it runs out, as it always was.
    ///
    /// Whole ticks rather than a division of two spans, because a hint that is already a whole
    /// cadence has to come back unchanged, and integer arithmetic says so without a rounding
    /// question.
    /// </summary>
    private static TimeSpan Honoured(TimeSpan waitHint) =>
        TimeSpan.FromTicks((waitHint.Ticks + Cadence.Ticks - 1) / Cadence.Ticks * Cadence.Ticks);
}
