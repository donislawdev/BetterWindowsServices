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
    /// The longest any one step was to be watched for, as asked for by whoever ran this.
    ///
    /// Here rather than left with the caller because it is half of the evidence this record
    /// exists to hold: what came of a step is only readable next to what it was given.
    /// </summary>
    public required TimeSpan Ceiling { get; init; }

    /// <summary>
    /// Steps that took longer than the ceiling and did not end by our giving up.
    ///
    /// <b>This is a real case and it surprises people, which is why it is a property rather
    /// than something a reader is left to spot.</b> <c>--timeout</c> caps how long this tool
    /// waits <i>after</i> the manager accepts a request. It cannot cap the manager's own
    /// answer, and the manager does not always answer quickly: measured on Windows Server
    /// 2025 on 2026-08-04, <c>StartService</c> for a service that never reports itself took
    /// <b>30 375-30 450 ms across three runs</b> before coming back with error 1053. So
    /// <c>bws start X --timeout 1</c> ran for half a minute, reported the truth, and looked
    /// like a switch that did nothing.
    ///
    /// <b>The test carries no threshold on purpose.</b> A step that ended in
    /// <see cref="StepOutcome.TimedOut"/> reached the ceiling because the ceiling worked, and
    /// its own line already says "gave up after". Any other step that ran past the ceiling
    /// spent that time somewhere the ceiling does not reach, and that is the whole of what
    /// there is to say. Picking a multiple of the ceiling instead would have been a number
    /// with no reason behind it.
    /// </summary>
    public IReadOnlyList<StepResult> OutranTheCeiling =>
        [.. Results.Where(result =>
            result.Outcome != StepOutcome.TimedOut
            && result.Milliseconds > Ceiling.TotalMilliseconds)];

    /// <summary>
    /// Every entry ended up where the plan wanted it.
    ///
    /// Steps that were already there count. Running the same plan twice must not report
    /// the second run as a failure - a runbook line that goes red for doing nothing is a
    /// runbook line somebody stops trusting.
    /// </summary>
    public bool Completed => Results.All(result => result.Arrived);

    /// <summary>
    /// What it would take to put every entry back where this run found it.
    ///
    /// <b>The cheapest honest form of the promise `ADR-11` makes about a plan being reversible,
    /// and deliberately not the machinery.</b> Nothing here undoes anything - it says what the
    /// commands would be, which is Nielsen's emergency exit for the price of a sentence. Backlog
    /// 58, and the full form belongs to whichever slice first writes from the window.
    ///
    /// <b>It is a net effect per entry, never a reversal of the steps, and that distinction is the
    /// whole reason this is a property rather than a loop in the caller.</b> Turning each step
    /// around one at a time would tell somebody who restarted a service to stop it - the two steps
    /// of a restart cancel out, and the entry ends exactly where it began. Reversing steps is the
    /// obvious implementation and it gives dangerous advice on the commonest write this tool
    /// performs.
    ///
    /// So each entry is asked two questions instead: where it was before the first step that moved
    /// it, and where it is after the last one. Equal means nothing to say.
    ///
    /// <b>A step that timed out counts as having moved the entry</b>, for the same reason
    /// <see cref="StepOutcome.TimedOut"/> is not <see cref="StepOutcome.Failed"/>: the manager
    /// accepted the request and the entry may well have arrived after we stopped watching. Leaving
    /// it out would be a claim about something nobody saw, and the direction of that error is the
    /// bad one - it would stay silent about an entry somebody was left holding.
    ///
    /// Skipped steps moved nothing by definition, and a refusal moved nothing either.
    ///
    /// <b>The order is the reverse of the run</b>, and it has to be: a stop cascade takes the
    /// dependants down before the entry they depend on, so putting them back starts that entry
    /// first. Handing back the commands in the order they happened would hand back a sequence
    /// whose first line fails.
    /// </summary>
    public IReadOnlyList<ReversalStep> Reversal
    {
        get
        {
            var moves = new Dictionary<string, (StepOperation First, StepOperation Last, int When)>(
                StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < Results.Count; index++)
            {
                var result = Results[index];

                if (result.Outcome != StepOutcome.Succeeded && result.Outcome != StepOutcome.TimedOut)
                {
                    continue;
                }

                var name = result.Step.ServiceName;
                var operation = result.Step.Operation;

                moves[name] = moves.TryGetValue(name, out var seen)
                    ? (seen.First, operation, index)
                    : (operation, operation, index);
            }

            return
            [
                .. moves
                    .Where(move => Before(move.Value.First) != After(move.Value.Last))
                    .OrderByDescending(move => move.Value.When)
                    .Select(move => new ReversalStep(
                        move.Key,
                        move.Value.Last == StepOperation.Stop ? StepOperation.Start : StepOperation.Stop))
            ];
        }
    }

    /// <summary>
    /// Whether an entry was running before the first step that moved it. A stop found it running,
    /// a start found it stopped.
    ///
    /// <b>This was written back to front and every test in PlanReversalTests said so at once</b> -
    /// the sentence above was right and the expression under it was its opposite, so a plain stop
    /// came out as having changed nothing and the whole property returned an empty list. Worth the
    /// line it takes to record: the comment was not what was wrong, and rereading it would not have
    /// found this.
    /// </summary>
    private static bool Before(StepOperation first) => first == StepOperation.Stop;

    /// <summary>Whether a step that moved an entry left it running.</summary>
    private static bool After(StepOperation last) => last == StepOperation.Start;
}

/// <summary>
/// One entry, and the operation that would put it back where a run found it.
///
/// No reason attached, unlike <see cref="PlanStep"/>, and that is on purpose: this is not a plan
/// and must not be mistaken for one on the way past. A plan is something this tool worked out and
/// will carry out, with a preview that matches it step for step - this is a sentence saying what
/// somebody could type. Giving it the shape of a plan would invite the next slice to run it, and
/// running it is exactly the machinery `ADR-11` promises and backlog 58 says does not exist yet.
/// </summary>
public sealed record ReversalStep(string ServiceName, StepOperation Operation);
