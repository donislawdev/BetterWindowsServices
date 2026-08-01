namespace Bws.Core.Planning;

/// <summary>
/// What actually happened to one step. The glossary calls this an outcome and names these
/// four, so these four are what there is.
/// </summary>
public enum StepOutcome
{
    /// <summary>The entry reached the state the step asked for.</summary>
    Succeeded,

    /// <summary>The manager refused, or the entry did not survive the attempt.</summary>
    Failed,

    /// <summary>
    /// We stopped waiting. Not the same as failed: the entry may well arrive after we
    /// stopped looking, and saying it failed would be a claim about something nobody saw.
    /// </summary>
    TimedOut,

    /// <summary>Never attempted. <see cref="StepResult.SkippedBecause"/> says why.</summary>
    Skipped
}

/// <summary>
/// Why a step was never attempted.
///
/// Three quite different stories, and folding them into one word would be the empty-value
/// mistake part 3 of 06-STRUKTURA-I-KONWENCJE is about: "skipped" alone cannot tell
/// somebody whether the machine is where they wanted it or half-way to somewhere else.
/// </summary>
public enum SkipReason
{
    /// <summary>The entry was already in the state the step asked for. Nothing to do.</summary>
    AlreadyThere,

    /// <summary>An earlier step did not work, so this one was never tried.</summary>
    EarlierStepFailed,

    /// <summary>Somebody interrupted the run before this step was reached.</summary>
    Cancelled
}

/// <summary>
/// One step, and what came of it.
///
/// Carries where the entry was left as well as the outcome, because those are different
/// questions. A step that timed out while the entry sat in StopPending is a different
/// message from one that timed out with the entry still running, and a person deciding
/// what to do next needs the second half.
/// </summary>
public sealed record StepResult
{
    public required PlanStep Step { get; init; }

    public required StepOutcome Outcome { get; init; }

    /// <summary>Set when, and only when, <see cref="Outcome"/> is <see cref="StepOutcome.Skipped"/>.</summary>
    public required SkipReason? SkippedBecause { get; init; }

    /// <summary>Where the entry was left, as last seen. Unknown when it could not be read.</summary>
    public required EntryStatus Status { get; init; }

    /// <summary>The manager's own number, or zero. See <see cref="ControlAnswer.ErrorCode"/>.</summary>
    public required int ErrorCode { get; init; }

    /// <summary>The system's words for that number. Null when nothing went wrong.</summary>
    public required string? Error { get; init; }

    /// <summary>How long this step took, waiting included.</summary>
    public required long Milliseconds { get; init; }

    /// <summary>The entry is where the step wanted it, whether or not we had to do anything.</summary>
    public bool Arrived =>
        Outcome == StepOutcome.Succeeded
        || (Outcome == StepOutcome.Skipped && SkippedBecause == SkipReason.AlreadyThere);
}

/// <summary>
/// A plan and what came of it, together.
///
/// One object rather than a bare list of outcomes, because the pair is the evidence. The
/// plan says what was going to happen and the results say what did, and the whole promise
/// of ADR-11 is that somebody can hold those two side by side. Splitting them would leave
/// the interesting comparison to whoever remembered to make it.
/// </summary>
public sealed record PlanRun
{
    public required OperationPlan Plan { get; init; }

    /// <summary>One per step of the plan, in the same order. Never shorter than the plan.</summary>
    public required IReadOnlyList<StepResult> Results { get; init; }

    /// <summary>Whether somebody interrupted the run.</summary>
    public required bool Cancelled { get; init; }

    /// <summary>
    /// Every entry ended up where the plan wanted it.
    ///
    /// Steps that were already there count. Running the same plan twice must not report
    /// the second run as a failure - a runbook line that goes red for doing nothing is a
    /// runbook line somebody stops trusting.
    /// </summary>
    public bool Completed => Results.All(result => result.Arrived);
}
