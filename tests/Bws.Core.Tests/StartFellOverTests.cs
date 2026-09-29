using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// A start the manager took and the service did not survive.
///
/// <b>Written 2026-09-30, stability report W-7.</b> Such a service goes back to Stopped with no wait
/// hint, and until that day the step watched it for the whole limit and reported that it ran out of
/// time - a sentence about waiting, a minute late, over a service that had already told the manager
/// exactly what went wrong. The number it left is its own exit code, and on the owner's decision of
/// that day it travels as <c>errorCode</c>, with the service's own code in the words only.
/// </summary>
public sealed class StartFellOverTests
{
    private const string Name = "Spooler";

    private static readonly ServiceProgress Starting =
        new(EntryStatus.StartPending, 1, TimeSpan.FromSeconds(2), ProcessId: FakeScmControl.FakeProcess);

    [Fact]
    public void A_start_that_falls_back_to_stopped_fails_at_once_with_the_service_exit_code()
    {
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .At(Name, EntryStatus.Stopped)
            .Reaching(Name, Starting, Fell(1064, 0));

        var result = Assert.Single(Run(control, clock).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.True(result.StoppedWhileStarting);
        Assert.Equal(1064, result.ErrorCode);
        Assert.Equal(EntryStatus.Stopped, result.Status);
        Assert.True(clock.Waited < TimeSpan.FromSeconds(1), $"Watched for {clock.Waited} before saying so.");
    }

    [Fact]
    public void A_service_specific_code_travels_in_the_words_and_never_as_the_number()
    {
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .At(Name, EntryStatus.Stopped)
            .Reaching(Name, Starting, Fell(1066, 42));

        var result = Assert.Single(Run(control, clock).Results);

        Assert.Equal(1066, result.ErrorCode);
        Assert.EndsWith("(42)", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_start_the_manager_refused_is_not_a_service_that_fell_over()
    {
        var clock = new FakeClock();
        var control = new FakeScmControl(clock).At(Name, EntryStatus.Stopped).RefusingRequests(Name, 1058);

        var result = Assert.Single(Run(control, clock).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.False(result.StoppedWhileStarting);
    }

    [Fact]
    public void A_stop_that_still_reads_running_just_after_the_request_is_watched_rather_than_failed()
    {
        // The mirror is deliberately NOT a failure: a service may take the stop and only report stop
        // pending a moment later, so Running straight after a stop is ordinary.
        var clock = new FakeClock();
        var control = new FakeScmControl(clock)
            .At(Name, EntryStatus.Running)
            .Reaching(
                Name,
                new ServiceProgress(EntryStatus.Running, 0, TimeSpan.Zero, ProcessId: FakeScmControl.FakeProcess),
                new ServiceProgress(EntryStatus.Stopped, 0, TimeSpan.Zero, ProcessId: 0));

        var run = new PlanRunner(control, clock).Run(PlanRunProgressTests.Stopping(), TimeSpan.FromSeconds(60));
        var result = Assert.Single(run.Results);

        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
    }

    private static ServiceProgress Fell(uint exitCode, uint serviceExitCode) =>
        new(EntryStatus.Stopped, 0, TimeSpan.Zero, ProcessId: 0, exitCode, serviceExitCode);

    private static PlanRun Run(FakeScmControl control, FakeClock clock) =>
        new PlanRunner(control, clock).Run(PlanRunProgressTests.Starting(), TimeSpan.FromSeconds(60));
}
