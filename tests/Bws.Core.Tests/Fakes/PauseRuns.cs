using Bws.Core.Planning;

namespace Bws.Core.Tests.Fakes;

/// <summary>
/// The readings, the one-step plans and the run that the tests of pausing share - PausePlanTests and
/// PauseWaitingTests.
/// </summary>
internal static class PauseRuns
{
    /// <summary>A reading of Spooler's double: no check point means nothing promised, one means four seconds.</summary>
    internal static ServiceProgress Progress(EntryStatus status, uint checkPoint) =>
        new(status, checkPoint, checkPoint == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(4), FakeScmControl.FakeProcess);

    /// <summary>A plan of one requested step on Spooler, under the ask that makes it.</summary>
    internal static OperationPlan Doing(StepOperation operation) => new()
    {
        Action = new ServiceAction(
            operation switch
            {
                StepOperation.Pause => ActionKind.Pause,
                StepOperation.Resume => ActionKind.Resume,
                _ => ActionKind.Stop
            },
            "Spooler"),
        Steps = [new PlanStep("Spooler", "Spooler", operation, StepReason.Requested)],
        Warnings = [],
        Problems = []
    };

    /// <summary>The plan run against the double, with a limit of thirty seconds.</summary>
    internal static PlanRun Ran(FakeScmControl control, OperationPlan plan, FakeClock? clock = null) =>
        new PlanRunner(control, clock ?? new FakeClock()).Run(plan, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
}
