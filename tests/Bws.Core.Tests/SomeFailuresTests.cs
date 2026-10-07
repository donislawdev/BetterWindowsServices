using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// Which failures a recovery list restarts an entry on - backlog 541 and the owner's decision of 2026-10-07.
///
/// <b>Measured before a line changed</b> (the throwaway machine, 2026-10-07): Spooler's list - restart,
/// restart, nothing - brought the entry back on its first two endings inside the hour and left it stopped on
/// the third and fourth, while the preview of <c>bws kill</c> said "Windows starts it again" every time.
/// Microsoft documents it: failure N runs item N and the last item repeats past the end of the list. A list
/// of nothing then restart left it stopped once and brought it back three times, and Spooler's list with a
/// reset period of zero brought it back four times - every failure was the first.
///
/// <b>Every shape the handover asked for has a test here</b>, whether or not the owner's machine carries it:
/// on 2026-10-07 none of its 204 lists with a restart ended in a restart after something else.
/// </summary>
public sealed class SomeFailuresTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    [Fact]
    public void Spoolers_list_restarts_it_on_its_first_two_failures_only()
    {
        var restart = Restarted(Hour, Restart(5), Restart(5), Nothing());

        Assert.Equal([TimeSpan.FromSeconds(5)], restart.After);
        var only = Assert.IsType<SomeFailures>(restart.Only);
        Assert.Equal([1, 2], only.Numbers);
        Assert.False(only.AndLater);
        Assert.Equal(Hour, only.ResetPeriod);
    }

    [Fact]
    public void A_list_of_restarts_only_restarts_it_on_every_failure()
    {
        // The last item repeats, so two restarts are every failure - and the sentence is the old one.
        Assert.Null(Restarted(Hour, Restart(5), Restart(10)).Only);
    }

    [Fact]
    public void Nothing_then_a_restart_restarts_it_from_the_second_failure_on()
    {
        var only = Assert.IsType<SomeFailures>(Restarted(Hour, Nothing(), Restart(1)).Only);

        Assert.Equal([2], only.Numbers);
        Assert.True(only.AndLater);
    }

    [Fact]
    public void A_restart_then_a_program_restarts_it_on_the_first_failure_only()
    {
        var only = Assert.IsType<SomeFailures>(
            Restarted(Hour, Restart(1), new RecoveryItem(RecoveryAction.RunProgram, TimeSpan.Zero)).Only);

        Assert.Equal([1], only.Numbers);
        Assert.False(only.AndLater);
    }

    [Fact]
    public void A_restart_in_the_middle_is_the_one_failure_it_restarts_on()
    {
        var only = Assert.IsType<SomeFailures>(Restarted(Hour, Nothing(), Restart(1), Nothing()).Only);

        Assert.Equal([2], only.Numbers);
        Assert.False(only.AndLater);
    }

    [Fact]
    public void A_count_that_never_starts_again_is_carried_as_such()
    {
        var only = Assert.IsType<SomeFailures>(
            Restarted(Timeout.InfiniteTimeSpan, Restart(60), Restart(60), Nothing()).Only);

        Assert.Equal(Timeout.InfiniteTimeSpan, only.ResetPeriod);
    }

    [Fact]
    public void With_a_reset_period_of_zero_every_failure_is_the_first()
    {
        // Spooler's list with a period of 0 brought the entry back on each of four endings.
        Assert.Null(Restarted(TimeSpan.Zero, Restart(1), Restart(1), Nothing()).Only);
    }

    [Fact]
    public void With_a_reset_period_of_zero_a_list_starting_with_nothing_restarts_nothing()
    {
        var plan = Forced(new FakeEndingFacts().Recovering("Netlogon", TimeSpan.Zero, Nothing(), Restart(1)));

        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.RecoveryRestarts);
    }

    [Fact]
    public void The_refusal_over_a_computer_restart_still_asks_the_whole_list()
    {
        // A reset period of zero reaches only the first item - and the refusal is the side to err on.
        var plan = Forced(new FakeEndingFacts().Recovering(
            "Netlogon", TimeSpan.Zero, Restart(1), new RecoveryItem(RecoveryAction.RestartComputer, TimeSpan.Zero)));

        Assert.Equal(PlanProblemKind.RecoveryRestartsComputer, Assert.Single(plan.Problems).Kind);
    }

    private static RecoveryRestart Restarted(TimeSpan resetPeriod, params RecoveryItem[] items) =>
        Assert.Single(Warning(
            Forced(new FakeEndingFacts().Recovering("Netlogon", resetPeriod, items)),
            PlanWarningKind.RecoveryRestarts).Restarts);

    private static RecoveryItem Restart(int seconds) => new(RecoveryAction.RestartService, TimeSpan.FromSeconds(seconds));

    private static RecoveryItem Nothing() => new(RecoveryAction.Nothing, TimeSpan.Zero);

    private static OperationPlan Forced(FakeEndingFacts facts)
    {
        var catalog = Housed(("MRxSmb20", 4444), ("LanmanWorkstation", 5555), ("SessionEnv", 6666), ("Netlogon", 7777));

        return new PlanBuilder(catalog.ReadAll(), catalog, facts).Build(new ServiceAction(ActionKind.ForceStop, "Netlogon"));
    }
}
