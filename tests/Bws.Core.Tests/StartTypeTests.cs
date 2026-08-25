using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The first ask in this product that changes a SETTING rather than asking the manager to move
/// something - `docs/03` calls it setStartType, in the singular, and the owner chose that shape on
/// 2026-08-25 with the alternative beside it.
///
/// <b>Why it gets a file of its own rather than a section in each of four.</b> Everything about it
/// is different from the three asks that came before: no cascade, nothing to wait for, no way back,
/// and no command line verb yet. Those are five decisions, and every one of them is a thing
/// somebody will later take for an oversight.
/// </summary>
public sealed class StartTypeTests
{
    /// <summary>
    /// One step, no cascade, and nothing said about dependents.
    ///
    /// <b>The cascade is the half that would have arrived by accident.</b> Every other ask goes
    /// through a stopping order, and asking for one here would have named the services that depend
    /// on this one in a warning about something that moves none of them.
    /// </summary>
    [Fact]
    public void Setting_a_start_type_is_one_step_and_takes_nothing_down_with_it()
    {
        // AN ENTRY THAT REALLY HAS DEPENDENTS, because on a catalogue where nothing depends on
        // anything this test would pass over a builder that cascades enthusiastically.
        var catalog = new FakeScmCatalog(new List<ScmEntry>(Specimens.All)
        {
            Held("MRxSmb20", "SMB 2.0 Redirector"),
            Held("LanmanWorkstation", "Stacja robocza")
        }).DependedOnBy("MRxSmb20", "LanmanWorkstation");

        var plan = new PlanBuilder(catalog.ReadAll(), catalog).Build(
            new ServiceAction(ActionKind.SetStartType, "MRxSmb20", IncludeDependents: true,
                To: StartType.Disabled));

        Assert.True(
            plan.IsRunnable,
            "The plan refused: " + string.Join(", ", plan.Problems.Select(problem => problem.Kind)));

        var step = Assert.Single(plan.Steps);

        Assert.Equal(StepOperation.SetStartType, step.Operation);
        Assert.Equal("MRxSmb20", step.ServiceName);
        Assert.Equal(StartType.Disabled, step.To);

        // The entry has dependents on this catalogue - that is what it is there for - and none of
        // them is mentioned, because nothing is being taken down.
        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.Cascade);
        Assert.DoesNotContain(
            plan.Warnings, warning => warning.Kind == PlanWarningKind.DependentsInTheWay);
    }

    /// <summary>
    /// Asking for the type it already has is said out loud rather than refused.
    ///
    /// The same answer the other asks give: doing nothing is a legitimate outcome, and the plan says
    /// beforehand that it may be the one.
    /// </summary>
    [Fact]
    public void Setting_the_type_it_already_has_is_said_before_anybody_presses()
    {
        var plan = Plan(StartType.Automatic, "Spooler");

        Assert.True(plan.IsRunnable);
        Assert.Contains(plan.Warnings, warning => warning.Kind == PlanWarningKind.AlreadyThere);
    }

    /// <summary>
    /// A driver is refused here as everywhere else.
    ///
    /// Open question 8 of the specification is still open, and a setting written onto a driver is
    /// among the things that do not come back without a reboot.
    /// </summary>
    [Fact]
    public void A_driver_is_refused_a_start_type_as_it_is_refused_everything_else()
    {
        var plan = Plan(StartType.Manual, "amduw23g-202073-df09ebb6");

        Assert.False(plan.IsRunnable);
        Assert.Equal(PlanProblemKind.NotOperable, Assert.Single(plan.Problems).Kind);
    }

    /// <summary>
    /// The run WRITES the setting and asks the manager to move nothing.
    ///
    /// <b>The second half is the one worth asserting.</b> Until 2026-08-25 nine places read this
    /// enum two ways - a stop, otherwise a start - so a step of a kind nobody taught them would
    /// have been sent to the manager as a START. A machine changed by a step whose preview said
    /// something else is the one fault the whole plan pattern stands against.
    /// </summary>
    [Fact]
    public void A_run_writes_the_setting_and_asks_the_manager_to_move_nothing()
    {
        var control = new FakeScmControl().At("Spooler", EntryStatus.Running);

        var run = Run(control, StartType.Disabled, "Spooler");

        Assert.Equal(StepOutcome.Succeeded, Assert.Single(run.Results).Outcome);
        Assert.Equal(("Spooler", StartType.Disabled), Assert.Single(control.Configured));
        Assert.Empty(control.Requested);
    }

    /// <summary>A refusal to write is a refused step, with the manager's own number on it.</summary>
    [Fact]
    public void A_refusal_to_write_the_setting_is_a_refused_step()
    {
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .RefusingConfiguration("Spooler", 5);

        var run = Run(control, StartType.Disabled, "Spooler");

        var result = Assert.Single(run.Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(5, result.ErrorCode);
    }

    /// <summary>
    /// THE WAY BACK SAYS NOTHING ABOUT A START TYPE, and this test exists to make that a decision
    /// rather than a surprise.
    ///
    /// Undoing one needs the type the entry had BEFORE, and nothing in a result carries it - a step
    /// knows what it set, not what it replaced. So the panel offers no way back for this kind rather
    /// than offering a wrong one. Backlog 229.
    /// </summary>
    [Fact]
    public void The_way_back_says_nothing_about_a_setting_it_cannot_put_back()
    {
        var control = new FakeScmControl().At("Spooler", EntryStatus.Running);

        var run = Run(control, StartType.Disabled, "Spooler");

        Assert.Empty(NetEffect.Of(run.Results));
    }

    /// <summary>
    /// No command line is offered for an ask the command line cannot make.
    ///
    /// <b>The window learned this verb on 2026-08-25 and the command line did not</b> - the owner
    /// chose that order, the same way the plan preview arrived in the window first. A line rendered
    /// for it would be one that fails on paste, which is the fault that class already refuses to
    /// commit over the dependents switch on a start.
    /// </summary>
    [Fact]
    public void No_command_is_offered_for_an_ask_the_command_line_cannot_make()
    {
        var catalog = Specimens.Catalog();

        var plan = new BulkPlanBuilder(catalog.ReadAll(), catalog)
            .Build(new BulkAction(ActionKind.SetStartType, ["Spooler"], To: StartType.Disabled));

        Assert.Empty(EquivalentCommand.For(plan));

        // And the three that DO have one still have it, so this is a hole with edges rather than a
        // section that quietly went away.
        var stopping = new BulkPlanBuilder(catalog.ReadAll(), catalog)
            .Build(new BulkAction(ActionKind.Stop, ["Spooler"]));

        Assert.Equal("bws stop Spooler", Assert.Single(EquivalentCommand.For(stopping)));
    }

    /// <summary>
    /// A kind of step nobody taught this product is refused where it is used, rather than answered
    /// for.
    ///
    /// <b>This is what the first step of 2026-08-25 bought, and it is worth a test because the thing
    /// it replaced looked identical from outside.</b> Nine ternaries read "a stop, otherwise a
    /// start". A compile error was not available - CS8524 means a switch over an enum still needs a
    /// discard, because the variable can hold a number nobody named - so what the discard does is
    /// the whole decision.
    /// </summary>
    [Fact]
    public void A_step_of_a_kind_nobody_taught_this_product_is_refused_rather_than_taken_for_a_start()
    {
        var invented = new StepResult
        {
            Step = new PlanStep("Spooler", "Bufor wydruku", (StepOperation)99, StepReason.Requested),
            Outcome = StepOutcome.Succeeded,
            SkippedBecause = null,
            Status = EntryStatus.Stopped,
            ErrorCode = 0,
            Error = null,
            Milliseconds = 1
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => NetEffect.Of([invented]));
    }

    /// <summary>Running, and set to start on request, so that setting it to disabled is a change.</summary>
    private static ScmEntry Held(string serviceName, string displayName) =>
        Entries.Named(serviceName, displayName) with
        {
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            ProcessId = Reading<int>.Present(4444)
        };


    /// <summary>
    /// EVERY PLACE THAT BRANCHES ON A KIND REFUSES ONE IT DOES NOT KNOW, and this is the test that
    /// makes that sentence true rather than written.
    ///
    /// <b>Nine of these were ternaries until 2026-08-25</b> - "a stop, otherwise a start" - and the
    /// discard arms that replaced them are themselves lines nothing executed. A refusal nobody has
    /// ever run is a refusal nobody has checked.
    ///
    /// <b>The manager is never touched, including by the two that talk to it.</b> Both refuse before
    /// they open a handle, which is the whole point of refusing at the top of the method.
    /// </summary>
    [Fact]
    public void Every_place_that_branches_on_a_kind_refuses_one_it_does_not_know()
    {
        var invented = (StepOperation)99;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => EquivalentCommand.For(new ReversalStep("Spooler", invented)));

        // The one that asks the manager to move something, refused before it opens a handle.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WindowsScmControl().Request("Spooler", invented));

        // And the builder, over an ask nobody taught it.
        var catalog = Specimens.Catalog();

        Assert.Throws<ArgumentOutOfRangeException>(() => new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction((ActionKind)99, "Spooler")));
    }

    /// <summary>
    /// A start type the manager cannot be told is refused before a handle is opened.
    ///
    /// <b>Boot and System belong to drivers</b>, which this tool refuses to operate on, and Unknown
    /// is not a type at all - it is what a reading says when the manager did not answer. Writing any
    /// of the three onto a service is a machine that may not come back, so the refusal is here
    /// rather than at the manager.
    /// </summary>
    [Fact]
    public void A_start_type_the_manager_cannot_be_told_is_refused_before_anything_is_opened()
    {
        var control = new WindowsScmControl();

        foreach (var refused in new[] { StartType.Boot, StartType.System, StartType.Unknown })
        {
            var answer = control.Configure("Spooler", refused);

            Assert.False(answer.Worked);
            Assert.NotNull(answer.Error);
        }
    }

    private static OperationPlan Plan(StartType to, string serviceName)
    {
        var catalog = Specimens.Catalog();

        return new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.SetStartType, serviceName, To: to));
    }

    private static PlanRun Run(FakeScmControl control, StartType to, string serviceName) =>
        new PlanRunner(control, new FakeClock()).Run(Plan(to, serviceName), TimeSpan.FromSeconds(5));
}
