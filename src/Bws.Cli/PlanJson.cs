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
/// <param name="StartType">
/// The type a step of that kind writes, and null for every other step.
///
/// <b>ADDED 2026-08-25, THE DAY THE COMMAND LINE LEARNED THE VERB, AND docs/02 SAID IT WOULD BE.</b>
/// The kinds of step grew a fourth on that day and it could not reach this document, because the
/// only interface that could ask for one was the window. A verb here means a plan can carry one, and
/// a step reading only "setStartType" would leave out the whole of what it does.
///
/// <b>Written as null rather than left out, like <see cref="StepResultJson.SkippedBecause"/> beside
/// it.</b> A reader that has to tell "no start type is involved" from "the field is missing" is a
/// reader who will one day guess, and this document is what a change process attaches to a ticket.
/// Every step gains the field and three quarters of them carry null - which is the shape this file
/// already uses everywhere else.
/// </param>
/// <param name="DelayedAuto">
/// Whether that step makes the entry start late, and null for every other step.
///
/// <b>A FIELD BESIDE startType RATHER THAN A FIFTH VALUE IN IT, the owner's decision of 2026-09-24.</b>
/// The listing has always said a late automatic entry as <c>"startType": "Automatic"</c> with
/// <c>"delayedAuto": true</c>, and the script reading this is the same script (docs/02, the row on
/// the shape of a plan). A value "AutomaticDelayed" here would be the one place that script ever met
/// it. Every setting writes the flag since that day - false for all but one - so a step of that kind
/// always carries true or false here, and an addition to the document changes no field it had.
/// </param>
internal sealed record PlanStepJson(
    string ServiceName, string DisplayName, string Operation, string Reason, string? StartType, bool? DelayedAuto);

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
/// <param name="StartType">
/// The type this step wrote, and null for every other step.
///
/// Repeated from the step for the reason everything else here is repeated from it: a result sits
/// beside its step and has to be readable on its own. A run of four results where one of them set
/// something would otherwise say what happened without saying what was asked.
/// </param>
/// <param name="ProcessId">
/// Which process was holding the entry, as last seen, or null.
///
/// <b>Here because the sentence beside it is not a contract and this is - owner's decision,
/// 2026-09-06.</b> The human line already names the process on a step that gave up, and leaving it
/// only there would force a script wanting it to match English prose. The document one file over
/// says why that is not allowed: a warning carries its kind beside its message precisely so nobody
/// has to, and a reader driven to a regular expression breaks the first time somebody improves the
/// wording.
///
/// <b>The fact cannot be recovered afterwards, which is what makes it worth a field.</b> A step
/// that timed out leaves an entry nothing can move by asking again. Asking the machine a second
/// time answers about a different moment - the process may have gone, or the number may now belong
/// to something else - so the reading taken when we gave up is held only here.
///
/// <b>Null means no process, and the three states behind it are readable from the fields
/// alongside.</b> An entry that reached Stopped has none. A step never attempted says so through
/// <c>skippedBecause</c>. A step whose reading was refused carries the manager's own
/// <c>errorCode</c> and <c>error</c> - so rule 8 is met by the object rather than by this field
/// growing a shape of its own. <b>One narrow case stays ambiguous and is named rather than
/// hidden:</b> a failed step with a null here was either never read or read and found without a
/// process, and only the error number tells those apart.
///
/// <b>It adds nothing a script could not already learn.</b> <c>bws list --json</c> has carried
/// <c>processId</c> for every entry since the listing existed, so this is the same fact in the one
/// document that was missing it, not a new capability.
/// </param>
internal sealed record StepResultJson(
    string ServiceName,
    string Operation,
    string Reason,
    string Outcome,
    string? SkippedBecause,
    string Status,
    int? ProcessId,
    int ErrorCode,
    string? Error,
    long Milliseconds,
    string? StartType,
    bool? DelayedAuto);

internal sealed record PlanJsonShape
{
    public required string Action { get; init; }
    public required string ServiceName { get; init; }
    public required bool IncludeDependents { get; init; }

    /// <summary>
    /// Whether a startup setting was asked to stop the entry as well - <c>--stop</c>. False for every
    /// other ask. Added 2026-09-24 beside <see cref="IncludeDependents"/>, which is the other switch
    /// that changes what a plan does rather than how it is shown. The stop itself appears as a step,
    /// and this says it was asked for rather than worked out.
    /// </summary>
    public required bool AlsoStop { get; init; }

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
            AlsoStop = plan.Action.AlsoStop,
            DryRun = run is null,

            Steps =
            [
                .. plan.Steps.Select(step => new PlanStepJson(
                    step.ServiceName,
                    step.DisplayName,
                    Camel(step.Operation.ToString()),
                    Camel(step.Reason.ToString()),
                    Written(step),
                    Delayed(step)))
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

                    // A number or nothing, the same shape the listing gives this field. Absent and
                    // never-asked both arrive as null on purpose - the argument is at the record.
                    result.ProcessId.IsPresent ? result.ProcessId.Value : null,
                    result.ErrorCode,
                    result.Error,
                    result.Milliseconds,
                    Written(result.Step),
                    Delayed(result.Step)))],

            Completed = run?.Completed,
            Cancelled = run?.Cancelled
        };

        return JsonSerializer.Serialize(shape, Options);
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>
    /// The start type a step writes, in the words the machine readable listing already uses.
    ///
    /// <b>The listing's spelling rather than the command line's, and that is deliberate.</b> A
    /// script reading this document is the same script that reads <c>bws list --json</c>, where the
    /// field bound by the glossary as <c>startType</c> carries Automatic, Manual and Disabled. The
    /// command line accepts lower case words, which is a different surface for a different reader,
    /// and a document that answered "manual" where the listing says "Manual" would make somebody
    /// write a comparison that works on one of the two.
    ///
    /// Asked of <see cref="PlanStep.To"/> rather than of the operation, so a step of that kind
    /// arriving without a type says null instead of failing here. The type half of the setting only
    /// - a late automatic step says "Automatic" here and true in <see cref="Delayed"/>, exactly as the
    /// listing says an entry that already is one.
    /// </summary>
    private static string? Written(PlanStep step) =>
        step.To is { } setting ? StartSettings.Written(setting).Type.ToString() : null;

    /// <summary>The late start half of the same setting, or null for a step that writes none.</summary>
    private static bool? Delayed(PlanStep step) =>
        step.To is { } setting ? StartSettings.Written(setting).Delayed : null;
}
