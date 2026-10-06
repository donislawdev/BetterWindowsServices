namespace Bws.Core.Tests;

/// <summary>
/// Which paths are shaped like a file on a disk - the rest of security report S-4, 2026-10-06. The
/// list of what IS a file is closed, so every row below that says false is a shape the list leaves
/// out on purpose, and every row that says true is one it names.
/// </summary>
public sealed class FileShapeTests
{
    [Theory]
    [InlineData(@"C:\Windows\System32\svchost.exe")]
    [InlineData(@"\\?\C:\Windows\System32\svchost.exe")]
    [InlineData(@"\\.\C:\Windows\System32\svchost.exe")]
    [InlineData(@"\\?\Volume{0b0b0b0b-0000-0000-0000-000000000000}\agent.exe")]
    [InlineData(@"\\server\share\agent.exe")]
    [InlineData(@"\\?\UNC\server\share\agent.exe")]
    [InlineData("//server/share/agent.exe")]
    public void A_drive_a_volume_and_a_share_name_a_file(string path) =>
        Assert.True(FileShape.NamesAFile(path));

    [Theory]
    [InlineData(@"\\.\pipe\agent")]
    [InlineData(@"\\?\pipe\agent")]
    [InlineData("//./pipe/agent")]
    [InlineData(@"\\server\pipe\agent")]
    [InlineData(@"\\server\PIPE\agent")]
    [InlineData(@"\\?\UNC\server\pipe\agent")]
    [InlineData(@"\\.\mailslot\agent")]
    [InlineData(@"\\*\mailslot\agent")]
    [InlineData(@"\\.\UNC\server\share\agent.exe")]
    [InlineData(@"\\?\GLOBALROOT\Device\Mup\server\share\agent.exe")]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"\\.\COM1")]
    [InlineData(@"\\server")]
    [InlineData("")]
    public void A_pipe_a_mailslot_a_device_and_an_object_name_do_not(string path) =>
        Assert.False(FileShape.NamesAFile(path));
}
