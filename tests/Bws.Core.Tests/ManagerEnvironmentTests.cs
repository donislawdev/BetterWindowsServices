namespace Bws.Core.Tests;

/// <summary>
/// The tests below replace variables in THIS process's environment for as long as each one runs,
/// so nothing else may run beside them.
/// </summary>
[CollectionDefinition("replacing this process's environment", DisableParallelization = true)]
public sealed class ReplacingTheEnvironment;

/// <summary>
/// That what a launch command names does not follow the environment of the process reading it -
/// security report S-3, 2026-10-06.
///
/// <b>The environment is replaced for real, because that is the claim.</b> An elevated process
/// inherits the environment of the account that started it, and that account can be written to by
/// a process with no administrator rights. Each test puts a value there that names a folder this
/// machine does not have, and asks whether the answer moved.
/// </summary>
[Collection("replacing this process's environment")]
public sealed class ManagerEnvironmentTests
{
    private static readonly string Windows = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows);

    [Fact]
    public void A_folder_the_system_injects_does_not_follow_this_process()
    {
        var expected = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Contoso", "x.exe");

        var expanded = Replaced("ProgramFiles", () => ManagerEnvironment.Expand(@"%ProgramFiles%\Contoso\x.exe", Windows));

        Assert.Equal(expected, expanded);
    }

    /// <summary>
    /// ProgramData is the one whose API was measured following the environment, so it comes from the
    /// profile list as written instead. Its value there carries %SystemDrive%, answered from the
    /// Windows directory.
    /// </summary>
    [Fact]
    public void ProgramData_does_not_follow_this_process_either()
    {
        var expanded = Replaced("ProgramData", () => ManagerEnvironment.Expand(@"%ProgramData%\Contoso\x.exe", Windows));

        Assert.StartsWith(Path.GetPathRoot(Windows)!, expanded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Elsewhere, expanded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%", expanded, StringComparison.Ordinal);
    }

    /// <summary>
    /// A variable only this process has is not the manager's, so it stays as written - which is
    /// what the manager does with a name it does not know.
    /// </summary>
    [Fact]
    public void A_variable_only_this_process_has_stays_as_written()
    {
        const string written = @"%BwsOnlyInThisProcess%\x.exe";

        var expanded = Replaced("BwsOnlyInThisProcess", () => ManagerEnvironment.Expand(written, Windows));

        Assert.Equal(written, expanded);
    }

    /// <summary>
    /// The machine's own values are read as written and expanded here: its TEMP is
    /// <c>%SystemRoot%\TEMP</c>, and SystemRoot replaced in this process must not reach into it.
    /// </summary>
    [Fact]
    public void A_machine_variable_naming_another_is_expanded_without_this_process()
    {
        var expanded = Replaced("SystemRoot", () => ManagerEnvironment.Expand(@"%TEMP%\x.log", Windows));

        Assert.DoesNotContain(Elsewhere, expanded, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Windows, expanded, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"%NoSuchVariableAnywhere%\x.exe", true)]
    [InlineData(@"%SystemRoot%\x.exe", false)]
    [InlineData(@"C:\odd%%name.exe", false)]
    [InlineData(@"C:\one%percent.exe", false)]
    public void An_unknown_name_is_told_apart_from_a_known_one_and_from_a_bare_percent_sign(string written, bool unresolved) =>
        Assert.Equal(unresolved, ManagerEnvironment.Unresolved(written, Windows));

    /// <summary>
    /// Nothing found under a name the table does not know is NOT READ, never missing - the manager
    /// may know the variable this code does not.
    /// </summary>
    [Fact]
    public void Nothing_found_under_an_unknown_name_is_not_read_rather_than_missing()
    {
        var resolved = BinaryPathResolver.Resolve(
            @"C:\Tools\%NoSuchVariableAnywhere%\svc.exe", "Any", isDriver: false, Windows,
            _ => Reading<bool>.Present(false), NetworkPaths.Follow);

        Assert.Equal(ReadOutcome.NotRead, resolved.OnDisk.Outcome);
    }

    /// <summary>
    /// The even claim: a file that IS there under the literal name is the answer, because a service
    /// can genuinely be launched from one.
    /// </summary>
    [Fact]
    public void A_file_found_under_the_literal_name_is_the_answer()
    {
        const string literal = @"C:\Tools\%NoSuchVariableAnywhere%\svc.exe";

        var resolved = BinaryPathResolver.Resolve(
            literal, "Any", isDriver: false, Windows,
            candidate => Reading<bool>.Present(candidate == literal), NetworkPaths.Follow);

        Assert.True(resolved.OnDisk.Value);
    }

    private const string Elsewhere = @"X:\Elsewhere";

    /// <summary>Runs one question with a variable of this process replaced, and puts it back.</summary>
    private static string Replaced(string name, Func<string> ask)
    {
        var before = System.Environment.GetEnvironmentVariable(name);

        System.Environment.SetEnvironmentVariable(name, Elsewhere);

        try
        {
            Assert.Equal(Elsewhere, System.Environment.GetEnvironmentVariable(name));

            return ask();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(name, before);
        }
    }
}
