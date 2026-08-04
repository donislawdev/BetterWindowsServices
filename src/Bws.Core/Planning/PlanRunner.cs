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

            if (abandoned || ((forwardFailed || cancelled) && !putsBack))
            {
                results.Add(Skipped(
                    step,
                    cancelled || abandoned ? SkipReason.Cancelled : SkipReason.EarlierStepFailed,
                    EntryStatus.Unknown,
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
        var started = clock.Now;
        var target = Target(step.Operation);
        var before = control.Read(step.ServiceName);

        if (!before.Worked)
        {
            return Refused(step, before, EntryStatus.Unknown, started);
        }

        if (before.Progress!.Value.Status == target)
        {
            // Asked for a state it is already in. Not a failure and not work - the plan said
            // this might happen and warned about it, and doing nothing is the honest answer.
            return Skipped(step, SkipReason.AlreadyThere, target, Elapsed(started));
        }

        var request = control.Request(step.ServiceName, step.Operation);

        if (!request.Worked)
        {
            // A refusal is not always a failure. On a live machine an entry can arrive by
            // itself between being read and being asked, and the manager then refuses to
            // stop something already stopped. Rather than teaching this layer the manager's
            // error numbers, ask the entry where it is and let the answer decide.
            var after = control.Read(step.ServiceName);

            if (after.Worked && after.Progress!.Value.Status == target)
            {
                return Skipped(step, SkipReason.AlreadyThere, target, Elapsed(started));
            }

            return Refused(step, request, Where(after), started);
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
    private StepResult WaitFor(PlanStep step, EntryStatus target, TimeSpan timeout, DateTimeOffset started)
    {
        var giveUpAt = started + timeout;
        var status = EntryStatus.Unknown;
        uint? checkPoint = null;
        DateTimeOffset? promisedBy = null;

        while (true)
        {
            var answer = control.Read(step.ServiceName);

            if (!answer.Worked)
            {
                return Refused(step, answer, status, started);
            }

            var progress = answer.Progress!.Value;
            var moved = checkPoint is null || progress.CheckPoint > checkPoint || progress.Status != status;

            status = progress.Status;

            if (status == target)
            {
                return Result(step, StepOutcome.Succeeded, status, Elapsed(started));
            }

            var now = clock.Now;

            if (moved)
            {
                checkPoint = progress.CheckPoint;

                // A wait hint of zero is what an entry reports when it has nothing pending,
                // so it is not a promise to hold anybody to. Then only our own cap applies.
                promisedBy = progress.WaitHint > TimeSpan.Zero ? now + progress.WaitHint : null;
            }

            if (now >= giveUpAt || (promisedBy is not null && now >= promisedBy))
            {
                return Result(step, StepOutcome.TimedOut, status, Elapsed(started));
            }

            clock.Wait(Cadence);
        }
    }

    private static EntryStatus Target(StepOperation operation) =>
        operation == StepOperation.Stop ? EntryStatus.Stopped : EntryStatus.Running;

    private static EntryStatus Where(ControlAnswer answer) =>
        answer.Worked ? answer.Progress!.Value.Status : EntryStatus.Unknown;

    private long Elapsed(DateTimeOffset started) => (long)(clock.Now - started).TotalMilliseconds;

    private StepResult Refused(PlanStep step, ControlAnswer answer, EntryStatus status, DateTimeOffset started) =>
        new()
        {
            Step = step,
            Outcome = StepOutcome.Failed,
            SkippedBecause = null,
            Status = status,
            ErrorCode = answer.ErrorCode,
            Error = answer.Error,
            Milliseconds = Elapsed(started)
        };

    private static StepResult Skipped(PlanStep step, SkipReason because, EntryStatus status, long milliseconds) =>
        new()
        {
            Step = step,
            Outcome = StepOutcome.Skipped,
            SkippedBecause = because,
            Status = status,
            ErrorCode = 0,
            Error = null,
            Milliseconds = milliseconds
        };

    private static StepResult Result(PlanStep step, StepOutcome outcome, EntryStatus status, long milliseconds) =>
        new()
        {
            Step = step,
            Outcome = outcome,
            SkippedBecause = null,
            Status = status,
            ErrorCode = 0,
            Error = null,
            Milliseconds = milliseconds
        };
}
