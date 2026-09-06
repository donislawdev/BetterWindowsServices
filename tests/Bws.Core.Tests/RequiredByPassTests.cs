using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The third family of the second phase of `ADR-13`: who breaks if an entry stops.
///
/// <b>Built 2026-09-06, and it is the direction the product could already ACT on and could not
/// SHOW.</b> The manager has been asked this before every cascade since the plan existed - the
/// argument for asking rather than inverting the declarations is at
/// <c>IScmCatalog.ReadDependents</c> - and there was no column, no field in the query language,
/// and no line in a snapshot. The owner's decision, and it took the schema version with it.
///
/// <b>Counted rather than timed here.</b> What the pass costs on a real machine is measured
/// elsewhere and written on <see cref="RequiredByPass"/>. What a test can hold it to is that it
/// asks once per entry, keeps what came back, and does not turn a refusal into an empty list.
/// </summary>
public sealed class RequiredByPassTests
{
    /// <summary>
    /// Every entry gets its own answer, and the pass asks the manager once for each.
    ///
    /// <b>Once per entry and no cache, unlike the memory pass.</b> There, several entries share
    /// one process and the answer is keyed by process id. Here the question is about this entry,
    /// so two entries never share an answer - and a test that did not count the asking could not
    /// tell a pass that asked once from one that asked twice about the same name.
    /// </summary>
    [Fact]
    public void Each_entry_is_asked_about_once_and_keeps_its_own_answer()
    {
        var spooler = Entries.Any;
        var other = Entries.Any with { ServiceName = "RpcSs", DisplayName = "Remote Procedure Call" };

        var catalog = new FakeScmCatalog(spooler, other)
            .DependedOnBy("RpcSs", "Spooler", "Dhcp");

        var filled = RequiredByPass.Fill([spooler, other], catalog);

        Assert.Equal(["Spooler", "RpcSs"], catalog.DependentsAsked);

        // Nothing stands on the print spooler on this fixture, and that is an ANSWER rather than
        // a gap: the manager said so. Absent and refused are told apart below.
        Assert.Equal(ReadOutcome.Absent, filled[0].RequiredBy.Outcome);

        Assert.Equal(["Spooler", "Dhcp"], filled[1].RequiredBy.Value);
    }

    /// <summary>
    /// A refusal stays a refusal, which is rule 8 in the one place it is easiest to lose.
    ///
    /// <b>An empty list and a refused question look identical to anything downstream that stores
    /// a list.</b> The first says the manager looked and found nobody. The second says nobody
    /// looked. A column showing the first where the second is true tells somebody it is safe to
    /// stop a service, which is the most expensive wrong answer this product can give.
    /// </summary>
    [Fact]
    public void A_refusal_is_carried_through_rather_than_flattened_into_an_empty_list()
    {
        var entry = Entries.Any;
        var catalog = new FakeScmCatalog(entry);

        catalog.RefuseDependentsFor.Add(entry.ServiceName);

        var filled = RequiredByPass.Fill([entry], catalog);

        Assert.Equal(ReadOutcome.Denied, filled[0].RequiredBy.Outcome);
        Assert.Equal(Entries.AccessDenied, filled[0].RequiredBy.ErrorCode);
    }

    /// <summary>
    /// The entries come back as new records and the ones handed in are untouched.
    ///
    /// The same claim the other two passes make, and for the reason written on all three: a plan
    /// and a snapshot are evidence, and evidence does not change after it has been shown.
    /// </summary>
    [Fact]
    public void The_entries_handed_in_are_left_as_they_were()
    {
        var entry = Entries.Any;
        var catalog = new FakeScmCatalog(entry).DependedOnBy(entry.ServiceName, "Fax");

        var filled = RequiredByPass.Fill([entry], catalog);

        Assert.Equal(ReadOutcome.NotRead, entry.RequiredBy.Outcome);
        Assert.Equal(["Fax"], filled[0].RequiredBy.Value);
    }
}
