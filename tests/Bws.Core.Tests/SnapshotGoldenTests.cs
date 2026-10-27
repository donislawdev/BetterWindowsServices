using System.Text;
using System.Text.Json.Nodes;
using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The snapshot file, byte for byte, against a copy kept beside this test.
///
/// <b>Every other test of the format asks one question of the text</b> - are the keys in order,
/// is the measurement left out, does it end with a newline. None of them can see a change nobody
/// thought to ask about: a field renamed by a tidy-up, a null that stops being written, a
/// character that starts coming out escaped, a line ending that moves. Each of those leaves every
/// snapshot a person already keeps differing from the next one on hundreds of lines, and `ADR-6`
/// exists to prevent exactly that. The field names are a frozen contract in `docs/02` as well, so
/// a change here is a schema change and not a repair.
///
/// <b>Written 2026-09-29 for S-11 of the performance report</b>, which found no such test, and
/// found the same day to be the precondition for NativeAOT: a trial AOT build printed an empty
/// object for every entry with exit code 0, and only a comparison of the bytes would have said
/// so - the text output of the same build was identical line for line.
///
/// <b>What the input is, and why it is not the machine.</b> The whole specimen catalogue with
/// signatures read, so every nested object and every state of a field is in the file, plus a
/// memory reading so the file shows it being left out. The metadata is fixed here rather than
/// taken, because the real one carries the machine name, the account and the tool version, and
/// a kept copy that changes with the machine it runs on is not a kept copy.
///
/// <b>When this goes red on purpose</b> - a new specimen, a field added with a schema bump - the
/// failure writes the new text beside the test binary and names the path. Copying it over the
/// kept one is the acceptance, and it is meant to be a deliberate act a reviewer sees in the diff.
/// </summary>
public sealed class SnapshotGoldenTests
{
    private const string Kept = "snapshot-schema-7.json";

    /// <summary>
    /// The copy kept before schema seven, 2026-10-27 - the same entries, with every trigger holding its
    /// kind and action only. Still read, and compared with a seven by those two alone.
    /// </summary>
    private const string KeptSix = "snapshot-schema-6.json";

    /// <summary>
    /// The copy kept before schema six, 2026-10-06 - the same shape and the same entries, with file
    /// versions that an older build could have taken from a language file. Still read, so it stays as
    /// the specimen of the file most people hold.
    /// </summary>
    private const string KeptFive = "snapshot-schema-5.json";

    /// <summary>
    /// The copy kept before schema five, 2026-09-29. Not written by this build any more and still read
    /// by it - the one bump that kept reading its predecessor - so it stays as the specimen of an
    /// older file somebody already has on disk.
    /// </summary>
    private const string KeptBefore = "snapshot-schema-4.json";

    [Fact]
    public void The_file_is_written_byte_for_byte_as_the_kept_copy()
    {
        var written = SnapshotJson.Render(Frozen());
        var kept = KeptText(Kept);

        if (string.Equals(written, kept, StringComparison.Ordinal))
        {
            return;
        }

        var actual = Path.Combine(AppContext.BaseDirectory, "snapshot-schema-7.actual.json");
        File.WriteAllText(actual, written, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Assert.Fail(
            "The snapshot file is no longer written the way it was. " + FirstDifference(kept, written) +
            " The new text is at " + actual + " - if the change is meant, it is a schema change (docs/02) " +
            "and the kept copy is replaced by hand in the same commit.");
    }

    [Fact]
    public void The_kept_copy_reads_back_and_is_written_again_unchanged()
    {
        // Reading loses nothing that writing produced. Without this the kept copy could hold a
        // field the reader drops on the floor, and the comparison of two snapshots would never see it.
        var kept = KeptText(Kept);

        Assert.True(SnapshotJson.TryRead(kept, out var snapshot, out var failure), failure);
        Assert.Equal(kept, SnapshotJson.Render(snapshot!));
    }

    [Fact]
    public void The_kept_copy_is_of_the_schema_this_build_writes()
    {
        // A schema bump without a new kept copy would leave this test comparing a new format with
        // an old file and calling the difference a fault. The file name carries the number so
        // the bump has to touch both.
        Assert.True(
            Snapshot.CurrentSchemaVersion == 7,
            $"The schema is now {Snapshot.CurrentSchemaVersion}. Keep a new copy named for it beside {Kept}.");
    }

    /// <summary>
    /// A file written by 0.3.0 still reads, and compares against one written now with nothing
    /// invented - stability report D-5, owner's decision of 2026-09-30.
    ///
    /// <b>The two kept copies hold the same entries</b>, so everything the comparison reports is
    /// about the format rather than the machine: the two fields version five added are "not known"
    /// rather than different, and the one per-user session copy in the catalogue is left out on
    /// each side and counted (D-1).
    /// </summary>
    [Fact]
    public void A_version_four_file_is_read_and_says_what_it_does_not_know()
    {
        Assert.True(SnapshotJson.TryRead(KeptText(KeptBefore), out var older, out var failure), failure);

        Assert.Equal(4, older!.Metadata.SchemaVersion);
        Assert.Null(older.Metadata.OperatingSystemVersion);
        Assert.Null(older.Metadata.NamesLanguage);

        Assert.True(SnapshotJson.TryRead(KeptText(Kept), out var newer, out failure), failure);
        Assert.True(SnapshotDiff.TryBetween(older, newer!, out var diff, out failure), failure);

        // Not "nothing reported" - the catalogue holds refused fields on purpose, and those are named
        // as not fully compared on any pair. What must be empty is anything claiming a change.
        Assert.False(diff.Drifted);
        Assert.Empty(diff.Changed);
        Assert.Equal(["operatingSystemVersion", "namesLanguage"], diff.Caveats.NotKnown);
        Assert.False(diff.Caveats.OperatingSystemDiffers);
        Assert.False(diff.Caveats.LanguageDiffers);
        Assert.Equal(new InstancesLeftOut(1, 1), diff.LeftOut);
    }

    /// <summary>
    /// A file written before schema six still reads, and its file versions are not compared with one
    /// written now - package SB, the owner's decision of 2026-10-06. Until six the version could come
    /// from a language file beside the binary, so comparing the two would report changes on a machine
    /// nobody touched (382 of 801 entries on the machine this was decided on).
    /// </summary>
    [Fact]
    public void A_version_five_file_is_read_and_its_file_versions_are_not_compared()
    {
        // One file version moved in the older copy, the way a language file would have moved it.
        var five = MovedVersion(KeptText(KeptFive));

        Assert.True(SnapshotJson.TryRead(five, out var older, out var failure), failure);
        Assert.True(SnapshotJson.TryRead(KeptText(Kept), out var newer, out failure), failure);
        Assert.True(SnapshotDiff.TryBetween(older!, newer!, out var diff, out failure), failure);

        Assert.True(diff.Caveats.FileVersionSourceDiffers);
        Assert.False(diff.Drifted);
        Assert.Empty(diff.Changed);

        // The canary: the same moved version between two sixes IS a difference, so the lines above
        // say something about the caveat and nothing about the edit.
        var six = five.Replace("\"schemaVersion\": 5", "\"schemaVersion\": 6", StringComparison.Ordinal);

        Assert.True(SnapshotJson.TryRead(six, out var olderSix, out failure), failure);
        Assert.True(SnapshotDiff.TryBetween(olderSix!, newer!, out var plain, out failure), failure);
        Assert.False(plain.Caveats.FileVersionSourceDiffers);
        Assert.True(plain.Drifted);
    }

    /// <summary>
    /// A file written before schema seven still reads, and its triggers are compared with a seven by kind
    /// and action alone, said once - logic report C01, the owner's decision of 2026-10-27. The two kept
    /// copies hold the same entries, so nothing may differ: without the caveat, every entry with a trigger
    /// would differ by format alone.
    /// </summary>
    [Fact]
    public void A_version_six_file_is_read_and_its_triggers_are_compared_by_kind_and_action()
    {
        Assert.True(SnapshotJson.TryRead(KeptText(KeptSix), out var older, out var failure), failure);
        Assert.True(SnapshotJson.TryRead(KeptText(Kept), out var newer, out failure), failure);
        Assert.True(SnapshotDiff.TryBetween(older!, newer!, out var diff, out failure), failure);

        Assert.True(diff.Caveats.TriggerDetailDiffers);
        Assert.False(diff.Drifted);
        Assert.Empty(diff.Changed);

        // The canary: the kind is still compared under the caveat, so one trigger turned into another
        // kind in the older file IS a difference - the lines above are about the narrowing, not about
        // triggers going unread.
        var moved = Once(KeptText(KeptSix), "\"kind\": \"CustomSystemStateChange\"", "\"kind\": \"DeviceArrival\"");

        Assert.True(SnapshotJson.TryRead(moved, out var olderMoved, out failure), failure);
        Assert.True(SnapshotDiff.TryBetween(olderMoved!, newer!, out var kinds, out failure), failure);
        Assert.True(kinds.Drifted);
        Assert.Equal("sppsvc", Assert.Single(kinds.Changed).ServiceName);
    }

    /// <summary>
    /// <b>Between two sevens a trigger's data is compared exactly</b>, the owner's decision of 2026-10-27:
    /// one RPC interface the manager writes in upper case, written in lower case, is a difference - a
    /// network endpoint trigger fires on an exact match. And the same edit in a file labelled six is not,
    /// because against an older file only the kind and the action are looked at.
    /// </summary>
    [Fact]
    public void A_change_of_case_in_trigger_data_is_a_difference_between_two_sevens_only()
    {
        Assert.True(SnapshotJson.TryRead(KeptText(Kept), out var newer, out var failure), failure);

        var lowered = Once(KeptText(Kept), "0497B57D-2E66-424F-A0C6-157CD5D41700", "0497b57d-2e66-424f-a0c6-157cd5d41700");

        Assert.True(SnapshotJson.TryRead(lowered, out var seven, out failure), failure);
        Assert.True(SnapshotDiff.TryBetween(seven!, newer!, out var exact, out failure), failure);

        var changed = Assert.Single(exact.Changed);

        Assert.Equal("Appinfo", changed.ServiceName);
        Assert.Equal("triggers", Assert.Single(changed.Differences).Field);
        Assert.False(exact.Caveats.TriggerDetailDiffers);

        var labelledSix = Once(lowered, "\"schemaVersion\": 7", "\"schemaVersion\": 6");

        Assert.True(SnapshotJson.TryRead(labelledSix, out var six, out failure), failure);
        Assert.True(SnapshotDiff.TryBetween(six!, newer!, out var narrowed, out failure), failure);
        Assert.True(narrowed.Caveats.TriggerDetailDiffers);
        Assert.Empty(narrowed.Changed);
    }

    /// <summary>
    /// <b>A seven whose trigger has no data list is refused</b> - review of PR #97. A seven always writes
    /// the list, so a file without it does not hold what its schema promises, and two such files would
    /// compare as equal on what neither holds. The same file labelled six reads, because a six never
    /// wrote the data - the rule belongs to the schema, which is why the second half is here.
    /// </summary>
    [Fact]
    public void A_version_seven_trigger_without_its_data_list_is_refused_and_a_six_is_not()
    {
        var tree = JsonNode.Parse(KeptText(Kept))!;
        var appinfo = tree["entries"]!.AsArray().Single(entry => (string?)entry!["serviceName"] == "Appinfo")!;

        appinfo["triggers"]![0]!.AsObject().Remove("data");

        var seven = tree.ToJsonString();

        Assert.False(SnapshotJson.TryRead(seven, out _, out var failure));
        Assert.Contains("'Appinfo') has trigger 1 without its whole data list", failure!, StringComparison.Ordinal);

        tree["metadata"]!["schemaVersion"] = 6;

        Assert.True(SnapshotJson.TryRead(tree.ToJsonString(), out _, out failure), failure);
    }

    /// <summary>The text with one occurrence of a piece replaced, failing when the piece is not there.</summary>
    private static string Once(string text, string piece, string replacement)
    {
        var at = text.IndexOf(piece, StringComparison.Ordinal);

        Assert.True(at >= 0, $"The kept copy holds no {piece}.");

        return string.Concat(text.AsSpan(0, at), replacement, text.AsSpan(at + piece.Length));
    }

    private static string MovedVersion(string text)
    {
        const string field = "\"fileVersion\": \"";
        var at = text.IndexOf(field, StringComparison.Ordinal);

        Assert.True(at >= 0, "The kept copy holds no file version to move.");

        return string.Concat(text.AsSpan(0, at + field.Length), "9.9.9.9-", text.AsSpan(at + field.Length));
    }

    // Without the second of the two names that differ only in case. The manager compares names
    // without case, so no machine produces that pair, and the reader refuses a file holding it
    // (Snapshot.BrokenEntries) - a kept copy with both would pin a state that cannot be read back.
    // Their settled order is asserted on its own in SnapshotTests.
    private static Snapshot Frozen() =>
        Snapshot.Of(
            MemoryPass.Fill(
                [.. Specimens.Inspected.Where(entry => entry.ServiceName != Specimens.CaseOnlyDifferenceSecond.ServiceName)],
                new FakeProcessMemoryReader()),
            note: null,
            new FakeClock()) with
        {
            Metadata = new SnapshotMetadata
            {
                SchemaVersion = 7,
                Machine = "GOLDEN",
                OperatingSystem = "Microsoft Windows NT 10.0.26100.0",
                TakenAt = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.FromHours(2)),
                TakenBy = @"EXAMPLE\operator",
                Elevated = true,
                Note = "before the change",
                Tool = "0.3.0",
                OperatingSystemVersion = "10.0.26100.4000",
                NamesLanguage = "en-US"
            }
        };

    /// <summary>
    /// The kept copy as its exact bytes, from inside the test assembly.
    ///
    /// Embedded rather than copied to the output folder, and read without looking for a byte
    /// order mark: a mark that crept into the kept copy is a difference in the file a person
    /// would get, so it has to fail here rather than be skipped over quietly. The line endings
    /// survive a checkout because .gitattributes marks the folder as not text.
    /// </summary>
    private static string KeptText(string name)
    {
        using var stream = typeof(SnapshotGoldenTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"{name} is not embedded in the test assembly.");
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: false);

        return reader.ReadToEnd();
    }

    private static string FirstDifference(string kept, string written)
    {
        var limit = Math.Min(kept.Length, written.Length);
        var at = 0;

        while (at < limit && kept[at] == written[at])
        {
            at++;
        }

        var line = 1 + kept[..at].Count(character => character == '\n');

        return $"First difference at character {at}, line {line}: kept [{Around(kept, at)}], written [{Around(written, at)}].";
    }

    // Twenty characters either side, with the line endings made visible, because a difference of
    // one carriage return is otherwise two lines that look the same.
    private static string Around(string text, int at)
    {
        var from = Math.Max(0, at - 20);
        var to = Math.Min(text.Length, at + 20);

        return text[from..to].Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
