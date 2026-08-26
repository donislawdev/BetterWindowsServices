using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Comparing two snapshots.
///
/// The engine behind `D2`. Everything here is about the same worry from different sides: an
/// audit tool is believed, so a difference it reports that never happened costs more than one
/// it misses. Half these tests exist to hold something back rather than to find it.
/// </summary>
public sealed class SnapshotDiffTests
{
    [Fact]
    public void Two_readings_of_the_same_machine_differ_in_nothing()
    {
        var diff = Between(Taken([Entry("Spooler"), Entry("BFE")]), Taken([Entry("Spooler"), Entry("BFE")]));

        Assert.False(diff.Any);
        Assert.Empty(diff.Added);
        Assert.Empty(diff.Removed);
        Assert.Empty(diff.Changed);
        Assert.Empty(diff.NotFullyCompared);
    }

    [Fact]
    public void A_moment_apart_is_not_a_difference()
    {
        // Metadata is not drift. Every snapshot differs from every other one in when it was
        // taken, and a comparison that said so would say it on every comparison ever made.
        var before = Taken([Entry("Spooler")]) with { Metadata = Metadata(elevated: true, at: 100) };
        var after = Taken([Entry("Spooler")]) with { Metadata = Metadata(elevated: true, at: 900) };

        Assert.False(Between(before, after).Any);
    }

    [Fact]
    public void A_privilege_spelled_differently_is_the_same_privilege()
    {
        // SeSystemTimePrivilege and SeSystemtimePrivilege are one privilege written two ways, and
        // both spellings are on THIS machine - Schedule declares the first, Sense the second. The
        // manager hands them back exactly as each service wrote them, so the same set read on two
        // machines can differ in nothing but capitals.
        //
        // ScmEntry has said since it was written that comparison is case-insensitive everywhere
        // and that a diff comparing these as plain text would report a change between two machines
        // that had none. It did exactly that until 2026-08-26.
        var diff = Between(
            Taken([Entry("Spooler") with
            {
                RequiredPrivileges = Reading<IReadOnlyList<string>>.Present(["SeSystemTimePrivilege"])
            }]),
            Taken([Entry("Spooler") with
            {
                RequiredPrivileges = Reading<IReadOnlyList<string>>.Present(["SeSystemtimePrivilege"])
            }]));

        Assert.False(diff.Any);
        Assert.Empty(diff.Changed);
    }

    [Fact]
    public void The_order_the_manager_answered_in_is_not_a_difference()
    {
        // Nothing promises the order dependencies come back in. Snapshot.Of sorts ENTRIES for
        // exactly that reason, having watched the order move - and the lists inside an entry were
        // left being compared as the text of a JSON array, which is order for order.
        var diff = Between(
            Taken([Entry("Spooler") with
            {
                DependsOn = Reading<IReadOnlyList<string>>.Present(["RPCSS", "http"])
            }]),
            Taken([Entry("Spooler") with
            {
                DependsOn = Reading<IReadOnlyList<string>>.Present(["http", "RPCSS"])
            }]));

        Assert.False(diff.Any);
    }

    [Fact]
    public void A_dependency_that_really_changed_is_still_a_difference()
    {
        // The half a careless fix breaks. Nothing above may be bought by comparing these loosely
        // enough to miss a dependency arriving, which is drift somebody has to see.
        var diff = Between(
            Taken([Entry("Spooler") with
            {
                DependsOn = Reading<IReadOnlyList<string>>.Present(["RPCSS"])
            }]),
            Taken([Entry("Spooler") with
            {
                DependsOn = Reading<IReadOnlyList<string>>.Present(["RPCSS", "http"])
            }]));

        Assert.True(diff.Any);
        Assert.Equal("dependsOn", Assert.Single(Assert.Single(diff.Changed).Differences).Field);
    }

    [Fact]
    public void What_a_person_reads_is_the_spelling_the_snapshot_holds()
    {
        // The price of the two tests above, checked rather than assumed. The comparison works on a
        // form nobody sees - sorted and folded - and the difference it reports has to carry the
        // text as each side actually wrote it, or the report would be about a form this tool
        // invented.
        var diff = Between(
            Taken([Entry("Spooler") with
            {
                RequiredPrivileges = Reading<IReadOnlyList<string>>.Present(["SeTcbPrivilege"])
            }]),
            Taken([Entry("Spooler") with
            {
                RequiredPrivileges = Reading<IReadOnlyList<string>>.Present(["SeBackupPrivilege"])
            }]));

        var difference = Assert.Single(Assert.Single(diff.Changed).Differences);

        Assert.Contains("SeTcbPrivilege", difference.Before, StringComparison.Ordinal);
        Assert.Contains("SeBackupPrivilege", difference.After, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_that_appeared_and_one_that_went_away_are_named()
    {
        var diff = Between(Taken([Entry("Spooler"), Entry("Gone")]), Taken([Entry("Spooler"), Entry("New")]));

        Assert.Equal("New", Assert.Single(diff.Added).ServiceName);
        Assert.Equal("Gone", Assert.Single(diff.Removed).ServiceName);
        Assert.True(diff.Any);
    }

    [Fact]
    public void A_changed_setting_is_reported_as_configuration()
    {
        var diff = Between(
            Taken([Entry("Spooler")]),
            Taken([Entry("Spooler") with { StartType = Reading<StartType>.Present(StartType.Disabled) }]));

        var difference = Assert.Single(Assert.Single(diff.Changed).Differences);

        Assert.Equal("startType", difference.Field);
        Assert.Equal(DifferenceGroup.Configuration, difference.Group);
        Assert.Equal("Disabled", difference.After);
    }

    [Fact]
    public void What_was_running_is_reported_apart_from_how_it_is_set_up()
    {
        // Owner's decision, and the reason is arithmetic: two snapshots a day apart differ
        // in status on dozens of entries and in configuration on none. Mixed together, the
        // drift somebody came for arrives underneath noise.
        // The specimen is already running, so this stops it. Setting it to Running would
        // have changed nothing and the test would have passed on an empty comparison -
        // exactly the trap ADR-10 names, where a fixture that cannot tell two cases apart
        // lets an assertion check nothing.
        var diff = Between(
            Taken([Entry("Spooler")]),
            Taken([Entry("Spooler") with { Status = EntryStatus.Stopped }]));

        var difference = Assert.Single(Assert.Single(diff.Changed).Differences);

        Assert.Equal("status", difference.Field);
        Assert.Equal(DifferenceGroup.RunningState, difference.Group);
        Assert.Equal("Running", difference.Before);
        Assert.Equal("Stopped", difference.After);
    }

    [Fact]
    public void A_new_process_identifier_is_never_a_difference()
    {
        // Everything running gets a new one after a restart. Comparing it would report most
        // of the machine as changed for saying nothing, which is how a report stops being
        // read. Same reasoning that keeps memory out of the file altogether.
        var diff = Between(
            Taken([Entry("Spooler") with { ProcessId = Reading<int>.Present(1234) }]),
            Taken([Entry("Spooler") with { ProcessId = Reading<int>.Present(5678) }]));

        Assert.False(diff.Any);
    }

    [Fact]
    public void An_entry_only_the_elevated_snapshot_could_see_is_not_reported_as_removed()
    {
        // The trap this comparison was designed around. Measured on a real machine: the
        // manager hands over 807 entries without elevation where it hands over 810 with it,
        // so an unelevated snapshot compared against an elevated one would otherwise report
        // three deletions nobody performed.
        var elevated = Taken([Entry("Spooler"), Entry("ZTDNS")]) with { Metadata = Metadata(elevated: true) };
        var restricted = Taken([Entry("Spooler")]) with { Metadata = Metadata(elevated: false) };

        var diff = Between(elevated, restricted);

        Assert.Equal("ZTDNS", Assert.Single(diff.Uncertain).ServiceName);
        Assert.Empty(diff.Removed);

        // And it is not drift, so a pipeline asking by exit code is not failed by it.
        Assert.False(diff.Any);
        Assert.True(diff.Caveats.ElevationDiffers);
    }

    [Fact]
    public void An_entry_only_the_restricted_snapshot_holds_really_did_appear()
    {
        // The other direction, and it is not symmetric. An unelevated reading is a subset of
        // an elevated one, so something missing from the *elevated* side is genuinely gone -
        // anything the restricted view could see, the full one could see too. Treating both
        // directions as uncertain would hide real removals behind a caveat.
        var restricted = Taken([Entry("Spooler"), Entry("Extra")]) with { Metadata = Metadata(elevated: false) };
        var elevated = Taken([Entry("Spooler")]) with { Metadata = Metadata(elevated: true) };

        var diff = Between(restricted, elevated);

        Assert.Equal("Extra", Assert.Single(diff.Removed).ServiceName);
        Assert.Empty(diff.Uncertain);
        Assert.True(diff.Any);
    }

    [Fact]
    public void A_field_one_side_could_not_read_is_named_and_not_counted_as_a_change()
    {
        // Comparing it would report a value turning into nothing, which is a fact about our
        // permissions dressed up as a fact about the machine. Leaving it out silently would
        // be worse: the reader would take "no differences" for "nothing changed".
        var diff = Between(
            Taken([Entry("LSM")]),
            Taken([Entry("LSM") with { SecurityDescriptor = Reading<string>.Denied(5, "Access is denied.") }]));

        var entry = Assert.Single(diff.NotFullyCompared);

        Assert.Equal("securityDescriptor", Assert.Single(entry.Incomparable));
        Assert.Empty(entry.Differences);
        Assert.Empty(diff.Changed);
        Assert.False(diff.Any);
    }

    [Fact]
    public void The_same_refusal_in_two_languages_is_not_a_difference()
    {
        // Found in a real file rather than imagined: Windows writes the sentence behind a
        // refusal in the language of the machine, so the same denial reads "Access is
        // denied." on one and "Odmowa dostępu." on another. Comparing the words would report
        // every refused field as drift on any pair of machines set up differently - `ADR-14`
        // one layer down.
        //
        // Refused on both sides is still named per entry rather than folded into the note
        // about the whole comparison. A refusal is about permissions on this one entry, and
        // on a real machine five entries refuse their descriptor while eight hundred hand it
        // over - a sentence about "the snapshot" would be false about most of it.
        var diff = Between(
            Taken([Entry("LSM") with { SecurityDescriptor = Reading<string>.Denied(5, "Access is denied.") }]),
            Taken([Entry("LSM") with { SecurityDescriptor = Reading<string>.Denied(5, "Odmowa dostepu.") }]));

        Assert.False(diff.Any);
        Assert.Equal("securityDescriptor", Assert.Single(Assert.Single(diff.NotFullyCompared).Incomparable));
    }

    [Fact]
    public void An_entry_can_be_changed_and_incompletely_compared_at_once()
    {
        var diff = Between(
            Taken([Entry("LSM")]),
            Taken([
                Entry("LSM") with
                {
                    StartType = Reading<StartType>.Present(StartType.Disabled),
                    SecurityDescriptor = Reading<string>.Denied(5, "Access is denied.")
                }
            ]));

        var entry = Assert.Single(diff.Changed);

        Assert.Equal("startType", Assert.Single(entry.Differences).Field);
        Assert.Equal("securityDescriptor", Assert.Single(entry.Incomparable));
        Assert.Empty(diff.NotFullyCompared);
        Assert.True(diff.Any);
    }

    [Fact]
    public void Two_machines_and_two_builds_are_said_out_loud()
    {
        var before = Taken([Entry("Spooler")]) with { Metadata = Metadata(machine: "STAGING", tool: "0.1.0") };
        var after = Taken([Entry("Spooler")]) with { Metadata = Metadata(machine: "PRODUCTION", tool: "0.2.0") };

        var caveats = Between(before, after).Caveats;

        Assert.True(caveats.MachineDiffers);
        Assert.True(caveats.ToolVersionDiffers);
    }

    /// <summary>
    /// Every comparison here is between two documents that really are snapshots, so the refusal
    /// is asserted once instead of in thirteen places - and asserted rather than ignored, since a
    /// helper that quietly returned nothing would turn a broken engine into thirteen null
    /// dereferences naming nothing. The refusal is the subject of its own tests next door.
    /// </summary>
    private static SnapshotDiff Between(Snapshot before, Snapshot after)
    {
        Assert.True(SnapshotDiff.TryBetween(before, after, out var diff, out var failure), failure);

        return diff!;
    }

    private static Snapshot Taken(IReadOnlyList<ScmEntry> entries) =>
        Snapshot.Of(entries, note: null, new FakeClock()) with
        {
            Metadata = Metadata()
        };

    /// <summary>
    /// Metadata a test controls rather than the machine's own.
    ///
    /// Snapshot.Of fills these in from the session it runs in, which would make every test
    /// about elevation pass or fail depending on how the test run was started.
    /// </summary>
    private static SnapshotMetadata Metadata(
        bool elevated = true, long at = 0, string machine = "TESTBOX", string tool = "0.1.0") =>
        new()
        {
            SchemaVersion = Snapshot.CurrentSchemaVersion,
            Machine = machine,
            OperatingSystem = "Microsoft Windows NT 10.0.26200.0",
            TakenAt = DateTimeOffset.UnixEpoch.AddSeconds(at),
            TakenBy = "TESTBOX\\somebody",
            Elevated = elevated,
            Note = null,
            Tool = tool
        };

    private static ScmEntry Entry(string name) =>
        Entries.Any with { ServiceName = name, DisplayName = $"{name} display name" };
}
