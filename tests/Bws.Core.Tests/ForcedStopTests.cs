using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// The one ask in this product that ends a process, checked without ending one.
///
/// <b>Every decision here was made twice</b> - once in the analysis and once again when the edge
/// cases were walked before any of it was written - and two of them came out the other way round
/// the second time. Both are held below: a polite stop is not sent to an entry that has already had
/// one, and the entries sharing a process are steps with a reason of their own rather than a
/// footnote reading "would break otherwise".
///
/// <b>Nothing here needs a machine, which is the whole point of the seam.</b> A plan that would end
/// the process behind eight hundred entries is a value, and a value can be read.
/// </summary>
public sealed class ForcedStopTests
{
    [Fact]
    public void A_forced_stop_asks_politely_first_and_ends_the_process_only_behind_that()
    {
        var plan = Plan(ActionKind.ForceStop, "MRxSmb20", includeDependents: false, Alone());

        Assert.Collection(
            plan.Steps,
            step =>
            {
                Assert.Equal(StepOperation.Stop, step.Operation);
                Assert.Equal(StepReason.Requested, step.Reason);
            },
            step =>
            {
                Assert.Equal(StepOperation.Terminate, step.Operation);

                // The reason is what the preview PRINTS beside it, and it says this step stands
                // behind one that may not work rather than describing something already decided.
                Assert.Equal(StepReason.Escalation, step.Reason);

                // NAMED IN THE PLAN, WHICH IS THE HALF THE WORD "terminate" CANNOT CARRY. The entry
                // is a service and what ends is a process, and the two are not the same thing.
                Assert.Equal(4444, step.ProcessId);
            });
    }

    [Fact]
    public void Asking_for_it_outright_leaves_one_step_and_the_preview_shows_the_difference()
    {
        var plan = Build(ActionKind.ForceStop, immediate: true, Alone());

        var only = Assert.Single(plan.Steps);

        Assert.Equal(StepOperation.Terminate, only.Operation);

        // NOT AN ESCALATION HERE, because nothing precedes it. Calling it one would print "if the
        // stop does not work" over a plan with no stop in it - which is a sentence about a step
        // that is not on the screen.
        Assert.Equal(StepReason.Requested, only.Reason);
    }

    [Fact]
    public void An_entry_already_asked_to_stop_is_not_asked_again()
    {
        // THE CASE THE WHOLE FEATURE EXISTS FOR, and the design had it wrong until the edge cases
        // were walked. The window reaches this plan only after a stop gave up, so the entry is
        // sitting in a pending state with a request already in flight. A second polite step would
        // send the same thing again and then wait the whole ceiling for it.
        var catalog = Rebuild(Alone(), "MRxSmb20", entry => entry with { Status = EntryStatus.StopPending });

        var only = Assert.Single(Plan(ActionKind.ForceStop, "MRxSmb20", includeDependents: false, catalog).Steps);

        Assert.Equal(StepOperation.Terminate, only.Operation);
    }

    [Fact]
    public void Entries_living_in_the_same_process_are_steps_with_a_reason_of_their_own()
    {
        var plan = Plan(ActionKind.ForceStop, "MRxSmb20", includeDependents: false, Sharing());

        var neighbour = plan.Steps.First(step => step.ServiceName == "LanmanWorkstation");

        Assert.Equal(StepOperation.Stop, neighbour.Operation);

        // NOT Cascade, AND A LIVE MACHINE SHOWED WHY WITHIN A MINUTE OF THE FIRST PLAN. The cascade
        // word is "would break otherwise", which claims the entry DEPENDS on the one being ended.
        // Sharing a process is a fact about how Windows packed them and nothing about either one
        // needing the other.
        Assert.Equal(StepReason.SharesTheProcess, neighbour.Reason);

        // And asked BEFORE the process ends, so it gets a chance to close its files.
        var order = plan.Steps.ToList();

        Assert.True(
            order.IndexOf(neighbour) < order.FindIndex(step => step.Operation == StepOperation.Terminate),
            "The entry sharing the process is asked to stop after the process has already gone, so "
            + "it never gets the chance to close anything - which is the whole reason it is a step.");
    }

    [Fact]
    public void What_dies_alongside_is_said_as_well_as_stepped()
    {
        var warning = Warning(
            Plan(ActionKind.ForceStop, "MRxSmb20", includeDependents: false, Sharing()),
            PlanWarningKind.TerminationTakesWithIt);

        Assert.Equal("LanmanWorkstation", Assert.Single(warning.Related));
    }

    [Fact]
    public void The_shared_process_warning_does_not_also_appear_and_contradict_it()
    {
        // That one says the process does not go away and the neighbours keep running. True of an
        // ordinary stop, and the exact opposite here - two warnings disagreeing about one machine
        // on one screen would be worse than either alone.
        var plan = Plan(ActionKind.ForceStop, "MRxSmb20", includeDependents: false, Sharing());

        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.SharedProcess);
    }

    [Fact]
    public void An_entry_the_machine_does_not_work_without_is_warned_about_rather_than_refused()
    {
        // Owner's decision, 2026-09-06: warned about, not refused. An administrator has the right
        // to manage their own machine, which is the line `R2` of the specification draws - and the
        // wording has to carry the glossary's `P1` distinction, which is "you should not" rather
        // than "you cannot".
        var catalog = Rebuild(Alone(), "MRxSmb20", entry => entry with { ServiceName = "RpcSs" });

        var plan = Plan(ActionKind.ForceStop, "RpcSs", includeDependents: false, catalog);

        Assert.True(plan.IsRunnable);
        Assert.Equal("RpcSs", Assert.Single(Warning(plan, PlanWarningKind.CriticalService).Related));
    }

    [Fact]
    public void An_entry_with_no_process_behind_it_gets_no_plan_at_all()
    {
        var catalog = Rebuild(
            Alone(),
            "MRxSmb20",
            entry => entry with { Status = EntryStatus.Stopped, ProcessId = Reading<int>.Absent() });

        var plan = Plan(ActionKind.ForceStop, "MRxSmb20", includeDependents: false, catalog);

        Assert.False(plan.IsRunnable);
        Assert.Equal(PlanProblemKind.NoProcessToEnd, Assert.Single(plan.Problems).Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void A_number_no_service_process_ever_has_is_refused_rather_than_ended(int processId)
    {
        // Zero is what the manager reports for an entry that is not running, and it is also the
        // system idle process. Four is the kernel. Passing either on would turn "nothing is running
        // this" into a request to end something that has always been there.
        var catalog = Rebuild(
            Alone(), "MRxSmb20", entry => entry with { ProcessId = Reading<int>.Present(processId) });

        Assert.Equal(
            PlanProblemKind.NoProcessToEnd,
            Assert.Single(Plan(ActionKind.ForceStop, "MRxSmb20", includeDependents: false, catalog).Problems).Kind);
    }

    [Fact]
    public void A_casualty_list_known_to_be_short_is_a_refusal_rather_than_a_warning()
    {
        // The same fact as CascadeUnreadable on an ordinary stop and a harder answer. There it
        // means the plan may do more than it shows, which is said out loud. Here it is a list of
        // what dies, known to be incomplete, and rule 5 of the untouchable rules leaves no wording
        // that makes that acceptable.
        var catalog = Alone();
        catalog.RefuseDependentsFor.Add("MRxSmb20");

        var plan = Plan(ActionKind.ForceStop, "MRxSmb20", includeDependents: true, catalog);

        Assert.False(plan.IsRunnable);
        Assert.Equal(PlanProblemKind.CascadeUnreadable, Assert.Single(plan.Problems).Kind);
    }

    [Fact]
    public void A_forced_restart_brings_back_everything_it_took_down_including_the_housemates()
    {
        var plan = Build(ActionKind.ForceRestart, immediate: false, Sharing());

        var started = plan.Steps
            .Where(step => step.Operation == StepOperation.Start)
            .Select(step => step.ServiceName)
            .ToArray();

        // The entry somebody asked about, and the one that only died because it shared the process.
        // Nothing works this out afterwards from what happened - it is decided when the plan is
        // built, which is the reason the housemates are steps in the first place.
        Assert.Equal(["MRxSmb20", "LanmanWorkstation"], started);
        Assert.All(plan.Steps.Where(step => step.Operation == StepOperation.Start),
            step => Assert.Equal(StepReason.Restore, step.Reason));
    }

    /// <summary>
    /// The chain with every entry in a process of its own, which is what 105 of 110 processes on a
    /// real machine look like.
    /// </summary>
    private static FakeScmCatalog Alone() => WithProcesses(
        ("MRxSmb20", 4444), ("LanmanWorkstation", 5555), ("SessionEnv", 6666), ("Netlogon", 7777));

    /// <summary>Two entries in one process, which is what an svchost group looks like.</summary>
    private static FakeScmCatalog Sharing() => WithProcesses(
        ("MRxSmb20", 4444), ("LanmanWorkstation", 4444), ("SessionEnv", 6666), ("Netlogon", 7777));

    private static FakeScmCatalog WithProcesses(params (string Name, int ProcessId)[] processes)
    {
        var catalog = Chain();

        foreach (var (name, processId) in processes)
        {
            catalog = Rebuild(catalog, name, entry => entry with { ProcessId = Reading<int>.Present(processId) });
        }

        return catalog;
    }

    private static OperationPlan Build(ActionKind kind, bool immediate, FakeScmCatalog catalog) =>
        new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(kind, "MRxSmb20", IncludeDependents: false, Immediate: immediate));
}
