using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The half of ADR-11 that decides what will happen, checked without anything happening.
///
/// This is the payoff the whole double was built for. Cascade, order and warnings are all
/// worked out before a single service is touched, so they can be checked here rather than
/// by stopping things on a machine and looking at the wreckage. The other half - that
/// execution does what the plan said - needs a machine and belongs to the run on the
/// virtual one.
///
/// The chain used throughout was measured on a real machine on 2026-08-01:
/// MRxSmb20 is needed by LanmanWorkstation, which is needed by SessionEnv and Netlogon.
/// The manager's own answer already reaches past the first hop, so the double states the
/// transitive sets exactly as the manager gives them.
/// </summary>
public sealed class PlanBuilderTests
{
    [Fact]
    public void A_name_nobody_has_produces_no_plan_at_all()
    {
        var plan = Plan(ActionKind.Stop, "NoSuchService");

        Assert.False(plan.IsRunnable);
        Assert.Empty(plan.Steps);
        Assert.Equal(PlanProblemKind.UnknownService, Assert.Single(plan.Problems).Kind);
    }

    [Fact]
    public void A_driver_is_refused_rather_than_attempted()
    {
        // Open question 8 of the specification asks whether drivers get operations at all.
        // Refusing is the answer that can be reversed later without having made anybody's
        // machine unbootable in the meantime.
        var plan = Plan(ActionKind.Stop, "amduw23g-202073-df09ebb6");

        Assert.False(plan.IsRunnable);
        Assert.Equal(PlanProblemKind.NotOperable, Assert.Single(plan.Problems).Kind);
    }

    [Fact]
    public void Stopping_something_nothing_needs_is_one_step()
    {
        var plan = Plan(ActionKind.Stop, "Spooler");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("Spooler", step.ServiceName);
        Assert.Equal(StepOperation.Stop, step.Operation);
        Assert.Equal(StepReason.Requested, step.Reason);
        Assert.Empty(plan.Cascade);
    }

    [Fact]
    public void Stopping_something_others_need_takes_them_down_first()
    {
        var plan = Plan(ActionKind.Stop, "MRxSmb20", Chain());

        Assert.Equal(
            ["SessionEnv", "Netlogon", "LanmanWorkstation", "MRxSmb20"],
            plan.Steps.Select(step => step.ServiceName));

        Assert.All(plan.Steps, step => Assert.Equal(StepOperation.Stop, step.Operation));

        // The three that came along were not asked for, and the preview has to say which.
        Assert.Equal(["SessionEnv", "Netlogon", "LanmanWorkstation"], plan.Cascade.Select(step => step.ServiceName));
    }

    [Fact]
    public void The_order_holds_whatever_order_the_manager_listed_them_in()
    {
        // The manager returned them deepest first in every sample, and that is promised
        // nowhere. An order that is right by luck fails on somebody else's machine at the
        // worst possible moment, so the order is worked out rather than inherited.
        var reversed = Chain(["LanmanWorkstation", "Netlogon", "SessionEnv"]);

        var order = Plan(ActionKind.Stop, "MRxSmb20", reversed)
            .Steps.Select(step => step.ServiceName).ToList();

        // Stated as the rule rather than as one correct sequence. SessionEnv and Netlogon
        // do not depend on each other, so either may go first and pinning one would be
        // asserting an accident. What must hold is that both are down before the thing they
        // both need, and that the service somebody actually asked about goes last.
        Assert.Equal(4, order.Count);
        Assert.Equal("MRxSmb20", order[^1]);
        Assert.True(order.IndexOf("SessionEnv") < order.IndexOf("LanmanWorkstation"));
        Assert.True(order.IndexOf("Netlogon") < order.IndexOf("LanmanWorkstation"));
    }

    [Fact]
    public void Something_already_stopped_is_not_a_step_in_somebody_elses_cascade()
    {
        // A preview is read carefully or not at all, and a line saying "stop the thing that
        // is already stopped" spends the reader's attention on nothing.
        var catalog = Chain();
        catalog = Rebuild(catalog, "Netlogon", entry => entry with { Status = EntryStatus.Stopped });

        var plan = Plan(ActionKind.Stop, "MRxSmb20", catalog);

        Assert.Equal(
            ["SessionEnv", "LanmanWorkstation", "MRxSmb20"],
            plan.Steps.Select(step => step.ServiceName));
    }

    [Fact]
    public void A_restart_puts_back_what_it_took_down_in_the_mirror_order()
    {
        // C11 in one action. What stopped last has to start first, or the starts fail on
        // the dependencies that are still down.
        var plan = Plan(ActionKind.Restart, "MRxSmb20", Chain());

        Assert.Equal(
            [
                ("SessionEnv", StepOperation.Stop),
                ("Netlogon", StepOperation.Stop),
                ("LanmanWorkstation", StepOperation.Stop),
                ("MRxSmb20", StepOperation.Stop),
                ("MRxSmb20", StepOperation.Start),
                ("LanmanWorkstation", StepOperation.Start),
                ("Netlogon", StepOperation.Start),
                ("SessionEnv", StepOperation.Start)
            ],
            plan.Steps.Select(step => (step.ServiceName, step.Operation)));
    }

    [Fact]
    public void Starting_something_does_not_drag_anybody_along()
    {
        // Starting is not the mirror of stopping. The manager brings up what the service
        // needs by itself, and nothing that merely depends on it has to move.
        var plan = Plan(ActionKind.Start, "MRxSmb20", Chain());

        var step = Assert.Single(plan.Steps);
        Assert.Equal(StepOperation.Start, step.Operation);
    }

    [Fact]
    public void Without_the_word_a_stop_leaves_the_others_alone_and_says_they_are_in_the_way()
    {
        // The safety property. Somebody asking to stop one service is not asking to stop
        // four, and the manager refuses the stop anyway while they run - so the honest plan
        // is the one step that was asked for, plus who is standing in front of it.
        var plan = Plan(ActionKind.Stop, "MRxSmb20", includeDependents: false, Chain());

        Assert.Equal(["MRxSmb20"], plan.Steps.Select(step => step.ServiceName));
        Assert.Empty(plan.Cascade);

        var warning = Warning(plan, PlanWarningKind.DependentsInTheWay);
        Assert.Equal(["SessionEnv", "Netlogon", "LanmanWorkstation"], warning.Related);

        // And no cascade warning, because nothing is going to cascade.
        Assert.DoesNotContain(plan.Warnings, other => other.Kind == PlanWarningKind.Cascade);
    }

    [Fact]
    public void A_cascade_that_would_need_a_driver_stopped_offers_no_plan_at_all()
    {
        // Found by reading a real plan rather than by reasoning. Stopping BFE on the owner's
        // machine drags in two kernel drivers, and a plan that refuses a driver as its
        // target while listing two of them as steps contradicts itself exactly where it has
        // to be trusted. A problem rather than a warning, because the steps underneath
        // would be a preview of something that was never going to happen.
        var catalog = new FakeScmCatalog(
            [
                Running("Firewall", "Base filtering"),
                Running("Inspector", "Network inspection"),
                Entries.Named("InspectDrv", "Inspection driver") with
                {
                    EntryType = EntryType.KernelDriver,
                    Status = EntryStatus.Running,
                    StartType = Reading<StartType>.Present(Core.StartType.Manual),
                    DelayedAuto = Reading<bool>.Absent()
                }
            ])
            .DependedOnBy("Firewall", "InspectDrv", "Inspector");

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Stop, "Firewall", IncludeDependents: true));

        Assert.False(plan.IsRunnable);
        Assert.Empty(plan.Steps);

        var problem = Assert.Single(plan.Problems);
        Assert.Equal(PlanProblemKind.CascadeNotOperable, problem.Kind);
        Assert.Equal(["InspectDrv"], problem.Related);
    }

    [Fact]
    public void A_driver_in_the_way_is_only_a_problem_when_it_would_have_to_move()
    {
        // Without the word, nothing in the cascade is going to be touched, so a driver
        // among them is somebody to name rather than a reason to refuse.
        var catalog = new FakeScmCatalog(
            [
                Running("Firewall", "Base filtering"),
                Entries.Named("InspectDrv", "Inspection driver") with
                {
                    EntryType = EntryType.KernelDriver,
                    Status = EntryStatus.Running,
                    StartType = Reading<StartType>.Present(Core.StartType.Manual),
                    DelayedAuto = Reading<bool>.Absent()
                }
            ])
            .DependedOnBy("Firewall", "InspectDrv");

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Stop, "Firewall"));

        Assert.True(plan.IsRunnable);
        Assert.Equal(["InspectDrv"], Warning(plan, PlanWarningKind.DependentsInTheWay).Related);
    }

    [Fact]
    public void Without_the_word_a_stop_with_nothing_in_the_way_is_unremarkable()
    {
        var plan = Plan(ActionKind.Stop, "Spooler", includeDependents: false);

        Assert.Single(plan.Steps);
        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.DependentsInTheWay);
    }

    [Fact]
    public void A_restart_of_a_disabled_entry_is_refused_rather_than_leaving_it_switched_off()
    {
        // Found by restarting a disabled but running service on a real machine: it stopped,
        // the manager refused to start it back, and the report explained the outage
        // afterwards. A command without --dry-run has no moment at which anybody reads a
        // warning, so the only thing that helps is not offering the plan.
        var catalog = new FakeScmCatalog([Disabled("LinkAgent", "LinkAgent")]);

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Restart, "LinkAgent"));

        Assert.False(plan.IsRunnable);
        Assert.Empty(plan.Steps);

        var problem = Assert.Single(plan.Problems);
        Assert.Equal(PlanProblemKind.CannotComeBack, problem.Kind);
        Assert.Equal(["LinkAgent"], problem.Related);
    }

    [Fact]
    public void A_restart_is_refused_for_a_disabled_entry_in_the_cascade_as_well()
    {
        // One level removed and exactly as bad: the cascade takes it down on the way to
        // something else, and the mirror half cannot put it back.
        var catalog = new FakeScmCatalog(
            [
                Running("Host", "Host service"),
                Disabled("Rider", "Rides on the host")
            ])
            .DependedOnBy("Host", "Rider");

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Restart, "Host", IncludeDependents: true));

        Assert.False(plan.IsRunnable);
        Assert.Equal(["Rider"], Assert.Single(plan.Problems).Related);
    }

    [Fact]
    public void Stopping_a_disabled_entry_is_still_perfectly_fine()
    {
        // Nothing is promised back, so nothing is broken. Somebody asking to stop a disabled
        // service is asking for exactly what they will get.
        var catalog = new FakeScmCatalog([Disabled("LinkAgent", "LinkAgent")]);

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Stop, "LinkAgent"));

        Assert.True(plan.IsRunnable);
    }

    [Fact]
    public void Whether_a_start_will_work_is_left_to_the_manager()
    {
        // The line this refusal must not cross. We do not predict success: the manager is
        // the authority and its reasons go past start type - the refusal it gives says "or
        // because it has no enabled devices associated with it" in the same sentence.
        // Refusing a plain start here would be us guessing at its job.
        var catalog = new FakeScmCatalog([Disabled("LinkAgent", "LinkAgent")]);

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Start, "LinkAgent"));

        Assert.True(plan.IsRunnable);
    }

    [Fact]
    public void A_start_type_nobody_could_read_is_not_a_reason_to_refuse()
    {
        // Missing information must not turn into a decision. That is the whole reason the
        // read outcomes have four states rather than two.
        var unreadable = Running("LinkAgent", "LinkAgent") with
        {
            StartType = Reading<StartType>.Denied(Entries.AccessDenied, "access denied")
        };

        var catalog = new FakeScmCatalog([unreadable]);

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Restart, "LinkAgent"));

        Assert.True(plan.IsRunnable);
    }

    // -- warnings -------------------------------------------------------------------------

    [Fact]
    public void A_cascade_is_warned_about_and_names_everyone_in_it()
    {
        var warning = Warning(Plan(ActionKind.Stop, "MRxSmb20", Chain()), PlanWarningKind.Cascade);

        Assert.Equal(["SessionEnv", "Netlogon", "LanmanWorkstation"], warning.Related);
    }

    [Fact]
    public void An_unreadable_cascade_is_said_out_loud_rather_than_shown_as_an_empty_one()
    {
        // The worst thing this pattern can produce is a preview shorter than what happens.
        // On a machine where the dependents cannot be read, the honest plan is one step and
        // an admission, not one step and silence.
        var catalog = Chain();
        catalog.RefuseDependentsFor.Add("MRxSmb20");

        var plan = Plan(ActionKind.Stop, "MRxSmb20", catalog);

        Assert.Single(plan.Steps);
        Assert.Equal("MRxSmb20", Warning(plan, PlanWarningKind.CascadeUnreadable).ServiceName);

        // And no cascade warning, because claiming there is none would be the lie.
        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.Cascade);
    }

    [Fact]
    public void Stopping_an_automatic_service_says_it_comes_back_after_a_reboot()
    {
        // Glossary pitfall P7. Somebody who stops an automatic service and walks away has
        // done something temporary, which is rarely what they meant.
        Assert.NotNull(Warning(Plan(ActionKind.Stop, "Spooler"), PlanWarningKind.ReturnsAfterReboot));
    }

    [Fact]
    public void Stopping_a_manual_service_says_nothing_about_reboots()
    {
        Assert.DoesNotContain(
            Plan(ActionKind.Stop, "PushToInstall").Warnings,
            warning => warning.Kind == PlanWarningKind.ReturnsAfterReboot);
    }

    [Fact]
    public void Stopping_one_of_several_services_in_a_process_names_the_neighbours()
    {
        // The obvious mental model is wrong here: the process does not go away, and the
        // neighbours keep running inside it.
        var warning = Warning(Plan(ActionKind.Stop, "DcomLaunch"), PlanWarningKind.SharedProcess);

        Assert.Equal(["PlugPlay"], warning.Related);
    }

    [Fact]
    public void Asking_for_a_state_something_is_already_in_says_so()
    {
        Assert.NotNull(Warning(Plan(ActionKind.Stop, "sppsvc"), PlanWarningKind.AlreadyThere));
        Assert.NotNull(Warning(Plan(ActionKind.Start, "Spooler"), PlanWarningKind.AlreadyThere));
    }

    [Fact]
    public void A_plan_is_worked_out_without_reading_the_listing_again()
    {
        // The caller already has a listing and reading it a second time costs a fifth of a
        // second to learn nothing. The double counts, so this is checked rather than hoped.
        var catalog = Chain();

        _ = new PlanBuilder(catalog.ReadAll(), catalog).Build(new ServiceAction(ActionKind.Restart, "MRxSmb20"));

        Assert.Equal(1, catalog.Reads);
    }

    // -- fixtures --------------------------------------------------------------------------

    /// <summary>
    /// The chain as the manager describes it, transitive sets included, in the order given.
    /// </summary>
    private static FakeScmCatalog Chain(IReadOnlyList<string>? asListed = null)
    {
        var entries = new List<ScmEntry>(Specimens.All)
        {
            Running("MRxSmb20", "SMB 2.0 Redirector"),
            Running("LanmanWorkstation", "Stacja robocza"),
            Running("SessionEnv", "Konfiguracja pulpitu zdalnego"),
            Running("Netlogon", "Logowanie do sieci")
        };

        var listed = asListed ?? ["SessionEnv", "Netlogon", "LanmanWorkstation"];

        return new FakeScmCatalog(entries)
            .DependedOnBy("MRxSmb20", [.. listed])
            .DependedOnBy("LanmanWorkstation", "SessionEnv", "Netlogon");
    }

    private static ScmEntry Running(string serviceName, string displayName) =>
        Entries.Named(serviceName, displayName) with
        {
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            ProcessId = Reading<int>.Present(4444)
        };

    /// <summary>
    /// Disabled and running at once, which is not a contradiction and is the case that
    /// matters here. Measured on a real machine: switching a service to disabled leaves it
    /// running until something stops it, which is glossary pitfall P7.
    /// </summary>
    private static ScmEntry Disabled(string serviceName, string displayName) =>
        Running(serviceName, displayName) with
        {
            StartType = Reading<StartType>.Present(Core.StartType.Disabled)
        };

    private static FakeScmCatalog Rebuild(FakeScmCatalog catalog, string serviceName, Func<ScmEntry, ScmEntry> change)
    {
        var entries = catalog.ReadAll()
            .Select(entry => string.Equals(entry.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)
                ? change(entry)
                : entry)
            .ToList();

        return new FakeScmCatalog(entries)
            .DependedOnBy("MRxSmb20", "SessionEnv", "Netlogon", "LanmanWorkstation")
            .DependedOnBy("LanmanWorkstation", "SessionEnv", "Netlogon");
    }

    /// <summary>
    /// Builds with the cascade included, which is what most of these are about. The plain
    /// form, where it is not, has tests of its own.
    /// </summary>
    private static OperationPlan Plan(ActionKind kind, string serviceName, FakeScmCatalog? catalog = null) =>
        Plan(kind, serviceName, includeDependents: true, catalog);

    private static OperationPlan Plan(
        ActionKind kind, string serviceName, bool includeDependents, FakeScmCatalog? catalog = null)
    {
        catalog ??= Specimens.Catalog();

        return new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(kind, serviceName, includeDependents));
    }

    private static PlanWarning Warning(OperationPlan plan, PlanWarningKind kind) =>
        Assert.Single(plan.Warnings, warning => warning.Kind == kind);
}
