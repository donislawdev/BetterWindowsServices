using Bws.Core;

namespace Bws.Integration.Tests;

/// <summary>
/// The question a plan that ends a shared process asks since 2026-10-27 - which entries the manager keeps and
/// did not list to this account (logic report R04) - put to the real manager.
///
/// <b>Under elevation on the owner's machine the answer is empty</b> (measured 2026-10-26: the listing held
/// 804 entries and the only key with a type it lacked was a driver the manager does not know), so what can be
/// held here on any machine is the shape of the answer rather than its content. Its content under a restricted
/// token is what tools/scm-probe/unlisted-probe.ps1 is for.
///
/// Read-only. The registry is opened for reading and every entry for its status alone.
/// </summary>
public sealed class UnlistedContractTests
{
    [Fact]
    public void The_listing_decides_what_is_asked_and_never_the_answer()
    {
        var listed = new WindowsScmCatalog().ReadAll().Select(entry => entry.ServiceName).ToList();
        var reader = new WindowsEndingFactsReader();

        var withTheListing = reader.ReadUnlisted(listed);
        var withNothing = reader.ReadUnlisted([]);

        // Every entry the listing shows can be queried by this account, so handing the listing in only saves
        // asking about it - an entry this account can open is never one the enumeration left out.
        Assert.Equal(ReadOutcome.Present, withTheListing.Outcome);
        Assert.Equal(ReadOutcome.Present, withNothing.Outcome);
        Assert.Equal(withTheListing.Value!.Order(StringComparer.OrdinalIgnoreCase), withNothing.Value!.Order(StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(withTheListing.Value!, name => listed.Contains(name, StringComparer.OrdinalIgnoreCase));
    }
}
