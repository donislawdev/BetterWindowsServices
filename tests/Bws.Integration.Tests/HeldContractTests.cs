using Bws.Core;

namespace Bws.Integration.Tests;

/// <summary>
/// The question a comparison with this machine asks since 2026-10-27 - which names from a snapshot the manager
/// still holds (logic report S03) - put to the real manager.
///
/// <b>What it cannot reach, said:</b> a name the manager holds and refuses to open. No entry on an ordinary
/// machine refuses an elevated or a standard account its status, so that answer is tested on its value
/// (WindowsEndingFactsReader.Withholds, in UnlistedNeighbourTests) and on a machine through
/// tools/scm-probe/unlisted-probe.ps1.
///
/// Read-only. Every name is opened for its status alone.
/// </summary>
public sealed class HeldContractTests
{
    [Fact]
    public void An_entry_that_exists_is_held_and_one_that_does_not_is_not()
    {
        // RpcSs is on every Windows there is - the manager cannot run without it.
        var held = new WindowsScmCatalog().ReadHeld(["RpcSs", "bws-no-such-entry-" + Guid.NewGuid().ToString("N")]);

        Assert.Equal("RpcSs", Assert.Single(held));
    }

    [Fact]
    public void A_name_cut_short_by_a_nul_is_not_answered_for_the_name_before_it()
    {
        // From a file, a name is anything - and handed to Windows it would end at the NUL and be RpcSs.
        Assert.Empty(new WindowsScmCatalog().ReadHeld(["RpcSs\0tail"]));
    }

    [Fact]
    public void Nothing_missing_is_an_empty_answer()
    {
        Assert.Empty(new WindowsScmCatalog().ReadHeld([]));
    }
}
