using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The first ask in this product that changes a SETTING rather than asking the manager to move
/// something - `docs/03` calls it setStartType, in the singular, and the owner chose that shape on
/// 2026-08-25 with the alternative beside it.
///
/// <b>Why it gets a file of its own rather than a section in each of four.</b> Everything about it
/// is different from the three asks that came before: no cascade, nothing to wait for, and a way
/// back that is a VALUE rather than a direction. Every one of those is a thing somebody will later
/// take for an oversight.
///
/// <b>TWO OF THOSE DIFFERENCES WERE ABSENCES FOR ONE DAY AND ARE NOT ANY MORE.</b> What stood here
/// said "no way back, and no command line verb yet", and both were true on 2026-08-25 and false by
/// the end of that week. They were the same absence seen twice: a way back is a line somebody could
/// type, so it could not exist until there was a verb to type. The tests that held them as decisions
/// rather than oversights are the tests that had to change when the decision did, which is the
/// shape working correctly.
///
/// <b>The last absence closed on 2026-09-24:</b> an automatic entry marked to start late had no way
/// back, because nothing this tool wrote could say "automatic, and late". It can now - the writing
/// side has <see cref="StartSetting"/> - and the entries still without a line are the ones whose
/// setting nobody could read, and the few that carry the late flag while not automatic.
/// StartSettingTests holds the rest of what that day added.
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
                To: StartSetting.Disabled));

        Assert.True(
            plan.IsRunnable,
            "The plan refused: " + string.Join(", ", plan.Problems.Select(problem => problem.Kind)));

        var step = Assert.Single(plan.Steps);

        Assert.Equal(StepOperation.SetStartType, step.Operation);
        Assert.Equal("MRxSmb20", step.ServiceName);
        Assert.Equal(StartSetting.Disabled, step.To);

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
        var plan = Plan(StartSetting.Automatic, "Spooler");

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
        var plan = Plan(StartSetting.Manual, "amduw23g-202073-df09ebb6");

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

        var run = Run(control, StartSetting.Disabled, "Spooler");

        Assert.Equal(StepOutcome.Succeeded, Assert.Single(run.Results).Outcome);
        Assert.Equal(("Spooler", StartSetting.Disabled), Assert.Single(control.Configured));
        Assert.Empty(control.Requested);
    }

    /// <summary>A refusal to write is a refused step, with the manager's own number on it.</summary>
    [Fact]
    public void A_refusal_to_write_the_setting_is_a_refused_step()
    {
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .RefusingConfiguration("Spooler", 5);

        var run = Run(control, StartSetting.Disabled, "Spooler");

        var result = Assert.Single(run.Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(5, result.ErrorCode);
    }

    /// <summary>
    /// THE WAY BACK NAMES THE TYPE THE ENTRY HAD, and until 2026-08-25 it said nothing at all.
    ///
    /// <b>What changed is not the arithmetic but what a step carries.</b> Undoing one of these needs
    /// the type the entry had BEFORE, and a step knows what it set rather than what it replaced - so
    /// the plan builder now reads it off the entry while the plan is being made, which is the only
    /// moment anything is looking at the machine as it was. Backlog 229.
    /// </summary>
    [Fact]
    public void The_way_back_names_the_type_the_entry_had_before()
    {
        var control = new FakeScmControl().At("Spooler", EntryStatus.Running);

        // Automatic on the catalogue, and its delay flag was READ and is false - which is what
        // makes automatic a thing this tool can put back.
        var run = Run(control, StartSetting.Disabled, "Spooler");

        var back = Assert.Single(NetEffect.Of(run.Results));

        Assert.Equal("Spooler", back.ServiceName);
        Assert.Equal(StepOperation.SetStartType, back.Operation);
        Assert.Equal(StartSetting.Automatic, back.To);

        // And it comes out as a line somebody can paste, with the value on it. A verb with no value
        // is a line this tool refuses.
        Assert.Equal("bws start-type Spooler automatic", EquivalentCommand.For(back));
    }

    /// <summary>
    /// Setting an entry to the type it already had leaves nothing to say.
    ///
    /// The same net effect arithmetic a restart gets, arriving at a setting: where it was before the
    /// first step that wrote it, where it is after the last, equal means silence. A way back reading
    /// "set it to what it already is" is a line somebody would run for nothing.
    /// </summary>
    [Fact]
    public void A_setting_that_ended_where_it_began_says_nothing()
    {
        var control = new FakeScmControl().At("Spooler", EntryStatus.Running);

        var run = Run(control, StartSetting.Automatic, "Spooler");

        Assert.Empty(NetEffect.Of(run.Results));
    }

    /// <summary>
    /// NO WAY BACK IS OFFERED WHERE THE SETTING BEFORE IS NOT KNOWN, and there are two ways not to
    /// know it: the manager turned down the configuration read, or it gave the type and turned down
    /// the late start flag beside it.
    ///
    /// <b>BITS stood in this theory until 2026-09-24</b>, as the delayed entry nothing could put
    /// back. It has its own test now, with its line - backlog 232. The half-read one took its place,
    /// because an automatic entry whose flag nobody could read is the case that would now go wrong
    /// quietly: "automatic" would put a delayed entry back starting at boot.
    /// </summary>
    [Theory]
    [InlineData("HalfRead")]
    [InlineData("Locked")]
    public void No_way_back_is_offered_where_the_type_before_is_not_known(string serviceName)
    {
        var control = new FakeScmControl().At(serviceName, EntryStatus.Running);

        var run = Run(control, StartSetting.Disabled, serviceName);

        // The step still ran - this is about what can be said afterwards, not about refusing to do
        // what somebody asked for.
        Assert.Equal(StepOutcome.Succeeded, Assert.Single(run.Results).Outcome);
        Assert.Empty(NetEffect.Of(run.Results));
    }

    /// <summary>
    /// The command line is offered for this ask, and it carries the value.
    ///
    /// <b>The window learned this verb one step ahead of the command line, and for a day the panel
    /// showed no line beside a start type change.</b> The section did not say "no command" - it
    /// disappeared, which is the honest shape while there really is none. It is back, and the test
    /// asserts the whole string rather than its parts, because what a person pastes is the string.
    /// </summary>
    [Fact]
    public void The_command_line_offered_for_this_ask_carries_the_type()
    {
        var catalog = Specimens.Catalog();

        var plan = new BulkPlanBuilder(catalog.ReadAll(), catalog)
            .Build(new BulkAction(ActionKind.SetStartType, ["Spooler"], To: StartSetting.Disabled));

        Assert.Equal("bws start-type Spooler disabled", Assert.Single(EquivalentCommand.For(plan)));
    }

    /// <summary>
    /// THE CASCADE TICK BOX DOES NOT PUT --dependents ON A SETTING, and it very nearly did.
    ///
    /// <b>The condition read "not a start", so every kind but one got the switch</b> - and a
    /// selection carries one such flag for the whole of it, so somebody who ticked the box and then
    /// changed a start type would have been handed a line the command line refuses. That is the
    /// exact fault the bridge guard caught once already over a start, arriving a second time by way
    /// of a kind that did not exist when the condition was written.
    ///
    /// A condition phrased as everything-except answers for kinds nobody has written yet, which is
    /// the same lesson as the nine two-way branches this file's other tests are about.
    /// </summary>
    [Fact]
    public void The_cascade_tick_box_does_not_reach_a_setting()
    {
        var rendered = EquivalentCommand.For(new ServiceAction(
            ActionKind.SetStartType, "Spooler", IncludeDependents: true, To: StartSetting.Manual));

        Assert.Equal("bws start-type Spooler manual", rendered);
    }

    /// <summary>
    /// A setting nobody can name gets no line at all, rather than a line naming it.
    ///
    /// <b>Nothing in this product can build such an ask</b> - every one of the four settings has a
    /// word since 2026-09-24, and the only way to hold anything else is a number cast into the type.
    /// It is asked anyway, because the cost of asking is one call and the cost of not asking is a
    /// line that reads as a command and is declined on paste.
    /// </summary>
    [Fact]
    public void An_ask_carrying_a_type_with_no_word_renders_no_line()
    {
        foreach (var nameless in new[] { (StartSetting)(-1), (StartSetting)99 })
        {
            var action = new ServiceAction(ActionKind.SetStartType, "Spooler", To: nameless);

            Assert.False(EquivalentCommand.Renders(action));
            Assert.Throws<ArgumentOutOfRangeException>(() => EquivalentCommand.For(action));
        }
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
            ProcessId = Reading<int>.NotRead(),
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
    /// A setting the manager cannot be told is refused before a handle is opened.
    ///
    /// <b>Until 2026-09-24 this asked Boot, System and Unknown</b> - read-side start types the
    /// writer refused. The writing side has its own four values now and cannot name those, so what
    /// is left to refuse is a number cast into the type, and it is refused before the manager is
    /// asked anything: a write of something nobody chose is a machine that may not come back.
    /// </summary>
    [Fact]
    public void A_start_type_the_manager_cannot_be_told_is_refused_before_anything_is_opened()
    {
        var control = new WindowsScmControl();

        foreach (var refused in new[] { (StartSetting)(-1), (StartSetting)99 })
        {
            var answer = control.Configure("Spooler", refused);

            Assert.False(answer.Worked);
            Assert.NotNull(answer.Error);
        }
    }

    /// <summary>
    /// Asking to set a start type without one is refused where the plan is built, not where it runs.
    ///
    /// <b>Neither interface can produce this, and the shape of the types allowed it anyway.</b> A
    /// step carries its start type optionally, because a stop and a start have none - and nothing
    /// tied the one operation that needs a type to actually having one. Three places then read it
    /// with a bang, and the one that matters is inside PlanRunner: it would have thrown MIDWAY
    /// THROUGH A RUN, after earlier steps had already changed the machine.
    ///
    /// Loud rather than a problem in the plan, and that is deliberate. A problem is a sentence
    /// somebody reads, and there is no way for anybody to reach this - so the sentence would be
    /// user-facing text about a state no user can be in. Same argument PlanRunner.Run makes about
    /// being handed a plan with problems.
    /// </summary>
    [Fact]
    public void A_start_type_change_with_no_type_is_refused_before_anything_is_planned()
    {
        var catalog = Specimens.Catalog();
        var builder = new PlanBuilder(catalog.ReadAll(), catalog);

        Assert.Throws<ArgumentException>(() =>
            builder.Build(new ServiceAction(ActionKind.SetStartType, "Spooler")));
    }

    private static OperationPlan Plan(StartSetting to, string serviceName)
    {
        var catalog = Specimens.Catalog();

        return new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.SetStartType, serviceName, To: to));
    }

    private static PlanRun Run(FakeScmControl control, StartSetting to, string serviceName) =>
        new PlanRunner(control, new FakeClock()).Run(Plan(to, serviceName), TimeSpan.FromSeconds(5));
}
