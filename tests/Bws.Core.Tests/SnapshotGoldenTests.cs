using System.Text;
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
    private const string Kept = "snapshot-schema-4.json";

    [Fact]
    public void The_file_is_written_byte_for_byte_as_the_kept_copy()
    {
        var written = SnapshotJson.Render(Frozen());
        var kept = KeptText();

        if (string.Equals(written, kept, StringComparison.Ordinal))
        {
            return;
        }

        var actual = Path.Combine(AppContext.BaseDirectory, "snapshot-schema-4.actual.json");
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
        var kept = KeptText();

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
            Snapshot.CurrentSchemaVersion == 4,
            $"The schema is now {Snapshot.CurrentSchemaVersion}. Keep a new copy named for it beside {Kept}.");
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
                SchemaVersion = 4,
                Machine = "GOLDEN",
                OperatingSystem = "Microsoft Windows NT 10.0.26100.0",
                TakenAt = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.FromHours(2)),
                TakenBy = @"EXAMPLE\operator",
                Elevated = true,
                Note = "before the change",
                Tool = "0.3.0"
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
    private static string KeptText()
    {
        using var stream = typeof(SnapshotGoldenTests).Assembly.GetManifestResourceStream(Kept)
            ?? throw new InvalidOperationException($"{Kept} is not embedded in the test assembly.");
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
