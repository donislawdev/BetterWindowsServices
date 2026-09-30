using System.Text.Json;
using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// What the terminal and the machine readable output say about a step that had nothing to put back and a
/// restart that only starts - stability report W-5, the owner's decision of 2026-09-30.
///
/// <b>The sentence is found by a key built from the value's name</b>, so a reason added without one would
/// print its own key rather than fail - which is why the first test asks every reason, not only the new one.
/// </summary>
public sealed class PutBackOutputTests
{
    [Fact]
    public void Every_reason_a_step_was_skipped_has_a_sentence_of_its_own()
    {
        var said = Enum.GetValues<SkipReason>().Select(reason => PlanText.Describe(Skipped(reason))).ToList();

        Assert.All(said, sentence => Assert.DoesNotContain("cli.run.outcome", sentence, StringComparison.Ordinal));
        Assert.Equal(said.Count, said.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_document_names_the_reason_and_the_warning()
    {
        var run = new PlanRun
        {
            Plan = new OperationPlan
            {
                Action = new ServiceAction(ActionKind.Restart, "AxInstSV"),
                Steps = [Skipped(SkipReason.NothingToPutBack).Step],
                Warnings = [new PlanWarning(PlanWarningKind.RestartOnlyStarts, "AxInstSV", [])],
                Problems = []
            },
            Results = [Skipped(SkipReason.NothingToPutBack)],
            Cancelled = false,
            Ceiling = TimeSpan.FromMinutes(1)
        };

        var document = JsonDocument.Parse(PlanJson.Render(run)).RootElement;

        Assert.Equal("NothingToPutBack", document.GetProperty("results")[0].GetProperty("skippedBecause").GetString());
        Assert.Equal("RestartOnlyStarts", document.GetProperty("warnings")[0].GetProperty("kind").GetString());
    }

    private static StepResult Skipped(SkipReason reason) => new()
    {
        Step = new PlanStep("AxInstSV", "ActiveX Installer", StepOperation.Start, StepReason.Restore),
        Outcome = StepOutcome.Skipped,
        SkippedBecause = reason,
        Status = EntryStatus.Unknown,
        ProcessId = Reading<int>.NotRead(),
        ErrorCode = 0,
        Error = null,
        Milliseconds = 0
    };
}
