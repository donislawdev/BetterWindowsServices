using System.Text;
using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// A snapshot as a file: written only where it may go and only when this build can read it back, and read
/// only when every byte is text - logic report R11, S01 and S02, 2026-10-27.
///
/// On the disk, in a directory of their own, for the reason AtomicFileTests gives: what is checked is what
/// the file system does.
/// </summary>
public sealed class SnapshotFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("bws-snapshot-file-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string Target => Path.Combine(_directory, "snapshot.json");

    [Fact]
    public void A_snapshot_too_large_to_read_back_is_not_written()
    {
        // S02: the reader refuses a file over the ceiling, and until 2026-10-27 the writer wrote one, said
        // so, and the same build then called it "not a snapshot this build wrote".
        var text = Rendered();

        var refusal = SnapshotOnDisk.Write(Target, text, force: false, ceiling: Encoding.UTF8.GetByteCount(text) - 1);

        Assert.Equal(DiskFault.WouldBeTooBig, refusal?.Fault);
        Assert.False(File.Exists(Target));
    }

    [Fact]
    public void A_snapshot_exactly_at_the_ceiling_is_written()
    {
        // The reader refuses only what is larger, so the writer may not refuse what is equal.
        var text = Rendered();

        Assert.Null(SnapshotOnDisk.Write(Target, text, force: false, ceiling: Encoding.UTF8.GetByteCount(text)));
        Assert.True(SnapshotOnDisk.TryLoad(Target, out _, out _));
    }

    [Fact]
    public void The_ceiling_counts_the_bytes_written_and_not_the_characters()
    {
        // Forty characters, eighty bytes in UTF-8 - the length the reader compares.
        // A letter outside ASCII, two bytes in UTF-8, written as its number so this file stays ASCII.
        var refusal = SnapshotOnDisk.Write(Target, new string((char)0x105, 40), force: false, ceiling: 60);

        Assert.Equal(DiskFault.WouldBeTooBig, refusal?.Fault);
    }

    [Fact]
    public void Without_force_another_program_s_file_arriving_before_the_move_stays_and_is_named()
    {
        // R11: the question said nothing was there, and the move used to replace whatever arrived since.
        var refusal = SnapshotOnDisk.Write(
            Target, Rendered(), force: false, SnapshotOnDisk.Biggest, meanwhile: () => File.WriteAllText(Target, "theirs"));

        Assert.Equal(DiskFault.NotASnapshot, refusal?.Fault);
        Assert.Equal("theirs", File.ReadAllText(Target));
        Assert.Single(Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void Without_force_a_snapshot_arriving_before_the_move_stays()
    {
        // Two runs started together by a script, both naming the file from the same second.
        var first = Rendered("first");

        var refusal = SnapshotOnDisk.Write(
            Target, Rendered("second"), force: false, SnapshotOnDisk.Biggest, meanwhile: () => File.WriteAllText(Target, first));

        Assert.Equal(DiskFault.AlreadyThere, refusal?.Fault);
        Assert.Equal(first, File.ReadAllText(Target));
    }

    [Fact]
    public void Without_force_a_folder_arriving_before_the_move_stays_and_is_named()
    {
        // The move refuses a folder with the same code as a file (AtomicFileTests), and the person is told it
        // is a folder rather than that a snapshot is already there.
        var refusal = SnapshotOnDisk.Write(
            Target, Rendered(), force: false, SnapshotOnDisk.Biggest, meanwhile: () => Directory.CreateDirectory(Target));

        Assert.Equal(DiskFault.IsFolder, refusal?.Fault);
        Assert.True(Directory.Exists(Target));
        Assert.Single(Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void With_force_a_snapshot_arriving_before_the_move_is_replaced()
    {
        // Told that a snapshot standing there may go - the gap is left as it was, and said in SnapshotOnDisk.
        var second = Rendered("second");

        var refusal = SnapshotOnDisk.Write(
            Target, second, force: true, SnapshotOnDisk.Biggest, meanwhile: () => File.WriteAllText(Target, Rendered("first")));

        Assert.Null(refusal);
        Assert.Equal(second, File.ReadAllText(Target));
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    [InlineData("utf-32")]
    [InlineData("utf-32BE")]
    public void A_whole_snapshot_behind_any_mark_a_reader_knows_is_read(string encoding)
    {
        // Saved by something else in another encoding, with its mark - read as it was before S01.
        var chosen = Encoding.GetEncoding(encoding);
        File.WriteAllBytes(Target, [.. chosen.GetPreamble(), .. chosen.GetBytes(Rendered())]);

        Assert.True(SnapshotOnDisk.TryLoad(Target, out var snapshot, out var refusal), refusal?.Detail);
        Assert.Equal("Print Spooler", Assert.Single(snapshot.Entries).DisplayName);
    }

    [Fact]
    public void A_broken_byte_behind_a_utf8_mark_is_refused_rather_than_replaced()
    {
        // S01, measured 2026-10-26: the same broken byte was refused without a mark and came back as
        // U+FFFD inside the display name with one.
        var (before, after) = Split(Rendered());
        File.WriteAllBytes(Target, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(before), 0xC3, 0x28, .. Encoding.UTF8.GetBytes(after)]);

        Assert.False(SnapshotOnDisk.TryLoad(Target, out _, out var refusal));
        Assert.Equal(DiskFault.NotUtf8, refusal.Fault);
    }

    [Fact]
    public void A_lone_surrogate_behind_a_utf16_mark_is_refused_rather_than_replaced()
    {
        var (before, after) = Split(Rendered());
        var little = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
        File.WriteAllBytes(Target, [0xFF, 0xFE, .. little.GetBytes(before), 0x00, 0xD8, .. little.GetBytes(after)]);

        Assert.False(SnapshotOnDisk.TryLoad(Target, out _, out var refusal));
        Assert.Equal(DiskFault.NotUtf8, refusal.Fault);
    }

    [Fact]
    public void A_path_with_no_length_is_refused_rather_than_read()
    {
        // S02: the size is asked of the open file, and a device has none. Until 2026-10-27 this path was
        // read to its end and called not a snapshot - and a device that never ends would never answer.
        Assert.False(SnapshotOnDisk.TryLoad("NUL", out _, out var refusal));
        Assert.Equal(DiskFault.Unreadable, refusal.Fault);
    }

    /// <summary>A real snapshot of one entry, its note telling two of them apart.</summary>
    private static string Rendered(string? note = null) =>
        SnapshotJson.Render(Snapshot.Of([Entries.Any], note, new FakeClock()));

    /// <summary>The text cut in the middle of the display name, so a broken byte lands inside a value.</summary>
    private static (string Before, string After) Split(string text)
    {
        var at = text.IndexOf("Print Spooler", StringComparison.Ordinal) + "Print".Length;

        return (text[..at], text[at..]);
    }
}
