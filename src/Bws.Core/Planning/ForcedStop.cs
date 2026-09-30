namespace Bws.Core.Planning;

/// <summary>
/// The shape of a plan that ends a process: what it drags in, whether it may be built at all, and
/// the steps and sentences that follow.
///
/// <b>Its own class rather than more of <see cref="PlanBuilder"/>, and the size ratchet asked for
/// it before anything was finished - which is the ratchet doing its job rather than getting in the
/// way.</b> The seam is a subject: everything left in that class works out what DEPENDS on what and
/// turns an ask into ordinary steps. This is the one ask that does something the manager cannot
/// refuse on the machine's behalf, and every question it raises - which process, who else lives in
/// it, whether the entry has already been asked politely, whether the machine survives - is a
/// question no other ask has.
///
/// <b>It decides and never writes</b>, exactly like the class it came out of, so a plan that ends
/// the process behind eight hundred entries can be checked without ending anything.
/// </summary>
internal static class ForcedStop
{
    /// <summary>
    /// Everything a plan that ends a process needs and an ordinary one has no use for.
    ///
    /// <b>One value rather than three arguments threaded through four methods</b>, because the
    /// three only ever travel together. Null says the ask does not end anything, which is every ask
    /// but two.
    /// </summary>
    /// <param name="CreatedAt">
    /// When that process started, carried so the step can freeze it beside the number. The two
    /// halves of an identity are only worth anything together, so they travel together from the
    /// moment they are read. Nothing when nobody read it.
    /// </param>
    /// <param name="Recovery">
    /// What the manager does to the entry and each neighbour once the process is gone, read when the
    /// plan was decided and carried to the warnings - since 2026-09-30, <see cref="Aftermath"/>.
    /// </param>
    internal readonly record struct Ending(
        int ProcessId,
        IReadOnlyList<ScmEntry> Sharing,
        bool Immediate,
        long? CreatedAt,
        IReadOnlyList<Aftermath.Recovered> Recovery);

    internal static bool Asked(ActionKind kind) =>
        kind is ActionKind.ForceStop or ActionKind.ForceRestart;

    /// <summary>
    /// Whether this plan can be built, and what it would end.
    ///
    /// <b>Two answers in one return because they are one decision</b> - either there is a process to
    /// name and a list of what dies with it, or there is a reason there is no preview to show.
    ///
    /// <b>The unreadable cascade is read out of the warnings rather than asked again</b>, because
    /// the class that worked out the order already found out and already said so. Asking the
    /// manager a second time would be a second answer to a question with one answer, and the two
    /// could disagree.
    /// </summary>
    /// <param name="blocking">
    /// Every running entry that depends on the target, whether or not the plan stops them - the plan
    /// stops them exactly when the ask carries <see cref="ServiceAction.IncludeDependents"/>.
    /// </param>
    /// <param name="processes">
    /// What to ask about the process and about what its ending sets off - <see cref="NobodyToAsk"/> when
    /// the caller has nothing to ask, which builds the plan exactly as it was before any of it was read.
    /// </param>
    internal static (PlanProblem? Refusal, Ending? Ending) Decide(
        IReadOnlyList<ScmEntry> entries,
        IScmCatalog catalog,
        ScmEntry target,
        IReadOnlyList<ScmEntry> blocking,
        IReadOnlyList<PlanWarning> warnings,
        ServiceAction action,
        IEndingFactsReader processes)
    {
        ThrowIfTheCourtesySkipsTheCascade(action);

        if (ProcessNeighbours.Endable(target) is not { } processId)
        {
            // Nothing to name in the preview, so there is no preview. Four quite different readings
            // arrive here and PlanProblemKind.NoProcessToEnd says why they are one answer.
            return (Because(PlanProblemKind.NoProcessToEnd, target), null);
        }

        // ASKED ONLY ONCE THERE IS A NUMBER, and only for this ask - three handle opens against one process
        // while a plan is built is nothing, and asking the system about process zero to be told there is
        // none would be a call made to learn something the line above already knows.
        var facts = processes.Read(processId);

        // ASKED BEFORE ANYTHING ELSE IS WORKED OUT, AND THAT ORDER IS THE POINT OF RUNG FIVE. Every
        // question below this one is about what else comes down on the way. If the thing at the end
        // of that road cannot be reached at all, working out the road is time spent describing a
        // journey nobody can take - and on a plan that is carried out, it is a machine taken apart
        // to reach something unreachable.
        if (Unreachable(facts, target, processId) is { } unreachable)
        {
            return (unreachable, null);
        }

        if (warnings.Any(warning => warning.Kind == PlanWarningKind.CascadeUnreadable))
        {
            // THE SAME FACT AS THE WARNING AND A HARDER ANSWER, and the difference is what the
            // preview is a preview OF. On an ordinary stop an incomplete cascade means the plan may
            // do more than it shows, which is said out loud and left to a person. Here it is a list
            // of what dies, known to be short, and no wording makes that acceptable.
            return (Because(PlanProblemKind.CascadeUnreadable, target), null);
        }

        if (InTheWay(action, target, blocking) is { } inTheWay)
        {
            return (inTheWay, null);
        }

        IReadOnlyList<ScmEntry> cascade = action.IncludeDependents ? blocking : [];

        // Minus anything the cascade is already taking down, because an entry named twice in one
        // plan is two steps doing one thing - and the second reports "already there" in a report
        // somebody is reading carefully.
        IReadOnlyList<ScmEntry> sharing =
        [
            .. ProcessNeighbours.Of(entries, target)
                .Where(entry => !cascade.Any(other => string.Equals(
                    other.ServiceName, entry.ServiceName, StringComparison.OrdinalIgnoreCase)))
        ];

        if (Needed(catalog, entries, target, cascade, sharing) is { } needed)
        {
            return (needed, null);
        }

        return Aftermath.Weigh(processes, facts, target, sharing, action, processId);
    }

    /// <summary>
    /// Whether Windows has already said no, asked before the plan is worked out rather than found
    /// out by a step that meets it.
    ///
    /// <b>Three answers, and two of them are not the same refusal.</b> A process that has gone is
    /// not a process that will not be ended - one is a machine that moved on and the other is a
    /// permission. Saying "Windows will not let this tool end it" about something that simply is
    /// not there any more would be a confident sentence about the wrong subject, and confident
    /// wrong sentences are what this project spends its documents guarding against.
    ///
    /// <b>Nothing read means nothing said.</b> A plan built with nobody to ask behaves exactly as
    /// it did before this existed: it names the process, it tries, and it finds out. That is a
    /// worse experience and an honest one - what it never does is claim to have checked.
    ///
    /// <b>A process that may be ended is still asked whether the machine survives it</b>, since
    /// 2026-09-30 - the same reading, and the same place in the order, because a critical process is
    /// the other thing at the end of the road that makes working out the road pointless.
    /// </summary>
    private static PlanProblem? Unreachable(EndingFacts facts, ScmEntry target, int processId)
    {
        var rights = facts.CanBeEnded;

        if (rights.Outcome == ReadOutcome.NotRead || (rights.IsPresent && rights.Value))
        {
            return Aftermath.Critical(facts, target);
        }

        if (rights.Outcome == ReadOutcome.Absent)
        {
            // It was there in the listing and it is not there now. The word for that already
            // exists and it is not a word about permissions.
            return Because(PlanProblemKind.NoProcessToEnd, target);
        }

        return new PlanProblem(
            PlanProblemKind.ProcessCannotBeEnded,
            target.ServiceName,
            [],
            ProcessId: processId,
            ErrorCode: rights.ErrorCode,
            Error: rights.Reason);
    }

    /// <summary>
    /// NOT A REFUSAL A PERSON CAN MEET, the same kind of loud as PlanBuilder.ThrowIfNobodyCouldAsk.
    ///
    /// Skipping the courtesy skips every stop in front of the ending, the cascade's too, so a plan
    /// built from this shape promised its dependants stopped and then ended the process under them -
    /// the preview saying one thing and the run doing another, which is rule 5 of the untouchable list
    /// (stability report W-4). The command line refuses the pair before anything is read, and the
    /// window never asks for dependants.
    /// </summary>
    private static void ThrowIfTheCourtesySkipsTheCascade(ServiceAction action)
    {
        if (action is { Immediate: true, IncludeDependents: true })
        {
            throw new ArgumentException(
                "Skipping the courtesy skips the cascade too, so an immediate ask cannot carry its "
                + "dependents. Refuse the pair where it is typed.",
                nameof(action));
        }
    }

    /// <summary>
    /// The running dependants a plan without them would leave standing on a process that is gone.
    ///
    /// <b>A REFUSAL WHERE AN ORDINARY STOP GETS A WARNING, on the owner's decision of 2026-09-29.</b>
    /// The manager refuses that stop with 1051 and nothing is harmed. The last step here asks no
    /// manager, and ends the process under entries still running on it.
    /// </summary>
    private static PlanProblem? InTheWay(ServiceAction action, ScmEntry target, IReadOnlyList<ScmEntry> blocking) =>
        action.IncludeDependents || blocking.Count == 0
            ? null
            : new PlanProblem(
                PlanProblemKind.DependentsInTheWay,
                target.ServiceName,
                [.. blocking.Select(entry => entry.ServiceName)]);

    /// <summary>
    /// Whether something running outside the plan needs an entry that dies only because it shares the
    /// process - and a refusal naming what, when it does.
    ///
    /// <b>The same question <see cref="PlanProblemKind.DependentsInTheWay"/> asks about the target,
    /// asked of its neighbours, and the external stability report (W-6) is why it is asked at all.</b>
    /// Nobody asked about them before 2026-09-29, so a neighbour holding up a running entry was ended
    /// under it without a word in the preview.
    ///
    /// <b>The manager is asked rather than the declarations inverted</b>, for the reason
    /// PlanBuilder.StoppingOrder gives: an entry can depend on a load order group, and the declaration
    /// does not say who belongs to it. One question per neighbour, and 105 of 110 processes on a
    /// measured machine hold one service, so on the ordinary plan there is nobody to ask.
    ///
    /// <b>An unreadable answer is a refusal</b> - a casualty list known to be short, exactly as for the
    /// target's own cascade in <see cref="Decide"/>.
    /// </summary>
    private static PlanProblem? Needed(
        IScmCatalog catalog,
        IReadOnlyList<ScmEntry> entries,
        ScmEntry target,
        IReadOnlyList<ScmEntry> cascade,
        IReadOnlyList<ScmEntry> sharing)
    {
        var dying = new HashSet<string>(
            cascade.Concat(sharing).Append(target).Select(entry => entry.ServiceName),
            StringComparer.OrdinalIgnoreCase);

        var needing = new List<string>();

        foreach (var neighbour in sharing)
        {
            var dependents = catalog.ReadDependents(neighbour.ServiceName);

            if (dependents.Outcome == ReadOutcome.Denied)
            {
                return Because(PlanProblemKind.CascadeUnreadable, target);
            }

            needing.AddRange(Running(entries, dependents).Where(name => !dying.Contains(name)));
        }

        return needing.Count == 0
            ? null
            : new PlanProblem(
                PlanProblemKind.NeighbourNeeded,
                target.ServiceName,
                [.. needing.Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>
    /// The names in a dependants answer that are running on this listing. Absent and unread give
    /// nobody, which is what PlanBuilder.StoppingOrder makes of them too.
    /// </summary>
    private static IEnumerable<string> Running(
        IReadOnlyList<ScmEntry> entries, Reading<IReadOnlyList<string>> dependents) =>
        !dependents.IsPresent
            ? []
            : entries
                .Where(entry => entry.Status != EntryStatus.Stopped
                    && dependents.Value!.Contains(entry.ServiceName, StringComparer.OrdinalIgnoreCase))
                .Select(entry => entry.ServiceName);

    /// <summary>The plain shape, for the reasons that carry nothing but a name.</summary>
    private static PlanProblem Because(PlanProblemKind kind, ScmEntry target) =>
        new(kind, target.ServiceName, []);

    /// <summary>
    /// Ask everything politely, then end what is left.
    ///
    /// <b>The order is the cascade, the entry itself, the neighbours, the ending - and until
    /// 2026-09-29 the neighbours came BEFORE the entry.</b> The external stability report (W-2) found
    /// what that cost: a neighbour refusing its stop was a failed step forward, which skipped the
    /// polite stop of the entry itself, and the process was ended at once. And where the entry would
    /// have stopped politely, the neighbours had been taken down for nothing - the process stays when
    /// its entry stops by itself, and so do they. So the entry is asked first, and the neighbours are
    /// asked only on the way to an ending that is actually going to happen: PlanRunner skips them as
    /// <see cref="SkipReason.ProcessStays"/> otherwise.
    ///
    /// <b>The neighbours were never in anybody's request.</b> They die when the process does, so the
    /// only question is whether they get to close their files on the way - and asking costs one step
    /// in a preview that already names them.
    ///
    /// <b>The entry somebody asked about gets a polite step only if it has not already had one.</b>
    /// From the window's offer under a failure this plan is built after a stop that gave up, so the
    /// entry is sitting in a pending state with a request already in flight. A second one would send
    /// the same thing again and then wait the whole ceiling for it - a minute of somebody's life
    /// spent on an answered question. Asked for outright - `bws kill`, or the window's own Force
    /// stop since 2026-09-16 - the entry has been asked nothing yet, and the polite step is the
    /// first step of the plan.
    ///
    /// <b>The last step is the only one in this product that asks nobody.</b> It carries the process
    /// number so the preview can name it and so the run can check it has not moved.
    /// </summary>
    internal static void AddSteps(
        List<PlanStep> steps,
        IReadOnlyList<ScmEntry> cascade,
        ScmEntry target,
        Ending ending,
        Func<ScmEntry, StepOperation, StepReason, PlanStep> step)
    {
        if (!ending.Immediate)
        {
            foreach (var dependent in cascade)
            {
                steps.Add(step(dependent, StepOperation.Stop, StepReason.Cascade));
            }

            if (!ProcessNeighbours.AlreadyAsked(target))
            {
                steps.Add(step(target, StepOperation.Stop, StepReason.Requested));
            }

            foreach (var sharing in ending.Sharing)
            {
                steps.Add(step(sharing, StepOperation.Stop, StepReason.SharesTheProcess));
            }
        }

        steps.Add(new PlanStep(
            target.ServiceName,
            ServiceDisplayName.Of(target.DisplayName, target.ServiceName),
            StepOperation.Terminate,

            // ASKED FOR WHEN NOTHING PRECEDES IT, AND THAT IS NOT BOOKKEEPING - IT IS WHAT THE
            // PREVIEW SAYS OUT LOUD. An escalation is a step standing BEHIND one that may not
            // work, and the sentence beside it says so. With the courtesy skipped there is no
            // step in front of it and the person asked for exactly this, so calling it an
            // escalation would print "if the stop does not work" over a plan with no stop in it.
            ending.Immediate ? StepReason.Requested : StepReason.Escalation,
            ProcessId: ending.ProcessId,
            ProcessCreatedAt: ending.CreatedAt,
            TakesWithIt: [.. ending.Sharing.Select(entry => entry.ServiceName)]));
    }

    /// <summary>
    /// Everything that comes back up: the entry, its neighbours, then the cascade in the mirror of the
    /// order it went down.
    ///
    /// <b>Two loops rather than a reversal of the plan, and the neighbours are why they were worth
    /// making steps.</b> What has a step going down has a step coming back, worked out here rather
    /// than guessed at afterwards from what happened.
    ///
    /// <b>The entry before its neighbours although it went down before them since 2026-09-29</b> -
    /// the ending takes them down in one moment, so there is no order to mirror between the two, and
    /// the entry somebody asked about is the one worth having back first. The cascade still comes up
    /// last, because every one of them depends on the entry.
    /// </summary>
    internal static void AddRestores(
        List<PlanStep> steps,
        IReadOnlyList<ScmEntry> cascade,
        ScmEntry target,
        Ending ending,
        Func<ScmEntry, StepOperation, StepReason, PlanStep> step)
    {
        steps.Add(step(target, StepOperation.Start, StepReason.Restore));

        for (var index = ending.Sharing.Count - 1; index >= 0; index--)
        {
            steps.Add(step(ending.Sharing[index], StepOperation.Start, StepReason.Restore));
        }

        for (var index = cascade.Count - 1; index >= 0; index--)
        {
            steps.Add(step(cascade[index], StepOperation.Start, StepReason.Restore));
        }
    }

    /// <summary>
    /// The two sentences only this ask produces.
    ///
    /// <b>Neither is the shared-process warning, which does not fire for a forcing ask at all.</b>
    /// That one says the process stays and the neighbours keep running - true of an ordinary stop,
    /// and the exact opposite of what happens here. Two warnings contradicting each other about one
    /// machine on one screen would be worse than either alone.
    /// </summary>
    internal static void AddWarnings(
        List<PlanWarning> warnings, ScmEntry target, IReadOnlyList<ScmEntry> cascade, Ending ending)
    {
        // Said even though every one of them is already a STEP, because the steps say they will be
        // asked to stop and this says what happens to the ones that do not.
        if (ending.Sharing.Count > 0)
        {
            warnings.Add(new PlanWarning(
                PlanWarningKind.TerminationTakesWithIt,
                target.ServiceName,
                [.. ending.Sharing.Select(entry => entry.ServiceName)]));
        }

        // ASKED OF EVERYTHING THAT DIES, NOT OF THE ONE SOMEBODY NAMED. A shared process is exactly
        // where a person takes down an entry the machine needs without ever typing its name.
        // Glossary pitfall P1: this says "you should not", which is a different sentence from "you
        // cannot" and from "confirm that you mean it", and the wording keeps them apart.
        //
        // THE CASCADE TOO, SINCE 2026-09-29 (stability report W-6). The ordinary stop asks it through
        // CriticalEntries.AddWarnings, which does not run for this kind - so a critical entry arriving
        // with --dependents on a forced stop was taken down without the sentence that plan exists to say.
        var critical = CriticalEntries.Named([.. cascade, .. ending.Sharing, target]);

        if (critical.Count > 0)
        {
            warnings.Add(new PlanWarning(PlanWarningKind.CriticalService, target.ServiceName, critical));
        }

        Aftermath.AddWarnings(warnings, target, ending.Recovery);
    }
}
