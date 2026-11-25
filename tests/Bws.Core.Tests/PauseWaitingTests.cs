using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.PauseRuns;

namespace Bws.Core.Tests;

/// <summary>
/// A step meeting an entry that somebody else is moving: a pause or a resume seen under way and undone by
/// the next look, and a start followed straight by a pause under a stop - which must wait for both, because
/// the manager takes a stop sent during PausePending and loses it (measured 2026-11-25).
/// </summary>
public sealed class PauseWaitingTests
{
    [Theory]
    [InlineData(StepOperation.Pause)]
    [InlineData(StepOperation.Resume)]
    public void A_move_somebody_began_that_the_next_look_sees_undone_fails_at_once_rather_than_at_the_limit(StepOperation operation)
    {
        var pausing = operation == StepOperation.Pause;
        var clock = new FakeClock();
        var control = new FakeScmControl(clock).MovedByOthers(
            "Spooler",
            Progress(pausing ? EntryStatus.PausePending : EntryStatus.ContinuePending, 1),
            Progress(pausing ? EntryStatus.Running : EntryStatus.Paused, 0));

        var run = Ran(control, Doing(operation), clock);

        var result = Assert.Single(run.Results);
        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.StartsWith(pausing ? "It began to pause and went back" : "It began to resume and went back", result.Error, StringComparison.Ordinal);
        Assert.True(clock.Waited < TimeSpan.FromSeconds(1), $"Watched for {clock.Waited} an entry the first look already saw back.");
        Assert.Empty(control.Requested);
        Assert.Empty(run.Reversal);
    }

    [Fact]
    public void A_stop_meeting_a_start_followed_straight_by_a_pause_waits_for_both_before_it_is_sent()
    {
        var control = new FakeScmControl()
            .MovedByOthers(
                "Spooler",
                Progress(EntryStatus.StartPending, 1),
                Progress(EntryStatus.PausePending, 1),
                Progress(EntryStatus.Paused, 0))
            .LosingRequestsWhile("Spooler", EntryStatus.PausePending);

        var result = Assert.Single(Ran(control, Doing(StepOperation.Stop)).Results);

        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.Equal(EntryStatus.Stopped, result.Status);
        Assert.False(result.ArrivedUnasked);
        Assert.Equal("Spooler", Assert.Single(control.Requested));
    }

    [Fact]
    public void A_stop_that_gives_up_says_the_state_the_entry_was_last_in_rather_than_the_first()
    {
        var clock = new FakeClock();
        var control = new FakeScmControl(clock).MovedByOthers(
            "Spooler", Progress(EntryStatus.StartPending, 1), Progress(EntryStatus.PausePending, 1));

        var result = Assert.Single(Ran(control, Doing(StepOperation.Stop), clock).Results);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(
            "It was still pausing when the waiting ran out, so it was never asked to stop. Ask again once it has paused.",
            result.Error);
        Assert.Empty(control.Requested);
    }
}
