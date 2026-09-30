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
/// Four quite different stories, and folding them into one word would be the empty-value
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
    Cancelled,

    /// <summary>
    /// A step asking a neighbour to stop, not needed because the process it lives in is not going to
    /// be ended - the entry the process was to be ended for stopped by itself, cannot be read, or is
    /// no longer in the process the plan named.
    ///
    /// <b>A fourth story, added on the owner's decision of 2026-09-29, and neither of the others will
    /// do.</b> The neighbour is still running, so "already there" would be a claim about something
    /// that is not true. And the step before it may well have worked - a polite stop that arrived is
    /// the reason, not a failure. The neighbours are asked AFTER the entry itself since that day
    /// (stability report W-2), so that a polite stop which works leaves them running.
    /// </summary>
    ProcessStays
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

    /// <summary>
    /// Which process was holding the entry, as last seen.
    ///
    /// <b>Here for the step that gave up, which is the one case where this record has to say
    /// something a person can act on.</b> An entry left in StopPending has been asked to stop,
    /// has not stopped, and will not be moved by asking again - so the only thing left to name
    /// is the process still holding it. Every other outcome carries it too, because it comes
    /// from the same reading and leaving it out of three results to have it in one would make
    /// its absence mean something it does not.
    ///
    /// <b>Three states and each is a different sentence.</b> Present is a process. Absent is the
    /// manager answering that there is none, which is ordinary for an entry that stopped.
    /// NotRead is nobody having asked - a step skipped before it was reached, or one whose very
    /// first reading was refused.
    /// </summary>
    public required Reading<int> ProcessId { get; init; }

    /// <summary>The manager's own number, or zero. See <see cref="ControlAnswer.ErrorCode"/>.</summary>
    public required int ErrorCode { get; init; }

    /// <summary>The system's words for that number. Null when nothing went wrong.</summary>
    public required string? Error { get; init; }

    /// <summary>How long this step took, waiting included.</summary>
    public required long Milliseconds { get; init; }

    /// <summary>
    /// How long the manager took to answer the request itself, before any watching - zero for a step
    /// that asked nothing.
    ///
    /// <b>Here since 2026-09-30 because <see cref="PlanRun.OutranTheCeiling"/> could no longer be read
    /// off <see cref="Milliseconds"/>.</b> The limit counts time WITHOUT PROGRESS from that day, so a
    /// step reporting progress for three minutes under a limit of one is ordinary, and the one thing
    /// the limit still cannot reach is the manager sitting on the request. Not in the machine readable
    /// output - it feeds a sentence, not a field anybody asked for.
    /// </summary>
    public long Answered { get; init; }

    /// <summary>
    /// The entry was on its way to Running and fell back to Stopped - the service stopped while it was
    /// starting. <see cref="ErrorCode"/> is then the service's own exit code rather than the manager's
    /// refusal, and the two interfaces word it that way.
    ///
    /// <b>A fact rather than a sentence, since 2026-09-30</b> (stability report W-7): the failed step's
    /// other fields cannot tell it apart from a start the manager refused, which also leaves the entry
    /// Stopped with an error number. Not in the machine readable output, on the owner's decision of that
    /// day - the number travels in <c>errorCode</c> and the words in <c>error</c>.
    /// </summary>
    public bool StoppedWhileStarting { get; init; }

    /// <summary>
    /// The process was ended, and the entry was seen in ANOTHER process before it was seen stopped -
    /// the manager started it again at once, as a recovery list or a trigger tells it to.
    ///
    /// <b>A failed step with our own sentence, on the owner's decision of 2026-09-30</b> (stability report
    /// W-3). Measured before the change: the step waited for Stopped, which with a restart at 0 ms showed
    /// for 42-58 ms or not at all, and reported running out of time after the whole minute - while in the
    /// same shape the manager marks the death 3-17 ms after the ending. Not in the machine readable output,
    /// like the fact above - <c>error</c> carries the
    /// sentence and <c>processId</c> the new process.
    /// </summary>
    public bool StartedAgain { get; init; }

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
    /// How long any one step could go without progress before it was given up on, as asked for by
    /// whoever ran this. Until 2026-09-30 it was the longest any one step was watched for at all - the
    /// owner's decision of 2026-09-29 (stability report W-1) made it count from the last progress.
    ///
    /// Here rather than left with the caller because it is half of the evidence this record
    /// exists to hold: what came of a step is only readable next to what it was given.
    /// </summary>
    public required TimeSpan Ceiling { get; init; }

    /// <summary>
    /// Steps where the manager alone took longer than the ceiling to answer the request.
    ///
    /// <b>This is a real case and it surprises people, which is why it is a property rather
    /// than something a reader is left to spot.</b> <c>--timeout</c> governs the watching
    /// <i>after</i> the manager accepts a request. It cannot cap the manager's own
    /// answer, and the manager does not always answer quickly: measured on Windows Server
    /// 2025 on 2026-08-04, <c>StartService</c> for a service that never reports itself took
    /// <b>30 375-30 450 ms across three runs</b> before coming back with error 1053. So
    /// <c>bws start X --timeout 1</c> ran for half a minute, reported the truth, and looked
    /// like a switch that did nothing.
    ///
    /// <b>READ OFF <see cref="StepResult.Answered"/> SINCE 2026-09-30, AND UNTIL THEN OFF THE WHOLE
    /// STEP.</b> The whole step used to be the right measure, because the ceiling capped the whole
    /// watch and anything over it had to have been spent in the manager. From that day the ceiling
    /// counts time without progress, so a stop reporting progress for ninety seconds under a limit of
    /// sixty is the limit working - and the old measure would have told somebody the manager took
    /// ninety seconds to answer, which it did not.
    ///
    /// <b>The test carries no threshold on purpose.</b> An answer longer than the ceiling was
    /// spent somewhere the ceiling does not reach, and that is the whole of what there is to say.
    /// Picking a multiple of the ceiling instead would have been a number with no reason behind it.
    /// </summary>
    public IReadOnlyList<StepResult> OutranTheCeiling =>
        [.. Results.Where(result => result.Answered > Ceiling.TotalMilliseconds)];

    /// <summary>
    /// Every entry ended up where the plan wanted it.
    ///
    /// <b>COUNTED BY ENTRY RATHER THAN BY STEP SINCE 2026-09-16, and the sentence above is the reason
    /// it had to be.</b> A forcing plan puts a polite stop in front of the step that ends the process,
    /// and the second exists for the case where the first does not arrive. Counted by step, the exact
    /// shape that plan was built for - polite stop timed out, process ended, entry in Stopped - came
    /// back "not where you asked" on the sheet and as exit code 3 on the command line, over an entry
    /// standing where every step wanted it. Predicted from reading, then measured on the throwaway
    /// machine before a line changed: <c>bws kill</c> against a running entry whose stop hangs gave
    /// exit code 3 and <c>completed: false</c>. The product had only ever met the one step shape
    /// before, where the polite step is skipped, so nothing had ever shown the wrong answer.
    ///
    /// <b>The last step for each entry and each aim decides.</b> Two steps that want the same thing of
    /// the same entry - a stop and the kill behind it - are one question with the later answer, and a
    /// restart's stop and start are two questions that both have to be yes. Steps that were already
    /// there count. Running the same plan twice must not report the second run as a failure - a
    /// runbook line that goes red for doing nothing is a runbook line somebody stops trusting.
    ///
    /// <b>Only what the runner SAW counts, and that is a limit said out loud rather than hidden.</b> An
    /// entry that merely shares the process - asked politely, timed out, then taken down when the
    /// process was ended - has no later step of its own that watched it arrive, so it still reads as
    /// not arrived. Claiming otherwise would be a claim nobody checked. Reading the neighbours again
    /// after a terminate is a separate change and a backlog row, not a widening of this one.
    ///
    /// <b>A neighbour skipped because the process stays is not counted at all (2026-09-29).</b> The plan
    /// wanted it down only on the way to ending the process, and that way was not needed - so a
    /// forced stop whose polite step worked reads as done rather than as three neighbours "not where
    /// you asked", which would be exit code 3 over a machine in exactly the state asked for.
    /// </summary>
    public bool Completed => Results
        .Where(result => result.SkippedBecause != SkipReason.ProcessStays)
        .GroupBy(result => (result.Step.ServiceName, Aim(result.Step.Operation)))
        .All(same => same.Last().Arrived);

    /// <summary>
    /// What an operation is trying to make true of its entry, with ending the process folded into
    /// stopping - the same folding <see cref="PlanRunner"/> does when it decides what a terminate
    /// waits for, and for the same reason: the manager moves the entry to Stopped either way, and
    /// the step is finished when the entry says so.
    /// </summary>
    private static StepOperation Aim(StepOperation operation) =>
        operation == StepOperation.Terminate ? StepOperation.Stop : operation;

    /// <summary>
    /// What it would take to put every entry back where this run found it.
    ///
    /// <b>The arithmetic moved to <see cref="NetEffect"/> on 2026-08-18 and this became the one
    /// line that hands it the results.</b> A run over a whole selection asks the same question of
    /// every step laid end to end, and two implementations of "where did this entry start and where
    /// did it finish" would be two things obliged to agree about the most dangerous sentence this
    /// tool prints. Everything that decided the shape of it - net effect rather than reversed steps,
    /// a timed out step counting as a move, the order being the reverse of the run - is written
    /// there, beside the code it explains.
    /// </summary>
    public IReadOnlyList<ReversalStep> Reversal => NetEffect.Of(Results);
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
/// <param name="To">
/// The startup setting that would put this entry back, for a way back that writes one.
///
/// <b>The setting the entry had BEFORE the run, which is the whole difficulty of this case.</b> Every
/// other way back is worked out from direction alone - something that was stopped gets started -
/// and a setting has no direction. It has a value, and the value has to have been carried from the
/// plan through <see cref="PlanStep.From"/> to reach here.
///
/// Null for a stop or a start, where there is no such value and inventing one would be a claim.
/// </param>
public sealed record ReversalStep(string ServiceName, StepOperation Operation, StartSetting? To = null);
