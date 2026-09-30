using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// A run gives back only what it took down - the external stability report of 2026-09-29 (W-5) and the
/// owner's decision on it of 2026-09-30.
///
/// <b>Three shapes of one mistake, and each started a service the run had never touched.</b> Stop pressed
/// before the first step of a restart, a restart of a whole selection interrupted half way, and a
/// dependant somebody else stopped between the preview and the run. Every step putting something back used
/// to be tried whatever had happened before it.
///
/// <b>The assertions are about what the manager was ASKED</b>, because that is the half a reported outcome
/// can hide - and each is red on the code before that day for exactly that reason.
/// </summary>
public sealed class PutBackTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private const int Held = 4812;

    [Fact]
    public void Stop_pressed_before_a_restart_of_a_stopped_entry_starts_nothing()
    {
        using var interruption = new CancellationTokenSource();
        interruption.Cancel();

        var control = new FakeScmControl().At("AxInstSV", EntryStatus.Stopped);

        var run = new PlanRunner(control, new FakeClock()).Run(
            Planned(ActionKind.Restart, Entry("AxInstSV", EntryStatus.Stopped)), Minute, interruption.Token);

        Assert.Equal(SkipReason.Cancelled, Assert.Single(run.Results).SkippedBecause);
        Assert.Empty(control.Requested);
    }

    [Fact]
    public void Stop_pressed_before_a_restart_gives_nothing_back()
    {
        using var interruption = new CancellationTokenSource();
        interruption.Cancel();

        // Running for the preview and stopped by somebody else since - the one shape in which the step
        // putting it back used to do harm rather than find nothing to do.
        var control = new FakeScmControl().At("Spooler", EntryStatus.Stopped);

        var run = new PlanRunner(control, new FakeClock()).Run(
            Planned(ActionKind.Restart, Entry("Spooler", EntryStatus.Running)), Minute, interruption.Token);

        Assert.Equal(
            [SkipReason.Cancelled, SkipReason.NothingToPutBack],
            run.Results.Select(result => result.SkippedBecause));

        Assert.Empty(control.Requested);
    }

    [Fact]
    public void A_dependant_somebody_else_stopped_before_the_run_is_left_stopped()
    {
        var control = new FakeScmControl()
            .At("Lanman", EntryStatus.Running)
            .At("Dependant", EntryStatus.Stopped);

        var run = Run(control, Planned(
            ActionKind.Restart, Entry("Lanman", EntryStatus.Running), Entry("Dependant", EntryStatus.Running)));

        Assert.Equal(SkipReason.AlreadyThere, Result(run, "Dependant", StepOperation.Stop).SkippedBecause);
        Assert.Equal(SkipReason.NothingToPutBack, Result(run, "Dependant", StepOperation.Start).SkippedBecause);
        Assert.Equal(["Lanman", "Lanman"], control.Requested);

        // Not where the plan wanted it at the end, and said so rather than counted as done.
        Assert.False(run.Completed);
    }

    [Fact]
    public void A_neighbour_found_stopped_is_not_started_after_the_process_ends()
    {
        var control = new FakeScmControl()
            .Reaching("Spooler", new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, Held))
            .At("Housemate", EntryStatus.Stopped);

        var run = Run(control, ForcedRestart(
            Step("Spooler", StepOperation.Stop, StepReason.Requested),
            Step("Housemate", StepOperation.Stop, StepReason.SharesTheProcess),
            Ending("Housemate"),
            Step("Spooler", StepOperation.Start, StepReason.Restore),
            Step("Housemate", StepOperation.Start, StepReason.Restore)));

        // Its own stop found it stopped, so it was not in the process the ending took down.
        Assert.Equal(SkipReason.NothingToPutBack, Result(run, "Housemate", StepOperation.Start).SkippedBecause);
        Assert.Equal(StepOutcome.Succeeded, Result(run, "Spooler", StepOperation.Start).Outcome);
        Assert.DoesNotContain("Housemate", control.Requested);
    }

    [Fact]
    public void An_entry_its_ending_took_down_is_given_back_although_its_own_stop_was_refused()
    {
        // The ending is what took it down, so that is what the step putting it back has to count.
        var control = new FakeScmControl().RefusingRequests("Spooler", 1052);

        var run = Run(control, ForcedRestart(
            Step("Spooler", StepOperation.Stop, StepReason.Requested),
            Ending(),
            Step("Spooler", StepOperation.Start, StepReason.Restore)));

        Assert.Equal(Held, Assert.Single(control.Ended));

        // Asked twice - the stop, and the start that gives it back. The double refuses both, which is
        // beside the point: what is tested is that the second was asked at all.
        Assert.Equal(["Spooler", "Spooler"], control.Requested);
    }

    [Fact]
    public void An_ending_Windows_answered_by_starting_the_entry_again_still_took_it_down()
    {
        // The process was ended and the manager started the entry again at once. The step putting it back
        // reads it and finds it running - which is a different sentence from "this run never stopped it".
        var control = new FakeScmControl()
            .ComingBack("Spooler", new ServiceProgress(EntryStatus.Running, 0, TimeSpan.Zero, 5555))
            .At("Housemate", EntryStatus.Running);

        var run = Run(control, ForcedRestart(
            Ending("Housemate") with { Reason = StepReason.Requested },
            Step("Spooler", StepOperation.Start, StepReason.Restore),
            Step("Housemate", StepOperation.Start, StepReason.Restore)));

        Assert.True(run.Results[0].StartedAgain);
        Assert.Equal(SkipReason.AlreadyThere, Result(run, "Spooler", StepOperation.Start).SkippedBecause);

        // The neighbour died with the process and nothing brought it back, so it is given back.
        Assert.Equal(StepOutcome.Succeeded, Result(run, "Housemate", StepOperation.Start).Outcome);
    }

    [Fact]
    public void An_interrupted_restart_of_a_selection_starts_nothing_it_did_not_stop()
    {
        using var interruption = new CancellationTokenSource();

        var catalog = new FakeScmCatalog([Entry("First", EntryStatus.Running), Entry("Second", EntryStatus.Running)]);

        var bulk = new BulkPlanBuilder(catalog.ReadAll(), catalog)
            .Build(new BulkAction(ActionKind.Restart, ["First", "Second"], false));

        // The second was stopped by somebody else after the preview.
        var control = new FakeScmControl()
            .At("First", EntryStatus.Running)
            .At("Second", EntryStatus.Stopped);

        var run = new BulkRunner(new PlanRunner(control, new FakeClock())).Run(
            bulk, Minute, interruption.Token, starting: (_, _) => interruption.Cancel());

        Assert.True(run.Cancelled);
        Assert.DoesNotContain("Second", control.Requested);

        Assert.Equal(
            SkipReason.NothingToPutBack,
            run.Runs.SelectMany(one => one.Results)
                .Single(result => result.Step is { ServiceName: "Second", Operation: StepOperation.Start })
                .SkippedBecause);
    }

    // -- fixtures --------------------------------------------------------------------------

    /// <summary>A plan from the builder, the first entry the target and every other one depending on it.</summary>
    private static OperationPlan Planned(ActionKind kind, ScmEntry target, params ScmEntry[] dependants)
    {
        var catalog = new FakeScmCatalog([target, .. dependants]);

        if (dependants.Length > 0)
        {
            catalog.DependedOnBy(target.ServiceName, [.. dependants.Select(entry => entry.ServiceName)]);
        }

        return new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(kind, target.ServiceName, IncludeDependents: dependants.Length > 0));
    }

    private static ScmEntry Entry(string serviceName, EntryStatus status) =>
        Entries.Named(serviceName, serviceName) with
        {
            Status = status,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            ProcessId = status == EntryStatus.Stopped ? Reading<int>.Absent() : Reading<int>.Present(4444)
        };

    private static PlanStep Step(string name, StepOperation operation, StepReason reason) =>
        new(name, name, operation, reason);

    private static PlanStep Ending(params string[] housemates) =>
        new("Spooler", "Spooler", StepOperation.Terminate, StepReason.Escalation, ProcessId: Held, TakesWithIt: housemates);

    private static OperationPlan ForcedRestart(params PlanStep[] steps) => new()
    {
        Action = new ServiceAction(ActionKind.ForceRestart, "Spooler"),
        Steps = steps,
        Warnings = [],
        Problems = []
    };

    private static PlanRun Run(FakeScmControl control, OperationPlan plan) =>
        new PlanRunner(control, new FakeClock()).Run(plan, TimeSpan.FromSeconds(30));

    private static StepResult Result(PlanRun run, string serviceName, StepOperation operation) =>
        Assert.Single(
            run.Results,
            result => result.Step.ServiceName == serviceName && result.Step.Operation == operation);
}
