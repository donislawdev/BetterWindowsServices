namespace Bws.Core.Planning;

/// <summary>What a person asked for, in their own words, before anything was worked out.</summary>
public enum ActionKind
{
    Stop,
    Start,
    Restart
}

/// <summary>What one step actually does to one entry.</summary>
public enum StepOperation
{
    Stop,
    Start
}

/// <summary>Why a step is in the plan, which is the part a person reads first.</summary>
public enum StepReason
{
    /// <summary>The entry that was asked about.</summary>
    Requested,

    /// <summary>Comes along because it breaks otherwise. Nobody asked for it.</summary>
    Cascade,

    /// <summary>The second half of a restart, putting back what the first half took down.</summary>
    Restore
}

/// <summary>
/// What a person asked for. Their words, before any analysis.
///
/// Kept apart from the plan on purpose. The action is what was wanted, the plan is what
/// would happen, and the whole value of the pattern is that those two are different
/// objects a person can compare.
/// </summary>
/// <param name="IncludeDependents">
/// Whether the entries that break may be taken down as well.
///
/// Off by default, and that is a safety property rather than a default worth arguing
/// about. Asking to stop one service is not asking to stop seven, and the manager refuses
/// the stop anyway when something running depends on it - so the honest answer to a plain
/// stop is a plan of one step and a warning naming who is in the way.
/// </param>
public sealed record ServiceAction(ActionKind Kind, string ServiceName, bool IncludeDependents = false);

/// <summary>One thing that will happen, to one entry.</summary>
public sealed record PlanStep(
    string ServiceName,
    string DisplayName,
    StepOperation Operation,
    StepReason Reason);

/// <summary>Kinds of thing worth saying before somebody presses the button.</summary>
public enum PlanWarningKind
{
    /// <summary>Stopping this takes others down with it, because it was asked to.</summary>
    Cascade,

    /// <summary>
    /// Others are running that need this one, and they were not included. The manager
    /// refuses a stop in that situation, so this plan will not get past its first step.
    /// </summary>
    DependentsInTheWay,

    /// <summary>The entry shares its process with others, so the process does not go away.</summary>
    SharedProcess,

    /// <summary>Automatic, so stopping it lasts until the next boot and no longer.</summary>
    ReturnsAfterReboot,

    /// <summary>
    /// The cascade could not be read in full. The plan below may therefore be shorter than
    /// what actually happens, which is the one thing a preview must never hide.
    /// </summary>
    CascadeUnreadable,

    /// <summary>Already in the state being asked for, so the step would do nothing.</summary>
    AlreadyThere
}

/// <summary>
/// Something worth knowing before the plan runs.
///
/// Carries a kind and the facts, never a sentence. The layer above decides the words,
/// because the same warning reads differently in a window and in a terminal, and neither
/// wording belongs in the part that works out what will happen.
/// </summary>
public sealed record PlanWarning(PlanWarningKind Kind, string ServiceName, IReadOnlyList<string> Related)
{
    internal PlanWarning(PlanWarningKind kind, string serviceName)
        : this(kind, serviceName, [])
    {
    }
}

/// <summary>Why a plan could not be made at all.</summary>
public enum PlanProblemKind
{
    /// <summary>No entry by that name.</summary>
    UnknownService,

    /// <summary>The entry is a driver, and operating on drivers is not something this does.</summary>
    NotOperable,

    /// <summary>
    /// Getting to the entry would mean stopping a driver, which this does not do either.
    ///
    /// A problem rather than a warning, and the difference matters. Listing the steps and
    /// noting an objection underneath would show eight things happening when the first of
    /// them is one we have already said we will not do. A preview that does not match what
    /// execution would do is the one thing this whole pattern exists to prevent.
    /// </summary>
    CascadeNotOperable
}

/// <summary>A reason there is no plan. Facts only, wording belongs above.</summary>
public sealed record PlanProblem(PlanProblemKind Kind, string ServiceName, IReadOnlyList<string> Related)
{
    internal PlanProblem(PlanProblemKind kind, string serviceName)
        : this(kind, serviceName, [])
    {
    }
}

/// <summary>
/// Everything that would happen, worked out and frozen.
///
/// ADR-11 in one type. A plan can be shown, counted, turned into a command and executed,
/// and those are the same object seen from different sides rather than four features that
/// have to be kept in step. The dry run is not a mode: it is this object, not executed.
///
/// Frozen because it is evidence. An object that can be quietly changed after a person has
/// looked at it stops being a preview of anything.
///
/// Steps are a flat ordered list. Running independent steps together is promised by C2 and
/// belongs to bulk operations in a later phase - here the whole plan concerns one service
/// and its cascade, where there is nothing to run side by side. Written down so the next
/// session knows it was decided rather than forgotten.
/// </summary>
public sealed record OperationPlan
{
    public required ServiceAction Action { get; init; }

    /// <summary>In the order they happen. Empty when <see cref="Problems"/> is not.</summary>
    public required IReadOnlyList<PlanStep> Steps { get; init; }

    public required IReadOnlyList<PlanWarning> Warnings { get; init; }

    /// <summary>Reasons there is no plan. A plan with any of these must never be executed.</summary>
    public required IReadOnlyList<PlanProblem> Problems { get; init; }

    public bool IsRunnable => Problems.Count == 0 && Steps.Count > 0;

    /// <summary>Entries the plan touches that nobody asked about.</summary>
    public IEnumerable<PlanStep> Cascade => Steps.Where(step => step.Reason == StepReason.Cascade);
}
