using System.Text.Json;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Core.Snapshots;

namespace Bws.Cli.Tests;

/// <summary>
/// Every enumerated value in the plan and the comparison documents is spelled the way its
/// enumeration spells it - rule 5 of docs/03, the rule DocumentValueGuards holds for the snapshot.
///
/// <b>The plan and the comparison broke it from the day each was written until 2026-09-30</b> -
/// stability report round 2 point 8, owner's decision, a breaking change in 0.x. A helper lowering
/// the first letter wrote <c>"outcome": "succeeded"</c> beside <c>"status": "Running"</c> in ONE
/// result, and the comparison handed its naming policy to the value converter too. A script reading
/// either had to know which field was in which convention.
///
/// <b>Parsed, case-sensitively, rather than checked for a capital</b> - the same reason the
/// snapshot's guard gives: a value has to BE a member, spelled exactly, or a script keying on it
/// is keying on nothing.
///
/// <b>Every member of every enumeration is put through the writer</b>, so a member added later is
/// covered the day it is added - a fixture choosing three would go stale at the first new kind.
/// </summary>
public sealed class MachineDocumentValueGuards
{
    [Fact]
    public void Every_enumerated_value_in_a_plan_is_a_member_spelled_as_its_enumeration_spells_it()
    {
        var checkedValues = 0;

        foreach (var kind in Enum.GetValues<ActionKind>())
        {
            var plan = JsonDocument.Parse(PlanJson.Render(Run(kind))).RootElement;

            checkedValues += Member<ActionKind>(plan, "action");

            foreach (var step in plan.GetProperty("steps").EnumerateArray())
            {
                checkedValues += Member<StepOperation>(step, "operation") + Member<StepReason>(step, "reason");
            }

            foreach (var warning in plan.GetProperty("warnings").EnumerateArray())
            {
                checkedValues += Member<PlanWarningKind>(warning, "kind");
            }

            foreach (var result in plan.GetProperty("results").EnumerateArray())
            {
                checkedValues += Member<StepOutcome>(result, "outcome") + Member<SkipReason>(result, "skippedBecause");
            }
        }

        // A GUARD THAT READ NOTHING PASSES - one renamed field and every check above skips itself.
        Assert.True(checkedValues > 100, $"Only {checkedValues} values were looked at. A field was probably renamed.");
    }

    [Fact]
    public void The_group_of_every_difference_is_a_member_spelled_as_its_enumeration_spells_it()
    {
        var differences = Enum.GetValues<DifferenceGroup>()
            .Select(group => new FieldDifference("field", group, "before", "after"))
            .ToList();

        var diff = new SnapshotDiff(
            [], [], [new ChangedEntry("Spooler", "Print Spooler", differences, [])], [], [], [],
            new ComparisonCaveats(false, false, false, false, false, false, false, []),
            new InstancesLeftOut(0, 0));

        var written = JsonDocument.Parse(DiffJson.Render(diff)).RootElement
            .GetProperty("changed")[0].GetProperty("differences").EnumerateArray()
            .Sum(difference => Member<DifferenceGroup>(difference, "group"));

        Assert.Equal(differences.Count, written);
    }

    /// <summary>
    /// A run of one plan holding a step of every operation, every reason, a warning of every kind and
    /// a result of every outcome and every skip reason.
    /// </summary>
    private static PlanRun Run(ActionKind kind)
    {
        var steps = Enum.GetValues<StepOperation>()
            .Select(operation => new PlanStep("Spooler", "Print Spooler", operation, StepReason.Requested))
            .Concat(Enum.GetValues<StepReason>()
                .Select(reason => new PlanStep("Spooler", "Print Spooler", StepOperation.Stop, reason)))
            .ToList();

        var outcomes = Enum.GetValues<StepOutcome>()
            .Select(outcome => Result(steps[0], outcome, skipped: null))
            .Concat(Enum.GetValues<SkipReason>().Select(reason => Result(steps[0], StepOutcome.Skipped, reason)))
            .ToList();

        return new PlanRun
        {
            Plan = new OperationPlan
            {
                Action = new ServiceAction(kind, "Spooler"),
                Steps = steps,
                Warnings = [.. Enum.GetValues<PlanWarningKind>().Select(warning => new PlanWarning(warning, "Spooler", ["W32Time"]))],
                Problems = []
            },
            Results = outcomes,
            Cancelled = false,
            Ceiling = TimeSpan.FromSeconds(60)
        };
    }

    private static StepResult Result(PlanStep step, StepOutcome outcome, SkipReason? skipped) => new()
    {
        Step = step,
        Outcome = outcome,
        SkippedBecause = skipped,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(4812),
        ErrorCode = 0,
        Error = null,
        Milliseconds = 10
    };

    /// <summary>One value parsed as a member of its enumeration. Null is an answer rather than a gap.</summary>
    private static int Member<T>(JsonElement holder, string field)
        where T : struct, Enum
    {
        if (!holder.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return 0;
        }

        var written = value.GetString()!;

        Assert.True(
            Enum.TryParse<T>(written, ignoreCase: false, out _),
            $"The document writes \"{field}\": \"{written}\", which is not how {typeof(T).Name} spells any of "
            + "its members. Values in a machine readable document are written the way the enumeration "
            + "writes them - rule 5 of docs/03.");

        return 1;
    }
}
