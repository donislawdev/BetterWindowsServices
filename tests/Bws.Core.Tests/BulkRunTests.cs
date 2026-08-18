using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Carrying out a plan over a selection, checked without a machine to break.
///
/// <b><see cref="PlanRunnerTests"/> holds everything about carrying out ONE plan and none of it is
/// repeated here.</b> Order inside a plan, waiting, wait hints, restores, refusals stopping the
/// steps behind them - all of that is the runner's and is tested next door. What is left, and what
/// this class is for, is the four answers that only exist once there is more than one plan: that
/// every plan is carried out even after somebody interrupted, that a plan which fails does not take
/// the rest of the selection with it, that steps are numbered across the whole thing, and that there
/// is ONE way back rather than one per run.
///
/// <b>What is deliberately NOT here: an entry moved by two runs being named once.</b> That property
/// is real and it is held by <see cref="NetEffect"/>, which both levels now share - and it is
/// already asserted at PlanReversalTests.An_entry_moved_twice_is_named_once. A copy here would be a
/// second copy of a rule tested next door, which is the thing BulkPlanTests refuses for the same
/// reason. Said out loud rather than left as an apparent gap.
///
/// The chain is the one measured on a real machine on 2026-08-01: MRxSmb20 is needed by
/// LanmanWorkstation, which is needed by SessionEnv and Netlogon.
/// </summary>
public sealed class BulkRunTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    [Fact]
    public void Every_plan_is_carried_out_in_the_order_the_preview_showed()
    {
        var plan = Bulk(ActionKind.Stop, ["Spooler", "MRxSmb20"]);
        var control = Running("Spooler", "MRxSmb20");

        var run = Run(plan, control);

        Assert.Equal(plan.Plans.Count, run.Runs.Count);
        Assert.True(run.Completed);
        Assert.False(run.Cancelled);
        Assert.Equal(["Spooler", "MRxSmb20"], control.Requested);
    }

    /// <summary>
    /// Owner's decision, 2026-08-18: the entries in a selection are independent of each other, so
    /// one that will not move does not hold up the rest.
    ///
    /// <b>The refusal is put FIRST on purpose.</b> Put last, this test would pass just as well if a
    /// failure stopped everything after it - which is the shape two tests in BulkPlanTests were in
    /// until the question "what would I mutate this with" was asked of them.
    /// </summary>
    [Fact]
    public void A_plan_that_fails_does_not_stop_the_plans_after_it()
    {
        var plan = Bulk(ActionKind.Stop, ["Spooler", "MRxSmb20"]);
        var control = Running("Spooler", "MRxSmb20").RefusingRequests("Spooler", AccessDenied);

        var run = Run(plan, control);

        Assert.False(run.Runs[0].Completed);
        Assert.True(run.Runs[1].Completed);
        Assert.False(run.Completed);

        // And it was really tried rather than reported from arithmetic.
        Assert.Equal(["Spooler", "MRxSmb20"], control.Requested);
    }

    /// <summary>
    /// THE ANSWER A SINGLE RUN CANNOT GIVE, and the reason <see cref="BulkRun.Reversal"/> hands
    /// every step of every run to the arithmetic in one go.
    ///
    /// Both entries were selected and one needs the other, so the selection is dealt with dependants
    /// first - LanmanWorkstation, then MRxSmb20. A joined list would be ordered by plan, and a way
    /// back has to be in the reverse of the order things moved.
    ///
    /// <b>What a wrong order actually costs is asserted next door rather than here</b>, in
    /// <see cref="The_way_back_after_a_start_is_the_order_the_manager_will_accept"/> - because the
    /// manager rescues a bad START order and refuses a bad STOP one, measured on 2026-08-19.
    /// </summary>
    [Fact]
    public void The_way_back_is_one_answer_for_the_whole_selection_in_the_reverse_order()
    {
        // Handed over the wrong way round on purpose, so passing means it was reordered rather than
        // that the input happened to be right.
        var plan = Bulk(ActionKind.Stop, ["MRxSmb20", "LanmanWorkstation"]);
        var control = Running("MRxSmb20", "LanmanWorkstation");

        var run = Run(plan, control);

        Assert.Equal(
            ["LanmanWorkstation", "MRxSmb20"],
            run.Runs.Select(one => one.Plan.Action.ServiceName));

        Assert.Equal(
            ["MRxSmb20", "LanmanWorkstation"],
            run.Reversal.Select(step => step.ServiceName));

        Assert.All(run.Reversal, step => Assert.Equal(StepOperation.Start, step.Operation));
    }

    /// <summary>
    /// THE DIRECTION WHERE A JOINED ORDER REALLY FAILS, and it is the opposite one to the direction
    /// this file first claimed - a run on a real machine on 2026-08-19 corrected it.
    ///
    /// <b>The asymmetry is the manager's, and it is the same one <see cref="BulkPlanBuilder"/>
    /// records about ordering a selection at all.</b> Measured on Windows Server 2025, on SessionEnv
    /// which needs LanmanWorkstation: <c>sc start SessionEnv</c> with LanmanWorkstation stopped
    /// SUCCEEDED and brought the other up with it, because the manager starts what a service needs.
    /// <c>sc stop LanmanWorkstation</c> while SessionEnv was running returned <b>error 1051</b> - it
    /// rescues nothing in that direction.
    ///
    /// So a bulk START, whose way back is a list of STOPS, is where the order is load bearing. A
    /// start selection is not reordered, so joining run by run hands the stops back in the order the
    /// plans ran, depended upon first, which is exactly the order the manager refuses. Reversed over
    /// the whole run, the dependant goes first and every line works.
    /// </summary>
    [Fact]
    public void The_way_back_after_a_start_is_the_order_the_manager_will_accept()
    {
        var plan = Bulk(ActionKind.Start, ["MRxSmb20", "LanmanWorkstation"]);

        var control = new FakeScmControl()
            .At("MRxSmb20", EntryStatus.Stopped)
            .At("LanmanWorkstation", EntryStatus.Stopped);

        var run = Run(plan, control);

        // A start selection keeps the order it came in, so the plans ran depended upon first.
        Assert.Equal(
            ["MRxSmb20", "LanmanWorkstation"],
            run.Runs.Select(one => one.Plan.Action.ServiceName));

        // And the way back is the reverse of that: the dependant is stopped first, which is the
        // only order a manager accepts. Joined plan by plan it would be the other way round.
        Assert.Equal(
            ["LanmanWorkstation", "MRxSmb20"],
            run.Reversal.Select(step => step.ServiceName));

        Assert.All(run.Reversal, step => Assert.Equal(StepOperation.Stop, step.Operation));
    }

    /// <summary>
    /// <b>THE STATE (T) OF RULE 10, which is the one this project has paid the most for.</b> The
    /// command line found it on a virtual machine rather than by reasoning: two presses of Ctrl+C
    /// used to end the process outright, leaving half a cascade down and printing nothing at all.
    ///
    /// A selection makes that worse by however many plans come after the interruption. Handing back
    /// fewer runs than there were plans would say nothing about any of them.
    /// </summary>
    [Fact]
    public void An_interrupted_selection_still_reports_what_it_left_alone()
    {
        using var interruption = new CancellationTokenSource();

        var plan = Bulk(ActionKind.Stop, ["Spooler", "MRxSmb20"]);
        var control = Running("Spooler", "MRxSmb20");

        var run = new BulkRunner(new PlanRunner(control, new FakeClock())).Run(
            plan,
            Minute,
            interruption.Token,
            starting: (_, _) => interruption.Cancel());

        Assert.True(run.Cancelled);

        // Every plan has a run, and every step of the plans after the interruption has a result
        // saying it was never tried. That is the whole difference between this and stopping short.
        Assert.Equal(plan.Plans.Count, run.Runs.Count);

        Assert.All(
            run.Runs[1].Results,
            result => Assert.Equal(SkipReason.Cancelled, result.SkippedBecause));

        // The one it did before being told to stop, and nothing after it.
        Assert.Equal(["Spooler"], control.Requested);
    }

    /// <summary>
    /// Progress is read against the numbered list the preview showed, and that list is numbered
    /// across the whole selection. Restarting at one for every plan would show "step 1" four times
    /// to somebody trying to work out where a run has got to.
    /// </summary>
    [Fact]
    public void A_step_announces_its_place_in_the_whole_selection()
    {
        var announced = new List<int>();

        var plan = Bulk(ActionKind.Stop, ["MRxSmb20", "LanmanWorkstation"], includeDependents: true);
        var control = Running("MRxSmb20", "LanmanWorkstation", "SessionEnv", "Netlogon");

        new BulkRunner(new PlanRunner(control, new FakeClock())).Run(
            plan, Minute, starting: (_, number) => announced.Add(number));

        Assert.Equal(Enumerable.Range(1, plan.Steps.Count()), announced);
    }

    [Fact]
    public void A_selection_of_one_goes_through_without_any_ceremony_for_many()
    {
        var run = Run(Bulk(ActionKind.Stop, ["Spooler"]), Running("Spooler"));

        var only = Assert.Single(run.Runs);

        Assert.Equal(StepOutcome.Succeeded, Assert.Single(only.Results).Outcome);
        Assert.True(run.Completed);

        var back = Assert.Single(run.Reversal);
        Assert.Equal("Spooler", back.ServiceName);
        Assert.Equal(StepOperation.Start, back.Operation);
    }

    [Fact]
    public void A_selection_that_was_already_where_it_was_asked_to_be_offers_no_way_back()
    {
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Stopped)
            .At("MRxSmb20", EntryStatus.Stopped);

        var run = Run(Bulk(ActionKind.Stop, ["Spooler", "MRxSmb20"]), control);

        Assert.True(run.Completed);
        Assert.Empty(control.Requested);
        Assert.Empty(run.Reversal);
    }

    [Fact]
    public void A_selection_with_nothing_in_it_is_never_carried_out()
    {
        var runner = new BulkRunner(new PlanRunner(new FakeScmControl(), new FakeClock()));

        Assert.Throws<InvalidOperationException>(() => runner.Run(Bulk(ActionKind.Stop, []), Minute));
    }

    /// <summary>
    /// <b>REFUSED BEFORE ANY OF THEM RUNS, not when the bad one is reached.</b> The builder never
    /// puts a plan with problems into a bulk, so this is a defence rather than a path - and the
    /// reason it is checked up front is that reaching it half way would mean a machine already moved
    /// by the plans before it, and an exception where a report should be.
    /// </summary>
    [Fact]
    public void A_selection_holding_a_plan_with_problems_is_never_carried_out()
    {
        var catalog = Chain();
        var builder = new PlanBuilder(catalog.ReadAll(), catalog);

        var bulk = new BulkPlan
        {
            Action = new BulkAction(ActionKind.Stop, ["Spooler", "amduw23g-202073-df09ebb6"]),
            Plans =
            [
                builder.Build(new ServiceAction(ActionKind.Stop, "Spooler")),
                builder.Build(new ServiceAction(ActionKind.Stop, "amduw23g-202073-df09ebb6"))
            ],
            Problems = []
        };

        var control = Running("Spooler");
        var runner = new BulkRunner(new PlanRunner(control, new FakeClock()));

        Assert.Throws<InvalidOperationException>(() => runner.Run(bulk, Minute));

        // Nothing was touched, which is the half that makes the check worth putting up front.
        Assert.Empty(control.Requested);
    }

    // -- fixtures --------------------------------------------------------------------------

    private const int AccessDenied = 5;

    private static BulkRun Run(BulkPlan plan, FakeScmControl control) =>
        new BulkRunner(new PlanRunner(control, new FakeClock())).Run(plan, Minute);

    private static FakeScmControl Running(params string[] serviceNames)
    {
        var control = new FakeScmControl();

        foreach (var serviceName in serviceNames)
        {
            control.At(serviceName, EntryStatus.Running);
        }

        return control;
    }

    private static BulkPlan Bulk(
        ActionKind kind, IReadOnlyList<string> names, bool includeDependents = false)
    {
        var catalog = Chain();

        return new BulkPlanBuilder(catalog.ReadAll(), catalog)
            .Build(new BulkAction(kind, names, includeDependents));
    }

    /// <summary>The chain as the manager describes it, transitive sets included.</summary>
    private static FakeScmCatalog Chain()
    {
        var entries = new List<ScmEntry>(Specimens.All)
        {
            Entry("MRxSmb20", "SMB 2.0 Redirector"),
            Entry("LanmanWorkstation", "Stacja robocza"),
            Entry("SessionEnv", "Konfiguracja pulpitu zdalnego"),
            Entry("Netlogon", "Logowanie do sieci")
        };

        return new FakeScmCatalog(entries)
            .DependedOnBy("MRxSmb20", "SessionEnv", "Netlogon", "LanmanWorkstation")
            .DependedOnBy("LanmanWorkstation", "SessionEnv", "Netlogon");
    }

    private static ScmEntry Entry(string serviceName, string displayName) =>
        Entries.Named(serviceName, displayName) with
        {
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            ProcessId = Reading<int>.Present(4444)
        };
}
