using System.Diagnostics;
using System.Text.Json;

namespace Bws.Integration.Tests;

internal readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Runs the real tool and the system tools it is checked against.
///
/// Arguments are handed over one by one rather than joined into a line. A query is one
/// argument containing spaces, and gluing the list together with spaces would quietly
/// turn it into several.
/// </summary>
internal static class CommandLineTool
{
    internal static ProcessResult Run(params string[] arguments) =>
        Start(Path(), arguments);

    internal static JsonElement[] Listing(params string[] arguments)
    {
        var run = Run(["list", .. arguments, "--json"]);

        Assert.Equal(0, run.ExitCode);

        return JsonDocument.Parse(run.StandardOutput).RootElement.EnumerateArray().ToArray();
    }

    internal static string Text(JsonElement entry, string property) =>
        entry.GetProperty(property).GetString() ?? string.Empty;

    internal static ProcessResult ServiceControl(params string[] arguments) =>
        Start("sc.exe", arguments);

    /// <summary>
    /// One line of PowerShell, for the questions sc.exe has no answer to.
    ///
    /// Signatures are the first of those. sc.exe says nothing about them, and the reason
    /// PowerShell is the right authority is not convenience: Get-AuthenticodeSignature walks
    /// both of the ways Windows signs a file, and walking only one of them is exactly the
    /// mistake this comparison exists to catch.
    /// </summary>
    internal static string PowerShell(string command)
    {
        var run = Start("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], forgetModulePath: true);

        // The exit code is checked, and that is not tidiness. Without it a PowerShell that
        // never ran hands back an empty string, and an empty string is a perfectly good value
        // to compare against - so the comparison fails saying the two verdicts differ, naming
        // our answer as the odd one out. Measured 2026-08-02: every one of twelve calls was
        // failing this way, and the message pointed at the wrong side of the comparison.
        Assert.True(run.ExitCode == 0,
            $"The second opinion never answered. powershell.exe exited {run.ExitCode} for `{command}`:\n{run.StandardError}");

        return run.StandardOutput.Trim();
    }

    internal static int ServiceControlCount(params string[] arguments) =>
        ServiceControl(arguments).StandardOutput
            .Split('\n')
            .Count(line => line.StartsWith("SERVICE_NAME:", StringComparison.Ordinal));

    private static ProcessResult Start(string executable, string[] arguments, bool forgetModulePath = false)
    {
        var startup = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startup.ArgumentList.Add(argument);
        }

        // Windows PowerShell inherits PSModulePath from whoever started it, and when that is
        // PowerShell 7 the inherited list puts PowerShell 7's own modules first. Windows
        // PowerShell then finds a Microsoft.PowerShell.Security built for the wrong runtime,
        // fails to load it, and reports Get-AuthenticodeSignature as a command that does not
        // exist. Dropping the variable makes it work its own default out.
        //
        // Measured 2026-08-02, and it is the reason this test class was red for a whole run:
        // the shell that launched dotnet test was pwsh, and nothing in the failure said so.
        // Interactive `& powershell.exe` does not show the problem, because PowerShell
        // rewrites the variable for a child it recognises - so this reproduces from a test
        // host and not from a prompt, which is the worst way round.
        if (forgetModulePath)
        {
            startup.Environment.Remove("PSModulePath");
        }

        using var process = Process.Start(startup)
            ?? throw new InvalidOperationException($"Could not start '{executable}'.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output, error);
    }

    private static string Path()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found above the test output.");

        var executable = Directory
            .EnumerateFiles(System.IO.Path.Combine(root, "src", "Bws.Cli", "bin"), "Bws.Cli.exe", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return executable ?? throw new InvalidOperationException(
            "Bws.Cli.exe was not found. Build the solution before running the integration tests.");
    }
}
