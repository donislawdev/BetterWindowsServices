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

    private static SnapshotDiff Of(IReadOnlyList<ChangedEntry> changed, InstancesLeftOut? leftOut = null) =>
        new(
            [], [], changed, [], [], [],
            new ComparisonCaveats(false, false, false, false, false, false, false, []),
            leftOut ?? new InstancesLeftOut(0, 0));

    private static ChangedEntry Changed(DifferenceGroup group) =>
        new("Spooler", "Print Spooler", [new FieldDifference("status", group, "Running", "Stopped")], []);

    private static JsonElement Document(SnapshotDiff diff) => JsonDocument.Parse(DiffJson.Render(diff)).RootElement;
}
