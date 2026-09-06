using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The half of ADR-11 that carries a plan out, checked without a machine to break.
///
/// What is worth checking here is not that a stop stops something - the manager does that
/// and needs no help. It is everything around it: that the steps happen in the order shown
/// and no other, that an entry asking for more time gets it, that one which has stopped
/// asking is given up on, that a failure does not drag the rest of the plan through the
/// same wall, and that a run which is interrupted still gives back what it took.
///
/// The chain is the one measured on a real machine on 2026-08-01, the same one the plan
/// tests use: MRxSmb20 is needed by LanmanWorkstation, which is needed by SessionEnv and
/// Netlogon.
/// </summary>
public sealed class PlanRunnerTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    [Fact]
    public void Every_step_happens_in_the_order_the_plan_showed()
    {
        // The plan is a promise about order. Carrying it out in some other order would make
        // the preview a description of something that did not happen.
        var control = Running("SessionEnv", "Netlogon", "LanmanWorkstation", "MRxSmb20");
        var run = Run(Plan(ActionKind.Stop, "MRxSmb20"), control);

        Assert.Equal(
            ["SessionEnv", "Netlogon", "LanmanWorkstation", "MRxSmb20"],
            run.Results.Select(result => result.Step.ServiceName));

        Assert.All(run.Results, result => Assert.Equal(StepOutcome.Succeeded, result.Outcome));
        Assert.Equal(run.Results.Select(result => result.Step.ServiceName), control.Requested);
        Assert.True(run.Completed);
    }

    [Fact]
    public void An_entry_already_where_it_was_asked_to_be_is_not_touched()
    {
        var control = Running("SessionEnv", "Netlogon", "LanmanWorkstation")
            .At("MRxSmb20", EntryStatus.Stopped);

        var run = Run(Plan(ActionKind.Stop, "MRxSmb20"), control);
        var last = run.Results[^1];

        Assert.Equal(StepOutcome.Skipped, last.Outcome);
        Assert.Equal(SkipReason.AlreadyThere, last.SkippedBecause);

        // And the manager was never asked, which is the half that would go unnoticed. A step
        // reported as skipped that quietly went through anyway is a report about a machine
        // other than the one in front of you.
        Assert.DoesNotContain("MRxSmb20", control.Requested);
    }

    [Fact]
    public void A_plan_where_nothing_was_left_to_do_is_still_a_plan_that_worked()
    {
        // The property every runbook rests on: running the same line twice must not report
        // the second run as a failure. A step that goes red for finding its work done is a
        // step somebody stops trusting, and then stops reading.
        var control = new FakeScmControl()
            .At("SessionEnv", EntryStatus.Stopped)
            .At("Netlogon", EntryStatus.Stopped)
            .At("LanmanWorkstation", EntryStatus.Stopped)
            .At("MRxSmb20", EntryStatus.Stopped);

        var run = Run(Plan(ActionKind.Stop, "MRxSmb20"), control);

        Assert.True(run.Completed);
        Assert.Empty(control.Requested);
        Assert.All(run.Results, result => Assert.Equal(SkipReason.AlreadyThere, result.SkippedBecause));
    }

    [Fact]
    public void A_step_that_is_refused_stops_the_ones_behind_it()
    {
        // Every later step was written down assuming the earlier ones worked. Carrying on
        // would produce a column of refusals that says nothing the first one did not.
        var control = Running("SessionEnv", "Netlogon", "LanmanWorkstation", "MRxSmb20")
            .RefusingRequests("Netlogon", AccessDenied);

        var run = Run(Plan(ActionKind.Stop, "MRxSmb20"), control);

        Assert.Equal(StepOutcome.Succeeded, run.Results[0].Outcome);
        Assert.Equal(StepOutcome.Failed, run.Results[1].Outcome);
        Assert.Equal(AccessDenied, run.Results[1].ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(run.Results[1].Error));

        Assert.All(run.Results.Skip(2), result =>
        {
            Assert.Equal(StepOutcome.Skipped, result.Outcome);
            Assert.Equal(SkipReason.EarlierStepFailed, result.SkippedBecause);
        });

        Assert.Equal(["SessionEnv", "Netlogon"], control.Requested);
        Assert.False(run.Completed);
    }

    [Fact]
    public void An_entry_that_cannot_even_be_looked_at_is_never_asked_to_move()
    {
        var control = Running("MRxSmb20").RefusingReads("MRxSmb20", AccessDenied);
        var run = Run(Plan(ActionKind.Stop, "MRxSmb20", dependents: false), control);

        var result = Assert.Single(run.Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(AccessDenied, result.ErrorCode);
        Assert.Equal(EntryStatus.Unknown, result.Status);
        Assert.Empty(control.Requested);
    }

    [Fact]
    public void A_refusal_that_turns_out_to_be_a_race_is_not_reported_as_a_failure()
    {
        // Entries move by themselves on a live machine. One that stops between being read
        // and being asked leaves the manager refusing to stop something already stopped,
        // and calling that a failure would send somebody looking for a problem that is not
        // there. The refusal is checked against where the entry actually is.
        var control = Running("MRxSmb20")
            .RefusingRequests("MRxSmb20", ServiceNotActive, becomes: EntryStatus.Stopped);

        var run = Run(Plan(ActionKind.Stop, "MRxSmb20", dependents: false), control);
        var result = Assert.Single(run.Results);

        Assert.Equal(SkipReason.AlreadyThere, result.SkippedBecause);
        Assert.True(run.Completed);

        // It was tried, unlike the entry that was already there when we first looked.
        Assert.Equal(["MRxSmb20"], control.Requested);
    }

    // -- waiting ---------------------------------------------------------------------------

    [Fact]
    public void An_entry_that_keeps_reporting_progress_is_given_more_time_than_it_first_asked_for()
    {
        // Win32 documents the promise: before its wait hint elapses, a service will either
        // raise its check point or change state. Keeping the promise earns a fresh hint. A
        // tool that instead started a stopwatch at the first hint would call a slow, healthy
        // shutdown a failure, which is the pitfall 02-DECYZJE-TECHNICZNE warns about by name.
        var readings = Enumerable
            .Range(1, 40)
            .Select(step => new ServiceProgress(EntryStatus.StopPending, (uint)step, TimeSpan.FromSeconds(5), ProcessId: 4812))
            .Append(new ServiceProgress(EntryStatus.Stopped, 0, TimeSpan.Zero, ProcessId: 0))
            .ToArray();

        var clock = new FakeClock();
        var control = Running("MRxSmb20").Reaching("MRxSmb20", readings);
        var run = Run(Plan(ActionKind.Stop, "MRxSmb20", dependents: false), control, clock);

        Assert.Equal(StepOutcome.Succeeded, Assert.Single(run.Results).Outcome);

        // Twice the hint it first gave, spent on an entry that kept saying it was working.
        Assert.Equal(TimeSpan.FromSeconds(10), clock.Waited);
    }

    [Fact]
    public void An_entry_that_stops_reporting_progress_is_given_up_on_when_its_own_promise_runs_out()
    {
        // The other side of the same rule, and the one that decides whether a terminal hangs.
        // The check point never moves, so the two seconds it asked for are all it gets - the
        // minute we were prepared to wait never comes into it.
        var clock = new FakeClock();
        var control = Running("MRxSmb20")
            .Reaching("MRxSmb20", new ServiceProgress(EntryStatus.StopPending, 7, TimeSpan.FromSeconds(2), ProcessId: 4812));

        var run = Run(Plan(ActionKind.Stop, "MRxSmb20", dependents: false), control, clock);
        var result = Assert.Single(run.Results);

        Assert.Equal(StepOutcome.TimedOut, result.Outcome);

        // Where it was left, which is the half a person needs to decide what to do next.
        Assert.Equal(EntryStatus.StopPending, result.Status);
        Assert.Equal(TimeSpan.FromSeconds(2), clock.Waited);

        // AND WHAT IS HOLDING IT THERE, WHICH IS THE OTHER HALF - 2026-09-06. An entry stuck in
        // StopPending will not be moved by asking again, so the process is the only thing left a
        // person can act on. Carried from the last reading rather than looked up afterwards: by
        // the time this is read the entry may be gone, and a second question would answer about
        // a different moment than the one being reported.
        Assert.True(result.ProcessId.IsPresent);
        Assert.Equal((int)FakeScmControl.FakeProcess, result.ProcessId.Value);
    }

    [Fact]
    public void An_entry_that_promises_nothing_at_all_is_given_up_on_at_the_limit_we_set()
    {
        // A wait hint of zero is what an entry reports when it has nothing pending, so it is
        // not a promise to hold anybody to. Then, and only then, our own limit decides.
        var clock = new FakeClock();
        var control = Running("MRxSmb20")
            .Reaching("MRxSmb20", new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, ProcessId: 4812));

        var run = Run(
            Plan(ActionKind.Stop, "MRxSmb20", dependents: false), control, clock, TimeSpan.FromSeconds(30));

        Assert.Equal(StepOutcome.TimedOut, Assert.Single(run.Results).Outcome);
        Assert.Equal(TimeSpan.FromSeconds(30), clock.Waited);
    }

    // -- what happens to the rest ----------------------------------------------------------

    [Fact]
    public void A_restart_that_fails_half_way_still_puts_back_what_it_took_down()
    {
        // The case that decides whether this is safe to run on somebody's production box. A
        // restart takes three services down to reach the fourth. If the fourth then refuses,
        // walking away would leave a machine trimmed by a plan that failed - which is the
        // one outcome nobody asked for. Putting things back is not part of the forward path
        // and does not stop when the forward path does.
        var control = Running("SessionEnv", "Netlogon", "LanmanWorkstation", "MRxSmb20")
            .RefusingRequests("MRxSmb20", AccessDenied);

        var run = Run(Plan(ActionKind.Restart, "MRxSmb20"), control);

        Assert.False(run.Completed);
        Assert.Equal(StepOutcome.Failed, Outcome(run, "MRxSmb20", StepOperation.Stop));

        // Its own start gives it back, so it is tried rather than abandoned - and finds
        // nothing to do, because the stop that failed left it running.
        Assert.Equal(SkipReason.AlreadyThere, Result(run, "MRxSmb20", StepOperation.Start).SkippedBecause);

        foreach (var name in (string[])["LanmanWorkstation", "Netlogon", "SessionEnv"])
        {
            Assert.Equal(StepOutcome.Succeeded, Outcome(run, name, StepOperation.Start));
        }
    }

    [Fact]
    public void Interrupting_a_restart_does_not_leave_the_service_stopped()
    {
        // Found on a virtual machine on 2026-08-01, not by reasoning. Pressing Ctrl+C while
        // a restart was stopping the service left it stopped, because bringing it back was
        // classified as forward progress rather than as giving something back. A restart
        // that leaves the thing it was restarting switched off is the worst outcome this
        // command has.
        using var interruption = new CancellationTokenSource();

        var control = Running("MRxSmb20");

        var run = new PlanRunner(control, new FakeClock()).Run(
            Plan(ActionKind.Restart, "MRxSmb20", dependents: false),
            Minute,
            interruption.Token,
            starting: (_, _) => interruption.Cancel());

        Assert.True(run.Cancelled);
        Assert.Equal(StepOutcome.Succeeded, Outcome(run, "MRxSmb20", StepOperation.Stop));
        Assert.Equal(StepOutcome.Succeeded, Outcome(run, "MRxSmb20", StepOperation.Start));
        Assert.True(run.Completed);
    }

    [Fact]
    public void A_restore_that_fails_does_not_stop_the_other_restores()
    {
        // Unlike the forward path, these do not depend on each other having worked. Giving
        // up on the rest because one would not come back would leave more down than had to be.
        var control = Running("SessionEnv", "Netlogon", "LanmanWorkstation", "MRxSmb20");
        var plan = Plan(ActionKind.Restart, "MRxSmb20");

        control.RefusingRequests("LanmanWorkstation", AccessDenied);

        var run = Run(plan, control);

        Assert.Equal(StepOutcome.Failed, Outcome(run, "LanmanWorkstation", StepOperation.Stop));

        // Nothing after it went down, so there is nothing to put back and the restores find
        // their work done. What matters is that they were reached at all.
        Assert.All(
            run.Results.Where(result => result.Step.Reason == StepReason.Restore),
            result => Assert.NotEqual(SkipReason.EarlierStepFailed, result.SkippedBecause));
    }

    [Fact]
    public void Interrupting_a_run_stops_the_forward_steps_and_still_gives_back_what_it_took()
    {
        // Somebody who presses Ctrl+C half way through a restart has to be left with a
        // machine, not with three stopped services and no message. The forward path stops
        // and the restores still run.
        using var interruption = new CancellationTokenSource();

        var control = Running("SessionEnv", "Netlogon", "LanmanWorkstation", "MRxSmb20");

        var run = new PlanRunner(control, new FakeClock()).Run(
            Plan(ActionKind.Restart, "MRxSmb20"),
            Minute,
            interruption.Token,
            starting: (_, number) =>
            {
                if (number == 1)
                {
                    interruption.Cancel();
                }
            });

        Assert.True(run.Cancelled);

        // The first step went through, the rest of the forward path did not, and it says so
        // in as many words rather than looking like a failure.
        Assert.Equal(StepOutcome.Succeeded, run.Results[0].Outcome);

        Assert.All(
            run.Results.Where(result => result.Step.Reason != StepReason.Restore).Skip(1),
            result => Assert.Equal(SkipReason.Cancelled, result.SkippedBecause));

        // And the one service that was taken down is back.
        Assert.Equal(StepOutcome.Succeeded, Outcome(run, "SessionEnv", StepOperation.Start));
    }

    [Fact]
    public void A_step_announces_its_place_in_the_plan_rather_than_its_place_in_the_queue()
    {
        // Progress on screen is read against the plan on screen. Counting attempts instead
        // would number the sixth step as the fifth as soon as anything was skipped, and
        // somebody watching a restart would be looking for a line that is not there.
        var announced = new List<int>();

        var control = Running("SessionEnv", "Netlogon", "LanmanWorkstation", "MRxSmb20")
            .RefusingRequests("Netlogon", AccessDenied);

        new PlanRunner(control, new FakeClock()).Run(
            Plan(ActionKind.Restart, "MRxSmb20"),
            Minute,
            starting: (_, number) => announced.Add(number));

        // Three and four are missing, because the forward path stopped at the refusal, and
        // the numbers that follow do not close the gap. That is the whole point.
        Assert.Equal([1, 2, 5, 6, 7, 8], announced);
    }

    [Fact]
    public void Asking_twice_leaves_things_as_they_are_and_still_reports_what_was_done()
    {
        // The second ask is expensive and exists anyway, because the alternative a person
        // reaches for is killing the process - and a killed process says nothing at all
        // about the half of a cascade it left switched off. Observed on a virtual machine
        // on 2026-08-01, where exactly that left a service stopped with no report.
        using var interruption = new CancellationTokenSource();
        using var abandonment = new CancellationTokenSource();

        var control = Running("SessionEnv", "Netlogon", "LanmanWorkstation", "MRxSmb20");

        var run = new PlanRunner(control, new FakeClock()).Run(
            Plan(ActionKind.Restart, "MRxSmb20"),
            Minute,
            interruption.Token,
            abandonment.Token,
            starting: (_, number) =>
            {
                if (number == 1)
                {
                    interruption.Cancel();
                    abandonment.Cancel();
                }
            });

        Assert.True(run.Cancelled);
        Assert.False(run.Completed);

        // Everything after the first step, the steps that put things back included.
        Assert.All(
            run.Results.Skip(1),
            result => Assert.Equal(SkipReason.Cancelled, result.SkippedBecause));

        // And there is still a result for every step, which is the whole difference between
        // this and killing the process.
        Assert.Equal(run.Plan.Steps.Count, run.Results.Count);
    }

    [Fact]
    public void A_plan_that_has_problems_is_never_carried_out()
    {
        // The plan already refuses drivers. A runner that would carry one out anyway would
        // make that refusal a matter of which caller you went through.
        var catalog = Specimens.Catalog();

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Stop, "amduw23g-202073-df09ebb6"));

        var runner = new PlanRunner(new FakeScmControl(), new FakeClock());

        Assert.Throws<InvalidOperationException>(() => runner.Run(plan, Minute));
    }

    // -- fixtures --------------------------------------------------------------------------

    private const int AccessDenied = 5;
    private const int ServiceNotActive = 1062;

    private static FakeScmControl Running(params string[] serviceNames)
    {
        var control = new FakeScmControl();

        foreach (var serviceName in serviceNames)
        {
            control.At(serviceName, EntryStatus.Running);
        }

        return control;
    }

    /// <summary>The chain as the manager describes it, transitive sets included.</summary>
    private static OperationPlan Plan(ActionKind kind, string serviceName, bool dependents = true)
    {
        var entries = new List<ScmEntry>
        {
            Entry("MRxSmb20", "SMB 2.0 Redirector"),
            Entry("LanmanWorkstation", "Stacja robocza"),
            Entry("SessionEnv", "Konfiguracja pulpitu zdalnego"),
            Entry("Netlogon", "Logowanie do sieci")
        };

        var catalog = new FakeScmCatalog(entries)
            .DependedOnBy("MRxSmb20", "SessionEnv", "Netlogon", "LanmanWorkstation")
            .DependedOnBy("LanmanWorkstation", "SessionEnv", "Netlogon");

        return new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(kind, serviceName, dependents));
    }

    private static ScmEntry Entry(string serviceName, string displayName) =>
        Entries.Named(serviceName, displayName) with
        {
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            ProcessId = Reading<int>.Present(4444)
        };

    private static PlanRun Run(
        OperationPlan plan, FakeScmControl control, FakeClock? clock = null, TimeSpan? timeout = null) =>
        new PlanRunner(control, clock ?? new FakeClock()).Run(plan, timeout ?? Minute);

    private static StepResult Result(PlanRun run, string serviceName, StepOperation operation) =>
        Assert.Single(
            run.Results,
            result => result.Step.ServiceName == serviceName && result.Step.Operation == operation);

    private static StepOutcome Outcome(PlanRun run, string serviceName, StepOperation operation) =>
        Result(run, serviceName, operation).Outcome;
}
