using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// A plan that starts an entry says so when what was read already names the manager's refusal.
///
/// <b>Written 2026-09-30, stability report W-7 and W-8, on the owner's decision of that day: a warning,
/// not a refusal.</b> Measured before the change on this machine: <c>bws start</c> on a disabled entry
/// gave a plan of one step, no warning and exit code 0. Each test here reads what was READ - an entry
/// whose start type or state nobody knows is not warned about, because a warning about it would be a
/// claim.
/// </summary>
public sealed class RefusedStartWarningTests
{
    private const string Name = "Spooler";

    [Fact]
    public void Starting_a_disabled_entry_that_is_stopped_says_Windows_will_refuse_it()
    {
        var plan = Build(ActionKind.Start, EntryStatus.Stopped, Reading<StartType>.Present(StartType.Disabled));

        Assert.Contains(plan.Warnings, warning => warning.Kind == PlanWarningKind.DisabledCannotStart);

        // A warning and not a refusal - the plan is still one step anybody can carry out.
        Assert.True(plan.IsRunnable);
    }

    [Fact]
    public void Starting_a_paused_entry_says_a_start_does_not_resume_it()
    {
        var plan = Build(ActionKind.Start, EntryStatus.Paused, Reading<StartType>.Present(StartType.Manual));

        Assert.Contains(plan.Warnings, warning => warning.Kind == PlanWarningKind.PausedCannotStart);
    }

    [Theory]
    [InlineData(EntryStatus.Running)]
    [InlineData(EntryStatus.StartPending)]
    [InlineData(EntryStatus.Unknown)]
    public void A_disabled_entry_that_is_not_stopped_is_not_warned_about(EntryStatus status)
    {
        // Running needs no start, starting is waited for rather than asked, and a state nobody read is
        // not "stopped".
        var plan = Build(ActionKind.Start, status, Reading<StartType>.Present(StartType.Disabled));

        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.DisabledCannotStart);
    }

    [Fact]
    public void An_entry_whose_start_type_nobody_read_is_not_called_disabled()
    {
        var plan = Build(ActionKind.Start, EntryStatus.Stopped, Reading<StartType>.NotRead());

        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.DisabledCannotStart);
    }

    [Fact]
    public void Stopping_a_paused_entry_is_not_warned_about()
    {
        // A paused service takes a stop, so only a start meets the refusal.
        var plan = Build(ActionKind.Stop, EntryStatus.Paused, Reading<StartType>.Present(StartType.Manual));

        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.PausedCannotStart);
    }

    private static OperationPlan Build(ActionKind kind, EntryStatus status, Reading<StartType> startType)
    {
        var entries = new List<ScmEntry>
        {
            Entries.Named(Name, "Print Spooler") with
            {
                Status = status,
                StartType = startType,
                DelayedAuto = Reading<bool>.Absent(),
                ProcessId = status == EntryStatus.Stopped ? Reading<int>.Absent() : Reading<int>.Present(4444)
            }
        };

        var catalog = new FakeScmCatalog(entries);

        return new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(kind, Name, IncludeDependents: false));
    }
}
