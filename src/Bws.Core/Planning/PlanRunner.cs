namespace Bws.Core.Planning;

/// <summary>
/// Carries a plan out, one step at a time, and reports what came of each.
///
/// The other half of ADR-11. <see cref="PlanBuilder"/> decides what will happen and this
/// decides nothing at all: it takes the steps exactly as they were shown, in the order they
/// were shown, and never works anything out again. That is deliberate and it is the whole
/// value of the dry run. If this asked the manager again about dependents, it could act on
/// an answer nobody saw, and the preview would stop being a promise. Drift between the two
/// moments shows up honestly instead, as a step the manager refuses.
///
/// Nothing runs side by side. C2 promises independent work in parallel and that belongs to
/// bulk operations, where there are independent things to run - here the whole plan is one
/// service and its cascade, which is a chain by construction.
/// </summary>
public sealed class PlanRunner(IScmControl control, IClock clock)
{
    /// <summary>
    /// How often the entry is asked where it has got to.
    ///
    /// Our choice, not the system's, and it carries no correctness: the deadline comes from
    /// the entry's own wait hint, this only decides how soon we notice. Short because
    /// somebody is watching a terminal and most stops are over in well under a second, and
    /// asking costs one call to the manager.
    /// </summary>
    private static readonly TimeSpan Cadence = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Runs every step, in order.
    /// </summary>
    /// <param name="timeout">
    /// The longest we will watch any one step. A cap rather than the deadline - the entry's
    /// own wait hint usually decides first, and this stops a plan hanging a terminal when an
    /// entry keeps reporting progress forever.
    /// </param>
    /// <param name="cancellation">
    /// Stop going forward. The steps that put things back are still carried out.
    ///
    /// Checked between steps, not during one. A step already asked for is watched to its
    /// end, because a service told to stop does not un-stop, and reporting a step we stopped
    /// looking at would be a claim about something nobody saw.
    /// </param>
    /// <param name="abandonment">
    /// Stop altogether, putting nothing back.
    ///
    /// Separate from <paramref name="cancellation"/> because they are different asks and the
    /// second one is expensive: it is how somebody ends up with half a cascade down. It
    /// exists anyway, because the alternative is a caller with no way out except killing the
    /// process, and a killed process reports nothing at all. Whatever this leaves behind is
    /// still in the results.
    /// </param>
    /// <param name="starting">
    /// Called before each step that is actually attempted, with its position in the plan.
    ///
    /// The position is handed over rather than counted by the caller, because steps get
    /// skipped and a counter of attempts would call the sixth step the third one. Progress
    /// that misnumbers itself is worse than no progress: somebody reading it is trying to
    /// work out where the plan has got to.
    /// </param>
    public PlanRun Run(
        OperationPlan plan,
        TimeSpan timeout,
        CancellationToken cancellation = default,
        CancellationToken abandonment = default,
        Action<PlanStep, int>? starting = null)
    {
        if (!plan.IsRunnable)
        {
            // Not a refusal a person can meet: the layer above shows the problems and never
            // gets here. Loud rather than quiet, because a plan with problems carried out
            // anyway is the worst bug this class could have.
            throw new InvalidOperationException(
                "A plan with problems, or with no steps, must never be run. Check IsRunnable first.");
        }

        var results = new List<StepResult>(plan.Steps.Count);
        var cancelled = false;
        var abandoned = false;
        var forwardFailed = false;

        for (var index = 0; index < plan.Steps.Count; index++)
        {
            var step = plan.Steps[index];

            cancelled |= cancellation.IsCancellationRequested;
            abandoned |= abandonment.IsCancellationRequested;

            // Putting things back is not part of the forward path and does not stop when the
            // forward path does. Those steps exist to give back what earlier steps took, and
            // abandoning them would leave the machine trimmed by a plan that failed - the
            // one outcome nobody asked for. Anything that was never taken down is found
            // already in place and reported as such, so this costs nothing when it is not
            // needed.
            var putsBack = step.Reason == StepReason.Restore;

            // THE STRONGER ATTEMPT STANDING BEHIND ONE THAT MAY NOT WORK, AND IT NEEDS THE OPPOSITE
            // TREATMENT FROM THE LINE ABOVE. An escalation exists for the case where an earlier
            // step did not arrive, so the ordinary rule - stop going forward once something failed -
            // would skip the only step that was ever going to help. It is still held by an
            // interruption, because that is somebody saying stop rather than something going wrong.
            var stronger = step.Reason == StepReason.Escalation;

            if (abandoned
                || (cancelled && !putsBack)
                || (forwardFailed && !putsBack && !stronger))
            {
                results.Add(Skipped(
                    step,
                    cancelled || abandoned ? SkipReason.Cancelled : SkipReason.EarlierStepFailed,
                    EntryStatus.Unknown,

                    // Nobody asked the manager about this entry, so there is nothing to say about
                    // the process behind it. Absent would claim there is none.
                    Reading<int>.NotRead(),
                    milliseconds: 0));

                continue;
            }

            starting?.Invoke(step, index + 1);

            var result = RunStep(step, timeout);

            results.Add(result);

            if (!result.Arrived && !putsBack)
            {
                forwardFailed = true;
            }
        }

        return new PlanRun
        {
            Plan = plan,
            Results = results,
            Ceiling = timeout,

            // One flag for both asks. Which of the two it was is already written into the
            // steps - a run that put things back and one that did not read differently
            // there - so a second field would say the same thing in a second place.
            Cancelled = cancelled || abandoned
                || cancellation.IsCancellationRequested || abandonment.IsCancellationRequested
        };
    }

    private StepResult RunStep(PlanStep step, TimeSpan timeout)
    {
        // THE RULER RATHER THAN THE WALL CLOCK, SINCE 2026-09-03 - backlog 299. Everything below
        // asks how long, never what time, and two readings of a wall clock across a machine
        // resuming or a time correction give a deadline already passed and a step time that is
        // negative. IClock.Elapsed is documented as a count that only goes forward.
        var started = clock.Elapsed;

        // A CONFIGURATION CHANGE IS DONE WHEN IT RETURNS, so everything below - the target status,
        // the read before, the waiting after - is about a question this step does not ask. Writing
        // a start type moves nothing, so there is no state to arrive at and nothing to poll.
        if (step.Operation == StepOperation.SetStartType)
        {
            return Configure(step, started);
        }

        var target = Target(step.Operation);
        var before = control.Read(step.ServiceName);

        if (!before.Worked)
        {
            return Refused(step, before, EntryStatus.Unknown, Reading<int>.NotRead(), started);
        }

        if (before.Progress!.Value.Status == target)
        {
            // Asked for a state it is already in. Not a failure and not work - the plan said
            // this might happen and warned about it, and doing nothing is the honest answer.
            return Skipped(step, SkipReason.AlreadyThere, target, Holding(before), Elapsed(started));
        }

        // ENDING A PROCESS DOES NOT GO THROUGH THE MANAGER, so it is not a Request - and the reading
        // taken four lines up is the whole reason this sits here rather than anywhere else. It is
        // the freshest answer available about where the entry is and what is holding it, taken
        // immediately before anything happens.
        var request = step.Operation == StepOperation.Terminate
            ? End(step, before)
            : control.Request(step.ServiceName, step.Operation);

        if (!request.Worked)
        {
            // A refusal is not always a failure. On a live machine an entry can arrive by
            // itself between being read and being asked, and the manager then refuses to
            // stop something already stopped. Rather than teaching this layer the manager's
            // error numbers, ask the entry where it is and let the answer decide.
            var after = control.Read(step.ServiceName);

            if (after.Worked && after.Progress!.Value.Status == target)
            {
                return Skipped(step, SkipReason.AlreadyThere, target, Holding(after), Elapsed(started));
            }

            return Refused(step, request, Where(after), Holding(after), started);
        }

        return WaitFor(step, target, timeout, started);
    }

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

    /// <summary>
    /// Writes a start type, and says where the entry is while it is at it.
    ///
    /// <b>The status is read AFTER rather than before, and it is not there to decide anything.</b>
    /// Nothing about this step depends on where the entry is - a running service can be set to
    /// disabled and keeps running - but every result in a run carries a status, and the honest one
    /// here is where the entry actually is once the setting has been written.
    ///
    /// <b>An unreadable status is not a failed step.</b> The setting was written or it was not, and
    /// that answer comes from the write rather than from a reading beside it.
    /// </summary>
    private StepResult Configure(PlanStep step, TimeSpan started)
    {
        var answer = control.Configure(step.ServiceName, step.To!.Value);
        var seen = control.Read(step.ServiceName);

        return answer.Worked
            ? Result(step, StepOutcome.Succeeded, Where(seen), Holding(seen), Elapsed(started))
            : Refused(step, answer, Where(seen), Holding(seen), started);
    }
    private StepResult WaitFor(PlanStep step, EntryStatus target, TimeSpan timeout, TimeSpan started)
    {
        var giveUpAt = started + timeout;
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
                promisedBy = progress.WaitHint > TimeSpan.Zero ? now + progress.WaitHint : null;
            }

            if (now >= giveUpAt || (promisedBy is not null && now >= promisedBy))
            {
                return Result(step, StepOutcome.TimedOut, status, processId, Elapsed(started));
            }

            clock.Wait(Cadence);
        }
    }

    /// <summary>
    /// Ends the process the step named, having checked it is still the one the step named.
    ///
    /// <b>THE ONE PLACE THIS CLASS LOOKS AT SOMETHING AGAIN, AND IT IS NOT THE CLASS WORKING
    /// ANYTHING OUT AFRESH.</b> Windows hands out process numbers again once a process is gone, and
    /// a plan built one moment is carried out the next - so the number in the preview can, by the
    /// time this runs, belong to something nobody has ever heard of. Refusing on a mismatch
    /// delivers exactly the plan that was shown or nothing at all, which is the promise. Deciding
    /// to end a DIFFERENT process would be the class inventing a step, and it does not do that.
    ///
    /// <b>The window between this reading and the call is narrow and is not closed.</b> Holding the
    /// process open would close it, at the cost of a handle outliving the step, and the honest
    /// answer is that a process identity Windows hands out in one piece is what this really wants
    /// and there is not one.
    /// </summary>
    private ControlAnswer End(PlanStep step, ControlAnswer before)
    {
        var holding = Holding(before);

        // THE CREATION TIME GOES DOWN WITH THE NUMBER AND THE OTHER SIDE CHECKS IT ON THE HANDLE IT
        // KILLS WITH. This class checks that the ENTRY still names the same process, which is a
        // different question from whether the NUMBER still names the same process - Windows gives
        // numbers out again, and only something holding a handle can rule that out. So this half of
        // the identity is carried rather than compared here. Backlog 323.
        return holding.IsPresent && holding.Value == step.ProcessId
            ? control.Terminate(holding.Value, step.ProcessCreatedAt)
            : ControlAnswer.Refused(0, ProcessMoved);
    }

    /// <summary>
    /// Said in the plainest words available, because it is the one refusal here that is ours rather
    /// than the system's - the same shape as the one <see cref="Configure"/> gives for a start type
    /// it has no number for.
    /// </summary>
    private const string ProcessMoved =
        "The process behind this entry is not the one the plan named, so nothing was ended. "
        + "Ask again to build a plan against the machine as it is now.";

    private static EntryStatus Target(StepOperation operation) => operation switch
    {
        StepOperation.Stop => EntryStatus.Stopped,

        // THE SAME TARGET AS A STOP, and that is the point of it rather than a shortcut. Ending the
        // process is how this step gets there, and the manager noticing the process die and moving
        // the entry is what finishes it - so the step is done when the ENTRY says so, never when
        // the call returns.
        StepOperation.Terminate => EntryStatus.Stopped,

        StepOperation.Start => EntryStatus.Running,
        _ => throw new ArgumentOutOfRangeException(
            nameof(operation), operation, EquivalentCommand.Unhandled)
    };

    private static EntryStatus Where(ControlAnswer answer) =>
        answer.Worked ? answer.Progress!.Value.Status : EntryStatus.Unknown;

    /// <summary>
    /// Which process was holding the entry, as last seen, in the three states that are true of it.
    ///
    /// <b>NotRead where the status itself could not be read</b>, because nobody asked the manager
    /// and got an answer - the same distinction <see cref="Where"/> flattens into Unknown, kept
    /// here because a number has no Unknown to flatten into. <b>Absent where the manager answered
    /// zero</b>, which is what it reports for an entry with no process rather than a process
    /// numbered nought.
    /// </summary>
    private static Reading<int> Holding(ControlAnswer answer)
    {
        if (!answer.Worked)
        {
            return Reading<int>.NotRead();
        }

        var processId = answer.Progress!.Value.ProcessId;

        return processId == 0 ? Reading<int>.Absent() : Reading<int>.Present((int)processId);
    }

    private long Elapsed(TimeSpan started) => (long)(clock.Elapsed - started).TotalMilliseconds;

    private StepResult Refused(
        PlanStep step, ControlAnswer answer, EntryStatus status, Reading<int> processId, TimeSpan started) =>
        new()
        {
            Step = step,
            Outcome = StepOutcome.Failed,
            SkippedBecause = null,
            Status = status,
            ProcessId = processId,
            ErrorCode = answer.ErrorCode,
            Error = answer.Error,
            Milliseconds = Elapsed(started)
        };

    private static StepResult Skipped(
        PlanStep step, SkipReason because, EntryStatus status, Reading<int> processId, long milliseconds) =>
        new()
        {
            Step = step,
            Outcome = StepOutcome.Skipped,
            SkippedBecause = because,
            Status = status,
            ProcessId = processId,
            ErrorCode = 0,
            Error = null,
            Milliseconds = milliseconds
        };

    private static StepResult Result(
        PlanStep step, StepOutcome outcome, EntryStatus status, Reading<int> processId, long milliseconds) =>
        new()
        {
            Step = step,
            Outcome = outcome,
            SkippedBecause = null,
            Status = status,
            ProcessId = processId,
            ErrorCode = 0,
            Error = null,
            Milliseconds = milliseconds
        };
}
