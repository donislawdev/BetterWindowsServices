namespace Bws.Core.Planning;

/// <summary>What a person asked for, in their own words, before anything was worked out.</summary>
public enum ActionKind
{
    Stop,
    Start,
    Restart,

    /// <summary>
    /// Set what the manager does with this entry at boot - `docs/03` part 4 names it, and the name
    /// is singular on purpose: ONE ask carrying the type it sets, rather than one ask per type.
    /// The owner chose that shape on 2026-08-25 with the alternative beside it.
    ///
    /// <b>It is the first ask in this product that changes CONFIGURATION rather than asking the
    /// manager to move something</b>, and the difference reaches everywhere: no cascade, nothing to
    /// wait for, and nothing that puts it back yet.
    /// </summary>
    SetStartType
}

/// <summary>What one step actually does to one entry.</summary>
public enum StepOperation
{
    Stop,
    Start,

    /// <summary>
    /// Write a start type. The type itself travels on the step - <see cref="PlanStep.To"/>.
    ///
    /// <b>Nine places branched on this enum two ways until 2026-08-25</b>, every one of them
    /// reading "a stop, otherwise a start", including the one that asks the manager to move a
    /// service. All nine refuse an unknown kind now rather than answering for it.
    /// </summary>
    SetStartType
}

/// <summary>Why a step is in the plan, which is the part a person reads first.</summary>
public enum StepReason
{
    /// <summary>The entry that was asked about.</summary>
    Requested,

    /// <summary>Comes along because it breaks otherwise. Nobody asked for it.</summary>
    Cascade,

    /// <summary>
    /// Gives back what an earlier step took down. The whole second half of a restart,
    /// including the start of the service somebody actually asked about.
    ///
    /// This is the reason a run that fails or is interrupted still finishes: a step that
    /// only gives something back can never make things worse by running, and skipping it
    /// leaves a machine trimmed by a plan that did not finish.
    /// </summary>
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
/// <param name="To">
/// The start type an ask of kind <see cref="ActionKind.SetStartType"/> sets, and nothing at all for
/// the other three. Nullable rather than a default value, because "no start type is being set" is a
/// real state and <see cref="StartType.Unknown"/> already means something else - the manager did not
/// say.
/// </param>
public sealed record ServiceAction(
    ActionKind Kind, string ServiceName, bool IncludeDependents = false, StartType? To = null);

/// <summary>One thing that will happen, to one entry.</summary>
/// <param name="To">
/// The start type this step writes, and nothing at all for a stop or a start.
/// </param>
/// <param name="From">
/// The start type the entry had when the plan was built, for a step that writes one.
///
/// <b>READ AT PLAN TIME BECAUSE THERE IS NOWHERE ELSE TO READ IT, and its absence is what kept a
/// start type change from having a way back until 2026-08-25.</b> A step knows what it set. A
/// result knows what came of it. Neither knows what was replaced, so the arithmetic in
/// <see cref="NetEffect"/> had no before to compare against and skipped these steps outright.
///
/// <b>Null is a real answer and not a missing one.</b> The manager can refuse a configuration
/// read - measured on a real machine under a restricted token, 3 refusals over 810 entries - and
/// an entry whose start type could not be read has no before that anybody knows. So does an entry
/// whose previous type this tool has no word for. Both come through as null, and what they buy is
/// silence rather than a wrong way back.
/// </param>
public sealed record PlanStep(
    string ServiceName,
    string DisplayName,
    StepOperation Operation,
    StepReason Reason,
    StartType? To = null,
    StartType? From = null);

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
    CascadeNotOperable,

    /// <summary>
    /// A restart would take something down that it could not start again, because the
    /// entry is disabled.
    ///
    /// Found by restarting a disabled but running service on a real machine: it stopped,
    /// the manager refused to start it back, and the report explained it afterwards. A
    /// warning would not have helped - a command without --dry-run has no moment at which
    /// anybody reads one, so the warning arrives after the outage it describes.
    ///
    /// The line this holds: we never predict whether a start will succeed, because the
    /// manager is the authority on that and the reasons go beyond start type. We do refuse
    /// to take something down when what we already read says we could not put it back.
    /// Stopping such an entry is untouched, because stopping is exactly what was asked for.
    /// </summary>
    CannotComeBack
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
