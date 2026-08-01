using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Writing a file the user cares about, or leaving what was there alone.
///
/// `ADR-18`, and the property under test is the one that only matters when something goes
/// wrong: a target that is either the old content or the new one, never half of either. A
/// snapshot cut off in the middle looks like data and is not, and in an audit tool it goes
/// into a comparison as a false truth.
///
/// These touch the disk, in a directory of their own that they clean up. There is no seam
/// over the file system and there should not be one: the thing being checked here is what
/// the file system actually does with a rename.
/// </summary>
public sealed class AtomicFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("bws-atomic-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void The_content_arrives()
    {
        var path = Path.Combine(_directory, "snapshot.json");

        AtomicFile.Write(path, "hello");

        Assert.Equal("hello", File.ReadAllText(path));
    }

    [Fact]
    public void Writing_again_replaces_what_was_there()
    {
        var path = Path.Combine(_directory, "snapshot.json");

        AtomicFile.Write(path, "first");
        AtomicFile.Write(path, "second");

        Assert.Equal("second", File.ReadAllText(path));
    }

    [Fact]
    public void Nothing_temporary_is_left_behind()
    {
        // The other half of writing beside and moving over. A temporary file surviving the
        // write would accumulate next to somebody's snapshots under a name they cannot
        // explain, and would eventually be mistaken for one.
        var path = Path.Combine(_directory, "snapshot.json");

        AtomicFile.Write(path, "content");

        Assert.Single(Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void The_file_is_utf8_without_a_mark_and_with_unix_line_endings()
    {
        // Both are about diffing. A byte order mark makes the first line differ from every
        // other tool's idea of the first line, and carriage returns make a file written here
        // differ from the same file written anywhere else - neither being a fact about a
        // service.
        var path = Path.Combine(_directory, "snapshot.json");

        AtomicFile.Write(path, "a\nb\n");

        var bytes = File.ReadAllBytes(path);

        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    [Fact]
    public void A_directory_that_does_not_exist_is_an_error_and_not_an_invitation()
    {
        // ADR-18 says the tool does not invent directories. One that creates a tree from a
        // mistyped path has scattered somebody's files where they will not look for them.
        var path = Path.Combine(_directory, "nowhere", "snapshot.json");

        Assert.Throws<DirectoryNotFoundException>(() => AtomicFile.Write(path, "content"));
        Assert.False(Directory.Exists(Path.Combine(_directory, "nowhere")));
    }

    [Fact]
    public void A_file_that_cannot_be_read_is_moved_aside_rather_than_destroyed()
    {
        // Quarantine rather than deletion, because a corrupt snapshot is sometimes the only
        // remaining trace of what was there - and this tool exists to keep evidence.
        var path = Path.Combine(_directory, "snapshot.json");
        File.WriteAllText(path, "not a snapshot");

        var aside = AtomicFile.Quarantine(path, new FakeClock());

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(aside));
        Assert.Equal("not a snapshot", File.ReadAllText(aside));
    }

    [Fact]
    public void A_second_quarantine_in_the_same_second_does_not_overwrite_the_first()
    {
        // The fake clock does not move on its own, so both calls land on the same second -
        // which is the case a name built from a timestamp gets wrong, and the one case where
        // getting it wrong destroys the very thing quarantine exists to keep.
        var path = Path.Combine(_directory, "snapshot.json");
        var clock = new FakeClock();

        File.WriteAllText(path, "first");
        var one = AtomicFile.Quarantine(path, clock);

        File.WriteAllText(path, "second");
        var two = AtomicFile.Quarantine(path, clock);

        Assert.NotEqual(one, two);
        Assert.Equal("first", File.ReadAllText(one));
        Assert.Equal("second", File.ReadAllText(two));
    }
}
