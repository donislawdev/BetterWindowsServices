using System.Diagnostics;
using System.Text;

namespace Bws.Architecture.Tests;

/// <summary>
/// A script of the repository run as its own process of PowerShell 7, the way the release ritual runs it,
/// with what it said on both channels. One copy for the guards that ask packaging scripts for their
/// refusals - InstallerGuards asks build-msi.ps1 and FeedGuards asks build-feeds.ps1.
/// </summary>
internal static class PwshScript
{
    internal static (int Code, string Said) Run(string script, TimeSpan limit, params string[] arguments)
    {
        var path = Path.Combine(SourceTree.Root(), script.Replace('/', Path.DirectorySeparatorChar));
        return Start(new[] { "-NoProfile", "-NonInteractive", "-File", path }.Concat(arguments), limit, script);
    }

    /// <summary>
    /// The folder of the pwsh.exe that runs these scripts. The pwsh found on PATH can be an execution alias
    /// (on the owner's machine it is the one in WindowsApps), and an alias copies as nothing, so a guard that
    /// needs the file asks pwsh where it lives.
    /// </summary>
    internal static string Home()
    {
        var (code, said) = Start(["-NoProfile", "-NonInteractive", "-Command", "$PSHOME"], TimeSpan.FromSeconds(30), "pwsh -Command $PSHOME");
        Assert.True(code == 0 && Directory.Exists(said.Trim()), $"pwsh did not name the folder it runs from (exit {code}):{Environment.NewLine}{said}");
        return said.Trim();
    }

    private static (int Code, string Said) Start(IEnumerable<string> arguments, TimeSpan limit, string what)
    {
        var start = new ProcessStartInfo("pwsh") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // Both channels are read as they arrive, so the limit below is the one that holds: a read to the
        // end would wait for pwsh to close its output, and a pwsh that hangs never would - the test run
        // would hang with it (review of #68). Each channel has its own builder, and the events of one
        // channel come one after another.
        var output = new StringBuilder();
        var error = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, line) => output.AppendLine(line.Data);
        process.ErrorDataReceived += (_, line) => error.AppendLine(line.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(limit))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"{what} did not finish in {limit.TotalSeconds} s and was ended. It had said:{Environment.NewLine}{output}{error}");
        }

        // Without a limit once it has exited: this is what waits for the last line of both channels.
        process.WaitForExit();
        return (process.ExitCode, output.ToString() + error);
    }
}
