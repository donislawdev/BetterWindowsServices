namespace Bws.Core.Planning;

/// <summary>
/// Where each kind of step is going, which states mean it is already on its way or heading elsewhere, what
/// counts as falling back, and the words for a step that never got to ask.
///
/// <b>Its own file since 2026-11-25</b>, when pausing and resuming turned these from two directions - up and
/// down - into four, and the watching beside them (PlanRunner.Waiting) went into the crowd near the length
/// ceiling. The watching asks these questions and does not change when the answers do.
/// </summary>
public sealed partial class PlanRunner
{
    /// <summary>
    /// Whether the entry is on its way somewhere other than where the step wants it, so that asking now
    /// would be asking the wrong moment. A stop waits for a start, a pause and a resume to finish - the
    /// last two since 2026-11-25. A pause waits for a start or a resume, and a resume for a pause.
    /// </summary>
    private static bool HeadingAway(StepOperation operation, EntryStatus status) => operation switch
    {
        StepOperation.Start => status == EntryStatus.StopPending,
        StepOperation.Pause => status is EntryStatus.StartPending or EntryStatus.ContinuePending,
        StepOperation.Resume => status == EntryStatus.PausePending,
        _ => status is EntryStatus.StartPending or EntryStatus.PausePending or EntryStatus.ContinuePending
    };

    /// <summary>
    /// Whether the entry is already heading where the step wants it. Continue pending is a paused
    /// service being resumed, which ends in Running.
    /// </summary>
    private static bool OnItsWay(StepOperation operation, EntryStatus status) => operation switch
    {
        StepOperation.Start or StepOperation.Resume => status is EntryStatus.StartPending or EntryStatus.ContinuePending,
        StepOperation.Pause => status == EntryStatus.PausePending,
        _ => status == EntryStatus.StopPending
    };

    /// <summary>The state an entry reports while a pause or a resume is under way, and nothing for the others.</summary>
    private static EntryStatus? Pending(StepOperation operation) => operation switch
    {
        StepOperation.Pause => EntryStatus.PausePending,
        StepOperation.Resume => EntryStatus.ContinuePending,
        _ => null
    };

    /// <summary>
    /// Whether the entry has fallen back rather than arrived, so the watching is over and the step failed.
    ///
    /// <b>A pause or a resume falls back only AFTER it was seen on its way</b> - measured 2026-11-25 on the
    /// throwaway machine: a pause the service turned down went from PausePending back to Running in about
    /// 100 ms. Running straight after the request is NOT that, for the reason a stop does not count it
    /// either: a service may take the control and report the pending state a moment later. Stopped is a
    /// fall at any point, for a start, a pause and a resume alike.
    /// </summary>
    private static bool FellBack(StepOperation operation, EntryStatus status, bool begun) => operation switch
    {
        StepOperation.Start => status == EntryStatus.Stopped,
        StepOperation.Pause => status == EntryStatus.Stopped || (begun && status == EntryStatus.Running),
        StepOperation.Resume => status == EntryStatus.Stopped || (begun && status == EntryStatus.Paused),
        _ => false
    };

    /// <summary>
    /// A pause or a resume that ended somewhere other than where it was going, in our own words unless the
    /// entry stopped with a number of its own. The manager says nothing here - it took the request - and the
    /// measured refusal left no exit code (2026-11-25), so a system sentence for zero would say "completed
    /// successfully" over a failure.
    /// </summary>
    private StepResult TurnedDown(PlanStep step, ControlAnswer seen, TimeSpan started)
    {
        var progress = seen.Progress!.Value;
        var stopped = progress.Status == EntryStatus.Stopped;
        var code = stopped ? unchecked((int)progress.ExitCode) : 0;
        var pausing = step.Operation == StepOperation.Pause;

        return Result(step, StepOutcome.Failed, progress.Status, Holding(seen), Elapsed(started)) with
        {
            ErrorCode = code,
            Error = code != 0 ? ManagerTerms.Describe(code)
                : stopped ? (pausing ? "It stopped instead of pausing." : "It stopped instead of resuming.")
                : pausing ? "It began to pause and went back to running, so the service turned the pause down."
                : "It began to resume and went back to being paused, so the service turned the resume down."
        };
    }

    /// <summary>
    /// Said in the plainest words available, because it is a refusal of ours rather than the system's -
    /// the same shape as <see cref="ProcessMoved"/>. Nothing was sent, and the sentence says so.
    /// </summary>
    private static string NeverAsked(StepOperation operation, EntryStatus leaving)
    {
        var (doing, done) = leaving switch
        {
            EntryStatus.StopPending => ("stopping", "stopped"),
            EntryStatus.PausePending => ("pausing", "paused"),
            EntryStatus.ContinuePending => ("resuming", "resumed"),
            _ => ("starting", "started")
        };

        return $"It was still {doing} when the waiting ran out, so it was never asked to {Verb(operation)}. "
            + $"Ask again once it has {done}.";
    }

    /// <summary>
    /// The other half of <see cref="NeverAsked"/>: the entry was already heading where the step wanted
    /// it, was only watched, and did not get there in time. Said in our words because the manager said
    /// nothing - nobody asked it anything.
    /// </summary>
    private static string NeverArrived(StepOperation operation) => operation switch
    {
        StepOperation.Start => "It was already starting when its turn came, and the waiting ran out before it started. "
            + "It was never asked to start, so nothing here counts it as started.",
        StepOperation.Pause => "It was already pausing when its turn came, and the waiting ran out before it paused. "
            + "It was never asked to pause, so nothing here resumes it.",
        StepOperation.Resume => "It was already resuming when its turn came, and the waiting ran out before it was running. "
            + "It was never asked to resume, so nothing here pauses it again.",
        _ => "It was already stopping when its turn came, and the waiting ran out before it stopped. "
            + "It was never asked to stop, so nothing here starts it again."
    };

    /// <summary>The word a sentence above uses for what the step would have asked.</summary>
    private static string Verb(StepOperation operation) => operation switch
    {
        StepOperation.Start => "start",
        StepOperation.Pause => "pause",
        StepOperation.Resume => "resume",
        _ => "stop"
    };
}
