using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Cli.Tests;

/// <summary>
/// What may stand at the path a snapshot is written to.
///
/// <b>A folder</b> - stability report D-6, measured 2026-09-29: <c>bws snapshot create &lt;an existing
/// folder&gt;</c> spent 1545 ms reading signatures and then ended with "Access to the path is denied."
/// and code 2, because the only refusal came from the write itself.
///
/// <b>Anything that is not a snapshot</b> - security report S-8, owner's decision of 2026-10-06, a
/// change to the frozen meaning of --force. Until that day --force replaced any file, moving one it
/// could not read into quarantine first, so a mistyped path in a script running as administrator took
/// another program's file away from it and ended with code 0.
/// </summary>
public sealed class SnapshotTargetTests : IDisposable
{
    private readonly List<string> _made = [];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_folder_is_refused_in_words_about_the_folder_with_or_without_force(bool force)
    {
        var folder = Directory.CreateTempSubdirectory("bws-target-").FullName;

        try
        {
            Assert.False(SnapshotFiles.MayWrite(folder, force, out var refusal));
            Assert.Equal(Texts.Of("cli.snapshot.isFolder", folder), refusal);
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    [Fact]
    public void A_file_that_is_not_there_yet_may_be_written()
    {
        // The ordinary case, beside the refusal so a check that refused everything would show here.
        var path = Fresh();

        Assert.True(SnapshotFiles.MayWrite(path, force: false, out var refusal));
        Assert.Null(refusal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_file_that_is_not_a_snapshot_is_refused_with_or_without_force_and_left_where_it_is(bool force)
    {
        var path = Fresh();
        const string Theirs = "somebody else's settings, not a snapshot";

        File.WriteAllText(path, Theirs);

        Assert.False(SnapshotFiles.MayWrite(path, force, out var refusal));

        // The reason the reader gave, naming the file, and then what this did about it.
        Assert.StartsWith(Texts.Of("cli.diff.notASnapshot", path, string.Empty).Trim(), refusal, StringComparison.Ordinal);
        Assert.EndsWith(Texts.Of("cli.snapshot.notReplaced"), refusal, StringComparison.Ordinal);

        // A decision and nothing else: nothing was moved, renamed or written.
        Assert.Equal(Theirs, File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*"));
    }

    [Fact]
    public void A_file_too_big_to_be_a_snapshot_is_refused_by_its_size_before_it_is_read()
    {
        // MEASURED 2026-10-06 on the shipped 0.3.0: 1.5 GB of zeros with --force ended after 4045 ms
        // with code 1 and "Insufficient memory to continue the execution of the program." The size
        // is the reason given, which a file this large can only get if nothing read it first.
        var path = Fresh();

        using (var stream = File.Create(path))
        {
            stream.SetLength((64L * 1024 * 1024) + 1);
        }

        Assert.False(SnapshotFiles.MayWrite(path, force: true, out var refusal));
        Assert.StartsWith(Texts.Of("cli.diff.tooBig", path, 64), refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_snapshot_is_not_replaced_without_force()
    {
        var path = Snapshotted();

        Assert.False(SnapshotFiles.MayWrite(path, force: false, out var refusal));
        Assert.Equal(Texts.Of("cli.snapshot.fileExists", path), refusal);
    }

    [Fact]
    public void A_snapshot_is_replaced_with_force()
    {
        // The other side, and without it the refusals above pass on a build that refuses always.
        var path = Snapshotted();

        Assert.True(SnapshotFiles.MayWrite(path, force: true, out var refusal));
        Assert.Null(refusal);
    }

    /// <summary>A snapshot this build reads, of no entries, at a fresh path.</summary>
    private string Snapshotted()
    {
        var path = Fresh();

        File.WriteAllText(path, SnapshotJson.Render(Snapshot.Of([], note: null, new SystemClock())));

        return path;
    }

    private string Fresh()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bws-target-{Guid.NewGuid():N}.json");

        _made.Add(path);

        return path;
    }

    public void Dispose()
    {
        foreach (var path in _made.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}
