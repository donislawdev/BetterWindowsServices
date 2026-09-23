using System.Text.Json;
using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// What the machine readable plan says about a step that gave up.
///
/// <b>The document rather than the sentence, and the split is the whole subject.</b> The line a
/// person reads names the process holding a stuck entry, and a script may not be made to find it
/// there: `docs/02` freezes the rule that a warning carries its kind beside its message so nobody
/// has to match English, and a number readable only out of prose is the same fault one field over.
///
/// <b>Asked of PlanJson rather than of a run, because a step that times out needs a wedged service
/// and there is not one to hand.</b> What is checked here is the mapping from a reading to a field,
/// which is where the three states could quietly become two.
/// </summary>
public sealed class PlanDocumentGuards
{
    [Fact]
    public void A_step_that_gave_up_names_the_process_still_holding_the_entry()
    {
        var document = Rendered(Held("Spooler", Reading<int>.Present(4812)));

        Assert.Equal(4812, document.GetProperty("processId").GetInt32());
    }

    [Fact]
    public void An_entry_with_no_process_behind_it_says_null_rather_than_process_zero()
    {
        // Zero is a value and "nothing is running it" is not. The manager reports zero for an entry
        // with no process, and a document repeating that number would be naming the system idle
        // process as the thing holding a stopped service.
        var document = Rendered(Held("Spooler", Reading<int>.Absent()));

        Assert.Equal(JsonValueKind.Null, document.GetProperty("processId").ValueKind);
    }

    [Fact]
    public void A_step_nobody_reached_says_null_too_and_the_reason_is_beside_it()
    {
        // The one place the two nulls are told apart. This field does not grow a shape of its own
        // for it - skippedBecause already carries the answer, on the same object.
        var document = Rendered(Held("Spooler", Reading<int>.NotRead()) with
        {
            Outcome = StepOutcome.Skipped,
            SkippedBecause = SkipReason.EarlierStepFailed
        });

        Assert.Equal(JsonValueKind.Null, document.GetProperty("processId").ValueKind);
        Assert.Equal("earlierStepFailed", document.GetProperty("skippedBecause").GetString());
    }

    /// <summary>
    /// The document over a forced stop whose polite step gave up and whose kill arrived says
    /// <c>completed: true</c>, because the entry is where the plan wanted it.
    ///
    /// <b>The command line's half of a core repair, 2026-09-16.</b> Exit code 3 is decided by the
    /// same flag one line away from this field, so a script reading the document and a script
    /// reading the code were both told the run had not finished - measured on the throwaway machine
    /// before the repair, <c>completed: false</c> and exit code 3 over an entry in Stopped. The exit
    /// code itself is proved by running the tool there, which is the only place a stop can hang -
    /// this holds the field the same run writes.
    /// </summary>
    [Fact]
    public void A_forced_stop_whose_kill_arrived_is_written_as_completed()
    {
        var asked = Held("Spooler", Reading<int>.Present(4812));

        var ended = new StepResult
        {
            Step = new PlanStep(
                "Spooler", "Spooler", StepOperation.Terminate, StepReason.Escalation, ProcessId: 4812),
            Outcome = StepOutcome.Succeeded,
            SkippedBecause = null,
            Status = EntryStatus.Stopped,
            ProcessId = Reading<int>.Present(4812),
            ErrorCode = 0,
            Error = null,
            Milliseconds = 260
        };

        var run = RunOf(asked) with
        {
            Plan = RunOf(asked).Plan with
            {
                Action = new ServiceAction(ActionKind.ForceStop, "Spooler"),
                Steps = [asked.Step, ended.Step]
            },
            Results = [asked, ended]
        };

        var document = JsonDocument.Parse(PlanJson.Render(run)).RootElement;

        Assert.True(document.GetProperty("completed").GetBoolean());

        // AND THE POLITE STEP IS STILL WRITTEN AS THE STEP THAT GAVE UP. The verdict changed, the
        // record of what happened did not - a document that tidied the timeout away would hide the
        // one line that says why a process was ended.
        Assert.Equal("timedOut", document.GetProperty("results")[0].GetProperty("outcome").GetString());
    }

    private static JsonElement Rendered(StepResult result) =>
        JsonDocument.Parse(PlanJson.Render(RunOf(result))).RootElement
            .GetProperty("results")[0];

    private static StepResult Held(string name, Reading<int> processId) => new()
    {
        Step = new PlanStep(name, name, StepOperation.Stop, StepReason.Requested),
        Outcome = StepOutcome.TimedOut,
        SkippedBecause = null,
        Status = EntryStatus.StopPending,
        ProcessId = processId,
        ErrorCode = 0,
        Error = null,
        Milliseconds = 60_000
    };

    private static PlanRun RunOf(StepResult result) => new()
    {
        Plan = new OperationPlan
        {
            Action = new ServiceAction(ActionKind.Stop, result.Step.ServiceName),
            Steps = [result.Step],
            Warnings = [],
            Problems = []
        },
        Results = [result],
        Cancelled = false,
        Ceiling = TimeSpan.FromSeconds(60)
    };
}
