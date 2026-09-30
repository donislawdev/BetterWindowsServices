using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// What a comparison looks at, and what makes it answer "drifted".
///
/// <b>Two decisions of the owner, 2026-09-30, stability report D-1 and D-2.</b> Per-user session
/// copies are left out of both sides and counted, because each belongs to one signed-in session and
/// comes and goes with it. And <c>--exit-code</c> answers about configuration only, because running
/// state has been reported apart from drift since 2026-08-01 and still decided the exit code.
/// </summary>
public sealed class ComparisonScopeTests
{
    [Fact]
    public void Session_copies_are_left_out_on_both_sides_and_counted()
    {
        // Two sessions signed in before, one after - a copy went away and another arrived, which
        // is somebody signing out and somebody else in, not a change to the machine.
        var diff = Between(
            [Entry("Spooler"), Copy("CDPUserSvc_1036d1"), Copy("CDPUserSvc_2e71a")],
            [Entry("Spooler"), Copy("CDPUserSvc_5c01f")]);

        Assert.Empty(diff.Added);
        Assert.Empty(diff.Removed);
        Assert.False(diff.Drifted);
        Assert.Equal(new InstancesLeftOut(2, 1), diff.LeftOut);
    }

    [Fact]
    public void A_copy_whose_settings_changed_is_not_compared_either()
    {
        // The same copy on both sides, differing - still left out, not only when it comes and goes.
        var diff = Between(
            [Copy("CDPUserSvc_1036d1")],
            [Copy("CDPUserSvc_1036d1") with { StartType = Reading<StartType>.Present(StartType.Disabled) }]);

        Assert.Empty(diff.Changed);
        Assert.Empty(diff.NotFullyCompared);
        Assert.Equal(new InstancesLeftOut(1, 1), diff.LeftOut);
    }

    [Fact]
    public void The_template_the_copies_are_made_from_is_still_compared()
    {
        var template = Entry("CDPUserSvc") with { PerUserRole = PerUserRole.Template };

        var diff = Between(
            [template],
            [template with { StartType = Reading<StartType>.Present(StartType.Disabled) }]);

        Assert.Equal("startType", Assert.Single(Assert.Single(diff.Changed).Differences).Field);
        Assert.True(diff.Drifted);
        Assert.False(diff.LeftOut.Any);
    }

    [Fact]
    public void A_name_shaped_like_a_copy_is_not_a_copy()
    {
        // By the role the type bits give, never by the name - a real service whose name happens
        // to end in an underscore and hex digits is a service, and it appearing is drift.
        var diff = Between([Entry("Spooler")], [Entry("Spooler"), Entry("Vendor_1036d1")]);

        Assert.Equal("Vendor_1036d1", Assert.Single(diff.Added).ServiceName);
        Assert.False(diff.LeftOut.Any);
    }

    [Fact]
    public void An_entry_that_differs_only_in_running_state_is_reported_and_is_not_drift()
    {
        // The case --exit-code paged somebody over until 2026-09-30: nothing about how the machine
        // is set up moved, a service stopped by itself.
        var diff = Between([Entry("Spooler")], [Entry("Spooler") with { Status = EntryStatus.Stopped }]);

        Assert.Equal(DifferenceGroup.RunningState, Assert.Single(Assert.Single(diff.Changed).Differences).Group);
        Assert.False(diff.Drifted);

        // And it is still in front of a person - the report must not say "no differences" over it.
        Assert.True(diff.Reported);
    }

    [Fact]
    public void Running_state_beside_a_configuration_change_is_drift()
    {
        var diff = Between(
            [Entry("Spooler")],
            [Entry("Spooler") with
            {
                Status = EntryStatus.Stopped,
                StartType = Reading<StartType>.Present(StartType.Disabled)
            }]);

        Assert.True(diff.Drifted);
    }

    [Fact]
    public void Nothing_to_report_is_said_as_nothing()
    {
        var diff = Between([Entry("Spooler")], [Entry("Spooler")]);

        Assert.False(diff.Reported);
        Assert.False(diff.Drifted);
    }

    private static SnapshotDiff Between(IReadOnlyList<ScmEntry> before, IReadOnlyList<ScmEntry> after)
    {
        Assert.True(SnapshotDiff.TryBetween(Taken(before), Taken(after), out var diff, out var failure), failure);

        return diff;
    }

    private static Snapshot Taken(IReadOnlyList<ScmEntry> entries) =>
        Snapshot.Of(entries, note: null, new FakeClock()) with { Metadata = ComparisonCaveatTests.Metadata() };

    private static ScmEntry Entry(string name) =>
        Entries.Any with { ServiceName = name, DisplayName = $"{name} display name" };

    private static ScmEntry Copy(string name) =>
        Entry(name) with { PerUserRole = PerUserRole.Instance };
}
