using Bws.Core;

namespace Bws.Core.Tests;

/// <summary>
/// Working out which file a launch command runs.
///
/// Every shape below was read off a real machine on 2026-08-01 rather than imagined. The
/// counts matter as much as the shapes: 294 entries name a drive letter, 237 start with
/// \SystemRoot\, 216 are relative, 45 are quoted, 29 name nothing and 4 use \??\. Checking
/// the raw string for a file on disk reported 785 of 825 as missing, so anything built on
/// the obvious approach would have called almost the whole machine broken.
/// </summary>
public sealed class BinaryPathResolverTests
{
    private const string Windows = @"C:\WINDOWS";

    [Fact]
    public void Arguments_are_not_part_of_the_file_name()
    {
        // The svchost shape, and the most common one on any machine. Cutting nothing off
        // leaves a "file" that cannot exist, which is where the 785 came from.
        var resolved = Resolve(
            @"C:\WINDOWS\system32\svchost.exe -k netsvcs -p",
            onDisk: [@"C:\WINDOWS\system32\svchost.exe"]);

        Assert.Equal(@"C:\WINDOWS\system32\svchost.exe", resolved.File);
        Assert.True(resolved.OnDisk.Value);
    }

    [Fact]
    public void Quotes_say_where_the_file_name_ends()
    {
        var resolved = Resolve(
            @"""C:\Program Files\Proton\VPN\v4.4.1\ProtonVPN.WireGuardService.exe"" ""C:\conf"" ""udp""",
            onDisk: []);

        Assert.Equal(@"C:\Program Files\Proton\VPN\v4.4.1\ProtonVPN.WireGuardService.exe", resolved.File);

        // Nothing on disk, and the answer is still the path a person can go and check. This
        // is one of the five entries on the machine whose file is genuinely gone.
        Assert.False(resolved.OnDisk.ValueOr(false));
    }

    [Fact]
    public void An_unquoted_path_with_spaces_is_resolved_by_trying_the_prefixes()
    {
        // The case that broke the obvious approach while this was being written: cutting at
        // the first space gave "C:\Program" and reported a file that is there as missing.
        // Two entries on the machine have this shape.
        const string command = @"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher Core\UpcElevationService.exe";

        var resolved = Resolve(command, onDisk: [command]);

        Assert.Equal(command, resolved.File);
        Assert.True(resolved.OnDisk.Value);
    }

    [Fact]
    public void The_shortest_prefix_that_is_on_disk_is_the_one_reported()
    {
        // The ordering only decides anything when two prefixes both exist, which no entry
        // on the machine does, so this case is built rather than captured. It is pinned
        // anyway, because the order is a choice and not an accident: taking the shortest is
        // what makes an unquoted path with spaces a hazard worth naming later, since the
        // shorter name is the one a planted file would take.
        //
        // NOT MEASURED: whether the manager resolves an unquoted command in exactly this
        // order. Finding out would mean planting a file and starting a service, which is not
        // something to do on a machine somebody works on.
        var resolved = Resolve(
            @"C:\Tools\Agent Service\agent.exe --run",
            onDisk: [@"C:\Tools\Agent", @"C:\Tools\Agent Service\agent.exe"]);

        Assert.Equal(@"C:\Tools\Agent", resolved.File);
    }

    [Fact]
    public void When_nothing_is_on_disk_the_answer_is_the_part_that_looks_like_a_file()
    {
        // Reporting a truncated path would read as a different mistake than the one being
        // reported. The person has to be able to check the answer.
        var resolved = Resolve(@"C:\Program Files\Gone\Gone.exe --flag", onDisk: []);

        Assert.Equal(@"C:\Program Files\Gone\Gone.exe", resolved.File);
        Assert.False(resolved.OnDisk.ValueOr(false));
    }

    [Theory]
    [InlineData(@"\SystemRoot\system32\drivers\AppvStrm.sys", @"C:\WINDOWS\system32\drivers\AppvStrm.sys")]
    [InlineData(@"system32\drivers\Acx01000.sys", @"C:\WINDOWS\system32\drivers\Acx01000.sys")]
    [InlineData(@"\??\C:\Program Files\Proton\x.sys", @"C:\Program Files\Proton\x.sys")]
    [InlineData(@"C:\WINDOWS\System32\spoolsv.exe", @"C:\WINDOWS\System32\spoolsv.exe")]
    public void Every_shape_the_manager_returns_lands_on_the_same_kind_of_path(string raw, string expected)
    {
        Assert.Equal(expected, Resolve(raw, onDisk: [expected]).File);
    }

    [Fact]
    public void A_driver_naming_nothing_runs_the_file_the_manager_assumes_for_it()
    {
        // 29 entries of 825, every one a driver. Absent path, known file - a pairing that
        // catches anybody who read "no path" as "no file".
        var resolved = Resolve(null, onDisk: [@"C:\WINDOWS\System32\drivers\Beep.sys"], serviceName: "Beep", isDriver: true);

        Assert.Equal(@"C:\WINDOWS\System32\drivers\Beep.sys", resolved.File);
        Assert.True(resolved.OnDisk.Value);
    }

    [Fact]
    public void Anything_else_naming_nothing_leaves_nothing_to_resolve()
    {
        // Not a made-up path reported as missing. "There is nothing to look for" and "what
        // I looked for is not there" are different answers and this tool never merges them.
        var resolved = Resolve(null, onDisk: [], serviceName: "PathLess", isDriver: false);

        Assert.Null(resolved.File);
        Assert.False(resolved.OnDisk.ValueOr(false));
    }

    [Fact]
    public void A_share_is_left_alone_rather_than_joined_to_the_windows_directory()
    {
        const string command = @"\\server\share\agent.exe";

        Assert.Equal(
            command,
            Resolve(command, onDisk: [command], networkPaths: NetworkPaths.Follow).File);
    }

    [Fact]
    public void A_path_on_another_machine_is_not_asked_about_at_all()
    {
        // The guard the whole slice exists for, and it asserts the ABSENCE of a call rather
        // than the value of an answer. That is deliberate: the defect being prevented is not
        // a wrong answer, it is a question - a File.Exists over SMB blocks for 21 053 ms on
        // an unreachable host and authenticates as whoever ran this. A test on the returned
        // value would pass on a build that asked and then discarded what it heard.
        var resolved = BinaryPathResolver.Resolve(
            @"\\server\share\agent.exe --run",
            "Any",
            isDriver: false,
            Windows,
            _ => throw new InvalidOperationException("The disk was asked about a path on another machine."),
            NetworkPaths.Skip);

        // Still named, because which file a command runs is worked out from its text. Only
        // the disk question needed the disk.
        Assert.Equal(@"\\server\share\agent.exe", resolved.File);

        // Not read, and emphatically not "missing". An audit tool reporting a file as absent
        // when it never looked is the exact failure rule 8 is about.
        Assert.Equal(ReadOutcome.NotRead, resolved.OnDisk.Outcome);
    }

    [Theory]
    // Two backslashes and local, both of them. The device namespace is a way of naming
    // things on this machine that skips path parsing, and treating it as remote would stop
    // the tool answering about files that are right here.
    [InlineData(@"\\?\C:\Tools\agent.exe", false)]
    [InlineData(@"\\.\PhysicalDrive0", false)]
    // A share, in both spellings.
    [InlineData(@"\\server\share\agent.exe", true)]
    [InlineData(@"\\?\UNC\server\share\agent.exe", true)]
    // Ordinary local paths.
    [InlineData(@"C:\WINDOWS\System32\spoolsv.exe", false)]
    [InlineData(@"\SystemRoot\system32\drivers\x.sys", false)]
    [InlineData("", false)]
    public void What_counts_as_leaving_this_machine(string path, bool leaves)
    {
        Assert.Equal(leaves, NetworkPath.LeavesThisMachine(path));
    }

    [Fact]
    public void Asked_to_follow_the_network_it_answers_like_any_other_path()
    {
        // The other half, because a switch that turns nothing on is the same silence from
        // the opposite side. Without this, removing the Follow branch entirely would leave
        // the test above green.
        const string command = @"\\server\share\agent.exe";

        var resolved = Resolve(command, onDisk: [command], networkPaths: NetworkPaths.Follow);

        Assert.Equal(command, resolved.File);
        Assert.Equal(ReadOutcome.Present, resolved.OnDisk.Outcome);
        Assert.True(resolved.OnDisk.Value);
    }

    [Fact]
    public void The_second_pass_does_not_open_a_file_on_another_machine_either()
    {
        // The more expensive half of the same rule, and the one with more to lose. The
        // listing asks the disk a yes-or-no question; this reads the whole file to hash it
        // and verify its signature, which over a share is a file transfer - on every listing
        // that asks for signatures and on every snapshot.
        //
        // The real inspector rather than a double, because what is being asserted is that it
        // does not go out, and a double cannot fail to go out. A build that lost this would
        // not merely fail here, it would take about twenty one seconds to do so - which is
        // itself the measurement being defended.
        var inspector = new WindowsBinaryInspector();
        const string onAShare = @"\\bws-test-no-such-host\share\agent.exe";

        Assert.Equal(ReadOutcome.NotRead, inspector.ReadSignature(onAShare).Outcome);
        Assert.Equal(ReadOutcome.NotRead, inspector.ReadFileVersion(onAShare).Outcome);
        Assert.Equal(ReadOutcome.NotRead, inspector.ReadHash(onAShare).Outcome);
    }

    private static BinaryPathResolver.ResolvedBinary Resolve(
        string? command,
        string[] onDisk,
        string serviceName = "Any",
        bool isDriver = false,
        NetworkPaths networkPaths = NetworkPaths.Follow)
    {
        var present = new HashSet<string>(onDisk, StringComparer.OrdinalIgnoreCase);

        // Follow by default in this helper and Skip by default in the product, which looks
        // backwards and is not. Every case above is about resolving a path, and answering
        // them under the production default would make each one silently a test about the
        // network rule instead. The two tests that are about that rule name it explicitly.
        return BinaryPathResolver.Resolve(command, serviceName, isDriver, Windows, present.Contains, networkPaths);
    }
}
