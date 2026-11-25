using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// A recovery write, from the ask to the way back - backlog 555 part (a2), the owner's decisions of 2026-11-24.
/// The fake machine is the plan's reader and the run's writer at once, so the plan reads what the run changes.
/// </summary>
public sealed class RecoveryPlanTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    /// <summary>Spooler as the owner's machine has it: restart, restart, nothing, an hour, no texts, the flag off.</summary>
    private static readonly RecoverySettings SpoolersOwn = new(
        new RecoveryList(
            [
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1)),
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1)),
                new RecoveryItem(RecoveryAction.Nothing, TimeSpan.Zero)
            ],
            Hour),
        false);

    private static readonly ScmEntry Spooler = Entries.Any with
    {
        ServiceName = "Spooler",
        DisplayName = "Bufor wydruku",
        Status = EntryStatus.Running,
        Account = Reading<string>.Present("LocalSystem")
    };

    [Fact]
    public void A_list_alone_keeps_the_period_the_entry_has_and_puts_both_back_afterwards()
    {
        var machine = new FakeScmControl().At("Spooler", EntryStatus.Running).WithRecovery("Spooler", Reading<RecoverySettings>.Present(SpoolersOwn));
        var asked = Change(RecoveryParts.Actions, items: [new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(2))]);

        var plan = Plan(machine, asked);
        var write = Assert.Single(plan.Steps).Recovering!;

        // Both halves, the period read off the entry: the manager writes them as one.
        Assert.Equal(RecoveryParts.Actions | RecoveryParts.ResetPeriod, write.Change.Parts);
        Assert.Equal(Hour, write.Change.To.List.ResetPeriod);

        var run = Run(machine, plan);

        Assert.True(run.Completed);
        Assert.Equal("bws recovery Spooler restart:1m restart:1m nothing --reset 1h", EquivalentCommand.For(Assert.Single(run.Reversal)));
    }

    [Fact]
    public void A_list_somebody_changed_after_the_preview_is_not_written_over()
    {
        var machine = new FakeScmControl().At("Spooler", EntryStatus.Running).WithRecovery("Spooler", Reading<RecoverySettings>.Present(SpoolersOwn));
        var plan = Plan(machine, Change(RecoveryParts.Actions, items: []));

        machine.ChangeRecoveryBehindTheBack("Spooler", SpoolersOwn with { List = SpoolersOwn.List with { ResetPeriod = TimeSpan.FromDays(1) } });

        var run = Run(machine, plan);

        Assert.Equal(StepOutcome.Failed, Assert.Single(run.Results).Outcome);
        Assert.Empty(machine.RecoveryWrites);
        Assert.Empty(run.Reversal);
    }

    [Fact]
    public void Stop_pressed_while_the_run_reads_the_recovery_again_writes_nothing()
    {
        // The run reads the entry's recovery once more before it writes, and Stop pressed during that reading
        // is heard after it - the step is held as one not reached (logic report R09).
        using var interruption = new CancellationTokenSource();
        var machine = new FakeScmControl().At("Spooler", EntryStatus.Running).WithRecovery("Spooler", Reading<RecoverySettings>.Present(SpoolersOwn));
        var plan = Plan(machine, Change(RecoveryParts.ResetPeriod, reset: TimeSpan.FromDays(1)));

        var run = new PlanRunner(new Interrupting(machine, interruption, Interrupting.Moment.WhileReadingRecovery), new FakeClock())
            .Run(plan, TimeSpan.FromSeconds(5), interruption.Token);

        Assert.Equal(SkipReason.Cancelled, Assert.Single(run.Results).SkippedBecause);
        Assert.Empty(machine.RecoveryWrites);
        Assert.Empty(run.Reversal);
    }

    [Fact]
    public void A_part_nobody_named_is_not_compared_before_the_write()
    {
        var machine = new FakeScmControl().At("Spooler", EntryStatus.Running).WithRecovery("Spooler", Reading<RecoverySettings>.Present(SpoolersOwn));
        var plan = Plan(machine, Texted(RecoveryParts.Command, "C:\\fix.cmd"));

        // The flag changes between preview and press - this write does not touch it, so it goes ahead.
        machine.ChangeRecoveryBehindTheBack("Spooler", SpoolersOwn with { OnNonCrashFailures = true });

        var run = Run(machine, plan);

        Assert.True(run.Completed);
        Assert.Equal("bws recovery Spooler --program \"\"", EquivalentCommand.For(Assert.Single(run.Reversal)));
    }

    [Fact]
    public void The_same_values_are_said_to_be_there_already_and_leave_no_way_back()
    {
        var machine = new FakeScmControl().At("Spooler", EntryStatus.Running).WithRecovery("Spooler", Reading<RecoverySettings>.Present(SpoolersOwn));
        var plan = Plan(machine, Change(RecoveryParts.ResetPeriod, reset: Hour));

        Assert.Contains(plan.Warnings, warning => warning.Kind == PlanWarningKind.AlreadyThere);
        Assert.Empty(Run(machine, plan).Reversal);
    }

    [Fact]
    public void A_list_holding_an_item_with_no_name_is_not_touched_and_its_texts_still_are()
    {
        var schedule = SpoolersOwn with
        {
            List = new RecoveryList([new RecoveryItem(RecoveryAction.Unnamed, TimeSpan.Zero)], Hour)
        };

        var machine = new FakeScmControl().WithRecovery("Spooler", Reading<RecoverySettings>.Present(schedule));

        Assert.Equal(PlanProblemKind.RecoveryUnnamed, Assert.Single(Plan(machine, Change(RecoveryParts.ResetPeriod, reset: Hour)).Problems).Kind);
        Assert.True(Plan(machine, Texted(RecoveryParts.RebootMessage, "bye")).IsRunnable);

        // The window's form carries the list it read, so a ticked list can ask for that item - refused with the
        // same reason, also over an entry whose own list could not be read.
        var unread = new FakeScmControl().WithRecovery("Spooler", Reading<RecoverySettings>.Denied(5, "Access is denied."));

        Assert.Equal(PlanProblemKind.RecoveryUnnamed, Assert.Single(Plan(machine, Change(RecoveryParts.Actions, schedule.List.Items)).Problems).Kind);
        Assert.Equal(PlanProblemKind.RecoveryUnnamed, Assert.Single(Plan(unread, Change(RecoveryParts.Actions | RecoveryParts.ResetPeriod, schedule.List.Items, Hour)).Problems).Kind);
    }

    [Fact]
    public void Half_the_list_of_an_entry_that_could_not_be_read_or_that_has_no_items_is_refused()
    {
        var unread = new FakeScmControl().WithRecovery("Spooler", Reading<RecoverySettings>.Denied(5, "Access is denied."));
        var empty = new FakeScmControl();

        Assert.Equal(PlanProblemKind.RecoveryUnreadable, Assert.Single(Plan(unread, Change(RecoveryParts.ResetPeriod, reset: Hour)).Problems).Kind);
        Assert.Equal(PlanProblemKind.NoActionsToReset, Assert.Single(Plan(empty, Change(RecoveryParts.ResetPeriod, reset: Hour)).Problems).Kind);

        // Both halves need nothing read - planned, with no way back to offer.
        var whole = Plan(unread, Change(RecoveryParts.Actions | RecoveryParts.ResetPeriod, reset: Hour));

        Assert.True(whole.IsRunnable);
        Assert.Null(Assert.Single(whole.Steps).Recovering!.From);
    }

    [Fact]
    public void A_program_that_will_run_names_the_account_and_a_computer_restart_is_said()
    {
        var machine = new FakeScmControl();
        var plan = Plan(
            machine,
            Texted(
                RecoveryParts.Actions | RecoveryParts.Command,
                "C:\\fix.cmd",
                [
                    new RecoveryItem(RecoveryAction.RunProgram, TimeSpan.Zero),
                    new RecoveryItem(RecoveryAction.RestartComputer, TimeSpan.FromMinutes(1))
                ]));

        var runsAs = Assert.Single(plan.Warnings, warning => warning.Kind == PlanWarningKind.ProgramRunsAs);

        Assert.Equal(["LocalSystem"], runsAs.Related);
        Assert.Contains(plan.Warnings, warning => warning.Kind == PlanWarningKind.RestartsComputerOnFailure);

        // A program set beside a list that never runs one is not said to run.
        Assert.DoesNotContain(
            Plan(new FakeScmControl().WithRecovery("Spooler", Reading<RecoverySettings>.Present(SpoolersOwn)), Texted(RecoveryParts.Command, "C:\\fix.cmd")).Warnings,
            warning => warning.Kind == PlanWarningKind.ProgramRunsAs);
    }

    /// <summary>
    /// A program no failure runs is said when this plan sets the program or changes the list - and not when it
    /// only moves the period of a list that already did not run it, nor when it removes the program (owner's
    /// decision P3, 2026-11-25).
    /// </summary>
    [Fact]
    public void A_program_no_failure_runs_is_said_when_this_plan_leaves_it_so()
    {
        var unused = SpoolersOwn with { List = SpoolersOwn.List with { Command = "C:\\old.cmd" } };
        var runsIt = new RecoveryItem(RecoveryAction.RunProgram, TimeSpan.FromMinutes(1));

        bool Says(RecoverySettings had, RecoveryChange asked) =>
            Plan(new FakeScmControl().WithRecovery("Spooler", Reading<RecoverySettings>.Present(had)), asked)
                .Warnings.Any(warning => warning.Kind == PlanWarningKind.ProgramUnused);

        Assert.True(Says(SpoolersOwn, Texted(RecoveryParts.Command, "C:\\fix.cmd")));
        Assert.True(Says(unused, Change(RecoveryParts.Actions, [new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(2))])));
        Assert.False(Says(unused, Change(RecoveryParts.ResetPeriod, reset: TimeSpan.FromHours(2))));
        Assert.False(Says(SpoolersOwn, Texted(RecoveryParts.Actions | RecoveryParts.Command, "C:\\fix.cmd", [runsIt])));
        Assert.False(Says(unused, Change(RecoveryParts.Command)));
    }

    /// <summary>
    /// The same for the message before a restart of the computer (owner's decision P6, 2026-11-25): said when it
    /// is set beside a list that never restarts the computer, and not when the list does.
    /// </summary>
    [Fact]
    public void A_message_no_failure_sends_is_said_when_this_plan_leaves_it_so()
    {
        bool Says(RecoveryChange asked) =>
            Plan(new FakeScmControl().WithRecovery("Spooler", Reading<RecoverySettings>.Present(SpoolersOwn)), asked)
                .Warnings.Any(warning => warning.Kind == PlanWarningKind.RebootMessageUnused);

        Assert.True(Says(Texted(RecoveryParts.RebootMessage, "Restarting")));
        Assert.False(Says(Texted(
            RecoveryParts.Actions | RecoveryParts.RebootMessage,
            "Restarting",
            [new RecoveryItem(RecoveryAction.RestartComputer, TimeSpan.FromMinutes(1))])));
        Assert.False(Says(Texted(RecoveryParts.Command, "C:\\fix.cmd")));
    }

    /// <summary>
    /// Two items or more with a period of zero run the first item only, and the plan says so whenever it writes
    /// the list - backlog 650, owner's decision P3 of 2026-11-25. One item at zero is not that, and a plan that
    /// does not write the list says nothing about it.
    /// </summary>
    [Fact]
    public void A_list_of_two_with_no_reset_period_is_said_to_run_its_first_item_only()
    {
        bool Says(RecoveryChange asked) =>
            Plan(new FakeScmControl().WithRecovery("Spooler", Reading<RecoverySettings>.Present(SpoolersOwn)), asked)
                .Warnings.Any(warning => warning.Kind == PlanWarningKind.EveryFailureFirst);

        Assert.True(Says(Change(RecoveryParts.ResetPeriod, reset: TimeSpan.Zero)));
        Assert.True(Says(Change(
            RecoveryParts.Actions | RecoveryParts.ResetPeriod,
            [new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1)), new RecoveryItem(RecoveryAction.RunProgram, TimeSpan.FromMinutes(1))],
            TimeSpan.Zero)));
        Assert.False(Says(Change(RecoveryParts.ResetPeriod, reset: Hour * 2)));
        Assert.False(Says(Change(
            RecoveryParts.Actions | RecoveryParts.ResetPeriod,
            [new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1))],
            TimeSpan.Zero)));
        Assert.False(Says(Texted(RecoveryParts.Command, "C:\\fix.cmd")));
    }

    private static PlanRun Run(FakeScmControl machine, OperationPlan plan) =>
        new PlanRunner(machine, new FakeClock()).Run(plan, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private static OperationPlan Plan(FakeScmControl machine, RecoveryChange asked)
    {
        var catalog = new FakeScmCatalog(Spooler);

        return new PlanBuilder(catalog.ReadAll(), catalog, recovery: machine)
            .Build(new ServiceAction(ActionKind.SetRecovery, "Spooler", Recovery: asked));
    }

    /// <summary>A change of these parts, with these items and this reset period.</summary>
    private static RecoveryChange Change(RecoveryParts parts, IReadOnlyList<RecoveryItem>? items = null, TimeSpan? reset = null) =>
        new(parts, new RecoverySettings(new RecoveryList(items ?? [], reset ?? TimeSpan.Zero), false));

    /// <summary>A change of these parts whose text is both the program and the message - only the named one is read.</summary>
    private static RecoveryChange Texted(RecoveryParts parts, string text, IReadOnlyList<RecoveryItem>? items = null) =>
        new(parts, new RecoverySettings(new RecoveryList(items ?? [], TimeSpan.Zero) { Command = text, RebootMessage = text }, false));
}
