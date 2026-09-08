using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// Rung five of specification <c>C3</c> - say straight away that it cannot be done, rather than
/// trying - and the second half of a process's identity, which the same reading brings back.
///
/// <b>WHY THIS EXISTS AS ITS OWN FILE AND NOT AS MORE OF <see cref="ForcedStopTests"/>.</b> That
/// file is about what a forced stop DECIDES from a listing: who dies with the process, what order,
/// which warnings. Everything here is about a second source that has nothing to do with the
/// listing - the operating system, asked about one process, at the moment the plan is built. The
/// two disagree in interesting ways, which is exactly why they are not the same subject.
///
/// <b>WHAT NONE OF THIS COVERS, said here rather than left to be assumed.</b> The check that
/// actually protects anybody is in <see cref="WindowsScmControl.Terminate"/>: it reads the
/// creation time back through the same handle it kills with, so nothing can slip between the check
/// and the ending. No test in this project can reach that - it needs a real process, a real
/// handle, and a number Windows has given to something else. What is checked here is that the plan
/// CARRIES the identity down to it, which is the half that lives above the seam. The other half is
/// proved by a tool run on a machine that can be thrown away.
/// </summary>
public class EndingRightsTests
{
    /// <summary>
    /// The whole of rung five in one test: refused when the plan is built, so the preview never
    /// shows a machine being taken apart to reach something unreachable.
    /// </summary>
    [Fact]
    public void A_process_windows_will_not_open_for_ending_is_refused_before_any_plan_exists()
    {
        var plan = Plan(Alone(), new FakeEndingFacts().Refusing(4444));

        Assert.False(plan.IsRunnable);
        Assert.Empty(plan.Steps);

        var problem = Assert.Single(plan.Problems);

        Assert.Equal(PlanProblemKind.ProcessCannotBeEnded, problem.Kind);
        Assert.Equal("MRxSmb20", problem.ServiceName);

        // The number and the system's own words both travel, because they are for different
        // readers: one keeps its meaning across machines and the other is what a person reads.
        Assert.Equal(4444, problem.ProcessId);
        Assert.Equal(5, problem.ErrorCode);
        Assert.Equal("Access is denied.", problem.Error);
    }

    /// <summary>
    /// A process that has gone is NOT a process that will not be ended, and the two must not share
    /// a sentence. One is a machine that moved on between the listing and the question, which
    /// happens on any live machine. The other is a permission.
    /// </summary>
    [Fact]
    public void A_process_that_went_away_is_nothing_to_end_rather_than_a_refusal_about_permission()
    {
        var plan = Plan(Alone(), new FakeEndingFacts().Gone(4444));

        Assert.False(plan.IsRunnable);
        Assert.Equal(PlanProblemKind.NoProcessToEnd, Assert.Single(plan.Problems).Kind);
    }

    /// <summary>
    /// Nothing asked means nothing claimed. A plan built without a reader is the plan this product
    /// built before rung five existed, which is a worse experience and an honest one.
    /// </summary>
    [Fact]
    public void With_nobody_to_ask_the_plan_is_built_exactly_as_it_was_before()
    {
        var catalog = Alone();

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.ForceStop, "MRxSmb20", IncludeDependents: false));

        Assert.True(plan.IsRunnable);

        var ending = Assert.Single(plan.Steps, step => step.Operation == StepOperation.Terminate);

        Assert.Equal(4444, ending.ProcessId);

        // AND IT SAYS SO BY CARRYING NOTHING, which is the point of the field being optional. A
        // zero here would be a creation time nothing has, compared against a real one, refusing
        // every ending on the machine.
        Assert.Null(ending.ProcessCreatedAt);
    }

    /// <summary>
    /// The two halves of an identity are read in one go and frozen into the step together, because
    /// a number read at one moment and a time read at another describe two different machines.
    /// </summary>
    [Fact]
    public void The_step_that_ends_a_process_carries_when_that_process_started()
    {
        var plan = Plan(Alone(), new FakeEndingFacts().StartedAt(4444, 4242));

        var ending = Assert.Single(plan.Steps, step => step.Operation == StepOperation.Terminate);

        Assert.Equal(4444, ending.ProcessId);
        Assert.Equal(4242, ending.ProcessCreatedAt);
    }

    /// <summary>
    /// A creation time nobody could read leaves the step without one, rather than with a value
    /// standing in for one. The ending then checks the number alone, exactly as it always did.
    /// </summary>
    [Fact]
    public void A_creation_time_nobody_could_read_is_carried_as_nothing_rather_than_as_a_number()
    {
        var plan = Plan(Alone(), new FakeEndingFacts().WithNoCreationTime(4444));

        Assert.True(plan.IsRunnable);
        Assert.Null(Assert.Single(plan.Steps, step => step.Operation == StepOperation.Terminate)
            .ProcessCreatedAt);
    }

    /// <summary>
    /// The identity reaches the one call that can use it.
    ///
    /// <b>This is the seam and it is worth being plain about what it does and does not prove.</b>
    /// It proves the plan hands the second half down. It does not prove anything about what
    /// happens on the other side of that call, because on the other side is a real handle to a
    /// real process, and the whole value of the check is that it happens THERE.
    /// </summary>
    [Fact]
    public void The_ending_is_handed_the_identity_the_plan_froze()
    {
        var catalog = Alone();

        // ASKED FOR STRAIGHT AWAY, because a polite stop that works makes the ending conditional
        // and the runner then skips it - correctly, and the step under test never runs. That is
        // the escalation behaving exactly as it should and it is not what this test is about.
        var plan = Plan(catalog, new FakeEndingFacts().StartedAt(4444, 4242), immediate: true);
        var control = new FakeScmControl().RunningIn("MRxSmb20", 4444);

        _ = new PlanRunner(control, new FakeClock()).Run(plan, TimeSpan.FromMinutes(1));

        Assert.Equal(4444, Assert.Single(control.Ended));
        Assert.Equal(4242, Assert.Single(control.EndedWith));
    }

    /// <summary>
    /// Asked about the process behind the entry and about nothing else.
    ///
    /// <b>A guard on the COST rather than on the behaviour, and it is here because the cheap
    /// mistake is the expensive one.</b> Two handle opens against one process while a plan is
    /// built is nothing. The same two against every entry in a listing would be on the path a
    /// person waits for, and nothing about the plan would look any different.
    /// </summary>
    [Fact]
    public void Only_the_process_the_plan_would_end_is_asked_about()
    {
        var facts = new FakeEndingFacts();

        _ = Plan(Sharing(), facts);

        Assert.Equal(4444, Assert.Single(facts.Asked));
    }

    /// <summary>
    /// An ordinary stop asks nobody, because nothing about it ends a process.
    /// </summary>
    [Fact]
    public void A_stop_that_ends_nothing_asks_nothing()
    {
        var catalog = Alone();
        var facts = new FakeEndingFacts();

        _ = new PlanBuilder(catalog.ReadAll(), catalog, facts)
            .Build(new ServiceAction(ActionKind.Stop, "MRxSmb20", IncludeDependents: false));

        Assert.Empty(facts.Asked);
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

    private static OperationPlan Plan(
        FakeScmCatalog catalog, FakeEndingFacts facts, bool immediate = false) =>
        new PlanBuilder(catalog.ReadAll(), catalog, facts)
            .Build(new ServiceAction(
                ActionKind.ForceStop, "MRxSmb20", IncludeDependents: false, Immediate: immediate));
}
