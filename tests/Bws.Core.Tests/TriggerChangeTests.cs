using System.Text.Json.Nodes;
using Bws.Core.Snapshots;

namespace Bws.Core.Tests;

/// <summary>
/// Which triggers a comparison says each side holds alone (backlog 625, owner's decision "a line per
/// trigger", 2026-10-27) - read off the kept copies of the snapshot, edited in one place each.
/// </summary>
public sealed class TriggerChangeTests
{
    private const string Seven = "snapshot-schema-7.json";
    private const string Six = "snapshot-schema-6.json";

    /// <summary>The RPC interface the manager writes in capitals on this machine, which the edits below change.</summary>
    private const string Interface = "0497B57D-2E66-424F-A0C6-157CD5D41700";

    /// <summary>
    /// One value changed is one trigger gone and one come - no pairing, a trigger has no identity to pair
    /// by. And the whole lists stay where they were, as JSON, because that is what <c>--json</c> writes.
    /// </summary>
    [Fact]
    public void A_trigger_whose_value_changed_is_one_removed_and_one_added()
    {
        var before = Tree(Seven);
        Data(Trigger(before, "Appinfo", 4))[0] = "0497b57d-2e66-424f-a0c6-157cd5d41700";

        var difference = Triggers(before, Tree(Seven));
        var change = difference.Triggers!;

        Assert.Equal("0497b57d-2e66-424f-a0c6-157cd5d41700", Assert.Single(Assert.Single(change.Removed).Data!).Values[0]);
        Assert.Equal(Interface, Assert.Single(Assert.Single(change.Added).Data!).Values[0]);
        Assert.StartsWith("[{", difference.Before, StringComparison.Ordinal);
        Assert.StartsWith("[{", difference.After, StringComparison.Ordinal);
    }

    /// <summary>A trigger dropped from the later side is said once, and nothing is said to have arrived.</summary>
    [Fact]
    public void A_trigger_taken_away_is_removed_and_nothing_is_added()
    {
        var after = Tree(Seven);
        Entry(after, "Appinfo")["triggers"]!.AsArray().RemoveAt(2);

        var change = Triggers(Tree(Seven), after).Triggers!;

        Assert.Equal("fd7a0523-dc70-43dd-9b2e-9c5ed48225b1", Assert.Single(Assert.Single(change.Removed).Data!).Values[0]);
        Assert.Empty(change.Added);
    }

    /// <summary>
    /// <b>A multiset, not a set</b>: the same trigger twice on one side and once on the other leaves one
    /// behind. A set would see the same members on both sides and leave a reported difference with no
    /// line under it.
    /// </summary>
    [Fact]
    public void A_trigger_held_twice_on_one_side_and_once_on_the_other_leaves_one()
    {
        var before = Tree(Seven);
        var triggers = Entry(before, "Appinfo")["triggers"]!.AsArray();
        triggers.Add(triggers[0]!.DeepClone());

        var change = Triggers(before, Tree(Seven)).Triggers!;

        Assert.Equal("201ef99a-7fa0-444c-9399-19ba84f12a1a", Assert.Single(Assert.Single(change.Removed).Data!).Values[0]);
        Assert.Empty(change.Added);
    }

    /// <summary>
    /// <b>The copies matched are the first ones on the later side</b>, so what is left over keeps its place -
    /// the order the lines came out in while the match searched the list, kept when it started counting (the
    /// review of PR #98).
    /// </summary>
    [Fact]
    public void The_copies_matched_are_the_first_so_what_arrived_keeps_its_order()
    {
        var before = Tree(Seven);
        var held = Entry(before, "Appinfo")["triggers"]!.AsArray();
        var first = held[0]!.DeepClone();
        var second = held[1]!.DeepClone();
        Entry(before, "Appinfo")["triggers"] = new JsonArray(first.DeepClone());

        var after = Tree(Seven);
        Entry(after, "Appinfo")["triggers"] = new JsonArray(first.DeepClone(), second, first.DeepClone());

        var change = Triggers(before, after).Triggers!;

        Assert.Empty(change.Removed);
        Assert.Equal(
            ["5f54ce7d-5b79-4175-8584-cb65313a0e98", "201ef99a-7fa0-444c-9399-19ba84f12a1a"],
            change.Added.Select(trigger => trigger.Data![0].Values[0]));
    }

    /// <summary>
    /// <b>A trigger held as nothing gives no lines</b>, and the person is shown the two whole lists instead -
    /// the reader lets it through in every schema, and until the review of PR #98 it came out as a line
    /// reading "/". The same against an older file, where the narrowing to kind and action once turned the
    /// nothing into a trigger of no kind.
    /// </summary>
    [Theory]
    [InlineData(Seven)]
    [InlineData(Six)]
    public void A_trigger_held_as_nothing_gives_no_lines_and_the_whole_lists_stay(string earlier)
    {
        var after = Tree(Seven);
        Entry(after, "sppsvc")["triggers"]!.AsArray().Insert(0, null);

        var difference = Assert.Single(Assert.Single(Diff(Tree(earlier), after).Changed).Differences);

        Assert.Equal("triggers", difference.Field);
        Assert.Null(difference.Triggers);
        Assert.StartsWith("[null,", difference.After, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A data item that is not whole gives no lines either</b> - a file older than seven is not refused
    /// for one, because that schema never held data, and each of these ended the comparison with an
    /// exception and code 1 before the review of PR #98 (<c>tools/snapshot-probe/trigger-shapes.ps1</c>).
    /// </summary>
    [Theory]
    [InlineData("[null]")]
    [InlineData("""[{"type":"String","values":null}]""")]
    [InlineData("""[{"type":"String","values":["a",null]}]""")]
    [InlineData("""[{"type":null,"values":["a"]}]""")]
    [InlineData("""[{"type":"String","values":[]}]""")]
    public void An_older_file_whose_data_item_is_not_whole_gives_no_lines(string data)
    {
        var after = Tree(Six);
        Entry(after, "sppsvc")["triggers"]![1]!["data"] = JsonNode.Parse(data);

        var difference = Assert.Single(Assert.Single(Diff(Tree(Six), after).Changed).Differences);

        Assert.Equal("triggers", difference.Field);
        Assert.Null(difference.Triggers);
        Assert.Contains(data, difference.After, StringComparison.Ordinal);
    }

    /// <summary>An entry with no triggers on one side has all of the other side's arriving.</summary>
    [Fact]
    public void Triggers_where_there_were_none_have_all_arrived()
    {
        var before = Tree(Seven);
        Entry(before, "Appinfo")["triggers"] = null;

        var change = Triggers(before, Tree(Seven)).Triggers!;

        Assert.Empty(change.Removed);
        Assert.Equal(6, change.Added.Count);
        Assert.All(change.Added, trigger => Assert.Equal("bc90d167-9470-4139-a9ba-be0bbbf5b74d", trigger.Subtype));
    }

    /// <summary>
    /// Against a file older than seven the lines hold what was compared - the kind and the action - and no
    /// subtype or data the older side never had.
    /// </summary>
    [Fact]
    public void Against_an_older_file_a_line_holds_the_kind_and_the_action_alone()
    {
        var before = Tree(Six);
        Entry(before, "sppsvc")["triggers"]![1]!["kind"] = "DeviceArrival";

        var difference = Assert.Single(Diff(before, Tree(Seven)).Changed);
        var change = Assert.Single(difference.Differences).Triggers!;

        Assert.Equal(new TriggerDocument("DeviceArrival", "Start", null, null), Assert.Single(change.Removed));
        Assert.Equal(new TriggerDocument("CustomSystemStateChange", "Start", null, null), Assert.Single(change.Added));
    }

    /// <summary>
    /// Kind thirty was named Aggregate inside schema seven (backlog 627), and every file older than seven
    /// holds it as Unknown - so against such a file the name is folded back, and a machine where nothing
    /// changed shows nothing.
    /// </summary>
    [Fact]
    public void Against_an_older_file_an_aggregate_trigger_held_there_as_unknown_is_the_same_trigger()
    {
        var before = Tree(Six);
        Entry(before, "sppsvc")["triggers"]![1]!["kind"] = "Unknown";
        var after = Tree(Seven);
        Trigger(after, "sppsvc", 1)["kind"] = "Aggregate";

        Assert.Empty(Diff(before, after).Changed);
    }

    /// <summary>
    /// The fold is for a file older than seven alone. Two files of schema seven compare the kind as written,
    /// so a file a build before backlog 627 wrote shows the change once - the price the owner accepted on
    /// 2026-10-28 for naming it without a schema eight.
    /// </summary>
    [Fact]
    public void Between_two_files_of_schema_seven_unknown_and_aggregate_are_two_kinds()
    {
        var before = Tree(Seven);
        Trigger(before, "sppsvc", 1)["kind"] = "Unknown";
        var after = Tree(Seven);
        Trigger(after, "sppsvc", 1)["kind"] = "Aggregate";

        var change = Assert.Single(Assert.Single(Diff(before, after).Changed).Differences).Triggers!;

        Assert.Equal("Unknown", Assert.Single(change.Removed).Kind);
        Assert.Equal("Aggregate", Assert.Single(change.Added).Kind);
    }

    /// <summary>Only the triggers field carries the lines - every other difference is its two values.</summary>
    [Fact]
    public void Only_the_triggers_field_carries_a_trigger_change()
    {
        var before = Tree(Seven);
        Entry(before, "Appinfo")["startType"] = "Disabled";

        var difference = Assert.Single(Assert.Single(Diff(before, Tree(Seven)).Changed).Differences);

        Assert.Equal("startType", difference.Field);
        Assert.Null(difference.Triggers);
    }

    private static FieldDifference Triggers(JsonNode before, JsonNode after)
    {
        var changed = Assert.Single(Diff(before, after).Changed);

        Assert.Equal("Appinfo", changed.ServiceName);

        return Assert.Single(changed.Differences);
    }

    private static SnapshotDiff Diff(JsonNode before, JsonNode after)
    {
        Assert.True(SnapshotJson.TryRead(before.ToJsonString(), out var earlier, out var failure), failure);
        Assert.True(SnapshotJson.TryRead(after.ToJsonString(), out var later, out failure), failure);
        Assert.True(SnapshotDiff.TryBetween(earlier!, later!, out var diff, out failure), failure);

        return diff;
    }

    private static JsonNode Tree(string kept) => JsonNode.Parse(SnapshotGoldenTests.KeptText(kept))!;

    private static JsonNode Entry(JsonNode tree, string name) =>
        tree["entries"]!.AsArray().Single(entry => (string?)entry!["serviceName"] == name)!;

    private static JsonNode Trigger(JsonNode tree, string name, int index) => Entry(tree, name)["triggers"]![index]!;

    private static JsonArray Data(JsonNode trigger) => trigger["data"]![0]!["values"]!.AsArray();
}
