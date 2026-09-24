using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// What 2026-09-24 added to setting a start type: a fourth setting, a writer that puts both halves
/// of it on the machine, a plan that says what a setting does NOT do, and a stop that can ride on
/// it. UX-GUI-006 with backlog 231 and 232, `docs/PROJEKT-TYP-STARTU-20260924.md`.
///
/// <b>The first two tests are the guard backlog 232 asked for before a delayed entry could have a
/// way back - "a guard on both sides of the write".</b> The writer asks StartSettings.Written what
/// to put on the machine and the plan asks StartSettings.Of what an entry has, so the way back is
/// only right while those two are each other's inverse. Which order the writer makes its two calls
/// in is NOT tested here and cannot be - WindowsScmControl calls the system directly - so that half
/// is held by tools/plan-journey on the throwaway machine, and the design says so.
/// </summary>
public sealed class StartSettingTests
{
    /// <summary>Every setting, read back from the pair it writes, is itself - the way back rests on it.</summary>
    [Theory]
    [InlineData(StartSetting.Automatic)]
    [InlineData(StartSetting.AutomaticDelayed)]
    [InlineData(StartSetting.Manual)]
    [InlineData(StartSetting.Disabled)]
    public void Every_setting_reads_back_as_itself_through_the_table_the_writer_uses(StartSetting setting)
    {
        var (type, delayed) = StartSettings.Written(setting);

        var entry = Specimens.Ordinary with
        {
            StartType = Reading<StartType>.Present(type),
            DelayedAuto = Reading<bool>.Present(delayed)
        };

        Assert.Equal(setting, StartSettings.Of(entry));
    }

    /// <summary>
    /// Every setting writes the late flag, false for all but one - the way sc.exe does, measured on
    /// the throwaway machine 2026-09-24, and the owner's decision the same day. Until then this
    /// tool left the flag as it found it, so "Automatic" kept a delayed entry delayed.
    /// </summary>
    [Fact]
    public void Every_setting_writes_the_late_flag_and_only_one_writes_it_true()
    {
        Assert.Equal((StartType.Automatic, false), StartSettings.Written(StartSetting.Automatic));
        Assert.Equal((StartType.Automatic, true), StartSettings.Written(StartSetting.AutomaticDelayed));
        Assert.Equal((StartType.Manual, false), StartSettings.Written(StartSetting.Manual));
        Assert.Equal((StartType.Disabled, false), StartSettings.Written(StartSetting.Disabled));
    }

    /// <summary>
    /// Nothing is named where no setting says what the entry has: a flag nobody could read, a type
    /// only drivers carry, and the late flag on an entry that is not automatic - which none of the
    /// four reproduces. An absent flag is known, and it is "not late".
    /// </summary>
    [Fact]
    public void Nothing_is_named_for_a_setting_this_tool_cannot_write()
    {
        Assert.Null(StartSettings.Of(Specimens.DelayRefused));
        Assert.Null(StartSettings.Of(Specimens.Ordinary with { StartType = Reading<StartType>.Present(StartType.Boot) }));
        Assert.Null(StartSettings.Of(Specimens.Ordinary with
        {
            StartType = Reading<StartType>.Present(StartType.Manual),
            DelayedAuto = Reading<bool>.Present(true)
        }));

        Assert.Equal(StartSetting.Manual, StartSettings.Of(Specimens.Ordinary with
        {
            StartType = Reading<StartType>.Present(StartType.Manual),
            DelayedAuto = Reading<bool>.Absent()
        }));
    }

    /// <summary>
    /// BACKLOG 232: a delayed automatic entry gets its way back, and it names the late setting.
    /// Until 2026-09-24 it got nothing, because the line would have rested on a property of the
    /// writer no guard held - the two tests above are that guard.
    /// </summary>
    [Fact]
    public void A_delayed_automatic_entry_gets_its_way_back_by_name()
    {
        var control = new FakeScmControl().At("BITS", EntryStatus.Running);

        var run = Run(control, Ask("BITS", StartSetting.Disabled));

        var back = Assert.Single(NetEffect.Of(run.Results));

        Assert.Equal(StartSetting.AutomaticDelayed, back.To);
        Assert.Equal("bws start-type BITS delayed", EquivalentCommand.For(back));
    }

    /// <summary>
    /// Automatic on a delayed entry is a real change now - the writer clears the flag - so the plan
    /// no longer says it is already there. The late setting on the same entry is.
    /// </summary>
    [Fact]
    public void Automatic_on_a_delayed_entry_is_a_change_and_the_late_setting_is_not()
    {
        Assert.DoesNotContain(
            Plan(Ask("BITS", StartSetting.Automatic)).Warnings,
            warning => warning.Kind == PlanWarningKind.AlreadyThere);

        Assert.Contains(
            Plan(Ask("BITS", StartSetting.AutomaticDelayed)).Warnings,
            warning => warning.Kind == PlanWarningKind.AlreadyThere);
    }

    /// <summary>UX-GUI-006: disabling a running entry says it keeps running.</summary>
    [Fact]
    public void Disabling_a_running_entry_says_it_keeps_running()
    {
        var warning = Assert.Single(
            Plan(Ask("Spooler", StartSetting.Disabled)).Warnings,
            one => one.Kind == PlanWarningKind.KeepsRunning);

        Assert.Equal("Spooler", warning.ServiceName);
    }

    /// <summary>
    /// Not on a stopped entry, not on Manual (the spec and pitfall P7 are about disabling), and not
    /// where the state was never read - an unread state is not "running".
    /// </summary>
    [Fact]
    public void Only_a_running_entry_set_to_disabled_is_said_to_keep_running()
    {
        Assert.DoesNotContain(
            Plan(Ask("Spooler", StartSetting.Disabled), Specimens.Ordinary with { Status = EntryStatus.Stopped }).Warnings,
            warning => warning.Kind == PlanWarningKind.KeepsRunning);

        Assert.DoesNotContain(
            Plan(Ask("Spooler", StartSetting.Manual)).Warnings,
            warning => warning.Kind == PlanWarningKind.KeepsRunning);

        Assert.DoesNotContain(
            Plan(Ask("Spooler", StartSetting.Disabled), Specimens.Ordinary with { Status = EntryStatus.Unknown }).Warnings,
            warning => warning.Kind is PlanWarningKind.KeepsRunning or PlanWarningKind.StartsAtNextBoot);
    }

    /// <summary>
    /// The mirror: a stopped entry set to start at boot is said to start at the next restart, for
    /// both settings that start at boot - and nothing is offered, because the spec asks for an offer
    /// only on the stopping side.
    /// </summary>
    [Theory]
    [InlineData(StartSetting.Automatic)]
    [InlineData(StartSetting.AutomaticDelayed)]
    public void A_stopped_entry_set_to_start_at_boot_is_said_to_start_at_the_next_restart(StartSetting setting)
    {
        var stopped = Specimens.Ordinary with
        {
            Status = EntryStatus.Stopped,
            StartType = Reading<StartType>.Present(StartType.Manual)
        };

        Assert.Contains(
            Plan(Ask("Spooler", setting), stopped).Warnings,
            warning => warning.Kind == PlanWarningKind.StartsAtNextBoot);

        Assert.DoesNotContain(
            Plan(Ask("Spooler", setting)).Warnings,
            warning => warning.Kind == PlanWarningKind.StartsAtNextBoot);
    }

    /// <summary>
    /// A late start in a load order group is refused when the plan is built, naming the group -
    /// Windows answers 87 to it (measured on Spooler and SCardSvr, 2026-09-24). Automatic on the
    /// same entry is fine.
    /// </summary>
    [Fact]
    public void A_late_start_in_a_load_order_group_is_refused_before_anything_is_planned()
    {
        var grouped = Specimens.Ordinary with { LoadOrderGroup = Reading<string>.Present("SpoolerGroup") };

        var refused = Plan(Ask("Spooler", StartSetting.AutomaticDelayed), grouped);

        Assert.False(refused.IsRunnable);
        var problem = Assert.Single(refused.Problems);
        Assert.Equal(PlanProblemKind.CannotStartLate, problem.Kind);
        Assert.Equal(["SpoolerGroup"], problem.Related);

        Assert.True(Plan(Ask("Spooler", StartSetting.Automatic), grouped).IsRunnable);
    }

    /// <summary>
    /// Taking the offer puts the stop AFTER the setting - so a refused setting holds the stop back -
    /// and the sentence it answered goes. Nor does the plan say the entry comes back after a
    /// restart: that sentence belongs to a stop that leaves the setting automatic.
    /// </summary>
    [Fact]
    public void Taking_the_offer_puts_the_stop_after_the_setting_and_drops_the_sentence()
    {
        var plan = Plan(Ask("Spooler", StartSetting.Disabled, alsoStop: true));

        Assert.Collection(
            plan.Steps,
            step => Assert.Equal((StepOperation.SetStartType, StartSetting.Disabled), (step.Operation, step.To!.Value)),
            step => Assert.Equal((StepOperation.Stop, StepReason.Requested), (step.Operation, step.Reason)));

        Assert.DoesNotContain(
            plan.Warnings,
            warning => warning.Kind is PlanWarningKind.KeepsRunning or PlanWarningKind.ReturnsAfterReboot);
    }

    /// <summary>An entry that is already stopped gets no stop step from the offer.</summary>
    [Fact]
    public void The_offer_adds_no_stop_to_an_entry_already_stopped()
    {
        var plan = Plan(
            Ask("Spooler", StartSetting.Disabled, alsoStop: true),
            Specimens.Ordinary with { Status = EntryStatus.Stopped });

        Assert.Equal(StepOperation.SetStartType, Assert.Single(plan.Steps).Operation);
    }

    /// <summary>
    /// A stop rides only on Disabled. Both interfaces refuse anything else before it gets here, so
    /// the builder is loud rather than polite about it - the same line as a setting without a value.
    /// </summary>
    [Theory]
    [InlineData(StartSetting.Automatic)]
    [InlineData(StartSetting.AutomaticDelayed)]
    [InlineData(StartSetting.Manual)]
    public void A_stop_rides_only_on_disabled(StartSetting setting)
    {
        Assert.Throws<ArgumentException>(() => Plan(Ask("Spooler", setting, alsoStop: true)));
    }

    /// <summary>
    /// The stop is a plain stop: what stands in its way is named, and without --dependents nothing
    /// that depends on the entry is taken down with it.
    /// </summary>
    [Fact]
    public void A_stop_riding_on_a_setting_names_what_is_in_its_way()
    {
        var catalog = new FakeScmCatalog(new List<ScmEntry>(Specimens.All)
        {
            Specimens.Ordinary with { ServiceName = "Fax", DisplayName = "Faks" }
        }).DependedOnBy("Spooler", "Fax");

        var plan = new PlanBuilder(catalog.ReadAll(), catalog).Build(Ask("Spooler", StartSetting.Disabled, alsoStop: true));

        Assert.Contains(
            plan.Warnings,
            warning => warning.Kind == PlanWarningKind.DependentsInTheWay && warning.Related.Contains("Fax"));
        Assert.DoesNotContain(plan.Steps, step => step.ServiceName == "Fax");
    }

    /// <summary>
    /// A selection whose setting carries a stop goes dependents first, the way a plain stop of the
    /// same selection does - LanmanWorkstation depends on MRxSmb20, so it stops first, whatever
    /// order they were picked in. Without the stop the same selection keeps its order and asks the
    /// manager nothing (BulkPlanTests.Setting_a_start_type_asks_the_manager_nothing_about_dependents).
    /// </summary>
    [Fact]
    public void A_selection_that_also_stops_is_ordered_like_a_stop()
    {
        var catalog = DependencyChain.Chain();

        var plan = new BulkPlanBuilder(catalog.ReadAll(), catalog).Build(new BulkAction(
            ActionKind.SetStartType, ["MRxSmb20", "LanmanWorkstation"], To: StartSetting.Disabled, AlsoStop: true));

        Assert.Equal(["LanmanWorkstation", "MRxSmb20"], plan.Plans.Select(one => one.Action.ServiceName));
    }

    /// <summary>The line for the offer is one command, with --stop on it.</summary>
    [Fact]
    public void The_line_for_the_offer_carries_the_stop()
    {
        Assert.Equal(
            "bws start-type Spooler disabled --stop",
            EquivalentCommand.For(Ask("Spooler", StartSetting.Disabled, alsoStop: true)));
    }

    /// <summary>
    /// THE WAY BACK FROM "DISABLE AND STOP" SETS THE ENTRY BACK BEFORE IT STARTS IT. The reverse of
    /// the run would start it first, and a disabled entry refuses to start - so the first line
    /// somebody pasted would fail. Found by the second analysis of 2026-09-24, before any run.
    /// </summary>
    [Fact]
    public void The_way_back_from_disabling_and_stopping_sets_it_back_before_starting_it()
    {
        var control = new FakeScmControl().At("Spooler", EntryStatus.Running);

        var run = Run(control, Ask("Spooler", StartSetting.Disabled, alsoStop: true));

        Assert.All(run.Results, result => Assert.Equal(StepOutcome.Succeeded, result.Outcome));
        Assert.Equal(
            ["bws start-type Spooler automatic", "bws start Spooler"],
            NetEffect.Of(run.Results).Select(EquivalentCommand.For));
    }

    private static ServiceAction Ask(string serviceName, StartSetting to, bool alsoStop = false) =>
        new(ActionKind.SetStartType, serviceName, To: to, AlsoStop: alsoStop);

    /// <summary>A plan over the specimens, with one of them replaced when a test needs another shape.</summary>
    private static OperationPlan Plan(ServiceAction action, ScmEntry? instead = null)
    {
        var entries = Specimens.All
            .Select(entry => instead is not null
                && string.Equals(entry.ServiceName, instead.ServiceName, StringComparison.OrdinalIgnoreCase)
                    ? instead
                    : entry)
            .ToList();

        var catalog = new FakeScmCatalog(entries);

        return new PlanBuilder(catalog.ReadAll(), catalog).Build(action);
    }

    private static PlanRun Run(FakeScmControl control, ServiceAction action) =>
        new PlanRunner(control, new FakeClock()).Run(Plan(action), TimeSpan.FromSeconds(5));
}
