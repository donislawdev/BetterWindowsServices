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
    SetStartType,

    /// <summary>
    /// Stop it, and end the process behind it if that does not work. `C3` of the specification, in
    /// the shape the owner settled on 2026-09-06.
    ///
    /// <b>THE FIRST ASK IN THIS PRODUCT THAT CAN END A PROCESS, and every other sentence about it
    /// follows from that.</b> Stopping and starting hand the manager a request and let it decide.
    /// This one, at its last step, does not ask anybody - so the plan has to name the process and
    /// everything that dies with it, or the preview is not a preview.
    ///
    /// <b>A kind of its own rather than a flag on <see cref="Stop"/>.</b> The verb carries the blast
    /// radius: somebody reading a shell history, a change ticket or a runbook sees what happened
    /// from the word, where a flag hides it behind one that reads as an ordinary stop. And on
    /// Windows the word FORCE is already taken for services - Stop-Service -Force means "even if
    /// something depends on it", which is what --dependents does here.
    /// </summary>
    ForceStop,

    /// <summary>
    /// The same, and then bring it back.
    ///
    /// <b>Separate from <see cref="ForceStop"/> for the reason <see cref="Restart"/> is separate
    /// from <see cref="Stop"/>:</b> what has to be put back is decided when the plan is built, not
    /// worked out afterwards from what happened. A restart whose stop had to be forced still owes
    /// the machine everything it took down - including the entries that shared the process and were
    /// never asked about.
    /// </summary>
    ForceRestart
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
    SetStartType,

    /// <summary>
    /// End the process behind an entry. The process travels on the step - <see cref="PlanStep.ProcessId"/>.
    ///
    /// <b>The only operation here that does not go through the service control manager at all</b>,
    /// and the only one that cannot be refused by the entry: a process cannot decline to be ended.
    /// Everything else on this enum asks somebody who may say no.
    ///
    /// <b>It still targets Stopped, like a stop</b>, and that is not a technicality. The manager
    /// notices the process die and moves the entry itself, so the step is finished when the entry
    /// says it is - not when the call returns. Win32 documents the call as asynchronous and says a
    /// process with pending driver work cannot exit until that work finishes, so a terminate that
    /// returns success and an entry that never reaches Stopped is a real pair, reported honestly as
    /// a step that ran out of time.
    /// </summary>
    Terminate
}

/// <summary>Why a step is in the plan, which is the part a person reads first.</summary>
public enum StepReason
{
    /// <summary>The entry that was asked about.</summary>
    Requested,

    /// <summary>Comes along because it breaks otherwise. Nobody asked for it.</summary>
    Cascade,

    /// <summary>
    /// Comes along because it lives in the process that is about to end. Nobody asked for it
    /// either, and it is here for an unrelated reason.
    ///
    /// <b>NOT <see cref="Cascade"/>, AND A LIVE MACHINE SHOWED WHY WITHIN A MINUTE OF THE FIRST
    /// PLAN.</b> Ending the process behind RpcSs takes RpcEptMapper with it, and the cascade word
    /// is "would break otherwise" - which says the second entry DEPENDS on the first. It does not.
    /// They share a process, which is a fact about how Windows packed them and nothing about
    /// either one needing the other, and a preview claiming a dependency that is not there is a
    /// preview somebody could reasonably act on.
    ///
    /// <b>It is a stop like any other</b> - asked politely, first, so the entry gets a chance to
    /// close its files before the process it lives in goes away.
    /// </summary>
    SharesTheProcess,

    /// <summary>
    /// Gives back what an earlier step took down. The whole second half of a restart,
    /// including the start of the service somebody actually asked about.
    ///
    /// This is the reason a run that fails or is interrupted still finishes: a step that
    /// only gives something back can never make things worse by running, and skipping it
    /// leaves a machine trimmed by a plan that did not finish.
    /// </summary>
    Restore,

    /// <summary>
    /// The stronger attempt, standing behind one that may not work. `C3`, rung three.
    ///
    /// <b>ONE PROPERTY, AND IT IS THE OPPOSITE OF THE ONE ABOVE.</b> A step marked this way is
    /// still attempted after an earlier step failed to arrive - because failing to arrive is
    /// exactly when it is needed. <see cref="Restore"/> has the same immunity for the opposite
    /// reason: that one runs after a failure because it cannot make anything worse, and this one is
    /// the most harmful step this product has. <b>Folding the two into one word would put "runs
    /// after a failure" and "is harmless" behind a single name, and the second would stop being
    /// true without anybody noticing.</b>
    ///
    /// <b>What keeps it safe is not this flag but the reading before it.</b> Every step is read
    /// before it is attempted, and one whose entry has already reached the state it wanted is
    /// skipped - so an escalation behind a stop that worked in the end does nothing at all.
    ///
    /// <b>An earlier step failing does not hold it back, and somebody asking to stop does.</b>
    /// Those are different asks and they were nearly folded into one: a step before it that did not
    /// go down is exactly the situation this exists for, and the preview named every entry that
    /// dies either way - so declining to run it would deliver LESS than what was shown, which is
    /// the same fault as delivering more. An interruption is the opposite: somebody has said stop,
    /// and ending a process after that would be acting on an instruction that was withdrawn.
    /// </summary>
    Escalation
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
/// <param name="Immediate">
/// Whether to skip asking politely and go straight to ending the process. Meaningless for every
/// kind but the two forcing ones.
///
/// <b>Off by default, and a plan built without it still ends the process</b> - it just asks first
/// and only ends what did not stop. What this buys is the entry's own chance to close its files,
/// which is the whole difference between stopping a service and losing whatever it was writing.
///
/// <b>It changes the PLAN rather than the running of it, and that is the property worth having.</b>
/// A preview taken with it shows one step and a preview taken without it shows several, so the
/// difference is visible before anybody presses anything. An escalation decided while a run is
/// under way could not be shown at all.
/// </param>
public sealed record ServiceAction(
    ActionKind Kind,
    string ServiceName,
    bool IncludeDependents = false,
    StartType? To = null,
    bool Immediate = false);

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
/// <param name="ProcessId">
/// The process a step of kind <see cref="StepOperation.Terminate"/> will end, and nothing at all
/// for the other three.
///
/// <b>READ WHEN THE PLAN IS BUILT AND SHOWN IN THE PREVIEW, because a step that ends a process has
/// to name the process it ends.</b> "Force stop Spooler" is not a preview of anything - the thing
/// that dies is a process, and everything else living in it dies too.
///
/// <b>It is also what the run checks against.</b> Windows hands out process numbers again after a
/// process is gone, and a plan is built at one moment and carried out at another - so the runner
/// reads the number again immediately before ending anything and refuses the step if it moved.
/// That is not the runner working the plan out afresh, which it never does: it is the runner
/// declining to carry out a step that stopped meaning what the preview said.
/// </param>
/// <param name="ProcessCreatedAt">
/// When that process started, read at the same moment as the number, and nothing at all when
/// nobody read it.
///
/// <b>THE OTHER HALF OF AN IDENTITY, AND IT IS CARRIED RATHER THAN SHOWN.</b> A process number on
/// its own stops meaning what the preview said the moment the process behind it exits, because
/// Windows gives numbers out again. The pair is the nearest thing to an identity Windows hands
/// over, and the second half is only worth reading if it travels with the first - so it is frozen
/// into the plan beside it rather than looked up again later.
///
/// <b>Never printed.</b> The preview names the process by number because a person can check that
/// against Task Manager. A file time would be noise carrying no decision.
///
/// <b>Nothing here is a state, not an oversight.</b> A plan built without anything to ask carries
/// no time, and the run then checks the number alone - exactly what it did before this existed.
/// </param>
public sealed record PlanStep(
    string ServiceName,
    string DisplayName,
    StepOperation Operation,
    StepReason Reason,
    StartType? To = null,
    StartType? From = null,
    int? ProcessId = null,
    long? ProcessCreatedAt = null);

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
    AlreadyThere,

    /// <summary>
    /// An entry in this plan does not accept a stop, so the manager will refuse the control
    /// instead of taking it.
    ///
    /// <b>The first warning here that predicts a specific refusal rather than describing a
    /// consequence</b>, and it is the reason the field behind it is read at all. Without it the
    /// plan looked identical whether a stop was going to work or was never going to be accepted,
    /// and the difference only showed up afterwards, as an error number.
    ///
    /// <b>A warning rather than a problem, deliberately.</b> Refusing to build the plan would
    /// decide for somebody who may have asked for a cascade in which this entry is one of
    /// several, and the manager - not us - is the authority on what it will accept by the time
    /// the step actually runs. The line this holds is the same one <c>CannotComeBack</c> draws:
    /// we say what we already read, and we do not predict the manager.
    ///
    /// <see cref="PlanWarning.Related"/> names every entry in the plan this is true of, in the
    /// order their steps happen, because the one in the way is often the cascade rather than the
    /// entry somebody named.
    /// </summary>
    DoesNotAcceptStop,

    /// <summary>
    /// Ending this process ends every other entry living in it, whether or not they stopped first.
    ///
    /// <b>Not the same sentence as <see cref="SharedProcess"/>, which it replaces on a forcing
    /// plan.</b> That one says the process does not go away and the neighbours keep running, which
    /// is true of an ordinary stop and the exact opposite of what happens here. Two warnings whose
    /// wording contradicts each other about the same machine would be worse than either.
    ///
    /// <b>The neighbours are also STEPS on such a plan</b>, asked to stop politely first, so this
    /// warning is about what happens to the ones that do not - which is the same thing either way.
    /// </summary>
    TerminationTakesWithIt,

    /// <summary>
    /// The entry is one the machine does not work without.
    ///
    /// <b>A warning rather than a refusal, on the owner's decision of 2026-09-06.</b> An
    /// administrator has the right to manage their own machine, which is the line `R2` of the
    /// specification already draws - we do not make it harder than the system's own tools do, and
    /// we do not pretend the problem is absent either. What the wording has to carry is the
    /// glossary's distinction `P1`: this is "you should not", which is a different sentence from
    /// "you cannot" and from "confirm that you mean it".
    /// </summary>
    CriticalService
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
    CannotComeBack,

    /// <summary>
    /// There is no process to end.
    ///
    /// <b>Four quite different situations arrive here and all four end the same way</b>, because
    /// what they have in common is the only thing that matters: nothing can be named in the
    /// preview. The manager did not answer where the process is. The manager answered that there
    /// is none, which is ordinary for an entry already stopped. The number is one no service
    /// process ever has - zero, which is what the manager reports for an entry that is not running,
    /// or four, which is the kernel. Or it is this tool's own process.
    ///
    /// <b>Refused when the plan is built rather than checked while it runs</b>, which is where
    /// every other impossible ask in this class is answered. A plan naming a process it cannot name
    /// is the shape `ADR-11` exists to prevent.
    /// </summary>
    NoProcessToEnd,

    /// <summary>
    /// The entries that would die alongside could not be read in full, so the list of them would be
    /// shorter than the truth.
    ///
    /// <b>THE SAME FACT AS <see cref="PlanWarningKind.CascadeUnreadable"/> AND A DIFFERENT ANSWER,
    /// and the difference is what the preview is a preview OF.</b> On an ordinary stop an
    /// incomplete cascade means the plan may do more than it shows, which is said out loud and left
    /// to a person. On a plan that ends a process it is a list of what dies, known to be short -
    /// and rule 5 of the untouchable rules says the preview shows exactly what execution does.
    /// There is no wording that makes an incomplete casualty list acceptable.
    /// </summary>
    CascadeUnreadable,

    /// <summary>
    /// Windows will not let this tool end that process, and it said so before anything was tried.
    ///
    /// <b>RUNG FIVE OF SPECIFICATION <c>C3</c>, WHICH ASKED FOR THIS FROM THE START: say straight
    /// away that it cannot be done, rather than trying.</b> Until 2026-09-08 the tool tried, took
    /// error 5 after the fact, and reported it as a step that failed - which is a true report of a
    /// worse experience, because by then the plan had already asked several other services to stop.
    ///
    /// <b>The question asked is a handle, not a protection level, and that distinction was
    /// measured rather than reasoned.</b> Opening a handle with the right to end a process changes
    /// nothing and answers exactly what the ending needs to know. The protection level also reads
    /// perfectly and does NOT answer it: on 2026-09-08, seven protected processes on one machine
    /// and three refusals, four and two on the other, with one program answering opposite ways on
    /// the two machines at the same level. A refusal built on protection would have turned away
    /// four processes out of seven that this tool can in fact end. See
    /// <c>docs/POMIAR-ZABIJANIE-20260908.md</c> and backlog 320.
    ///
    /// <b>A refusal rather than a warning, for the reason <see cref="CascadeNotOperable"/> gives.</b>
    /// Listing the steps and noting underneath that the last one cannot work would show a machine
    /// being taken apart to reach something unreachable. Rule 5 of the untouchable rules says the
    /// preview shows exactly what execution does, and a preview whose last step is known to be
    /// impossible does not.
    /// </summary>
    ProcessCannotBeEnded
}

/// <summary>A reason there is no plan. Facts only, wording belongs above.</summary>
/// <param name="Kind">Which reason.</param>
/// <param name="ServiceName">The entry somebody asked about.</param>
/// <param name="Related">Other entries the reason names, when it names any.</param>
/// <param name="ProcessId">
/// The process the refusal is about, for <see cref="PlanProblemKind.ProcessCannotBeEnded"/> and
/// nothing else. Zero everywhere else, the way <see cref="PlanStep.To"/> is nothing for every
/// operation but one.
/// </param>
/// <param name="ErrorCode">
/// The system's own number for the refusal, for the same one kind. Kept beside the sentence
/// because the two are for different readers and only the number keeps its meaning across
/// machines - <see cref="Reading{T}.ErrorCode"/> makes the same argument at length.
/// </param>
/// <param name="Error">The system's own words for that number, in the system's language.</param>
public sealed record PlanProblem(
    PlanProblemKind Kind,
    string ServiceName,
    IReadOnlyList<string> Related,
    int ProcessId = 0,
    int ErrorCode = 0,
    string? Error = null)
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
