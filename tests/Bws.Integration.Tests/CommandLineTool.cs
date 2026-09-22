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

        // BOTH CHANNELS AT ONCE, and reading them one after the other is a deadlock waiting for
        // a noisy run. A pipe holds a few kilobytes; once the error channel fills, the child
        // blocks writing to it, and a parent sitting in ReadToEnd on the data channel never gets
        // to the line that would drain it. Neither side can move, and a test that hangs reports
        // nothing at all - which this project has already paid for once, choosing a regex engine.
        //
        // Nothing here produces that much on the error channel today. What does is a run with
        // one line per refused entry, which is a shape the tool could easily grow.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    /// <summary>
    /// The tool these tests are about.
    ///
    /// <b>The build this test project was itself built in, not the newest one on disk.</b> Until
    /// 2026-08-03 this took whatever executable had the latest timestamp anywhere under
    /// <c>bin</c>, which meant a Release run could be measuring a Debug binary, or the other way
    /// round, purely because of what had been built last. Nothing would have said so - both
    /// answer every one of these tests the same way until the day one of them does not.
    ///
    /// This project has already paid once for a test run against a build that was not the one
    /// under test: <c>dotnet test --no-build</c> after a failed compile tests the previous binary
    /// and passes.
    /// </summary>
    private static string Path()
    {
        var root = SourceTree.Root();

        // ...\tests\Bws.Integration.Tests\bin\<configuration>\<framework>\ - the framework is the
        // same for every project here, so the configuration is what has to match.
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        var configuration = output.Parent?.Name
            ?? throw new InvalidOperationException($"Cannot tell which configuration '{output.FullName}' was built in.");

        var built = System.IO.Path.Combine(root, "src", "Bws.Cli", "bin", configuration);

        // bws.exe, not Bws.Cli.exe: the project is called after its place in the tree and the
        // file after what a user types - Bws.Cli.csproj sets the AssemblyName and says why.
        var executable = Directory.Exists(built)
            ? Directory
                .EnumerateFiles(built, "bws.exe", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        return executable ?? throw new InvalidOperationException(
            $"bws.exe was not found under '{built}'. These tests run the real tool, and it has " +
            $"to be the {configuration} build, because that is the one they were compiled beside. " +
            "Build the solution in this configuration before running them.");
    }
}
