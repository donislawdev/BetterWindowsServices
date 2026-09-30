namespace Bws.Cli.Tests;

/// <summary>
/// A folder named as the file a snapshot goes to - stability report D-6, measured 2026-09-29:
/// <c>bws snapshot create &lt;an existing folder&gt;</c> spent 1545 ms reading signatures and then
/// ended with "Access to the path is denied." and code 2, because the only refusal came from the
/// write itself.
/// </summary>
public sealed class SnapshotTargetTests
{
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
        var path = Path.Combine(Path.GetTempPath(), $"bws-target-{Guid.NewGuid():N}.json");

        Assert.True(SnapshotFiles.MayWrite(path, force: false, out var refusal));
        Assert.Null(refusal);
    }
}
