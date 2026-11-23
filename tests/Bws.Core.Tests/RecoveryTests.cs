using System.Text.Json.Nodes;
using Bws.Core.Querying;
using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// services.msc's recovery tab in the listing, the machine readable output, the snapshot and the query
/// language - backlog 555, 2026-11-23, the owner's decisions to hold it in the JSON and the snapshot
/// (schema eight) and to read everything that tab shows.
/// </summary>
public sealed class RecoveryTests
{
    /// <summary>
    /// <b>The units are the manager's and sc.exe's</b> - milliseconds for a delay, seconds for the period -
    /// and INFINITE is the number the manager holds, never null, because null in this document means a
    /// field nobody read. Every member is written, so a script can count on the shape - in the ordinal key
    /// order every object of a snapshot is written in.
    /// </summary>
    [Fact]
    public void A_recovery_of_every_kind_is_written_in_the_units_the_manager_keeps()
    {
        var written = Written(Specimens.RecoveryOfEveryKind);

        Assert.Equal(
            """{"actions":[{"action":"RestartService","delayMilliseconds":60000},{"action":"RunProgram","delayMilliseconds":120000},{"action":"RestartComputer","delayMilliseconds":300000}],"command":"C:\\Tools\\notify.cmd /fail=%1%","onNonCrashFailures":true,"rebootMessage":"Restarting after a failure of Watched","resetPeriodSeconds":4294967295}""",
            written["recovery"]!.ToJsonString());
    }

    /// <summary>
    /// <b>Nothing set is null with no reason given, as for the triggers</b>, and a refusal is null named
    /// under "unreadable" - two different answers that must not read alike.
    /// </summary>
    [Fact]
    public void An_entry_with_none_set_writes_null_and_a_refused_one_says_so()
    {
        var none = Written(Specimens.Ordinary);
        var refused = Written(Specimens.Refused);

        Assert.Null(none["recovery"]);
        Assert.Null(none["unreadable"]?["recovery"]);
        Assert.Null(refused["recovery"]);
        Assert.NotNull(refused["unreadable"]!["recovery"]);
    }

    /// <summary>
    /// Between two eights a changed recovery is a difference on the field "recovery" - here one delay of
    /// MSiSCSI's shape moved from 300 s to 600 s. And the same pair with the older one labelled seven is
    /// not, because a seven never held recovery and the caveat says so once instead.
    /// </summary>
    [Fact]
    public void A_changed_recovery_differs_between_two_eights_and_is_left_out_against_a_seven()
    {
        var before = Snapshot.Of([Specimens.RecoveryWithAProgramThatNeverRuns], note: null, new FakeClock());
        var after = Snapshot.Of([Moved(Specimens.RecoveryWithAProgramThatNeverRuns)], note: null, new FakeClock());

        Assert.True(SnapshotDiff.TryBetween(before, after, out var exact, out var failure), failure);

        var changed = Assert.Single(exact.Changed);

        Assert.Equal("recovery", Assert.Single(changed.Differences).Field);
        Assert.False(exact.Caveats.RecoveryNotRecorded);

        var seven = Labelled(before, 7);

        Assert.True(SnapshotDiff.TryBetween(seven, after, out var narrowed, out failure), failure);
        Assert.True(narrowed.Caveats.RecoveryNotRecorded);
        Assert.Empty(narrowed.Changed);
    }

    /// <summary>
    /// An eight whose recovery has no list of actions is refused - it does not hold what its schema
    /// promises. The same file labelled seven reads, because the rule belongs to the schema.
    /// </summary>
    [Fact]
    public void An_eight_with_a_recovery_missing_its_actions_is_refused_and_a_seven_is_not()
    {
        var text = SnapshotJson.Render(Snapshot.Of([Specimens.RecoveryOfEveryKind], note: null, new FakeClock()));
        var tree = JsonNode.Parse(text)!;

        tree["entries"]![0]!["recovery"]!["actions"] = null;

        var broken = tree.ToJsonString();

        Assert.False(SnapshotJson.TryRead(broken, out _, out var failure));
        Assert.Contains("recovery", failure, StringComparison.Ordinal);

        tree["metadata"]!["schemaVersion"] = 7;

        Assert.True(SnapshotJson.TryRead(tree.ToJsonString(), out _, out failure), failure);
    }

    /// <summary>
    /// <c>recovery:none</c> is the spec's own example (A3): an entry with nothing set. A refused recovery is
    /// not "none" - nobody could tell - and a value names what a failure sets off, never an item that does
    /// nothing.
    /// </summary>
    [Fact]
    public void The_query_asks_what_a_failure_sets_off()
    {
        IReadOnlyList<ScmEntry> entries =
            [Specimens.Ordinary, Specimens.Refused, Specimens.RecoveryOfEveryKind, Specimens.RecoveryWithAProgramThatNeverRuns];

        Assert.Contains(Specimens.Ordinary.ServiceName, Match("recovery:none", entries));
        Assert.DoesNotContain("Locked", Match("recovery:none", entries));
        Assert.Equal(["Watched", "Leftover"], Match("recovery:restart", entries));
        Assert.Equal(["Watched"], Match("recovery:program", entries));
        Assert.Equal(["Watched"], Match("recovery:reboot", entries));
    }

    private static JsonObject Written(ScmEntry entry) =>
        JsonNode.Parse(SnapshotJson.Render(Snapshot.Of([entry], note: null, new FakeClock())))!["entries"]![0]!.AsObject();

    private static ScmEntry Moved(ScmEntry entry)
    {
        var settings = entry.Recovery.Value!;
        var items = settings.List.Items.ToArray();

        items[1] = items[1] with { Delay = TimeSpan.FromSeconds(600) };

        return entry with
        {
            Recovery = Reading<RecoverySettings>.Present(settings with { List = settings.List with { Items = items } })
        };
    }

    private static Snapshot Labelled(Snapshot snapshot, int schema)
    {
        var tree = JsonNode.Parse(SnapshotJson.Render(snapshot))!;

        tree["metadata"]!["schemaVersion"] = schema;

        Assert.True(SnapshotJson.TryRead(tree.ToJsonString(), out var labelled, out var failure), failure);

        return labelled!;
    }

    private static List<string> Match(string query, IReadOnlyList<ScmEntry> entries)
    {
        var parsed = QueryParser.Parse(query);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));

        return [.. parsed.Query!.Filter(entries).Entries.Select(entry => entry.ServiceName)];
    }
}
