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
            Caveats = new ComparisonCaveats(false, false, false, false, true, true, false, false, ["operatingSystemVersion", "namesLanguage"])
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
            Caveats = new ComparisonCaveats(false, false, false, false, false, false, true, false, [])
        };

        Assert.Contains(Texts.Of("cli.diff.caveat.fileVersion"), DiffText.Render(diff), StringComparison.Ordinal);

        // After everything that was there before it, so a script reading the document by position before
        // 2026-10-06 still finds what it found - and followed only by the caveat schema seven added.
        var caveats = Document(diff).GetProperty("caveats");

        Assert.True(caveats.GetProperty("fileVersionSourceDiffers").GetBoolean());
        Assert.Equal(
            ["fileVersionSourceDiffers", "triggerDetailDiffers"],
            caveats.EnumerateObject().TakeLast(2).Select(field => field.Name));
    }

    /// <summary>
    /// The caveat of schema seven (logic report C01, 2026-10-27): said in the text and written last in
    /// the document, so nothing a script read by position before that day moved.
    /// </summary>
    [Fact]
    public void The_caveat_of_schema_seven_is_said_and_written_last()
    {
        var diff = Of(changed: []) with
        {
            Caveats = new ComparisonCaveats(false, false, false, false, false, false, false, true, [])
        };

        Assert.Contains(Texts.Of("cli.diff.caveat.triggers"), DiffText.Render(diff), StringComparison.Ordinal);

        var caveats = Document(diff).GetProperty("caveats");

        Assert.True(caveats.GetProperty("triggerDetailDiffers").GetBoolean());
        Assert.Equal("triggerDetailDiffers", caveats.EnumerateObject().Last().Name);
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
    public void A_script_finds_entries_the_manager_still_holds_under_their_own_key_at_the_end()
    {
        // Backlog 623, owner's decision: their own key, so a script tells "the manager says it is there"
        // from "the other side may not have been shown it" - and last, so the document only grew at its end.
        var root = Document(Of(changed: []) with
        {
            Uncertain = [new EntryPresence("Zeta", "Zeta")],
            Unlisted = [new EntryPresence("Alpha", "Alpha")]
        });

        Assert.Equal("unlisted", root.EnumerateObject().Last().Name);
        Assert.Equal("Alpha", Assert.Single(root.GetProperty("unlisted").EnumerateArray()).GetProperty("serviceName").GetString());
        Assert.Equal("Zeta", Assert.Single(root.GetProperty("uncertain").EnumerateArray()).GetProperty("serviceName").GetString());
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
        // Last when it came, so a script reading the document by position before 2026-10-14 still finds
        // what it found - and since 2026-10-27 followed only by "unlisted", which came after it. And the
        // entries are in it by name, so a script loses nothing that the old per-entry place under
        // notFullyCompared told it.
        var root = Document(Of(changed: []) with
        {
            RefusedByBoth = [new RefusedField("description", ["IsoSessionCore", "Tcpip6"])]
        });

        Assert.Equal(["refusedByBoth", "unlisted"], root.EnumerateObject().TakeLast(2).Select(property => property.Name));

        var refused = Assert.Single(root.GetProperty("refusedByBoth").EnumerateArray());

        Assert.Equal("description", refused.GetProperty("field").GetString());
        Assert.Equal(["IsoSessionCore", "Tcpip6"], refused.GetProperty("entries").EnumerateArray().Select(name => name.GetString()));
        Assert.Empty(root.GetProperty("notFullyCompared").EnumerateArray());
    }

    /// <summary>
    /// <b>A line per trigger only one side holds, the other side "nothing"</b> - backlog 625, the owner's
    /// decision "a line per trigger" of 2026-10-27, in the words of the preview the owner chose. A value
    /// changed in one trigger is one line out and one line in. And the machine document keeps the two
    /// whole lists, which is the contract a script reads.
    /// </summary>
    [Fact]
    public void A_trigger_change_is_a_line_per_trigger_and_the_json_keeps_the_two_lists()
    {
        var removed = new TriggerDocument(
            "FirewallPort", "Start", "b7569e07-8421-4ee0-ad10-86915afdad09", [new TriggerDataDocument("String", ["500", "UDP"])]);
        var added = removed with { Data = [new TriggerDataDocument("String", ["501", "UDP"])] };
        var difference = new FieldDifference("triggers", DifferenceGroup.Configuration, "[before]", "[after]")
        {
            Triggers = new TriggerChange([removed], [added])
        };
        var diff = Of(changed: [new ChangedEntry("IKEEXT", "IKE and AuthIP", [difference], [])]);

        var text = DiffText.Render(diff);

        Assert.Contains("    triggers: Firewall port/Start  Port opened  500, UDP -> nothing" + Environment.NewLine, text, StringComparison.Ordinal);
        Assert.Contains("    triggers: nothing -> Firewall port/Start  Port opened  501, UDP" + Environment.NewLine, text, StringComparison.Ordinal);
        Assert.DoesNotContain("[before]", text, StringComparison.Ordinal);

        var written = Document(diff).GetProperty("changed")[0].GetProperty("differences")[0];

        Assert.Equal("[before]", written.GetProperty("before").GetString());
        Assert.Equal("[after]", written.GetProperty("after").GetString());
        Assert.False(written.TryGetProperty("triggers", out _));
    }

    /// <summary>
    /// A triggers field that differs with no trigger on either side alone - nothing against an empty list,
    /// which a file can hold - still says its two values. A difference with no line under it is silence.
    /// </summary>
    [Fact]
    public void A_trigger_difference_with_no_line_to_give_says_its_two_values()
    {
        var difference = new FieldDifference("triggers", DifferenceGroup.Configuration, null, "[]")
        {
            Triggers = new TriggerChange([], [])
        };

        var text = DiffText.Render(Of(changed: [new ChangedEntry("Spooler", "Print Spooler", [difference], [])]));

        Assert.Contains("    triggers: nothing -> []" + Environment.NewLine, text, StringComparison.Ordinal);
    }

    private static SnapshotDiff Of(IReadOnlyList<ChangedEntry> changed, InstancesLeftOut? leftOut = null) =>
        new(
            [], [], changed, [], [], [], [],
            new ComparisonCaveats(false, false, false, false, false, false, false, false, []),
            leftOut ?? new InstancesLeftOut(0, 0),
            []);

    private static ChangedEntry Changed(DifferenceGroup group) =>
        new("Spooler", "Print Spooler", [new FieldDifference("status", group, "Running", "Stopped")], []);

    private static JsonElement Document(SnapshotDiff diff) => JsonDocument.Parse(DiffJson.Render(diff)).RootElement;
}
