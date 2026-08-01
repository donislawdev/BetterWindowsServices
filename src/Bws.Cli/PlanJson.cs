using System.Text.Json;
using System.Text.Json.Serialization;
using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// The machine readable shape of a plan. A PUBLIC CONTRACT, like the listing: this is what
/// a runbook diffs and what a change process attaches to a ticket.
///
/// Warnings carry their kind as well as their sentence. The sentence is for a person and
/// may be reworded, the kind is for a script deciding whether to go ahead, and a script
/// that had to match on English prose would break the first time the wording improved.
/// </summary>
internal sealed record PlanStepJson(string ServiceName, string DisplayName, string Operation, string Reason);

internal sealed record PlanWarningJson(string Kind, string ServiceName, IReadOnlyList<string> Related, string Message);

internal sealed record PlanJsonShape
{
    public required string Action { get; init; }
    public required string ServiceName { get; init; }
    public required bool IncludeDependents { get; init; }
    public required IReadOnlyList<PlanStepJson> Steps { get; init; }
    public required IReadOnlyList<PlanWarningJson> Warnings { get; init; }
}

internal static class PlanJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    internal static string Render(OperationPlan plan)
    {
        var shape = new PlanJsonShape
        {
            Action = Camel(plan.Action.Kind.ToString()),
            ServiceName = plan.Action.ServiceName,
            IncludeDependents = plan.Action.IncludeDependents,

            Steps =
            [
                .. plan.Steps.Select(step => new PlanStepJson(
                    step.ServiceName,
                    step.DisplayName,
                    Camel(step.Operation.ToString()),
                    Camel(step.Reason.ToString())))
            ],

            Warnings =
            [
                .. plan.Warnings.Select(warning => new PlanWarningJson(
                    Camel(warning.Kind.ToString()),
                    warning.ServiceName,
                    warning.Related,
                    PlanText.Describe(warning)))
            ]
        };

        return JsonSerializer.Serialize(shape, Options);
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
