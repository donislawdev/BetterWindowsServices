namespace Bws.Core.Planning;

/// <summary>
/// Turns what a person asked for into everything that would happen.
///
/// Reads and reasons, never writes. That is what makes the interesting half of ADR-11
/// testable without a machine to break: cascade, order and warnings are all decided here,
/// so a test can check the plan instead of checking the wreckage afterwards.
///
/// The listing is handed in rather than fetched, because the caller usually has one
/// already and reading it again would cost a fifth of a second to learn nothing new.
/// </summary>
/// <param name="processes">
/// Something to ask about the process behind an entry, for the one ask that ends one.
///
/// <b>Optional, and the absence is a state rather than a default.</b> Nothing here writes, so
/// every caller that can reach a real machine should hand one over - both of the two in this
/// product do. Without it a forced stop is planned exactly as it was before rung five of
/// specification <c>C3</c> existed: the process is named, the plan is built, and a refusal is
/// discovered by the step that meets it. <see cref="EndingFacts.NobodyAsked"/> is what that looks
/// like from the inside, and it is a value with a name rather than a null threaded through.
/// </param>
public sealed class PlanBuilder(
    IReadOnlyList<ScmEntry> entries,
    IScmCatalog catalog,
    IEndingFactsReader? processes = null)
{
    public OperationPlan Build(ServiceAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (action.Kind == ActionKind.SetStartType && action.To is null)
        {
            // NOT A REFUSAL A PERSON CAN MEET, and that is why it is loud here rather than a
            // problem in the plan. Neither interface can produce it - the command line turns it
            // back in Refusals.AboutTheAsk and the window only ever offers three start types - so
            // a sentence for it would be user-facing text nobody could ever read, which this
            // project treats as a lie of its own.
            //
            // What it is instead is a shape the types allow: PlanStep.To is optional because a
            // stop and a start have no start type to carry, and nothing tied the one operation
            // that needs it to having one. Three places dereference it with a bang, and the worst
            // of them is PlanRunner.Configure - which would throw HALFWAY THROUGH A RUN, after
            // earlier steps had already changed the machine. Same argument as PlanRunner.Run
            // makes about a plan with problems: caught before anything happens rather than
            // discovered while it is happening.
            throw new ArgumentException(
                "Setting a start type needs a start type. A SetStartType action without one cannot "
                + "be planned - the step would have nothing to write.",
                nameof(action));
        }

        // Identity is the service name, compared without case, because that is how Windows
        // compares it. Never the display name, which is translated (ADR-14).
        var target = entries.FirstOrDefault(entry =>
            string.Equals(entry.ServiceName, action.ServiceName, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            return Refuse(action, PlanProblemKind.UnknownService);
        }

        if (target.IsDriver)
        {
            // Deliberately refused rather than attempted. Open question 8 of the product
            // specification asks whether drivers get operations at all, and operating on one
            // is often irreversible without a reboot. Refusing is the answer that can be
            // changed later without having broken anybody's machine in the meantime.
            return Refuse(action, PlanProblemKind.NotOperable);
        }

        var warnings = new List<PlanWarning>();
        var steps = new List<PlanStep>();

        // NOTHING IS IN THE WAY OF A CONFIGURATION CHANGE, and that is not the same sentence as
        // "a start has nothing in the way". A start is unblocked because dependents cannot hold it
        // down - setting a start type does not move the service at all, so the question does not
        // arise. Both end up with an empty list and they get there for different reasons.
        var blocking = action.Kind is ActionKind.Start or ActionKind.SetStartType
            ? []
            : StoppingOrder(target, warnings);

        // Asking to stop one service is not asking to stop seven. Without the word, the
        // ones in the way are named and left alone, and the plan says plainly that the
        // manager will refuse the stop while they run.
        var cascade = action.IncludeDependents ? blocking : [];

        // Found by looking at a real plan rather than by reasoning: stopping BFE on this
        // machine drags in WdNisDrv and wtd, both kernel drivers. Refusing a driver as the
        // target and then quietly listing two of them as steps would be the plan pattern
        // contradicting itself in the one place it is meant to be trusted.
        var driversInTheWay = cascade.Where(entry => entry.IsDriver).ToList();

        if (driversInTheWay.Count > 0)
        {
            return Refuse(
                action,
                PlanProblemKind.CascadeNotOperable,
                [.. driversInTheWay.Select(entry => entry.ServiceName)]);
        }

        // WHAT ENDING A PROCESS DRAGS IN, WORKED OUT BEFORE ANY STEP EXISTS. Everything above is
        // about entries that DEPEND on this one. This is an unrelated question with an unrelated
        // answer - who merely lives in the same process - and on a plan that ends that process it
        // is the casualty list. ForcedStop carries the whole of it.
        ForcedStop.Ending? ending = null;

        if (ForcedStop.Asked(action.Kind))
        {
            var (refusal, decided) = ForcedStop.Decide(
                entries, target, cascade, warnings, action.Immediate, Ask(target));

            if (refusal is { } why)
            {
                return Refuse(action, why);
            }

            ending = decided;
        }

        if (StuckDown(action.Kind, target, cascade) is { Count: > 0 } cannotComeBack)
        {
            return Refuse(action, PlanProblemKind.CannotComeBack, cannotComeBack);
        }

        if (!action.IncludeDependents && blocking.Count > 0)
        {
            warnings.Add(new PlanWarning(
                PlanWarningKind.DependentsInTheWay,
                target.ServiceName,
                [.. blocking.Select(entry => entry.ServiceName)]));
        }

        AddSteps(steps, action, target, cascade, ending);

        AddWarnings(warnings, target, action, cascade, ending);

        return new OperationPlan
        {
            Action = action,
            Steps = steps,
            Warnings = warnings,
            Problems = []
        };
    }


    /// <summary>
    /// The steps one ask turns into, which is the whole of what this class decides.
    ///
    /// <b>Out of Build on 2026-08-25 because an analyser asked</b> - naming the fourth arm of that
    /// switch took the method three lines past the length it allows. The seam is a subject rather
    /// than a line count: everything else in Build is about what is IN THE WAY, and this is what to
    /// do once that is known.
    /// </summary>
    private static void AddSteps(
        List<PlanStep> steps,
        ServiceAction action,
        ScmEntry target,
        IReadOnlyList<ScmEntry> cascade,
        ForcedStop.Ending? ending)
    {
        var to = action.To;

        switch (action.Kind)
        {
            case ActionKind.Stop:
                AddStops(steps, cascade, target);
                break;

            case ActionKind.Start:
                steps.Add(PlanSteps.Made(target, StepOperation.Start, StepReason.Requested));
                break;

            case ActionKind.SetStartType:
                // ONE STEP AND NO CASCADE. Nothing is taken down, nothing comes back, and nothing
                // depends on the order - which is why this arm is one line under a switch whose
                // other arms are four.
                steps.Add(PlanSteps.Made(target, StepOperation.SetStartType, StepReason.Requested, to));
                break;

            case ActionKind.Restart:
                // C11 in four moves: take the dependents down, take the service down, bring
                // it back, put the dependents back. The second half runs in the mirror of
                // the first, because what stopped last has to start first.
                //
                // The service somebody asked about gets Restore for its own start, not
                // Requested, and the difference is not cosmetic. Restore means "gives back
                // what an earlier step took", and everything downstream keys on that: a run
                // that is interrupted or fails still carries these out. Found on a virtual
                // machine on 2026-08-01 by pressing Ctrl+C during a restart, which left the
                // service stopped - the plan had taken it down and then classified putting
                // it back as forward progress to be abandoned.
                AddStops(steps, cascade, target);
                steps.Add(PlanSteps.Made(target, StepOperation.Start, StepReason.Restore));

                for (var index = cascade.Count - 1; index >= 0; index--)
                {
                    steps.Add(PlanSteps.Made(cascade[index], StepOperation.Start, StepReason.Restore));
                }

                break;

            case ActionKind.ForceStop:
                ForcedStop.AddSteps(steps, cascade, target, ending!.Value, PlanSteps.Moving);
                break;

            case ActionKind.ForceRestart:
                // THE MIRROR COSTS ONE LINE BECAUSE THE STOPPING HALF ALREADY PUT EVERY CASUALTY IN
                // THE PLAN AS A STEP. That was the fourth argument for making the neighbours steps
                // rather than a footnote: what has a step going down has a step coming back.
                ForcedStop.AddSteps(steps, cascade, target, ending!.Value, PlanSteps.Moving);
                ForcedStop.AddRestores(steps, cascade, target, ending.Value, PlanSteps.Moving);
                break;

            default:
                // A KIND NOBODY TAUGHT THIS BUILDER USED TO GET A RESTART, which is four steps on a
                // real machine that nobody asked for. It refuses here instead - the ask has to be
                // given a plan in the place that builds plans.
                throw new ArgumentOutOfRangeException(
                    nameof(action), action.Kind, EquivalentCommand.Unhandled);
        }
    }


    /// <summary>
    /// Everything a restart would take down and could not put back, and nothing at all for an
    /// ask that puts nothing back.
    ///
    /// <b>Out of Build 2026-09-06 because the analyser asked, and the seam is the same one it
    /// found last time:</b> everything left in Build is about what is IN THE WAY, and this is a
    /// question about the way back. A disabled entry that is running can be stopped and cannot
    /// be started again, so a restart of it is an outage dressed as a round trip.
    ///
    /// <b>Only where the answer is known.</b> An unreadable start type is not a reason to
    /// refuse - that would turn missing information into a decision, which is the opposite of
    /// what the four read outcomes exist for.
    /// </summary>
    private static List<string> StuckDown(
        ActionKind kind, ScmEntry target, IReadOnlyList<ScmEntry> cascade) =>
        kind is ActionKind.Restart or ActionKind.ForceRestart
            ? [.. cascade
                .Append(target)
                .Where(entry => entry.StartType is { IsPresent: true, Value: StartType.Disabled })
                .Select(entry => entry.ServiceName)]
            : [];

    private static void AddStops(List<PlanStep> steps, IReadOnlyList<ScmEntry> cascade, ScmEntry target)
    {
        foreach (var dependent in cascade)
        {
            steps.Add(PlanSteps.Made(dependent, StepOperation.Stop, StepReason.Cascade));
        }

        steps.Add(PlanSteps.Made(target, StepOperation.Stop, StepReason.Requested));
    }

    /// <summary>
    /// The entries that have to stop before this one can, in the order they have to do it.
    ///
    /// The manager is asked who breaks, rather than us inverting what everything declares,
    /// because an entry can declare a load order group and the declaration does not say who
    /// belongs to it. Measured on 2026-08-01: the manager's answer already reaches past the
    /// first hop, so asking once about the target is enough to find the whole set.
    ///
    /// The order, though, is worked out here rather than taken from the order the manager
    /// happened to return them in. That order looked right in every sample and is not
    /// promised anywhere, and a stop order that is right by luck fails on somebody else's
    /// machine at the worst moment.
    /// </summary>
    private List<ScmEntry> StoppingOrder(ScmEntry target, List<PlanWarning> warnings)
    {
        var dependents = catalog.ReadDependents(target.ServiceName);

        if (dependents.Outcome == ReadOutcome.Denied)
        {
            // Rule 8. A preview that is shorter than what will happen is the worst thing
            // this pattern can produce, so an unreadable cascade is said out loud rather
            // than rendered as an empty one.
            warnings.Add(new PlanWarning(PlanWarningKind.CascadeUnreadable, target.ServiceName));
            return [];
        }

        if (!dependents.IsPresent)
        {
            return [];
        }

        // Only what is actually running. Stopping something already stopped is not a step,
        // it is noise in a preview somebody has to read carefully.
        //
        // ONE ENTRY HOWEVER MANY TIMES THE MANAGER NAMED IT, SINCE 2026-09-03 - backlog 305. The
        // list below is whatever EnumDependentServices returned, and nothing promises the names in
        // it are distinct or that two of them differ by more than case. A repeat used to reach
        // Order, where the entries are put in a dictionary by name, and a dictionary meets a
        // repeated key with an ArgumentException - so a name the manager happened to say twice
        // turned the whole preview into a sentence about a failure, in a panel and in a terminal,
        // with nothing to say it was the preview rather than the machine.
        //
        // Dropping the repeat rather than the step, which is the distinction BulkPlanBuilder
        // draws about its own input: this is one entry described twice, not two things to do.
        var running = dependents.Value!
            .Select(Find)
            .OfType<ScmEntry>()
            .Where(entry => entry.Status != EntryStatus.Stopped)
            .DistinctBy(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Order(running);
    }

    /// <summary>
    /// Puts the cascade in the order it has to happen: an entry may stop only once everything
    /// that depends on it has stopped.
    ///
    /// <b>The rule itself moved to <see cref="DependentsFirst"/> on 2026-08-18, because the bulk
    /// plan of `C2` asks it about a SELECTION as well as about a cascade.</b> Identical question,
    /// and two implementations of it would be two things that have to agree about which service
    /// goes down first - where disagreeing means an outage rather than an oddity.
    ///
    /// What stays here is the mapping back to entries, because the steps carry a display name and
    /// the ordering has no business knowing there is such a thing.
    /// </summary>
    private List<ScmEntry> Order(List<ScmEntry> cascade)
    {
        var byName = cascade.ToDictionary(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase);

        return
        [
            .. DependentsFirst
                .Order(catalog, [.. cascade.Select(entry => entry.ServiceName)])
                .Select(name => byName[name])
        ];
    }

    private void AddWarnings(
        List<PlanWarning> warnings,
        ScmEntry target,
        ServiceAction action,
        IReadOnlyList<ScmEntry> cascade,
        ForcedStop.Ending? ending)
    {
        if (cascade.Count > 0)
        {
            warnings.Add(new PlanWarning(
                PlanWarningKind.Cascade,
                target.ServiceName,
                [.. cascade.Select(entry => entry.ServiceName)]));
        }

        var alreadyThere = action.Kind switch
        {
            ActionKind.Stop => target.Status == EntryStatus.Stopped,
            ActionKind.Start => target.Status == EntryStatus.Running,

            // Only where the start type could be READ. An unreadable one is not "already that",
            // and saying so would turn missing information into a claim - the thing the four read
            // outcomes exist to prevent.
            ActionKind.SetStartType =>
                target.StartType is { IsPresent: true } kept && kept.Value == action.To,

            _ => false
        };

        if (alreadyThere)
        {
            warnings.Add(new PlanWarning(PlanWarningKind.AlreadyThere, target.ServiceName));
        }

        // A SHARED PROCESS MATTERS WHEN SOMETHING IS BEING STOPPED, and a start type is not that.
        // Without this the warning would tell somebody that changing a setting leaves the
        // neighbours running, which is true, irrelevant, and exactly the kind of sentence that
        // teaches people to stop reading warnings.
        if (action.Kind is ActionKind.Stop or ActionKind.Restart
            && target.EntryType == EntryType.SharedProcess)
        {
            var neighbours = entries
                .Where(entry => entry.ProcessId.IsPresent
                    && target.ProcessId.IsPresent
                    && entry.ProcessId.Value == target.ProcessId.Value
                    && !string.Equals(entry.ServiceName, target.ServiceName, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.ServiceName)
                .ToList();

            if (neighbours.Count > 0)
            {
                // Worth saying because the obvious mental model is wrong: stopping one of
                // these does not free the process, and the neighbours keep running in it.
                warnings.Add(new PlanWarning(PlanWarningKind.SharedProcess, target.ServiceName, neighbours));
            }
        }

        CriticalEntries.AddWarnings(warnings, target, action, cascade);

        // WHAT THE MANAGER IS GOING TO SAY, SAID BEFORE IT SAYS IT - backlog 8. An entry that does
        // not accept a stop refuses the control outright, so the step fails rather than times out,
        // and until this was read the plan had no way of telling those two apart in advance.
        //
        // The cascade is asked as well as the target, and it is the more useful half: an entry in
        // the way that will not take a stop makes every step after it in the plan unreachable, and
        // that is worth knowing before the first one runs rather than after.
        //
        // Only where the answer is PRESENT. Absent means the entry is already stopped, where the
        // question does not arise - and turning that into a warning would put a sentence about a
        // refusal next to a step that is going to be skipped for having nothing to do.
        if (action.Kind is ActionKind.Stop or ActionKind.Restart
                or ActionKind.ForceStop or ActionKind.ForceRestart)
        {
            var refusing = cascade
                .Append(target)
                .Where(entry => entry.AcceptsStop is { IsPresent: true, Value: false })
                .Select(entry => entry.ServiceName)
                .ToList();

            if (refusing.Count > 0)
            {
                warnings.Add(new PlanWarning(
                    PlanWarningKind.DoesNotAcceptStop, target.ServiceName, refusing));
            }
        }

        // Glossary pitfall P7. Somebody who stops an automatic service and walks away has
        // done something that lasts until the next boot, which is rarely what they meant.
        if (action.Kind is ActionKind.Stop or ActionKind.ForceStop
            && target.StartType is { IsPresent: true, Value: StartType.Automatic })
        {
            warnings.Add(new PlanWarning(PlanWarningKind.ReturnsAfterReboot, target.ServiceName));
        }

        // THE TWO SENTENCES ONLY A FORCING ASK PRODUCES, and neither of them is the shared process
        // warning above - that one does not fire for these kinds at all, because it says the
        // neighbours keep running and here they do not.
        if (ending is { } dies)
        {
            ForcedStop.AddWarnings(warnings, target, dies);
        }
    }

    private ScmEntry? Find(string serviceName) =>
        entries.FirstOrDefault(entry =>
            string.Equals(entry.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// What the process behind this entry will say about itself, asked once, for the one ask that
    /// ends one.
    ///
    /// <b>ASKED HERE AND NOWHERE ELSE, AND ONLY FOR THAT ASK.</b> Two handle opens against one
    /// process while a plan is built is nothing. The same two against every running entry on every
    /// listing would be a cost on the path a person waits for, paid for a pair of facts that are
    /// different a second later - which is the argument the specification already makes about
    /// memory, and the reason memory is off unless somebody asks for it.
    ///
    /// <b>Not asked at all when there is no number to ask about.</b> An entry that is not running
    /// has no process, and <see cref="ForcedStop.Decide"/> has a word for that already - asking
    /// the operating system about process zero to be told so would be a call made to learn
    /// something this class already knows.
    /// </summary>
    private EndingFacts Ask(ScmEntry target) =>
        processes is not null && ProcessNeighbours.Endable(target) is { } endable
            ? processes.Read(endable)
            : EndingFacts.NobodyAsked();

    private static OperationPlan Refuse(
        ServiceAction action, PlanProblemKind kind, IReadOnlyList<string>? related = null) => new()
    {
        Action = action,
        Steps = [],
        Warnings = [],
        Problems = [new PlanProblem(kind, action.ServiceName, related ?? [])]
    };

    /// <summary>
    /// The same refusal, for a reason that arrives already made.
    ///
    /// <b>One place decides why there is no plan for a forced stop, and it is not this class.</b>
    /// <see cref="ForcedStop"/> knows the process number and the system's own words for the
    /// refusal, and rebuilding the problem here out of pieces passed up would be a second place
    /// that has to agree with the first.
    /// </summary>
    private static OperationPlan Refuse(ServiceAction action, PlanProblem problem) => new()
    {
        Action = action,
        Steps = [],
        Warnings = [],
        Problems = [problem]
    };
}
