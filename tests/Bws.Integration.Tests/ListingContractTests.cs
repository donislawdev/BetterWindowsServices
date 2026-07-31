using System.Diagnostics;
using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// Runs the real command line tool against the real service control manager and checks
/// the things the owner can check by hand: does it agree with the system, does it keep
/// its output channels apart, does it end with the right code.
///
/// Read-only. Nothing here changes any service, so it is safe on the machine we work on.
/// Anything that writes belongs on a throwaway virtual machine.
/// </summary>
public sealed class ListingContractTests
{
    [Fact]
    public void The_entry_count_agrees_with_the_service_control_manager()
    {
        var ours = Listing().Length;
        var theirs = ServiceControlCount("type= all");

        // sc.exe is the authority here. A difference is a bug on our side unless it is
        // deliberate and explained, and for the total there is nothing to explain away.
        Assert.Equal(theirs, ours);
    }

    [Fact]
    public void The_driver_count_agrees_with_the_service_control_manager()
    {
        var ours = Listing().Count(entry =>
            Text(entry, "entryType") is "KernelDriver" or "FileSystemDriver");

        Assert.Equal(ServiceControlCount("type= driver"), ours);
    }

    [Fact]
    public void We_show_more_services_than_sc_because_we_include_per_user_ones()
    {
        var ours = Listing().Count(entry =>
            Text(entry, "entryType") is not ("KernelDriver" or "FileSystemDriver"));

        var theirs = ServiceControlCount("type= service");

        // This is the one intended difference. sc.exe asks for plain Win32 services only,
        // so it leaves out per-user services. We show them, collapsed later in the GUI.
        // Asserting "greater" rather than an exact gap keeps this from breaking every time
        // somebody logs in or out.
        Assert.True(
            ours > theirs,
            $"Expected more entries than sc.exe reports for services. Ours: {ours}, sc.exe: {theirs}.");
    }

    [Fact]
    public void Data_goes_to_the_output_channel_and_nothing_else_does()
    {
        var run = Run("--json");

        Assert.Equal(0, run.ExitCode);

        // Valid JSON with nothing else mixed in. The moment a warning slips into this
        // channel, every pipe reading our output breaks.
        var parsed = JsonDocument.Parse(run.StandardOutput);
        Assert.True(parsed.RootElement.GetArrayLength() > 0);
    }

    [Fact]
    public void A_failed_run_writes_nothing_to_the_data_channel()
    {
        var run = Run("--no-such-option");

        Assert.Equal(2, run.ExitCode);
        Assert.Empty(run.StandardOutput.Trim());
        Assert.Contains("Unknown option", run.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_entry_carries_a_name_a_display_name_and_a_type()
    {
        foreach (var entry in Listing())
        {
            Assert.False(string.IsNullOrWhiteSpace(Text(entry, "serviceName")));
            Assert.False(string.IsNullOrWhiteSpace(Text(entry, "displayName")));
            Assert.NotEqual("Unknown", Text(entry, "entryType"));
        }
    }

    private static JsonElement[] Listing() =>
        JsonDocument.Parse(Run("--json").StandardOutput).RootElement.EnumerateArray().ToArray();

    private static string Text(JsonElement entry, string property) =>
        entry.GetProperty(property).GetString() ?? string.Empty;

    private static int ServiceControlCount(string typeFilter)
    {
        var run = Start("sc.exe", $"query {typeFilter} state= all");
        return run.StandardOutput
            .Split('\n')
            .Count(line => line.StartsWith("SERVICE_NAME:", StringComparison.Ordinal));
    }

    private static ProcessResult Run(params string[] arguments) =>
        Start(CommandLineToolPath(), string.Join(' ', arguments));

    private static ProcessResult Start(string executable, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException($"Could not start '{executable}'.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output, error);
    }

    private static string CommandLineToolPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found above the test output.");

        var executable = Directory
            .EnumerateFiles(Path.Combine(root, "src", "Bws.Cli", "bin"), "Bws.Cli.exe", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return executable ?? throw new InvalidOperationException(
            "Bws.Cli.exe was not found. Build the solution before running the integration tests.");
    }

    private readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
