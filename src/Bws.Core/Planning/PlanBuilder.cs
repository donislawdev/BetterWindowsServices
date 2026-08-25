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
public sealed class PlanBuilder(IReadOnlyList<ScmEntry> entries, IScmCatalog catalog)
{
    public OperationPlan Build(ServiceAction action)
    {
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
        // down; setting a start type does not move the service at all, so the question does not
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

        if (action.Kind == ActionKind.Restart)
        {
            // Everything a restart has to put back: the entry asked about, and whatever the
            // cascade takes down on the way to it. A disabled one cannot come back, so the
            // restart would be an outage dressed as a round trip.
            //
            // Only where the answer is known. An unreadable start type is not a reason to
            // refuse - that would turn missing information into a decision, which is the
            // opposite of what the four read outcomes exist for.
            var stuckDown = cascade
                .Append(target)
                .Where(entry => entry.StartType is { IsPresent: true, Value: StartType.Disabled })
                .Select(entry => entry.ServiceName)
                .ToList();

            if (stuckDown.Count > 0)
            {
                return Refuse(action, PlanProblemKind.CannotComeBack, stuckDown);
            }
        }

        if (!action.IncludeDependents && blocking.Count > 0)
        {
            warnings.Add(new PlanWarning(
                PlanWarningKind.DependentsInTheWay,
                target.ServiceName,
                [.. blocking.Select(entry => entry.ServiceName)]));
        }

        AddSteps(steps, action.Kind, target, cascade, action.To);

        AddWarnings(warnings, target, action, cascade);

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
        ActionKind kind,
        ScmEntry target,
        IReadOnlyList<ScmEntry> cascade,
        StartType? to)
    {
        switch (kind)
        {
            case ActionKind.Stop:
                AddStops(steps, cascade, target);
                break;

            case ActionKind.Start:
                steps.Add(Step(target, StepOperation.Start, StepReason.Requested));
                break;

            case ActionKind.SetStartType:
                // ONE STEP AND NO CASCADE. Nothing is taken down, nothing comes back, and nothing
                // depends on the order - which is why this arm is one line under a switch whose
                // other arms are four.
                steps.Add(Step(target, StepOperation.SetStartType, StepReason.Requested, to));
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
                steps.Add(Step(target, StepOperation.Start, StepReason.Restore));

                for (var index = cascade.Count - 1; index >= 0; index--)
                {
                    steps.Add(Step(cascade[index], StepOperation.Start, StepReason.Restore));
                }

                break;

            default:
                // A KIND NOBODY TAUGHT THIS BUILDER USED TO GET A RESTART, which is four steps on a
                // real machine that nobody asked for. It refuses here instead - the ask has to be
                // given a plan in the place that builds plans.
                throw new ArgumentOutOfRangeException(
                    nameof(kind), kind, EquivalentCommand.Unhandled);
        }
    }
    private static void AddStops(List<PlanStep> steps, IReadOnlyList<ScmEntry> cascade, ScmEntry target)
    {
        foreach (var dependent in cascade)
        {
            steps.Add(Step(dependent, StepOperation.Stop, StepReason.Cascade));
        }

        steps.Add(Step(target, StepOperation.Stop, StepReason.Requested));
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
        var running = dependents.Value!
            .Select(Find)
            .OfType<ScmEntry>()
            .Where(entry => entry.Status != EntryStatus.Stopped)
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
        List<PlanWarning> warnings, ScmEntry target, ServiceAction action, IReadOnlyList<ScmEntry> cascade)
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

        // Glossary pitfall P7. Somebody who stops an automatic service and walks away has
        // done something that lasts until the next boot, which is rarely what they meant.
        if (action.Kind == ActionKind.Stop
            && target.StartType is { IsPresent: true, Value: StartType.Automatic })
        {
            warnings.Add(new PlanWarning(PlanWarningKind.ReturnsAfterReboot, target.ServiceName));
        }
    }

    private ScmEntry? Find(string serviceName) =>
        entries.FirstOrDefault(entry =>
            string.Equals(entry.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));

    private static PlanStep Step(
        ScmEntry entry, StepOperation operation, StepReason reason, StartType? to = null) =>
        new(entry.ServiceName, entry.DisplayName, operation, reason, to);

    private static OperationPlan Refuse(
        ServiceAction action, PlanProblemKind kind, IReadOnlyList<string>? related = null) => new()
    {
        Action = action,
        Steps = [],
        Warnings = [],
        Problems = [new PlanProblem(kind, action.ServiceName, related ?? [])]
    };
}
