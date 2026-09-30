using System.Globalization;

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
    /// What a step does before it asks for anything, when the entry is already on its way somewhere -
    /// or null, when it should go on and ask.
    ///
    /// <b>New on 2026-09-30, on the owner's decision of 2026-09-29</b> (stability report W-1 and W-7).
    /// Until then a step asked whenever the entry was not where it wanted it, and the manager refuses
    /// both halves of this: a stop to an entry already stopping, and a start to one still stopping.
    /// The second is how a restart used to end. The stop gave up on a service that was still honestly
    /// stopping, the start that puts it back was refused, and the service finished stopping after the
    /// run had left and stayed stopped.
    ///
    /// <b>Already heading where the step wants it:</b> nothing is asked, the entry is watched, and if it
    /// arrives the step succeeded - which is what the outcome has always meant, the entry reaching the
    /// state the step asked for, not this tool sending it there. <b>Heading the other way:</b> it is
    /// watched until it leaves that state, and the step then goes on as though it had just found it
    /// there. One that never leaves it is a failure with nothing sent, in our own words, because the
    /// way back must not count as a move something this tool never asked for.
    /// </summary>
    private StepResult? Settle(
        PlanStep step, EntryStatus target, ControlAnswer before, TimeSpan timeout, TimeSpan started, Halt halt)
    {
        var status = before.Progress!.Value.Status;
        bool Unasked() => halt.Unasked(step.Reason);

        if (status != Leaving(target))
        {
            return OnItsWay(target, status) ? WaitFor(step, target, timeout, started, Unasked, asked: false) : null;
        }

        var watched = Watch(step.ServiceName, now => now != status, timeout, Unasked);
        var seen = watched.Seen ?? watched.Last;

        return watched.End switch
        {
            Settled.Over when Where(seen) == target =>
                Result(step, StepOutcome.Succeeded, target, Holding(seen), Elapsed(started)),

            Settled.Over when OnItsWay(target, Where(seen)) =>
                WaitFor(step, target, timeout, started, Unasked, asked: false),

            Settled.Over => null,
            Settled.Unreadable => Refused(step, watched.Last, Where(seen), Holding(seen), started),
            Settled.Halted => Skipped(step, SkipReason.Cancelled, Where(seen), Holding(seen), Elapsed(started)),
            _ => Refused(step, ControlAnswer.Refused(0, NeverAsked(target)), Where(seen), Holding(seen), started)
        };
    }

    /// <summary>
    /// Watches an entry on its way to where the step wants it, and says what came of it.
    ///
    /// <b>A start that falls back to Stopped is over, and it is a failure with the service's own
    /// number</b> - since 2026-09-30, stability report W-7. The manager marks an entry start pending
    /// before the start call returns, so Stopped after that is the service having stopped, and the
    /// exit code it left is the only honest answer. Until that day such a start was watched for the
    /// whole limit and reported as having run out of time. A stop that goes back to Running is NOT
    /// treated the same way: a service may take the control and only report stop pending a moment
    /// later, so Running straight after a stop is ordinary.
    /// </summary>
    /// <param name="halted">When the watching ends because somebody asked the run to stop.</param>
    /// <param name="asked">
    /// Whether the manager was asked anything. An abandoned watch after a request reports that the
    /// watching ended - the entry may still arrive, which is what TimedOut says - and one before any
    /// request is a step never attempted.
    /// </param>
    private StepResult WaitFor(
        PlanStep step, EntryStatus target, TimeSpan timeout, TimeSpan started, Func<bool> halted, bool asked)
    {
        var watched = Watch(step.ServiceName, now => now == target || FellBack(target, now), timeout, halted);

        // The last reading that worked rather than the one that ended it, because the point of both
        // is the step that gives up: by then the entry is where nobody can act on it, and the last
        // process seen holding it is the only handle a person has on what to do next.
        var seen = watched.Seen ?? watched.Last;

        return watched.End switch
        {
            Settled.Unreadable => Refused(step, watched.Last, Where(seen), Holding(seen), started),
            Settled.Over when Where(seen) == target =>
                Result(step, StepOutcome.Succeeded, target, Holding(seen), Elapsed(started)),

            Settled.Over => StoppedAgain(step, seen, started),
            Settled.Halted when !asked =>
                Skipped(step, SkipReason.Cancelled, Where(seen), Holding(seen), Elapsed(started)),

            _ => Result(step, StepOutcome.TimedOut, Where(seen), Holding(seen), Elapsed(started))
        };
    }

    /// <summary>
    /// Asks where an entry is until it reaches a state <paramref name="over"/> accepts, stops going
    /// anywhere, cannot be read, or <paramref name="halted"/> says to stop looking.
    ///
    /// <b>Two deadlines, and the entry's own comes first.</b> Win32 documents the promise: before its
    /// wait hint elapses a service will either raise its check point or change state. Keeping that
    /// promise buys it a fresh wait hint, so an entry that genuinely needs a minute gets one. Breaking
    /// it is the documented signal that something has gone wrong.
    ///
    /// <b>The limit is ours, and since 2026-09-30 it is counted from the LAST PROGRESS</b> rather than
    /// from the start of the step - the owner's decision of 2026-09-29, stability report W-1. It
    /// decides alone when the entry promises nothing, and it never gives up on an entry that keeps
    /// moving. Until that day it was a wall across the whole step, and a service stopping honestly for
    /// seventy seconds was given up on at sixty.
    /// </summary>
    private Watched Watch(string serviceName, Func<EntryStatus, bool> over, TimeSpan timeout, Func<bool> halted)
    {
        var pause = FirstLook;
        ControlAnswer? seen = null;
        uint? checkPoint = null;
        var movedAt = TimeSpan.Zero;
        TimeSpan? promisedBy = null;

        while (true)
        {
            var answer = control.Read(serviceName);

            if (!answer.Worked)
            {
                return new Watched(Settled.Unreadable, answer, seen);
            }

            var progress = answer.Progress!.Value;
            var moved = seen is null
                || progress.CheckPoint > checkPoint
                || progress.Status != seen.Progress!.Value.Status;

            seen = answer;

            if (over(progress.Status))
            {
                return new Watched(Settled.Over, answer, seen);
            }

            var now = clock.Elapsed;

            if (moved)
            {
                checkPoint = progress.CheckPoint;
                movedAt = now;

                // A wait hint of zero is what an entry reports when it has nothing pending,
                // so it is not a promise to hold anybody to. Then only our own limit applies.
                promisedBy = progress.WaitHint > TimeSpan.Zero ? now + Honoured(progress.WaitHint) : null;
            }

            if (now >= movedAt + timeout || (promisedBy is not null && now >= promisedBy))
            {
                return new Watched(Settled.GaveUp, answer, seen);
            }

            if (halted())
            {
                return new Watched(Settled.Halted, answer, seen);
            }

            clock.Wait(pause);
            pause = pause * 2 < Cadence ? pause * 2 : Cadence;
        }
    }

    /// <summary>The state that means the entry is on its way somewhere else than the step wants it.</summary>
    private static EntryStatus Leaving(EntryStatus target) =>
        target == EntryStatus.Running ? EntryStatus.StopPending : EntryStatus.StartPending;

    /// <summary>
    /// Whether the entry is already heading where the step wants it. Continue pending is a paused
    /// service being resumed, which ends in Running.
    /// </summary>
    private static bool OnItsWay(EntryStatus target, EntryStatus status) => target == EntryStatus.Running
        ? status is EntryStatus.StartPending or EntryStatus.ContinuePending
        : status == EntryStatus.StopPending;

    private static bool FellBack(EntryStatus target, EntryStatus status) =>
        target == EntryStatus.Running && status == EntryStatus.Stopped;

    /// <summary>
    /// A start that ended in Stopped, with the service's own exit code as the number.
    ///
    /// <b>The words are the system's for that number</b>, the same as for any refusal. For 1066 -
    /// "the service has returned a service-specific error code" - the service's own number follows in
    /// brackets, because the system's words say there is one and not what it is. It is not a Windows
    /// error number, so it never goes where one is expected - the owner's decision of 2026-09-30.
    /// </summary>
    private StepResult StoppedAgain(PlanStep step, ControlAnswer seen, TimeSpan started)
    {
        var progress = seen.Progress!.Value;
        var code = unchecked((int)progress.ExitCode);
        var words = ManagerTerms.Describe(code);

        return Result(step, StepOutcome.Failed, progress.Status, Holding(seen), Elapsed(started)) with
        {
            ErrorCode = code,
            Error = code == ServiceSpecific
                ? string.Create(CultureInfo.InvariantCulture, $"{words} ({progress.ServiceExitCode})")
                : words,
            StoppedWhileStarting = true
        };
    }

    /// <summary>ERROR_SERVICE_SPECIFIC_ERROR - the service's own number is in the second field.</summary>
    private const int ServiceSpecific = 1066;

    /// <summary>
    /// Said in the plainest words available, because it is a refusal of ours rather than the system's -
    /// the same shape as <see cref="ProcessMoved"/>. Nothing was sent, and the sentence says so.
    /// </summary>
    private static string NeverAsked(EntryStatus target) => target == EntryStatus.Running
        ? "It was still stopping when the waiting ran out, so it was never asked to start. "
            + "Ask again once it has stopped."
        : "It was still starting when the waiting ran out, so it was never asked to stop. "
            + "Ask again once it has started.";

    /// <summary>How one watch ended.</summary>
    private enum Settled
    {
        /// <summary>The entry reached a state the watch was waiting for.</summary>
        Over,

        /// <summary>It stopped going anywhere - its own promise broke, or the limit passed without progress.</summary>
        GaveUp,

        /// <summary>Somebody asked the run to stop in a way this watch honours.</summary>
        Halted,

        /// <summary>A reading was refused.</summary>
        Unreadable
    }

    /// <summary>
    /// How a watch ended, the answer that ended it, and the last reading that worked - null when none
    /// did. A refused reading ends a watch without a status, and the status worth reporting is the
    /// one before it.
    /// </summary>
    private readonly record struct Watched(Settled End, ControlAnswer Last, ControlAnswer? Seen);

    /// <summary>
    /// The two ways a run can be told to stop, as a step being watched sees them.
    ///
    /// <b>Before anything is asked, exactly as a step not yet reached</b> - the rule in
    /// <see cref="Held"/>, so the two can never disagree. <b>After the manager was asked, only
    /// abandonment</b>, because a service told to stop does not un-stop, and the first level promises
    /// to watch what it already asked for.
    /// </summary>
    private readonly record struct Halt(CancellationToken Interruption, CancellationToken Abandonment)
    {
        internal bool Unasked(StepReason reason) => Held(
            reason,
            Abandonment.IsCancellationRequested,
            Interruption.IsCancellationRequested,
            forwardFailed: false,
            cascadeFailed: false) is not null;

        internal bool Asked => Abandonment.IsCancellationRequested;
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
