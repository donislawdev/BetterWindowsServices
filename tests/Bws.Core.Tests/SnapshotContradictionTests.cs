using System.Text.Json.Nodes;
using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// A document that says two things about one field that cannot both be true is refused when it is read,
/// before its lists decide what a comparison looks at - logic report S06, owner's decision 2026-10-26.
///
/// Every specimen here starts as a snapshot this build wrote and is damaged in one place, so a refusal can
/// only be about that place.
/// </summary>
public sealed class SnapshotContradictionTests
{
    [Fact]
    public void An_undamaged_snapshot_is_read()
    {
        Assert.True(SnapshotJson.TryRead(Damaged(_ => { }), out _, out var failure), failure);
    }

    [Fact]
    public void A_value_beside_never_asked_about_is_refused_and_named()
    {
        // The report's own case: startType Automatic and "notRead": ["startType"]. The comparison believed
        // the list, so a startup type that changed was called one nobody could compare.
        var refused = Refused(entry => entry["notRead"] = new JsonArray("startType"));

        Assert.Contains("'startType'", refused, StringComparison.Ordinal);
        Assert.Contains("'Spooler'", refused, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_beside_a_refusal_is_refused()
    {
        var refused = Refused(entry => entry["unreadable"] = new JsonObject
        {
            ["account"] = new JsonObject { ["errorCode"] = 5, ["message"] = "Access is denied." }
        });

        Assert.Contains("'account'", refused, StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_both_refused_and_never_asked_about_is_refused()
    {
        var refused = Refused(entry =>
        {
            entry["triggers"] = null;
            entry["notRead"] = new JsonArray("triggers");
            entry["unreadable"] = new JsonObject { ["triggers"] = new JsonObject { ["errorCode"] = 5, ["message"] = "Access is denied." } };
        });

        Assert.Contains("'triggers' both as refused and as never asked about", refused, StringComparison.Ordinal);
    }

    [Fact]
    public void A_list_name_spelled_with_a_capital_is_the_same_field()
    {
        // The comparison reads "StartType" in a list as startType, so the check reads it the same way.
        var refused = Refused(entry => entry["notRead"] = new JsonArray("StartType"));

        Assert.Contains("'startType'", refused, StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_with_no_value_beside_never_asked_about_is_what_this_build_writes()
    {
        Assert.True(
            SnapshotJson.TryRead(Damaged(entry => { entry["startType"] = null; entry["notRead"] = new JsonArray("startType"); }), out _, out var failure),
            failure);
    }

    [Fact]
    public void A_name_nobody_has_heard_of_in_a_list_is_left_alone()
    {
        // The decision: a later build may write a field this one does not know.
        Assert.True(
            SnapshotJson.TryRead(Damaged(entry => entry["notRead"] = new JsonArray("somethingNew")), out _, out var failure),
            failure);
    }

    [Fact]
    public void An_empty_service_name_is_refused()
    {
        var refused = Refused(entry => entry["serviceName"] = string.Empty);

        Assert.Contains("empty service name", refused, StringComparison.Ordinal);
    }

    /// <summary>The failure TryRead gives the damaged snapshot - and a check that it gave one.</summary>
    private static string Refused(Action<JsonObject> damage)
    {
        Assert.False(SnapshotJson.TryRead(Damaged(damage), out _, out var failure));

        return failure!;
    }

    /// <summary>A snapshot of one ordinary entry, with that entry changed in the file.</summary>
    private static string Damaged(Action<JsonObject> damage)
    {
        var tree = JsonNode.Parse(SnapshotJson.Render(Snapshot.Of([Entries.Any], note: null, new FakeClock())))!.AsObject();

        damage(tree["entries"]![0]!.AsObject());

        return tree.ToJsonString();
    }
}
