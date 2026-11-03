using System.Diagnostics;

namespace Bws.Architecture.Tests;

/// <summary>
/// The reads packaging/sign-release.ps1 makes of the release after the card has signed, and the mode
/// that finishes its last step alone.
///
/// <b>Why this class exists.</b> The signing of 0.5.0 (2026-11-03) stopped in step 8 on ONE failed read
/// of the release, with phase C finished and the draft complete, and all the script could say was
/// "cannot read the release" - the read was a single call with its error output thrown away. Every read
/// of the release now goes through Read-Release, which asks again and keeps what gh said, and
/// -CheckDraft runs step 8 over the work folder without signing again.
///
/// <b>The read is asked of the script's own function, not of a copy.</b> The ritual cannot run in a
/// test - it needs a tag, the card and the real repository - so Read-Release is cut out of the script
/// with PowerShell's own parser and run against a stand-in gh, a batch file first on the path that
/// fails as many times as it is told and counts how often it was asked.
/// </summary>
public sealed class SigningRitualGuards
{
    private const string Script = "packaging/sign-release.ps1";

    [Fact]
    public void A_read_of_the_release_that_fails_twice_is_asked_again_and_answers()
    {
        var said = ReadRelease(fails: 2, empty: false);
        Assert.True(said.Contains("READ isDraft=True", StringComparison.Ordinal) && said.Contains("CALLS 3", StringComparison.Ordinal),
            $"Two failed reads and a third that answers must come back as the answer, after three calls. Read-Release said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void A_read_that_keeps_failing_stops_with_what_gh_said()
    {
        var said = ReadRelease(fails: 99, empty: false);
        Assert.True(said.Contains("THREW", StringComparison.Ordinal) && said.Contains("CALLS 3", StringComparison.Ordinal),
            $"A read that never answers stops the script after three calls. Read-Release said:{Environment.NewLine}{said}");
        Assert.True(said.Contains("HTTP 502: Bad Gateway", StringComparison.Ordinal),
            $"The refusal carries gh's own words - without them nobody can tell a network fault from a missing release. Read-Release said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void A_read_that_prints_nothing_is_not_an_answer()
    {
        var said = ReadRelease(fails: 0, empty: true);
        Assert.True(said.Contains("THREW", StringComparison.Ordinal) && said.Contains("printed no document", StringComparison.Ordinal),
            $"gh exiting 0 with nothing on its output is no release - ConvertFrom-Json makes an empty string into $null without a word. Read-Release said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void Every_read_of_the_release_goes_through_the_one_that_asks_again()
    {
        var script = Text(Script);
        var reads = script.Split("gh release view").Length - 1;
        Assert.True(reads == 1, $"sign-release.ps1 calls gh release view {reads} times. Once, inside Read-Release - a read beside it fails on the first error and says nothing of why.");
        foreach (var line in new[] { "$draft = Read-Release 'assets'", "$view = Read-Release 'assets,isDraft'", "$state = Read-Release 'isDraft'" })
        {
            Assert.True(script.Contains(line, StringComparison.Ordinal), $"sign-release.ps1 has no line saying {line}");
        }
    }

    [Fact]
    public void The_draft_check_alone_comes_before_the_work_folder_is_cleared_and_the_card_is_reached()
    {
        var script = Text(Script);
        var alone = script.IndexOf("if ($CheckDraft) {", StringComparison.Ordinal);
        var cleared = script.IndexOf("Remove-Item -LiteralPath $workspace", StringComparison.Ordinal);
        var card = script.IndexOf("$certificate = Get-CodeSigningCertificates", StringComparison.Ordinal);
        Assert.True(alone > 0 && cleared > alone && card > alone,
            $"-CheckDraft is decided at {alone}, the work folder is cleared at {cleared} and the card reached at {card}. It has to be decided first - it reads the folder a signing run left, and signs nothing.");

        var branch = script[alone..cleared];
        Assert.True(branch.Contains("Confirm-Draft $attested", StringComparison.Ordinal) && branch.Contains("return", StringComparison.Ordinal),
            "The -CheckDraft branch no longer runs step 8 and returns - without the return it would go on to clear the folder and sign.");
        Assert.True(script.TrimEnd().EndsWith("Confirm-Draft $attested", StringComparison.Ordinal),
            "The ritual no longer ends with step 8 - the same function -CheckDraft runs, so the two cannot drift apart.");
    }

    /// <summary>
    /// Read-Release cut out of the script and run once against a stand-in gh, with what it printed:
    /// READ and the answer, or THREW and the message, then CALLS and how often gh was asked.
    /// </summary>
    private static string ReadRelease(int fails, bool empty)
    {
        var folder = Directory.CreateTempSubdirectory("bws-signing-read-");
        try
        {
            WriteStandIn(folder.FullName);
            WriteDriver(folder.FullName);
            var said = RunDriver(folder.FullName, fails, empty);
            Assert.False(said.Contains("NO READ-RELEASE", StringComparison.Ordinal), $"{Script} has no function called Read-Release any more.");
            return said;
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A gh that fails FAKE_GH_FAILS times with a line on its error channel and then answers, or exits 0
    /// with nothing printed when FAKE_GH_EMPTY is 1 - counting every call in a file beside itself.
    /// </summary>
    private static void WriteStandIn(string folder) =>
        File.WriteAllText(Path.Combine(folder, "gh.cmd"), string.Join("\r\n",
            "@echo off",
            "set n=0",
            "if exist \"%~dp0count\" set /p n=<\"%~dp0count\"",
            "set /a n=n+1",
            ">\"%~dp0count\" echo %n%",
            "if \"%FAKE_GH_EMPTY%\"==\"1\" exit /b 0",
            "if %n% LEQ %FAKE_GH_FAILS% (",
            "  echo HTTP 502: Bad Gateway 1>&2",
            "  exit /b 1",
            ")",
            "echo {\"isDraft\":true}",
            ""));

    /// <summary>
    /// The script that finds Read-Release in sign-release.ps1 with PowerShell's parser, defines it, puts the
    /// stand-in first on the path and asks it once, under the same Stop and StrictMode the ritual runs with.
    /// </summary>
    private static void WriteDriver(string folder)
    {
        var driver = Path.Combine(folder, "driver.ps1");
        File.WriteAllText(driver, string.Join("\n",
            "param([string] $Script, [string] $Fake)",
            "$ErrorActionPreference = 'Stop'",
            "Set-StrictMode -Version Latest",
            "$tokens = $null",
            "$errors = $null",
            "$tree = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$tokens, [ref]$errors)",
            "$read = $tree.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Read-Release' }, $true)",
            "if (-not $read) { 'NO READ-RELEASE'; exit 2 }",
            ". ([scriptblock]::Create($read.Extent.Text))",
            "$env:PATH = $Fake + [IO.Path]::PathSeparator + $env:PATH",
            "$Tag = 'v9.9.9'",
            "$repo = 'nobody/nothing'",
            "try { $view = Read-Release 'isDraft' 'in a test' -PauseSeconds 0; \"READ isDraft=$($view.isDraft)\" }",
            "catch { \"THREW $($_.Exception.Message)\" }",
            "\"CALLS $((Get-Content -LiteralPath (Join-Path $Fake 'count') -Raw).Trim())\"",
            ""));
    }

    /// <summary>The driver WriteDriver left in the folder, as its own process, with what it said on both channels.</summary>
    private static string RunDriver(string folder, int fails, bool empty)
    {
        var start = new ProcessStartInfo("pwsh") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(folder, "driver.ps1"), "-Script", Path.Combine(SourceTree.Root(), Script.Replace('/', Path.DirectorySeparatorChar)), "-Fake", folder })
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["FAKE_GH_FAILS"] = fails.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["FAKE_GH_EMPTY"] = empty ? "1" : "0";

        // Both channels read as they arrive and a limit on the wait, for the reason InstallerGuards gives
        // beside the same lines: a pwsh that hangs must not hang the test run with it.
        var output = new System.Text.StringBuilder();
        var error = new System.Text.StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, line) => output.AppendLine(line.Data);
        process.ErrorDataReceived += (_, line) => error.AppendLine(line.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(TimeSpan.FromSeconds(60)))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"The driver of Read-Release did not finish in 60 s and was ended. It had said:{Environment.NewLine}{output}{error}");
        }

        process.WaitForExit();
        return output.ToString() + error;
    }

    /// <summary>A file of the repository with its whole-line comments taken out, so a word in the explanation of a line never counts as the line.</summary>
    private static string Text(string relative)
    {
        var path = Path.Combine(SourceTree.Root(), relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"There is no {relative}.");
        return string.Join('\n', File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith('#')));
    }
}
