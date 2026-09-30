using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// A restart of an entry that is not running is planned as its start - the external stability report of
/// 2026-09-29 (W-5) and the owner's decision on it of 2026-09-30, which is also what Restart-Service does.
///
/// <b>Measured before the change</b> on this machine: <c>bws restart AxInstSV --dry-run</c> on a stopped
/// entry showed a stop and a start "put back", and the start was carried out even when Stop was pressed
/// before the first step. And a restart of a stopped disabled entry was refused with a sentence about
/// stopping it, which it is not.
/// </summary>
public sealed class RestartOfStoppedTests
{
    [Fact]
    public void A_restart_of_a_stopped_entry_is_one_start_asked_for()
    {
        var plan = Build(new ServiceAction(ActionKind.Restart, "AxInstSV"), Entry("AxInstSV", EntryStatus.Stopped));

        var step = Assert.Single(plan.Steps);

        Assert.Equal(StepOperation.Start, step.Operation);
        Assert.Equal(StepReason.Requested, step.Reason);

        // The ask is kept, so the command handed back is the one somebody typed.
        Assert.Equal(ActionKind.Restart, plan.Action.Kind);
        Assert.Equal(PlanWarningKind.RestartOnlyStarts, plan.Warnings[0].Kind);
    }

    [Fact]
    public void A_restart_of_a_running_entry_is_still_a_stop_and_a_start_put_back()
    {
        var plan = Build(new ServiceAction(ActionKind.Restart, "Spooler"), Entry("Spooler", EntryStatus.Running));

        Assert.Equal(
            [(StepOperation.Stop, StepReason.Requested), (StepOperation.Start, StepReason.Restore)],
            plan.Steps.Select(step => (step.Operation, step.Reason)));

        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.RestartOnlyStarts);
    }

    [Fact]
    public void A_restart_of_a_stopped_disabled_entry_warns_the_way_its_start_does()
    {
        var disabled = Entry("AmdCrash", EntryStatus.Stopped) with
        {
            StartType = Reading<StartType>.Present(Core.StartType.Disabled)
        };

        var plan = Build(new ServiceAction(ActionKind.Restart, "AmdCrash"), disabled);

        // Refused until 2026-09-30 with "restarting it would stop it and could not start it again" - false
        // of an entry that is not running. Now what a start of it gets, on the owner's decision of package C.
        Assert.True(plan.IsRunnable);

        Assert.Equal(
            [PlanWarningKind.RestartOnlyStarts, PlanWarningKind.DisabledCannotStart],
            plan.Warnings.Select(warning => warning.Kind));
    }

    [Fact]
    public void A_restart_of_a_stopped_entry_takes_no_dependant_down()
    {
        // A dependant still running on a stopped entry is rare and real - the entry died under it. It is not
        // in the way of anything, because nothing here is going to be stopped.
        var catalog = new FakeScmCatalog([Entry("Lanman", EntryStatus.Stopped), Entry("Dependant", EntryStatus.Running)])
            .DependedOnBy("Lanman", "Dependant");

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Restart, "Lanman", IncludeDependents: true));

        Assert.Equal("Lanman", Assert.Single(plan.Steps).ServiceName);
        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.Cascade);
    }

    private static OperationPlan Build(ServiceAction action, ScmEntry entry)
    {
        var catalog = new FakeScmCatalog([entry]);

        return new PlanBuilder(catalog.ReadAll(), catalog).Build(action);
    }

    private static ScmEntry Entry(string serviceName, EntryStatus status) =>
        Entries.Named(serviceName, serviceName) with
        {
            Status = status,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            ProcessId = status == EntryStatus.Stopped ? Reading<int>.Absent() : Reading<int>.Present(4444)
        };
}
