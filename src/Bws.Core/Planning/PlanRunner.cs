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
public sealed partial class PlanRunner(IScmControl control, IClock clock)
{
    /// <summary>
    /// Runs every step, in order.
    /// </summary>
    /// <param name="timeout">
    /// How long any one step may go WITHOUT PROGRESS - its check point not rising and its state not
    /// changing - before it is given up on. The entry's own wait hint usually decides first.
    ///
    /// <b>Counted from the last progress since 2026-09-30, on the owner's decision of 2026-09-29</b>
    /// (stability report W-1). Until then it was the longest any step was watched at all, which gave
    /// up on a service that took seventy honest seconds to stop - and a restart then asked a service
    /// still stopping to start, was refused, and left it stopped. The price is said out loud rather
    /// than hidden: an entry that reports progress forever is now watched forever. The terminal gets
    /// out of that with a second Ctrl+C, and the window has no way out yet (backlog 497).
    /// </param>
    /// <param name="cancellation">
    /// Stop going forward. The steps that put things back are still carried out.
    ///
    /// Checked between steps, and while a step is waiting to be ASKED - an entry still on its way
    /// somewhere when the step reaches it. A step already asked for is watched to its end, because a
    /// service told to stop does not un-stop, and reporting a step we stopped looking at would be a
    /// claim about something nobody saw.
    /// </param>
    /// <param name="abandonment">
    /// Stop altogether, putting nothing back - between steps and, since 2026-09-30, while a step is
    /// being watched (stability report W-11). A step abandoned after it was asked for reports that
    /// the watching ended rather than that it failed, because the entry may still arrive.
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
        var cascadeFailed = false;

        for (var index = 0; index < plan.Steps.Count; index++)
        {
            var step = plan.Steps[index];

            cancelled |= cancellation.IsCancellationRequested;
            abandoned |= abandonment.IsCancellationRequested;

            var because = Held(step.Reason, abandoned, cancelled, forwardFailed, cascadeFailed)
                ?? Unneeded(step, plan, results);

            if (because is { } skipped)
            {
                results.Add(Skipped(
                    step,
                    skipped,
                    EntryStatus.Unknown,

                    // Nobody asked the manager about this entry, so there is nothing to say about
                    // the process behind it. Absent would claim there is none.
                    Reading<int>.NotRead(),
                    milliseconds: 0));

                continue;
            }

            starting?.Invoke(step, index + 1);

            var result = RunStep(step, timeout, new Halt(cancellation, abandonment));

            results.Add(result);

            if (!result.Arrived && step.Reason != StepReason.Restore)
            {
                forwardFailed = true;
                cascadeFailed |= step.Reason == StepReason.Cascade;
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

    /// <summary>
    /// Why a step is not to be tried at all, from what has happened so far - or nothing when it is.
    ///
    /// <b>Putting things back is not part of the forward path and does not stop when the forward path
    /// does.</b> Those steps exist to give back what earlier steps took, and abandoning them would
    /// leave the machine trimmed by a plan that failed - the one outcome nobody asked for. <b>What
    /// was never taken down is not given back at all, since 2026-09-30</b> - <see cref="Unneeded"/>
    /// says why. This comment used to say such an entry would be found already in place, which was
    /// true of an entry left running and false of one stopped all along (stability report W-5).
    ///
    /// <b>THE STEPS STANDING BEHIND THE ENTRY'S OWN STOP NEED THE OPPOSITE TREATMENT.</b> The ending
    /// of a process and, since 2026-09-29, the neighbours asked on the way to it exist for the case
    /// where the stop in front of them did not arrive - so the ordinary rule, stop going forward once
    /// something failed, would skip the only steps that were ever going to help. A neighbour refusing
    /// its own stop holds nothing back either: it dies with the process, and the preview says so. They
    /// are still held by an interruption, because that is somebody saying stop rather than something
    /// going wrong.
    ///
    /// <b>AND BY A CASCADE STEP THAT DID NOT ARRIVE, on the owner's decision of 2026-09-29</b> (stability
    /// report W-2). A dependant that refused to stop is still running on the process, and ending the
    /// process under it is exactly what the manager's refusal was protecting. Until that day the ending
    /// went ahead after any failure at all.
    /// </summary>
    private static SkipReason? Held(
        StepReason reason, bool abandoned, bool cancelled, bool forwardFailed, bool cascadeFailed)
    {
        var putsBack = reason == StepReason.Restore;
        var behind = reason is StepReason.Escalation or StepReason.SharesTheProcess;

        if (abandoned || (cancelled && !putsBack))
        {
            return SkipReason.Cancelled;
        }

        return (forwardFailed && !putsBack && !behind) || (cascadeFailed && behind)
            ? SkipReason.EarlierStepFailed
            : null;
    }

    /// <summary>
    /// Why a step nothing held back is still not worth trying - or nothing when it is.
    ///
    /// <b>Two answers, one question: the plan wanted this step only on the way to something that is not
    /// going to happen.</b> A neighbour is asked to stop only so its process can be ended, and
    /// <see cref="Stays"/> says when it will not be. A step putting an entry back exists only to give
    /// back what the run took, and <see cref="NetEffect.TookDown"/> says when it took nothing.
    ///
    /// <b>The second arrived on the owner's decision of 2026-09-30</b> (stability report W-5). Until then
    /// every step putting something back was tried, so Stop pressed before the first step of a restart
    /// started a service that had been stopped all along - and an interrupted bulk restart and a
    /// dependant somebody else had stopped in the meantime did the same. Asked from what the run itself
    /// recorded, so it costs no reading of the machine.
    /// </summary>
    private SkipReason? Unneeded(PlanStep step, OperationPlan plan, List<StepResult> results) => step.Reason switch
    {
        StepReason.SharesTheProcess when Stays(plan) => SkipReason.ProcessStays,
        StepReason.Restore when !NetEffect.TookDown(results, step.ServiceName) => SkipReason.NothingToPutBack,
        _ => null
    };

    /// <summary>
    /// Whether the process a plan ends is going to stay, asked just before a neighbour would be told
    /// to stop on the way to ending it.
    ///
    /// <b>The answer is what the ending step would find if it ran now</b> - the entry it ends for
    /// already stopped (the step would be "already there"), unreadable, or held by a process other
    /// than the one the plan froze (the step would refuse). In each of the three nothing is going to be
    /// ended, so asking a neighbour to stop would take down a service for no reason. The neighbours
    /// come after the entry's own polite stop since 2026-09-29, and this is what lets that stop leave
    /// them running when it works.
    ///
    /// <b>One reading per neighbour, and it is not the runner working the plan out again.</b> It
    /// invents no step and changes none - it declines one that stopped being needed, the same way a
    /// step whose entry is already where it was going is declined. A plan with no ending in it holds
    /// no neighbours, and one that somehow did has no process to ask them to make way for.
    /// </summary>
    private bool Stays(OperationPlan plan)
    {
        if (plan.Steps.FirstOrDefault(step => step.Operation == StepOperation.Terminate) is not { } ending)
        {
            return true;
        }

        // THE QUESTION END ASKS, ASKED THE SAME WAY, and one question answers all three. An entry that
        // stopped is held by no process - the manager answers zero, which Holding makes an absence -
        // and one that cannot be read is held by nothing anybody saw. Until a mutation run on
        // 2026-09-29 this spelled the stopped case out as well, and removing it changed nothing.
        var holding = Holding(control.Read(ending.ServiceName));

        return !(holding.IsPresent && holding.Value == ending.ProcessId);
    }

    private StepResult RunStep(PlanStep step, TimeSpan timeout, Halt halt)
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

        // AN ENTRY ALREADY ON ITS WAY IS WAITED FOR BEFORE ANYTHING IS ASKED, since 2026-09-30 - Settle
        // says how. Not for an ending: that step exists for the entry stuck in StopPending, and waiting
        // for it to finish stopping would be the one wrong answer there.
        if (step.Operation != StepOperation.Terminate
            && Settle(step, target, before, timeout, started, halt) is { } settled)
        {
            return settled;
        }

        // ENDING A PROCESS DOES NOT GO THROUGH THE MANAGER, so it is not a Request - and the reading
        // taken above is the whole reason this sits here rather than anywhere else. It is the freshest
        // answer available about where the entry is and what is holding it, taken immediately before
        // anything happens.
        var asking = clock.Elapsed;
        var request = step.Operation == StepOperation.Terminate
            ? End(step, before)
            : control.Request(step.ServiceName, step.Operation);
        var answered = Elapsed(asking);

        if (!request.Worked)
        {
            // A refusal is not always a failure. On a live machine an entry can arrive by
            // itself between being read and being asked, and the manager then refuses to
            // stop something already stopped. Rather than teaching this layer the manager's
            // error numbers, ask the entry where it is and let the answer decide.
            var after = control.Read(step.ServiceName);

            if (after.Worked && after.Progress!.Value.Status == target)
            {
                return Skipped(step, SkipReason.AlreadyThere, target, Holding(after), Elapsed(started))
                    with { Answered = answered };
            }

            return Refused(step, request, Where(after), Holding(after), started) with { Answered = answered };
        }

        return WaitFor(step, target, timeout, started, () => halt.Asked, asked: true)
            with { Answered = answered };
    }

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
        // the identity is carried rather than compared here. Backlog 323. And the process is read for
        // who lives in it NOW before anything dies, since 2026-09-30 - Crowded says why.
        return holding.IsPresent && holding.Value == step.ProcessId
            ? Crowded(step) ?? control.Terminate(holding.Value, step.ProcessCreatedAt)
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
