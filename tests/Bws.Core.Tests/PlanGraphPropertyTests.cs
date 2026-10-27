using Bws.Core.Planning;

namespace Bws.Core.Tests;

/// <summary>
/// A plan built over any dependency graph, checked against rules stated here in words rather than
/// read off the builder.
///
/// <b>Why this exists at all.</b> Every plan test until 2026-10-17 was built over one chain measured on
/// a real machine (MRxSmb20, LanmanWorkstation and two dependants) and a handful of hand-made variants.
/// The graph is someone else's input as much as any text is - every installer writes it - and the
/// shapes nobody wrote out by hand are the ones a real machine eventually has: a dependant the
/// manager names twice, one it names that the listing never saw, an answer refused, a process shared
/// by four entries that need each other.
///
/// <b>The oracle never asks the builder what it decided.</b> What counts as needing is what the
/// generated manager says (<see cref="Graph.Names"/>), what counts as running is the listing, and the
/// rules are the ones the plan pattern promises: nothing is done twice, nothing is taken down before
/// the things that need it, nothing is put back that the plan did not take down, the cascade is
/// exactly the running dependants the manager named, and an answer that could not be read is said.
/// </summary>
public sealed class PlanGraphPropertyTests
{
    [Fact]
    [Trait("search", "weekly")]
    public void A_plan_over_any_dependency_graph_does_each_thing_once_and_nothing_before_what_needs_it()
    {
        Graphs.Any.Searched(
            graph => Wrong(graph, Built(graph)) is null,
            new(10_000) { Print = graph => $"{Wrong(graph, Built(graph))} - {graph}" });
    }

    private static OperationPlan Built(Graph graph) =>
        new PlanBuilder(graph.Entries, graph.Catalog()).Build(graph.Ask);

    private static string? Wrong(Graph graph, OperationPlan plan) =>
        Shapeless(plan)
        ?? Twice(plan)
        ?? Unlisted(graph, plan)
        ?? StoppedTooSoon(graph, plan)
        ?? StartedTooSoon(graph, plan)
        ?? PutBackUntaken(plan)
        ?? CascadeWrong(graph, plan)
        ?? Unsaid(graph, plan);

    /// <summary>A plan is steps or a reason, never both and never neither.</summary>
    private static string? Shapeless(OperationPlan plan) => (plan.Problems.Count, plan.Steps.Count) switch
    {
        ( > 0, > 0) => "refused and still holding steps",
        (0, 0) => "neither a step nor a reason",
        _ => null
    };

    /// <summary>
    /// One entry, one operation, once - the rule backlog 305 wrote for a name the manager repeats. Found
    /// the target in its own cascade over a cycle on its first run, 2026-10-17 (finding M1, fixed in
    /// PlanBuilder.StoppingOrder on the owner's decision).
    /// </summary>
    private static string? Twice(OperationPlan plan) =>
        plan.Steps
            .GroupBy(step => (Name: step.ServiceName.ToUpperInvariant(), step.Operation))
            .FirstOrDefault(same => same.Count() > 1) is { } repeated
            ? $"{repeated.Key.Name} {repeated.Key.Operation} {repeated.Count()} times"
            : null;

    private static string? Unlisted(Graph graph, OperationPlan plan) =>
        plan.Steps.FirstOrDefault(step => graph.Listed(step.ServiceName) is null) is { } stranger
            ? $"a step for {stranger.ServiceName}, which the listing does not have"
            : null;

    /// <summary>
    /// Whatever needs an entry stops before it - the cascade, the target and the neighbours in a process
    /// being ended alike, because the manager refuses to stop any of them while something needing it
    /// runs. Only where the manager said so one way and not the other, because inside a cycle both orders
    /// break a promise and neither is wrong.
    ///
    /// <b>Found two faults on its first runs, 2026-10-17, both fixed on the owner's decision</b>: a cycle
    /// among the dependants put an entry the cycle needed first (finding C, DependentsFirst), and the
    /// neighbours stood in the order of the listing (finding B, ForcedStop.InOrder).
    /// </summary>
    private static string? StoppedTooSoon(Graph graph, OperationPlan plan) =>
        FirstPair([.. plan.Steps.Where(step => step.Operation == StepOperation.Stop).Select(step => step.ServiceName)], (earlier, later) =>
            graph.Names(earlier, later) && !graph.Names(later, earlier)
                ? $"{earlier} stopped before {later}, which needs it"
                : null);

    /// <summary>
    /// The mirror for putting back: an entry starts before whatever needs it. Not asked of the neighbours
    /// in a process being ended, and that is not leniency: the manager starts what a service needs
    /// before the service itself (Microsoft Learn, the remarks on Win32_Service.StartService), so a
    /// neighbour started late is found already running rather than refused.
    /// </summary>
    private static string? StartedTooSoon(Graph graph, OperationPlan plan) =>
        FirstPair(Moving(plan, StepOperation.Start), (earlier, later) =>
            graph.Names(later, earlier) && !graph.Names(earlier, later)
                ? $"{earlier} started before {later}, which it needs"
                : null);

    private static List<string> Moving(OperationPlan plan, StepOperation operation)
    {
        var neighbours = plan.Steps
            .SelectMany(step => step.Reason == StepReason.SharesTheProcess ? [step.ServiceName] : step.TakesWithIt ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. plan.Steps
            .Where(step => step.Operation == operation && !neighbours.Contains(step.ServiceName))
            .Select(step => step.ServiceName)];
    }

    private static string? FirstPair(List<string> names, Func<string, string, string?> judged)
    {
        for (var earlier = 0; earlier < names.Count; earlier++)
        {
            for (var later = earlier + 1; later < names.Count; later++)
            {
                if (judged(names[earlier], names[later]) is { } wrong)
                {
                    return wrong;
                }
            }
        }

        return null;
    }

    /// <summary>A step putting an entry back has a step before it that took that entry down.</summary>
    private static string? PutBackUntaken(OperationPlan plan) =>
        plan.Steps
            .Select((step, at) => (step, at))
            .Where(pair => pair.step.Reason == StepReason.Restore)
            .FirstOrDefault(pair => !plan.Steps.Take(pair.at).Any(before => TakesDown(before, pair.step.ServiceName)))
            is { step: not null } untaken
            ? $"{untaken.step.ServiceName} put back without a step taking it down"
            : null;

    private static bool TakesDown(PlanStep step, string name) =>
        (step.Operation is StepOperation.Stop or StepOperation.Terminate && Same(step.ServiceName, name))
        || (step.Operation == StepOperation.Terminate && (step.TakesWithIt ?? []).Contains(name, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// The cascade is exactly the running entries the manager named as needing the target, each once,
    /// and none at all where nobody asked for one or the target is not being stopped - or where the ask is
    /// a startup setting, whose stop never carries one since 2026-10-26 (logic report R08).
    /// </summary>
    private static string? CascadeWrong(Graph graph, OperationPlan plan)
    {
        var cascade = plan.Cascade.Select(step => step.ServiceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expected = Expected(graph, plan);

        return cascade.SetEquals(expected)
            ? null
            : $"cascade [{string.Join(" ", cascade)}] where the running dependants named were [{string.Join(" ", expected)}]";
    }

    private static HashSet<string> Expected(Graph graph, OperationPlan plan)
    {
        var target = graph.Listed(graph.Ask.ServiceName);

        if (target is null
            || !graph.Ask.IncludeDependents
            || graph.Ask.Kind == ActionKind.SetStartType
            || !StopsTarget(plan, target)
            || graph.Answers[target.ServiceName] is not { IsPresent: true } answer)
        {
            return [];
        }

        return answer.Value!
            .Select(graph.Listed)
            .OfType<ScmEntry>()
            .Where(entry => entry.Status != EntryStatus.Stopped && !Same(entry.ServiceName, target.ServiceName))
            .Select(entry => entry.ServiceName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Rule 8 in a preview: a plan stopping the target over an answer it could not read says so.</summary>
    private static string? Unsaid(Graph graph, OperationPlan plan) =>
        graph.Listed(graph.Ask.ServiceName) is { } target
        && StopsTarget(plan, target)
        && graph.Answers[target.ServiceName].Outcome == ReadOutcome.Denied
        && plan.Warnings.All(warning => warning.Kind != PlanWarningKind.CascadeUnreadable)
            ? "stops the target over a cascade nobody could read, and does not say so"
            : null;

    private static bool StopsTarget(OperationPlan plan, ScmEntry target) =>
        plan.Steps.Any(step => Same(step.ServiceName, target.ServiceName)
            && step.Operation is StepOperation.Stop or StepOperation.Terminate);

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
