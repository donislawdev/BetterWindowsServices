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

/// <summary>
/// What came of one step. Sits at the same position as the step it belongs to, and repeats
/// enough of it to be read on its own.
/// </summary>
/// <param name="Outcome">One of the four the glossary names. What a script decides on.</param>
/// <param name="SkippedBecause">
/// Why a step was never attempted, and null for every other outcome. Three quite different
/// stories - already there, an earlier step failed, somebody interrupted - and a script that
/// saw only "skipped" could not tell a machine that is where it should be from one that is
/// half way to somewhere else.
/// </param>
/// <param name="Status">Where the entry was left, in the same words the listing uses.</param>
/// <param name="Milliseconds">How long the step took, waiting included.</param>
internal sealed record StepResultJson(
    string ServiceName,
    string Operation,
    string Reason,
    string Outcome,
    string? SkippedBecause,
    string Status,
    int ErrorCode,
    string? Error,
    long Milliseconds);

internal sealed record PlanJsonShape
{
    public required string Action { get; init; }
    public required string ServiceName { get; init; }
    public required bool IncludeDependents { get; init; }

    /// <summary>
    /// Whether this document is a preview or a record of something that happened. Explicit
    /// rather than inferred from the absence of results, because a reader guessing at that
    /// is a reader who will one day guess wrong about whether a machine was touched.
    /// </summary>
    public required bool DryRun { get; init; }

    public required IReadOnlyList<PlanStepJson> Steps { get; init; }
    public required IReadOnlyList<PlanWarningJson> Warnings { get; init; }

    /// <summary>Null on a preview. Never an empty list, which would read as "ran and did nothing".</summary>
    public required IReadOnlyList<StepResultJson>? Results { get; init; }

    /// <summary>Every entry ended up where the plan wanted it. Null on a preview.</summary>
    public required bool? Completed { get; init; }

    /// <summary>Somebody interrupted the run. Null on a preview.</summary>
    public required bool? Cancelled { get; init; }
}

internal static class PlanJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    internal static string Render(OperationPlan plan) => Render(plan, run: null);

    internal static string Render(PlanRun run) => Render(run.Plan, run);

    private static string Render(OperationPlan plan, PlanRun? run)
    {
        var shape = new PlanJsonShape
        {
            Action = Camel(plan.Action.Kind.ToString()),
            ServiceName = plan.Action.ServiceName,
            IncludeDependents = plan.Action.IncludeDependents,
            DryRun = run is null,

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
            ],

            Results = run is null
                ? null
                : [.. run.Results.Select(result => new StepResultJson(
                    result.Step.ServiceName,
                    Camel(result.Step.Operation.ToString()),
                    Camel(result.Step.Reason.ToString()),
                    Camel(result.Outcome.ToString()),
                    result.SkippedBecause is null ? null : Camel(result.SkippedBecause.Value.ToString()),
                    result.Status.ToString(),
                    result.ErrorCode,
                    result.Error,
                    result.Milliseconds))],

            Completed = run?.Completed,
            Cancelled = run?.Cancelled
        };

        return JsonSerializer.Serialize(shape, Options);
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
