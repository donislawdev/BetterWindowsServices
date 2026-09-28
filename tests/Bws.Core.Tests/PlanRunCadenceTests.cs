using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// How soon a step notices that its entry has arrived, and what that must not cost.
///
/// <b>Written 2026-09-29 with the doubling pauses in <see cref="PlanRunner"/></b> - backlog 466,
/// S-6 of the external performance report. On the throwaway machine Spooler and W32Time changed
/// state in 2-50 ms and every step reported 260-289 ms, because the runner looked once straight
/// after the request and then only every quarter of a second. Three things are held here, and
/// only the first is the reason for the change. The other two are what the change could have
/// broken without anybody noticing: giving up sooner on an entry that promised too little, and
/// asking the manager all the time.
///
/// Every entry here arrives by the clock rather than by the number of looks, because the looks
/// no longer come at a steady pace and "arrived after 40 ms" is the only honest way to ask.
/// </summary>
public sealed class PlanRunCadenceTests
{
    private const string Name = "Spooler";

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(40)]
    [InlineData(120)]
    [InlineData(600)]
    [InlineData(3000)]
    public void An_entry_is_seen_soon_after_it_arrives_rather_than_a_whole_cadence_later(int arrivesAfter)
    {
        // The promise the doubling keeps: never left waiting longer than the step had already
        // taken plus the first pause, and never a whole cadence. Before 2026-09-29 an entry that
        // arrived after 5 ms was reported at 250, forty times late.
        var clock = new FakeClock();
        var control = Stopping(clock, TimeSpan.FromMilliseconds(arrivesAfter), Pending(TimeSpan.FromSeconds(5)));

        var result = Assert.Single(Run(control, clock).Results);
        var late = result.Milliseconds - arrivesAfter;

        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.True(late >= 0, $"Reported at {result.Milliseconds} ms, before the entry arrived.");

        Assert.True(
            late < Math.Min(arrivesAfter + PlanRunner.FirstLook.TotalMilliseconds, PlanRunner.Cadence.TotalMilliseconds),
            $"Arrived after {arrivesAfter} ms and was only seen at {result.Milliseconds} ms.");
    }

    [Theory]
    [InlineData(50, 240)]
    [InlineData(300, 480)]
    public void An_entry_that_promised_less_than_it_needed_is_given_the_patience_it_always_had(
        int promised, int arrivesAfter)
    {
        // THE MISTAKE LOOKING SOONER COULD HAVE MADE, and the costlier one. Both of these arrived
        // without ever raising their check point, so each broke its promise. Looked at once a
        // quarter of a second, both were seen arriving and reported as done - so a runner that
        // looks sooner and gives up at the promise itself would now report as timed out a step
        // that finished, on a machine that is fine.
        var clock = new FakeClock();

        var control = Stopping(
            clock, TimeSpan.FromMilliseconds(arrivesAfter), Pending(TimeSpan.FromMilliseconds(promised)));

        Assert.Equal(StepOutcome.Succeeded, Assert.Single(Run(control, clock).Results).Outcome);
    }

    [Fact]
    public void A_long_wait_asks_the_manager_about_as_often_as_it_always_did()
    {
        // The cost side. The first pauses are short, and pauses that stayed short would turn a
        // thirty second wait into three thousand questions to the manager instead of about a
        // hundred and twenty. The doubling adds a handful at the start and nothing after.
        var clock = new FakeClock();

        var control = new FakeScmControl()
            .At(Name, EntryStatus.Running)
            .Reaching(Name, new ServiceProgress(EntryStatus.StopPending, 0, TimeSpan.Zero, ProcessId: 4812));

        var run = Run(control, clock, TimeSpan.FromSeconds(30));
        var atTheCadence = (int)(TimeSpan.FromSeconds(30) / PlanRunner.Cadence);

        Assert.Equal(StepOutcome.TimedOut, Assert.Single(run.Results).Outcome);
        Assert.InRange(control.Reads, atTheCadence, atTheCadence + 10);
    }

    private static ServiceProgress Pending(TimeSpan waitHint) =>
        new(EntryStatus.StopPending, CheckPoint: 1, waitHint, ProcessId: 4812);

    private static FakeScmControl Stopping(IClock clock, TimeSpan after, ServiceProgress meanwhile) =>
        new FakeScmControl(clock)
            .At(Name, EntryStatus.Running)
            .Arriving(Name, after, meanwhile);

    private static PlanRun Run(FakeScmControl control, FakeClock clock, TimeSpan? timeout = null)
    {
        var plan = new OperationPlan
        {
            Action = new ServiceAction(ActionKind.Stop, Name),
            Steps = [new PlanStep(Name, Name, StepOperation.Stop, StepReason.Requested)],
            Warnings = [],
            Problems = []
        };

        return new PlanRunner(control, clock).Run(plan, timeout ?? TimeSpan.FromMinutes(1));
    }
}
