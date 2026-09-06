using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// What happens to a step being watched when the wall clock moves underneath it. Backlog 299.
///
/// <b>Not a hypothetical, and the machines this project tests on are the ones it happens to.</b> A
/// step is watched for up to a minute - <c>Carrying.Ceiling</c> - and a virtual machine resuming
/// corrects its clock in one jump. Until 2026-09-03 the runner asked <c>IClock.Now</c> both for the
/// deadline and for how long a step took, so a jump forward gave up on a healthy service and a jump
/// backward wrote a NEGATIVE number into the <c>milliseconds</c> field of the machine readable
/// output - a value no reader of that field can do anything sensible with.
///
/// <b>The clocks below are the fault itself rather than an arrangement to provoke it.</b> Each one
/// moves its wall clock the way a real one moves on a correction, and leaves the ruler alone the
/// way a real one leaves it alone. Neither of them touches the runner.
/// </summary>
public sealed class PlanRunClockTests
{
    [Fact]
    public void A_clock_jumping_forward_does_not_give_up_on_a_service_that_is_still_working()
    {
        // The entry keeps its side of the Win32 promise: the check point rises every time it is
        // asked, so it has earned every second it is taking. An hour arrives on the wall clock
        // while it does, which is what resuming a suspended machine looks like from in here.
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .Reaching(
                "Spooler",
                new ServiceProgress(EntryStatus.StopPending, 1, TimeSpan.FromSeconds(5), ProcessId: 4812),
                new ServiceProgress(EntryStatus.StopPending, 2, TimeSpan.FromSeconds(5), ProcessId: 4812),
                new ServiceProgress(EntryStatus.Stopped, 0, TimeSpan.Zero, ProcessId: 0));

        var run = new PlanRunner(control, new JumpingClock(TimeSpan.FromHours(1)))
            .Run(Stopping(), TimeSpan.FromMinutes(1));

        var result = Assert.Single(run.Results);

        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.Equal(EntryStatus.Stopped, result.Status);
    }

    [Fact]
    public void A_clock_jumping_backward_does_not_report_a_step_as_having_taken_less_than_no_time()
    {
        // The direction that reaches a file somebody keeps. A step time is a duration, and a
        // duration below zero is not a fact about a slow machine - it is a fact about two
        // readings of a clock somebody set in between.
        var control = new FakeScmControl()
            .At("Spooler", EntryStatus.Running)
            .Reaching(
                "Spooler",
                new ServiceProgress(EntryStatus.StopPending, 1, TimeSpan.FromSeconds(5), ProcessId: 4812),
                new ServiceProgress(EntryStatus.Stopped, 0, TimeSpan.Zero, ProcessId: 0));

        var run = new PlanRunner(control, new JumpingClock(TimeSpan.FromHours(-1)))
            .Run(Stopping(), TimeSpan.FromMinutes(1));

        var result = Assert.Single(run.Results);

        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.True(
            result.Milliseconds >= 0,
            $"A step reported {result.Milliseconds} ms, which is a duration measured with a clock " +
            "somebody moved rather than with one that only goes forward.");
    }

    /// <summary>Stopping one entry, with nothing depending on it.</summary>
    private static OperationPlan Stopping()
    {
        var entries = new List<ScmEntry>
        {
            Entries.Named("Spooler", "Print Spooler") with
            {
                Status = EntryStatus.Running,
                StartType = Reading<StartType>.Present(StartType.Automatic),
                DelayedAuto = Reading<bool>.Absent(),
                ProcessId = Reading<int>.Present(4444)
            }
        };

        var catalog = new FakeScmCatalog(entries);

        return new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Stop, "Spooler", IncludeDependents: false));
    }

    /// <summary>
    /// A clock whose wall time moves by a fixed amount every time anything waits, and whose ruler
    /// does not.
    ///
    /// <b>Both halves are what a real machine does.</b> Correcting the time, arriving at daylight
    /// saving, and resuming from suspend all move <see cref="IClock.Now"/> without moving the
    /// performance counter <see cref="IClock.Elapsed"/> is built on - which is exactly why the two
    /// are separate members rather than one.
    /// </summary>
    private sealed class JumpingClock(TimeSpan jump) : IClock
    {
        private DateTimeOffset _now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset Now => _now;

        public TimeSpan Elapsed { get; private set; }

        public void Wait(TimeSpan duration)
        {
            Elapsed += duration;
            _now += jump;
        }
    }
}
