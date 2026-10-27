using System.ComponentModel;
using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// A snapshot compared with this machine asks the manager by name about every entry the reading lacks -
/// logic report S03, owner's decision "detect the hidden ones", 2026-10-26.
///
/// The listing leaves out, without an error, what this account may not query (Microsoft's page on
/// EnumServicesStatusEx). Until 2026-10-27 such an entry, at equal elevation, came back as removed and
/// counted as drift.
/// </summary>
public sealed class LiveComparisonTests
{
    [Fact]
    public void An_entry_the_manager_still_holds_is_unlisted_and_not_removed()
    {
        var machine = new FakeScmCatalog(Entry("Spooler"));
        machine.HeldUnlisted.Add("ZTDNS");

        var diff = Against(Taken(elevated: true, "Spooler", "ZTDNS"), Taken(elevated: true, "Spooler"), machine);

        Assert.Equal("ZTDNS", Assert.Single(diff.Unlisted).ServiceName);
        Assert.Empty(diff.Removed);
        Assert.Empty(diff.Uncertain);

        // Not drift, so a pipeline is not failed by a change of permissions - and still put in front of a person.
        Assert.False(diff.Drifted);
        Assert.True(diff.Reported);
    }

    [Fact]
    public void An_entry_the_manager_does_not_hold_is_removed()
    {
        var machine = new FakeScmCatalog(Entry("Spooler"));

        var diff = Against(Taken(elevated: true, "Spooler", "Gone"), Taken(elevated: true, "Spooler"), machine);

        Assert.Equal("Gone", Assert.Single(diff.Removed).ServiceName);
        Assert.Empty(diff.Unlisted);
        Assert.True(diff.Drifted);
        Assert.Equal("Gone", Assert.Single(Assert.Single(machine.HeldAsked)));
    }

    [Fact]
    public void Asked_by_name_a_reading_without_elevation_can_tell_removed_from_unseen()
    {
        // Two files cannot settle this and call it "cannot tell". The manager can, so both answers come back.
        var machine = new FakeScmCatalog(Entry("Spooler"));
        machine.HeldUnlisted.Add("Hidden");

        var diff = Against(Taken(elevated: true, "Spooler", "Hidden", "Gone"), Taken(elevated: false, "Spooler"), machine);

        Assert.Equal("Gone", Assert.Single(diff.Removed).ServiceName);
        Assert.Equal("Hidden", Assert.Single(diff.Unlisted).ServiceName);
        Assert.Empty(diff.Uncertain);
    }

    [Fact]
    public void Two_files_are_never_asked_about_and_keep_cannot_tell()
    {
        Assert.True(SnapshotDiff.TryBetween(
            Taken(elevated: true, "Spooler", "Hidden"), Taken(elevated: false, "Spooler"), out var diff, out var failure), failure);

        Assert.Equal("Hidden", Assert.Single(diff.Uncertain).ServiceName);
        Assert.Empty(diff.Unlisted);
    }

    [Fact]
    public void Nothing_missing_asks_the_manager_nothing()
    {
        var machine = new FakeScmCatalog(Entry("Spooler"));

        Against(Taken(elevated: true, "Spooler"), Taken(elevated: true, "Spooler"), machine);

        Assert.Empty(machine.HeldAsked);
    }

    [Fact]
    public void A_manager_that_will_not_open_fails_the_whole_comparison()
    {
        // A whole answer or none - half of one would call the entries it could not ask about removed.
        var machine = new FakeScmCatalog(Entry("Spooler")) { FailHeld = new Win32Exception(5) };

        Assert.Throws<Win32Exception>(() =>
            SnapshotDiff.TryAgainstMachine(Taken(elevated: true, "Spooler", "Gone"), Taken(elevated: true, "Spooler"), machine, out _, out _));
    }

    private static SnapshotDiff Against(Snapshot before, Snapshot now, IScmCatalog machine)
    {
        Assert.True(SnapshotDiff.TryAgainstMachine(before, now, machine, out var diff, out var failure), failure);

        return diff;
    }

    private static Snapshot Taken(bool elevated, params string[] names)
    {
        var taken = Snapshot.Of([.. names.Select(Entry)], note: null, new FakeClock());

        return taken with { Metadata = taken.Metadata with { Elevated = elevated } };
    }

    private static ScmEntry Entry(string name) =>
        Entries.Any with { ServiceName = name, DisplayName = $"{name} display name" };
}
