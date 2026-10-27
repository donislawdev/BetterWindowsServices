using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Two launch commands compared: the path without case, the arguments exactly - logic report R06, owner's
/// decision of 2026-10-26, which reverses half of backlog 249.
/// </summary>
public sealed class LaunchCommandComparisonTests
{
    [Fact]
    public void An_argument_whose_case_changed_is_drift()
    {
        // The report's own scenario: the program reads its arguments, and ACME may not be acme.
        var diff = Between(@"""C:\App\svc.exe"" --tenant=ACME", @"""C:\App\svc.exe"" --tenant=acme");

        Assert.True(diff.Drifted);
        Assert.Equal("binaryPath", Assert.Single(Assert.Single(diff.Changed).Differences).Field);
    }

    [Theory]
    [InlineData(@"""C:\App\svc.exe"" --tenant=ACME", @"""c:\app\SVC.EXE"" --tenant=ACME")]
    [InlineData(@"C:\WINDOWS\system32\svchost.exe -k netsvcs -p", @"C:\Windows\System32\svchost.exe -k netsvcs -p")]
    [InlineData(@"C:\Program Files\App\svc.exe -x", @"c:\program files\app\svc.exe -x")]
    [InlineData(@"C:\Windows\System32\spoolsv.exe", @"c:\windows\system32\SPOOLSV.EXE")]
    public void A_path_whose_case_changed_is_not_drift(string was, string now)
    {
        // The half of backlog 249 that stands: two spellings of one path run one file.
        Assert.False(Between(was, now).Drifted);
    }

    [Theory]
    [InlineData(@"""C:\App\svc.exe"" --tenant=ACME", 16)]
    [InlineData(@"  ""C:\App\svc.exe""", 18)]
    [InlineData(@"""C:\App\svc.exe --never-closed", 30)]
    [InlineData(@"C:\WINDOWS\system32\svchost.exe -k netsvcs", 31)]
    [InlineData(@"C:\Program Files\App\svc.exe -x", 28)]
    [InlineData(@"C:\Program Files\App\svc.exe", 28)]
    [InlineData(@"C:\WINDOWS\system32\svchost -k TSLicensing", 27)]
    [InlineData(@"\SystemRoot\System32\drivers\x.sys", 34)]
    [InlineData("", 0)]
    [InlineData("   ", 3)]
    public void The_file_ends_where_the_text_alone_says(string command, int end)
    {
        // Quotes settle it, then the first prefix ending in a name Windows runs, then the first space - the
        // last because nothing without the disk can say more, and the price is written beside the rule.
        Assert.Equal(end, SnapshotDiff.FileEnds(command));
    }

    private static SnapshotDiff Between(string was, string now)
    {
        Assert.True(SnapshotDiff.TryBetween(Taken(was), Taken(now), out var diff, out var failure), failure);

        return diff;
    }

    private static Snapshot Taken(string command) =>
        Snapshot.Of([Entries.Any with { BinaryPath = Reading<string>.Present(command) }], note: null, new FakeClock());
}
