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

        var cascade = action.Kind == ActionKind.Start
            ? []
            : StoppingOrder(target, warnings);

        switch (action.Kind)
        {
            case ActionKind.Stop:
                AddStops(steps, cascade, target);
                break;

            case ActionKind.Start:
                steps.Add(Step(target, StepOperation.Start, StepReason.Requested));
                break;

            default:
                // C11 in four moves: take the dependents down, take the service down, bring
                // it back, put the dependents back. The second half runs in the mirror of
                // the first, because what stopped last has to start first.
                AddStops(steps, cascade, target);
                steps.Add(Step(target, StepOperation.Start, StepReason.Requested));

                for (var index = cascade.Count - 1; index >= 0; index--)
                {
                    steps.Add(Step(cascade[index], StepOperation.Start, StepReason.Restore));
                }

                break;
        }

        AddWarnings(warnings, target, action, cascade);

        return new OperationPlan
        {
            Action = action,
            Steps = steps,
            Warnings = warnings,
            Problems = []
        };
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
    /// Puts the cascade in the order it has to happen: an entry may stop only once
    /// everything that depends on it has stopped.
    ///
    /// Costs one question per member. That is affordable because a cascade is small - and
    /// where it is not, the plan is about to take down half the machine and a moment spent
    /// getting the order right is not the expensive part.
    /// </summary>
    private List<ScmEntry> Order(List<ScmEntry> cascade)
    {
        var blockedBy = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var names = cascade.Select(entry => entry.ServiceName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in cascade)
        {
            var theirs = catalog.ReadDependents(entry.ServiceName);

            blockedBy[entry.ServiceName] = theirs.IsPresent
                ? [.. theirs.Value!.Where(names.Contains)]
                : [];
        }

        var ordered = new List<ScmEntry>(cascade.Count);
        var remaining = new List<ScmEntry>(cascade);
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (remaining.Count > 0)
        {
            var ready = remaining.FirstOrDefault(entry => blockedBy[entry.ServiceName].All(done.Contains));

            // A cycle would leave nothing ready. None was ever observed, and looping forever
            // over one is a far worse answer than an order the manager will reject with a
            // message the step outcome can report.
            ready ??= remaining[0];

            ordered.Add(ready);
            done.Add(ready.ServiceName);
            remaining.Remove(ready);
        }

        return ordered;
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
            _ => false
        };

        if (alreadyThere)
        {
            warnings.Add(new PlanWarning(PlanWarningKind.AlreadyThere, target.ServiceName));
        }

        if (action.Kind != ActionKind.Start && target.EntryType == EntryType.SharedProcess)
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

    private static PlanStep Step(ScmEntry entry, StepOperation operation, StepReason reason) =>
        new(entry.ServiceName, entry.DisplayName, operation, reason);

    private static OperationPlan Refuse(ServiceAction action, PlanProblemKind kind) => new()
    {
        Action = action,
        Steps = [],
        Warnings = [],
        Problems = [new PlanProblem(kind, action.ServiceName)]
    };
}
