using System.Runtime.InteropServices;
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

    /// <summary>
    /// Nothing depending on an entry is a fact about the entry, not about our permissions.
    ///
    /// Confusing the two makes a plan claim it could not check the cascade when it checked and
    /// found none - and that claim reaches a person on the screen where they decide whether to
    /// change a machine.
    ///
    /// <b>The second half was written on 2026-08-26 and what it measured is worth more than what
    /// it asserts.</b> Until that day this call discarded its return value and decided from the
    /// error number alone, which the file's own neighbour says eighty lines away is wrong: Windows
    /// does not clear the last error on success, so a call with nothing to hand over can leave
    /// whatever the previous one put there. The call now asks through the return value.
    ///
    /// <b>THE CHANGE COULD NOT BE SHOWN TO CHANGE ANY OUTCOME ON THIS MACHINE.</b> A mutation entry
    /// putting the old shape back came out MISSED - poisoning the thread with an error before the
    /// call does not survive it, so this build of Windows clears the error on this path and the
    /// failure has no way of happening here. The entry was withdrawn rather than kept as a guard
    /// proving nothing. The change stays: it costs one comparison, it makes this call read like the
    /// enumeration eighty lines away, and "the platform happens to clear it today" is not something
    /// to build on.
    /// </summary>
    [Fact]
    public void Nothing_depending_on_it_is_absent_rather_than_a_refusal()
    {
        var dependents = new WindowsScmCatalog().ReadDependents("Spooler");

        Assert.Equal(ReadOutcome.Absent, dependents.Outcome);

        // And still absent with something left on the thread. This pinned no bug on the machine it
        // was written on - see above - and it pins the answer for whichever build stops clearing it.
        Marshal.SetLastSystemError(5);

        Assert.Equal(ReadOutcome.Absent, new WindowsScmCatalog().ReadDependents("Spooler").Outcome);
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
