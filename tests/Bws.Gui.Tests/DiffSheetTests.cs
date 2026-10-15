using Bws.Core;
using Bws.Core.Snapshots;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the window shows in place of the list when a snapshot is compared with the machine - W1 of
/// `docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md`. The sheet is built from the same engine the
/// command line uses, so these hold what the WINDOW adds: one row per field, the list's own words
/// for a field, the caveats above the table, and nothing from a foreign file reordering the text.
/// </summary>
public sealed class DiffSheetTests
{
    [Fact]
    public void A_snapshot_compared_with_the_same_machine_is_a_sentence_and_no_rows()
    {
        var sheet = Sheet([Rows.Entry("Spooler")], [Rows.Entry("Spooler")]);

        Assert.Empty(sheet.Lines);
        Assert.Equal(Texts.Of("gui.diff.same"), sheet.Same);
        Assert.Equal(Texts.Of("gui.diff.counts", 0, 0, 0), sheet.Counts);
    }

    [Fact]
    public void A_changed_field_is_one_row_named_by_the_heading_the_list_puts_over_it()
    {
        var sheet = Sheet(
            [Rows.Entry("Spooler") with { StartType = Reading<StartType>.Present(StartType.Automatic) }],
            [Rows.Entry("Spooler") with { StartType = Reading<StartType>.Present(StartType.Manual) }]);

        var line = Assert.Single(sheet.Lines);

        Assert.Equal("Spooler", line.ServiceName);
        Assert.Equal(Texts.Of(Columns.Of("startType")!.LabelKey), line.What);
        Assert.Equal("Automatic", line.Before);
        Assert.Equal("Manual", line.After);
        Assert.Equal(string.Empty, sheet.Same);
    }

    [Fact]
    public void A_field_about_what_was_running_says_so_beside_its_name()
    {
        // The distinction --exit-code draws since 2026-09-30, and the one a person most needs: a
        // service that stopped by itself is not somebody changing the machine.
        var sheet = Sheet([Rows.Entry("Spooler")], [Rows.Stopped("Spooler")]);

        var line = Assert.Single(sheet.Lines, each => each.Before == "Running");

        Assert.Equal(Texts.Of("gui.diff.row.state", Texts.Of(Columns.Of("status")!.LabelKey)), line.What);
    }

    [Fact]
    public void An_entry_that_came_and_one_that_went_are_rows_that_say_so()
    {
        var sheet = Sheet([Rows.Entry("Gone")], [Rows.Entry("Arrived")]);

        Assert.Contains(sheet.Lines, line => line.ServiceName == "Arrived" && line.What == Texts.Of("gui.diff.row.added"));
        Assert.Contains(sheet.Lines, line => line.ServiceName == "Gone" && line.What == Texts.Of("gui.diff.row.removed"));
        Assert.Equal(Texts.Of("gui.diff.counts", 1, 1, 0), sheet.Counts);
    }

    [Fact]
    public void A_field_refused_to_both_sides_is_one_sentence_above_the_table_and_no_row()
    {
        // Backlog 559 in the window: the ten descriptions this machine refuses to every reader were
        // ten rows of nothing on every comparison.
        var refused = Rows.Entry("IsoSessionCore") with
        {
            Description = Reading<string>.Denied(15100, "The resource loader failed to find MUI file.")
        };

        var sheet = Sheet([refused], [refused]);

        Assert.Empty(sheet.Lines);
        Assert.Contains(
            Texts.Of("gui.diff.caveat.refusedByBoth.one", Texts.Of(Columns.Of("description")!.LabelKey), 1, "IsoSessionCore"),
            sheet.Caveats);
    }

    [Fact]
    public void A_character_in_a_foreign_file_cannot_reorder_the_text_around_it()
    {
        // The other side may be a file somebody else wrote. A right to left override in a display name
        // would show one thing and carry another - written out as its code point, as the command line
        // has done since C-4. Built from a number, because an escape typed here lands on disk as the
        // character itself.
        var hostile = "exe" + char.ConvertFromUtf32(0x202E) + "txt.";
        var sheet = Sheet([Rows.Entry("Gone", hostile)], []);

        var line = Assert.Single(sheet.Lines);

        Assert.Contains("<U+202E>", line.DisplayName, StringComparison.Ordinal);
        Assert.DoesNotContain(char.ConvertFromUtf32(0x202E), line.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_name_two_files_wrote_cannot_reorder_the_text_around_it_either()
    {
        // A field neither side read is said once above the table under the name the files give it,
        // and when both sides are files a pair of them decides that name.
        var hostile = "exe" + char.ConvertFromUtf32(0x202E) + "txt";
        EntryDocument[] both = [EntryDocument.From(Rows.Entry("Spooler")) with { NotRead = [hostile] }];

        Assert.True(SnapshotDiff.TryBetween(Documented(both), Documented(both), out var diff, out _));

        var caveats = DiffSheet.Of(Documented(both).Metadata, diff).Caveats;

        Assert.Contains(caveats, caveat => caveat.Contains("exe<U+202E>txt", StringComparison.Ordinal));
        Assert.DoesNotContain(caveats, caveat => caveat.Contains(char.ConvertFromUtf32(0x202E), StringComparison.Ordinal));
    }

    [Fact]
    public void A_snapshot_taken_without_rights_says_so_and_its_note_is_shown()
    {
        var sheet = Of(Taken([Rows.Entry("Spooler")], elevated: false, note: "Before the patch"), [Rows.Entry("Spooler")]);

        var when = DateTimeOffset.UnixEpoch.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);

        Assert.Equal(Texts.Of("gui.diff.about.unelevated", "TESTBOX", when, "TESTBOX\\somebody"), Assert.Single(sheet.Sides).About);
        Assert.Equal(Texts.Of("gui.diff.note", "Before the patch"), Assert.Single(sheet.Sides).Note);
        Assert.Contains(Texts.Of("gui.diff.caveat.elevation"), sheet.Caveats);
    }

    [Fact]
    public void A_snapshot_with_no_note_has_no_line_for_one()
    {
        Assert.Equal(string.Empty, Assert.Single(Sheet([Rows.Entry("Spooler")], [Rows.Entry("Spooler")]).Sides).Note);
    }

    private static DiffSheet Sheet(IReadOnlyList<ScmEntry> earlier, IReadOnlyList<ScmEntry> later) =>
        Of(Taken(earlier, elevated: true, note: null), later);

    private static DiffSheet Of(Snapshot earlier, IReadOnlyList<ScmEntry> later)
    {
        Assert.True(SnapshotDiff.TryBetween(earlier, Taken(later, elevated: true, note: null), out var diff, out _));

        return DiffSheet.Of(earlier.Metadata, diff);
    }

    private static Snapshot Taken(IReadOnlyList<ScmEntry> entries, bool elevated, string? note) =>
        Documented([.. entries.Select(EntryDocument.From)], elevated, note);

    private static Snapshot Documented(IReadOnlyList<EntryDocument> entries, bool elevated = true, string? note = null) =>
        new(
            new SnapshotMetadata
            {
                SchemaVersion = Snapshot.CurrentSchemaVersion,
                Machine = "TESTBOX",
                OperatingSystem = "Microsoft Windows NT 10.0.26200.0",
                TakenAt = DateTimeOffset.UnixEpoch,
                TakenBy = "TESTBOX\\somebody",
                Elevated = elevated,
                Note = note,
                Tool = "0.4.0"
            },
            entries);
}
