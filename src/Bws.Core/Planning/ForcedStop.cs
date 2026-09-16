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
    internal readonly record struct Ending(
        int ProcessId, IReadOnlyList<ScmEntry> Sharing, bool Immediate, long? CreatedAt);

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
    internal static (PlanProblem? Refusal, Ending? Ending) Decide(
        IReadOnlyList<ScmEntry> entries,
        ScmEntry target,
        IReadOnlyList<ScmEntry> cascade,
        IReadOnlyList<PlanWarning> warnings,
        bool immediate,
        EndingFacts facts)
    {
        if (ProcessNeighbours.Endable(target) is not { } processId)
        {
            // Nothing to name in the preview, so there is no preview. Four quite different readings
            // arrive here and PlanProblemKind.NoProcessToEnd says why they are one answer.
            return (Because(PlanProblemKind.NoProcessToEnd, target), null);
        }

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

        // Minus anything the cascade is already taking down, because an entry named twice in one
        // plan is two steps doing one thing - and the second reports "already there" in a report
        // somebody is reading carefully.
        IReadOnlyList<ScmEntry> sharing =
        [
            .. ProcessNeighbours.Of(entries, target)
                .Where(entry => !cascade.Any(other => string.Equals(
                    other.ServiceName, entry.ServiceName, StringComparison.OrdinalIgnoreCase)))
        ];

        return (null, new Ending(
            processId,
            sharing,
            immediate,
            facts.Created.IsPresent ? facts.Created.Value : null));
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
    /// </summary>
    private static PlanProblem? Unreachable(EndingFacts facts, ScmEntry target, int processId)
    {
        var rights = facts.CanBeEnded;

        if (rights.Outcome == ReadOutcome.NotRead || (rights.IsPresent && rights.Value))
        {
            return null;
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

    /// <summary>The plain shape, for the reasons that carry nothing but a name.</summary>
    private static PlanProblem Because(PlanProblemKind kind, ScmEntry target) =>
        new(kind, target.ServiceName, []);

    /// <summary>
    /// Ask everything politely, then end what is left.
    ///
    /// <b>The neighbours are asked first and they were never in anybody's request.</b> They die
    /// when the process does, so the only question is whether they get to close their files on the
    /// way - and asking costs one step in a preview that already names them.
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

            foreach (var sharing in ending.Sharing)
            {
                steps.Add(step(sharing, StepOperation.Stop, StepReason.SharesTheProcess));
            }

            if (!ProcessNeighbours.AlreadyAsked(target))
            {
                steps.Add(step(target, StepOperation.Stop, StepReason.Requested));
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
            ProcessCreatedAt: ending.CreatedAt));
    }

    /// <summary>
    /// Everything that comes back up, in the mirror of the order it went down.
    ///
    /// <b>Two loops rather than a reversal of the plan, and the neighbours are why they were worth
    /// making steps.</b> What has a step going down has a step coming back, worked out here rather
    /// than guessed at afterwards from what happened.
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
    internal static void AddWarnings(List<PlanWarning> warnings, ScmEntry target, Ending ending)
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
        var critical = CriticalEntries.Named(ending.Sharing.Append(target));

        if (critical.Count > 0)
        {
            warnings.Add(new PlanWarning(PlanWarningKind.CriticalService, target.ServiceName, critical));
        }
    }
}
