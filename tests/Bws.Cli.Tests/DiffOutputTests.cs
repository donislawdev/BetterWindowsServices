using System.Text.Json;
using Bws.Core.Snapshots;

namespace Bws.Cli.Tests;

/// <summary>
/// What the comparison says, as text and as a document, for what package E of the stability report
/// added on 2026-09-30: drift meaning configuration only (D-2), per-user session copies left out and
/// counted (D-1), and three caveats about the two files' metadata (D-5).
/// </summary>
public sealed class DiffOutputTests
{
    [Fact]
    public void An_entry_that_differs_only_in_running_state_is_printed_and_does_not_make_the_document_differ()
    {
        // The reader of Drifted that had a second question in it: "No differences." printed over an
        // entry that differs only in state would hide the one thing this comparison found.
        var diff = Of(changed: [Changed(DifferenceGroup.RunningState)]);

        var text = DiffText.Render(diff);

        Assert.DoesNotContain(Texts.Of("cli.diff.same"), text, StringComparison.Ordinal);
        Assert.Contains("Spooler", text, StringComparison.Ordinal);
        Assert.False(Document(diff).GetProperty("differs").GetBoolean());
    }

    [Fact]
    public void A_configuration_difference_makes_the_document_differ()
    {
        Assert.True(Document(Of(changed: [Changed(DifferenceGroup.Configuration)])).GetProperty("differs").GetBoolean());
    }

    [Fact]
    public void Session_copies_left_out_are_counted_in_one_line_and_in_the_document()
    {
        var diff = Of(changed: [], leftOut: new InstancesLeftOut(3, 2));

        Assert.Contains(Texts.Of("cli.diff.caveat.instancesLeftOut", 3, 2), DiffText.Render(diff), StringComparison.Ordinal);

        var counted = Document(diff).GetProperty("instancesLeftOut");

        Assert.Equal(3, counted.GetProperty("earlier").GetInt32());
        Assert.Equal(2, counted.GetProperty("later").GetInt32());
    }

    [Fact]
    public void No_line_about_session_copies_when_there_were_none()
    {
        var text = DiffText.Render(Of(changed: []));

        Assert.DoesNotContain(Texts.Of("cli.diff.caveat.instancesLeftOut", 0, 0), text, StringComparison.Ordinal);
        Assert.Equal(Texts.Of("cli.diff.same") + Environment.NewLine, text);
    }

    [Fact]
    public void The_three_caveats_of_schema_five_are_said_and_written()
    {
        var diff = Of(changed: []) with
        {
            Caveats = new ComparisonCaveats(false, false, false, false, true, true, false, ["operatingSystemVersion", "namesLanguage"])
        };

        var text = DiffText.Render(diff);

        Assert.Contains(Texts.Of("cli.diff.caveat.language"), text, StringComparison.Ordinal);
        Assert.Contains(Texts.Of("cli.diff.caveat.account"), text, StringComparison.Ordinal);
        Assert.Contains(Texts.Of("cli.diff.caveat.notKnown", "operatingSystemVersion, namesLanguage"), text, StringComparison.Ordinal);

        var caveats = Document(diff).GetProperty("caveats");

        Assert.True(caveats.GetProperty("languageDiffers").GetBoolean());
        Assert.True(caveats.GetProperty("accountDiffers").GetBoolean());
        Assert.Equal(
            ["operatingSystemVersion", "namesLanguage"],
            caveats.GetProperty("notKnown").EnumerateArray().Select(name => name.GetString()));
    }

    [Fact]
    public void The_caveat_of_schema_six_is_said_and_written_last()
    {
        var diff = Of(changed: []) with
        {
            Caveats = new ComparisonCaveats(false, false, false, false, false, false, true, [])
        };

        Assert.Contains(Texts.Of("cli.diff.caveat.fileVersion"), DiffText.Render(diff), StringComparison.Ordinal);

        // Last, so a script reading the document by position before 2026-10-06 still finds what it found.
        var caveats = Document(diff).GetProperty("caveats");

        Assert.True(caveats.GetProperty("fileVersionSourceDiffers").GetBoolean());
        Assert.Equal("fileVersionSourceDiffers", caveats.EnumerateObject().Last().Name);
    }

    [Fact]
    public void A_field_refused_to_both_snapshots_is_one_line_naming_the_entries_and_nothing_else_differs()
    {
        // Backlog 559: this machine compared with itself a minute later used to print ten entries
        // under "Not fully compared", because Windows refuses their descriptions to every reader.
        // One line per field now, the entries named in it, and the report still says it found
        // nothing - since neither side holds a value, there was nothing a second look could settle.
        var diff = Of(changed: []) with
        {
            RefusedByBoth = [new RefusedField("description", ["IsoSessionCore", "Tcpip6"])]
        };

        var text = DiffText.Render(diff);

        Assert.Contains(
            Texts.Of("cli.diff.caveat.refusedByBoth.many", "description", 2, "IsoSessionCore, Tcpip6"),
            text,
            StringComparison.Ordinal);
        Assert.Contains(Texts.Of("cli.diff.same"), text, StringComparison.Ordinal);
        Assert.DoesNotContain(Texts.Of("cli.diff.notFullyCompared", 2), text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_the_manager_still_holds_has_its_own_heading_and_is_seen_by_one_side()
    {
        // Logic report S03: not removed, and not "cannot tell" either - the manager says it holds it.
        var diff = Of(changed: []) with { Unlisted = [new EntryPresence("ZTDNS", "ZTDNS display name")] };

        var text = DiffText.Render(diff);

        Assert.Contains(Texts.Of("cli.diff.unlisted", 1), text, StringComparison.Ordinal);
        Assert.Contains(Texts.Of("cli.diff.incomplete.seenByOne.one", 1), text, StringComparison.Ordinal);
        Assert.DoesNotContain(Texts.Of("cli.diff.same"), text, StringComparison.Ordinal);
        Assert.False(Document(diff).GetProperty("differs").GetBoolean());
    }

    [Fact]
    public void A_script_finds_entries_the_manager_still_holds_among_the_uncertain_in_order()
    {
        // The owner's decision named them uncertain, and a key of their own would be a new contract.
        var diff = Of(changed: []) with
        {
            Uncertain = [new EntryPresence("Zeta", "Zeta")],
            Unlisted = [new EntryPresence("Alpha", "Alpha")]
        };

        var names = Document(diff).GetProperty("uncertain").EnumerateArray()
            .Select(entry => entry.GetProperty("serviceName").GetString());

        Assert.Equal(["Alpha", "Zeta"], names);
    }

    [Fact]
    public void One_entry_refused_a_field_on_both_sides_is_said_in_the_singular()
    {
        var text = DiffText.Render(Of(changed: []) with
        {
            RefusedByBoth = [new RefusedField("securityDescriptor", ["LSM"])]
        });

        Assert.Contains(
            Texts.Of("cli.diff.caveat.refusedByBoth.one", "securityDescriptor", 1, "LSM"),
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_refused_to_both_snapshots_is_written_last_with_its_entries()
    {
        // Last, so a script reading the document by position before 2026-10-14 still finds what it
        // found. And the entries are in it by name, so a script loses nothing that the old
        // per-entry place under notFullyCompared told it.
        var root = Document(Of(changed: []) with
        {
            RefusedByBoth = [new RefusedField("description", ["IsoSessionCore", "Tcpip6"])]
        });

        Assert.Equal("refusedByBoth", root.EnumerateObject().Last().Name);

        var refused = Assert.Single(root.GetProperty("refusedByBoth").EnumerateArray());

        Assert.Equal("description", refused.GetProperty("field").GetString());
        Assert.Equal(["IsoSessionCore", "Tcpip6"], refused.GetProperty("entries").EnumerateArray().Select(name => name.GetString()));
        Assert.Empty(root.GetProperty("notFullyCompared").EnumerateArray());
    }

    private static SnapshotDiff Of(IReadOnlyList<ChangedEntry> changed, InstancesLeftOut? leftOut = null) =>
        new(
            [], [], changed, [], [], [], [],
            new ComparisonCaveats(false, false, false, false, false, false, false, []),
            leftOut ?? new InstancesLeftOut(0, 0),
            []);

    private static ChangedEntry Changed(DifferenceGroup group) =>
        new("Spooler", "Print Spooler", [new FieldDifference("status", group, "Running", "Stopped")], []);

    private static JsonElement Document(SnapshotDiff diff) => JsonDocument.Parse(DiffJson.Render(diff)).RootElement;
}
