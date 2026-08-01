using Bws.Core;

namespace Bws.Integration.Tests;

/// <summary>
/// What the manager actually answers when asked who breaks.
///
/// Two things had to be measured rather than assumed, because the cascade is built on both
/// and getting either wrong produces a preview shorter than what happens.
///
/// Read-only. Asking who depends on a service changes nothing.
/// </summary>
public sealed class DependentsContractTests
{
    /// <summary>
    /// A chain that exists on a real machine, confirmed with sc qc on 2026-08-01:
    /// LanmanWorkstation declares MRxSmb20, and SessionEnv and Netlogon declare
    /// LanmanWorkstation. Nothing here is stopped or started to arrange it.
    /// </summary>
    private const string Root = "MRxSmb20";
    private const string Middle = "LanmanWorkstation";

    [Fact]
    public void The_answer_already_reaches_past_the_first_hop()
    {
        // Measured, not assumed, and the assumption would have been wrong: the guess was
        // that this returns direct dependents and a cascade would have to recurse. It does
        // not. Asking once about the target finds the whole set.
        var catalog = new WindowsScmCatalog();

        var reachable = Names(catalog.ReadDependents(Root));
        var direct = Names(catalog.ReadDependents(Middle));

        Assert.Contains(Middle, reachable);

        Assert.All(direct, name => Assert.Contains(name, reachable));
        Assert.True(
            reachable.Count > direct.Count,
            $"{Root} reported {reachable.Count} dependents and {Middle} reported {direct.Count}. " +
            "Without something two hops away this proves nothing about reach.");
    }

    [Fact]
    public void The_whole_answer_comes_back_however_long_it_is()
    {
        // sc.exe asks with a fixed buffer, prints three entries and gives up with
        // ERROR_MORE_DATA. RpcSs really is needed by two hundred and twelve entries on the
        // machine this was written on, and a cascade built on three of them would be a
        // preview of a small fraction of what was about to happen.
        var dependents = Names(new WindowsScmCatalog().ReadDependents("RpcSs"));

        Assert.True(
            dependents.Count > 100,
            $"Only {dependents.Count} dependents came back for RpcSs, which is the shape of a " +
            "buffer that was never resized.");
    }

    [Fact]
    public void Nothing_depending_on_it_is_absent_rather_than_a_refusal()
    {
        // A fact about the service, not about our permissions. Confusing the two here would
        // make a plan claim it could not check the cascade when it checked and found none.
        var dependents = new WindowsScmCatalog().ReadDependents("Spooler");

        Assert.Equal(ReadOutcome.Absent, dependents.Outcome);
    }

    [Fact]
    public void A_name_nobody_has_is_reported_rather_than_thrown()
    {
        var dependents = new WindowsScmCatalog().ReadDependents("NoSuchServiceAnywhere");

        Assert.Equal(ReadOutcome.Denied, dependents.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(dependents.Reason));
    }

    private static IReadOnlyList<string> Names(Reading<IReadOnlyList<string>> dependents) =>
        dependents.IsPresent ? dependents.Value! : [];
}
