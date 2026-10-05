namespace Bws.Core.Tests;

/// <summary>
/// Which file a launch command names, for the shapes only a driver, a bare name or a refusal
/// produces - stability report R-1, R-2, R-3 and R-7, built 2026-10-05.
///
/// A file of its own rather than more of BinaryPathResolverTests, whose helper already takes five
/// arguments - the ceiling for a test signature - and whose cases are all about a disk that answers
/// yes or no. Every case here needs a disk that can also REFUSE, which is the one thing that file
/// never had to say.
///
/// The counts that make these worth having were taken that day on the owner's machine with
/// tools/scm-probe/scm-probe.ps1: one driver with an unquoted space in its path, zero services with a
/// bare name, zero paths beginning with two backslashes. Most cases are therefore built rather than
/// captured, and say so.
/// </summary>
public sealed class LaunchPathReadingTests
{
    private const string Windows = @"C:\WINDOWS";

    private static readonly Func<string, Reading<bool>> NeverAsked =
        _ => throw new InvalidOperationException("The disk was asked about a path nobody may follow.");

    [Fact]
    public void A_driver_path_with_a_space_is_one_file_name_and_a_planted_program_is_not_it()
    {
        // The shape of the one driver on the owner's machine with an unquoted space, vendor renamed.
        // Split like a command line, the planted file is the first prefix on disk and its signature
        // and hash would have described a file the kernel never loads.
        //
        // BOTH PLANTED NAMES ARE NEEDED, and the first version of this test had only the second. A
        // driver gets no .exe appended either, so C:\Program.exe alone was never reachable - the
        // mutation registry put the command line rules back for drivers and this stayed green,
        // because two rules produced the same answer. C:\Program, as written, is reachable only by
        // splitting at the space, which is the one thing this test is about.
        var resolved = Resolve(
            @"\??\C:\Program Files\Contoso\Callout\callout.sys",
            new Disk(Present: [@"C:\Program", @"C:\Program.exe", @"C:\Program Files\Contoso\Callout\callout.sys"]),
            isDriver: true);

        Assert.Equal(@"C:\Program Files\Contoso\Callout\callout.sys", resolved.File);
        Assert.True(resolved.OnDisk.Value);
    }

    [Fact]
    public void A_driver_gets_no_exe_appended_to_a_name_without_an_extension()
    {
        // CreateProcess appends .exe, the kernel does not - so a file sitting there with .exe on the
        // end is not the driver, and "missing" is the true answer about the name it was given.
        var resolved = Resolve(
            @"\SystemRoot\System32\drivers\filter",
            new Disk(Present: [@"C:\WINDOWS\System32\drivers\filter.exe"]),
            isDriver: true);

        Assert.Equal(@"C:\WINDOWS\System32\drivers\filter", resolved.File);
        Assert.Equal(ReadOutcome.Present, resolved.OnDisk.Outcome);
        Assert.False(resolved.OnDisk.Value);
    }

    [Fact]
    public void A_driver_named_in_the_kernel_namespace_is_not_looked_for_on_drive_c()
    {
        // Until 2026-10-05 this became C:\Device\..., a confident "missing". GLOBALROOT is how
        // Win32 spells the same name, and it is not followed without being asked, because it can
        // lead to a share as easily as to a disk.
        var resolved = BinaryPathResolver.Resolve(
            @"\Device\HarddiskVolume3\Drivers\x.sys", "Any", isDriver: true, Windows, NeverAsked, NetworkPaths.Skip);

        Assert.Equal(@"\\?\GLOBALROOT\Device\HarddiskVolume3\Drivers\x.sys", resolved.File);
        Assert.Equal(ReadOutcome.NotRead, resolved.OnDisk.Outcome);
    }

    [Fact]
    public void A_service_rooted_on_no_drive_is_still_looked_for_on_the_windows_drive()
    {
        // The other half of the case above: CreateProcess roots such a path on the caller's drive.
        var resolved = Resolve(@"\Device\agent.exe", new Disk(Present: [@"C:\Device\agent.exe"]));

        Assert.Equal(@"C:\Device\agent.exe", resolved.File);
        Assert.True(resolved.OnDisk.Value);
    }

    [Theory]
    // Until 2026-10-05 both were cut loose from the kernel prefix and joined to C:\WINDOWS - the
    // first as a relative UNC\..., reported missing about a file on a share nobody looked at.
    [InlineData(@"\??\UNC\server\share\x.sys", @"\\server\share\x.sys")]
    [InlineData(@"\??\GLOBALROOT\Device\Mup\server\share\x.sys", @"\\?\GLOBALROOT\Device\Mup\server\share\x.sys")]
    public void The_kernel_prefix_keeps_a_share_a_share(string command, string expected)
    {
        var resolved = BinaryPathResolver.Resolve(command, "Any", isDriver: true, Windows, NeverAsked, NetworkPaths.Skip);

        Assert.Equal(expected, resolved.File);
        Assert.Equal(ReadOutcome.NotRead, resolved.OnDisk.Outcome);
    }

    [Fact]
    public void A_bare_name_is_found_where_CreateProcess_looks_first()
    {
        // System32 before the Windows directory - the order in the CreateProcess documentation,
        // read 2026-10-05. Joined to the Windows directory, which is what stood here until then,
        // this named the second file.
        var resolved = Resolve(
            "agent.exe -run",
            new Disk(Present: [@"C:\WINDOWS\agent.exe", @"C:\WINDOWS\System32\agent.exe"]));

        Assert.Equal(@"C:\WINDOWS\System32\agent.exe", resolved.File);
    }

    [Fact]
    public void A_bare_name_only_in_the_windows_directory_is_still_found_there()
    {
        var resolved = Resolve("agent -run", new Disk(Present: [@"C:\WINDOWS\agent.exe"]));

        Assert.Equal(@"C:\WINDOWS\agent.exe", resolved.File);
        Assert.True(resolved.OnDisk.Value);
    }

    [Fact]
    public void A_refused_file_is_not_called_missing()
    {
        // R-1. Until 2026-10-05 the existence question could only answer yes or no, so a file this
        // token may not look at came back as no.
        var resolved = Resolve(
            @"""C:\Program Files\Shield\agent.exe"" --protect",
            new Disk(Refused: [@"C:\Program Files\Shield\agent.exe"]));

        Assert.Equal(@"C:\Program Files\Shield\agent.exe", resolved.File);
        Assert.Equal(ReadOutcome.Denied, resolved.OnDisk.Outcome);
        Assert.Equal(AccessDenied.Code, resolved.OnDisk.ErrorCode);
    }

    [Fact]
    public void A_refused_shorter_name_ends_the_walk_before_a_longer_one_that_is_there()
    {
        // A refusal comes back only for a name that exists (measured, see FileOnDisk), so it is the
        // file CreateProcess reaches first. Walking past it would name a file Windows does not run.
        var resolved = Resolve(
            @"C:\Tools\Agent Service\agent.exe --run",
            new Disk(Present: [@"C:\Tools\Agent Service\agent.exe"], Refused: [@"C:\Tools\Agent"]));

        Assert.Equal(@"C:\Tools\Agent", resolved.File);
        Assert.Equal(ReadOutcome.Denied, resolved.OnDisk.Outcome);
    }

    [Fact]
    public void A_recognizer_driver_naming_no_file_runs_the_one_assumed_for_a_driver()
    {
        // R-7. Unknown as a kind, and until 2026-10-05 therefore not a driver either - so an empty
        // path named nothing at all instead of the default under System32\drivers.
        var enumerated = new EnumeratedEntry(
            "Recognizer", "Recognizer", EntryType.Unknown, PerUserRole.None, EntryStatus.Running, 0, AcceptsStop: false,
            RecognizerDriver: true);

        var configuration = ScmConfiguration.Refused(0) with { BinaryPath = Reading<string>.Absent() };

        Assert.Equal(
            @"C:\WINDOWS\System32\drivers\Recognizer.sys",
            configuration.WithBinary(enumerated, Windows, NetworkPaths.Skip).BinaryFile.Value);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(15)]
    [InlineData(123)]
    [InlineData(161)]
    public void Five_codes_mean_the_file_is_not_there(int code)
    {
        var answer = FileOnDisk.FromFailure(unchecked((int)0x80070000) | code, "said");

        Assert.Equal(ReadOutcome.Present, answer.Outcome);
        Assert.False(answer.Value);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(21)]
    [InlineData(32)]
    public void Every_other_code_is_a_refusal_carrying_its_number(int code)
    {
        // The direction that fails safe: a code nobody here has met says "not compared", never
        // a confident "missing".
        var answer = FileOnDisk.FromFailure(unchecked((int)0x80070000) | code, "said");

        Assert.Equal(ReadOutcome.Denied, answer.Outcome);
        Assert.Equal(code, answer.ErrorCode);
    }

    [Fact]
    public void A_file_a_directory_and_nothing_answer_as_the_disk_does()
    {
        var file = Path.Combine(Path.GetTempPath(), "bws-on-disk-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllText(file, "x");

        try
        {
            Assert.True(FileOnDisk.Ask(file).Value);

            // A directory is not the file a command runs - File.Exists said the same.
            Assert.Equal(ReadOutcome.Present, FileOnDisk.Ask(Path.GetTempPath()).Outcome);
            Assert.False(FileOnDisk.Ask(Path.GetTempPath()).Value);

            Assert.Equal(ReadOutcome.Present, FileOnDisk.Ask(file + ".absent").Outcome);
            Assert.False(FileOnDisk.Ask(file + ".absent").Value);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void The_inspector_answers_a_refused_file_with_a_refusal_and_a_missing_one_with_absence()
    {
        // The signature, version and hash all start here. Until 2026-10-05 all three said "absent"
        // about a file this token may not read.
        var refused = WindowsBinaryInspector.Unreachable<string>(Reading<bool>.Denied(AccessDenied.Code, "said"));

        Assert.Equal(ReadOutcome.Denied, refused?.Outcome);
        Assert.Equal(AccessDenied.Code, refused?.ErrorCode);
        Assert.Equal(ReadOutcome.Absent, WindowsBinaryInspector.Unreachable<string>(Reading<bool>.Present(false))?.Outcome);
        Assert.Null(WindowsBinaryInspector.Unreachable<string>(Reading<bool>.Present(true)));
    }

    private static BinaryPathResolver.ResolvedBinary Resolve(string command, Disk disk, bool isDriver = false) =>
        BinaryPathResolver.Resolve(command, "Any", isDriver, Windows, disk.Ask, NetworkPaths.Follow);

    /// <summary>A disk that holds some names, refuses others, and has never heard of the rest.</summary>
    private sealed record Disk(string[]? Present = null, string[]? Refused = null)
    {
        internal Reading<bool> Ask(string path) =>
            Refused?.Contains(path, StringComparer.OrdinalIgnoreCase) == true
                ? Reading<bool>.Denied(AccessDenied.Code, "refused")
                : Reading<bool>.Present(Present?.Contains(path, StringComparer.OrdinalIgnoreCase) == true);
    }
}
