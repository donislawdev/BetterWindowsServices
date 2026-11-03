using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bws.Architecture.Tests;

/// <summary>
/// The reads packaging/sign-release.ps1 makes of the release after the card has signed, step 8 that
/// confirms the draft, and the mode that runs step 8 alone.
///
/// <b>Why this class exists.</b> The signing of 0.5.0 (2026-11-03) stopped in step 8 on ONE failed read
/// of the release, with phase C finished and the draft complete, and all the script could say was
/// "cannot read the release" - the read was a single call with its error output thrown away. Every read
/// of the release now goes through Read-Release, which asks again and keeps what gh said, step 8 is
/// Confirm-Draft, and -CheckDraft runs it over the work folder without signing again. The review of #111
/// then found four ways Confirm-Draft could say PASS or wait over the wrong thing, and those are the
/// scenarios below.
///
/// <b>The script's own functions are run, not copies.</b> The ritual cannot run in a test - it needs a
/// tag, the card and the real repository - so the functions are cut out of the script with PowerShell's
/// own parser and run against a stand-in gh: a batch file first on the path that hands its arguments to
/// a small PowerShell script, which answers from a scenario written beside it and counts its calls.
/// </summary>
public sealed class SigningRitualGuards
{
    private const string Script = "packaging/sign-release.ps1";

    private static readonly string Alpha = Digest("alpha");
    private static readonly string Beta = Digest("beta");

    [Fact]
    public void A_read_of_the_release_that_fails_twice_is_asked_again_and_answers()
    {
        var said = Read("fail", times: 2);
        Assert.True(said.Contains("READ isDraft=True", StringComparison.Ordinal) && said.Contains("CALLS 3", StringComparison.Ordinal),
            $"Two failed reads and a third that answers must come back as the answer, after three calls. Read-Release said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void A_read_that_keeps_failing_stops_with_what_gh_said()
    {
        var said = Read("fail", times: 99);
        Assert.True(said.Contains("THREW", StringComparison.Ordinal) && said.Contains("CALLS 3", StringComparison.Ordinal),
            $"A read that never answers stops the script after three calls. Read-Release said:{Environment.NewLine}{said}");
        Assert.True(said.Contains("HTTP 502: Bad Gateway", StringComparison.Ordinal),
            $"The refusal carries gh's own words - without them nobody can tell a network fault from a missing release. Read-Release said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void A_read_that_prints_nothing_is_not_an_answer()
    {
        var said = Read("empty", times: 99);
        Assert.True(said.Contains("THREW", StringComparison.Ordinal) && said.Contains("printed no document", StringComparison.Ordinal),
            $"gh exiting 0 with nothing on its output is no release - ConvertFrom-Json makes an empty string into $null without a word. Read-Release said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void A_read_that_is_not_json_is_asked_again()
    {
        var said = Read("garbled", times: 1);
        Assert.True(said.Contains("READ isDraft=True", StringComparison.Ordinal) && said.Contains("CALLS 2", StringComparison.Ordinal),
            $"A read that printed something other than JSON is asked again, like any failed read. Read-Release said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void A_complete_draft_with_the_signed_bytes_passes()
    {
        var said = Confirm(Complete());
        Assert.True(said.Contains("CONFIRMED", StringComparison.Ordinal) && said.Contains("PASS:", StringComparison.Ordinal),
            $"A draft with every asset, every bundle and the signed bytes is confirmed. Confirm-Draft said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void A_release_already_published_is_refused_at_once_rather_than_waited_for()
    {
        var said = Confirm(Complete() with { IsDraft = false, Bundles = ["a.zip.sigstore.json"] });
        Assert.True(said.Contains("NOT a draft any more", StringComparison.Ordinal),
            $"A published release with a bundle missing is refused as published - not waited for and then called a draft nothing was published from. Confirm-Draft said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void A_bundle_nobody_expects_does_not_stand_in_for_a_missing_one()
    {
        var said = Confirm(Complete() with { Bundles = ["a.zip.sigstore.json", "other.zip.sigstore.json"] });
        Assert.True(said.Contains("THREW", StringComparison.Ordinal) && said.Contains("attestation bundles missing: b.zip.sigstore.json", StringComparison.Ordinal),
            $"Two bundles where two are expected, but not the two expected, is an incomplete draft. Confirm-Draft said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void A_digest_under_another_name_in_the_checksums_is_refused()
    {
        var said = Confirm(Complete() with { Sums = $"{Beta}  a.zip\n{Alpha}  b.zip\n" });
        Assert.True(said.Contains("THREW", StringComparison.Ordinal) && said.Contains("does NOT give", StringComparison.Ordinal),
            $"Each signed digest has to stand on the line of its own file. Confirm-Draft said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void An_asset_replaced_after_phase_C_is_refused_by_its_own_digest()
    {
        var said = Confirm(Complete() with { DigestOfA = Digest("gamma") });
        Assert.True(said.Contains("THREW", StringComparison.Ordinal) && said.Contains("a.zip on the release is NOT the file we signed", StringComparison.Ordinal),
            $"Checksums that name the signed digests prove nothing about an archive replaced beside them. Confirm-Draft said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void An_asset_without_a_digest_is_downloaded_and_hashed()
    {
        var said = Confirm(Complete() with { DigestOfA = null });
        Assert.True(said.Contains("CONFIRMED", StringComparison.Ordinal),
            $"An asset the release gives no digest for is downloaded and hashed, and the signed bytes pass. Confirm-Draft said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void An_asset_without_a_digest_whose_bytes_differ_is_refused()
    {
        var said = Confirm(Complete() with { DigestOfA = null, BytesOfA = "gamma" });
        Assert.True(said.Contains("THREW", StringComparison.Ordinal) && said.Contains(Digest("gamma"), StringComparison.Ordinal),
            $"A missing digest is never taken as a match - the downloaded bytes are hashed and named. Confirm-Draft said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void Every_read_of_the_release_goes_through_the_one_that_asks_again()
    {
        var script = Text(Script);
        var reads = script.Split("gh release view").Length - 1;
        Assert.True(reads == 1, $"sign-release.ps1 calls gh release view {reads} times. Once, inside Read-Release - a read beside it fails on the first error and says nothing of why.");
        foreach (var line in new[] { "$draft = Read-Release 'assets,isDraft'", "$view = Read-Release 'assets,isDraft'", "$state = Read-Release 'isDraft'" })
        {
            Assert.True(script.Contains(line, StringComparison.Ordinal), $"sign-release.ps1 has no line saying {line}");
        }
    }

    [Fact]
    public void A_published_release_is_refused_before_anything_on_it_is_changed()
    {
        var script = Text(Script);
        var read = script.IndexOf("$draft = Read-Release 'assets,isDraft'", StringComparison.Ordinal);
        var refused = script.IndexOf("if (-not $draft.isDraft) {", StringComparison.Ordinal);
        var deleted = script.IndexOf("'gh', 'release', 'delete-asset'", StringComparison.Ordinal);
        var uploaded = script.IndexOf("'gh', 'release', 'upload'", StringComparison.Ordinal);
        Assert.True(read > 0 && refused > read && deleted > refused && uploaded > refused,
            $"Step 7 reads the release at {read}, refuses a published one at {refused}, deletes bundles at {deleted} and uploads at {uploaded}. The refusal has to come before both - a second run on a published release would take its bundles off and overwrite its files.");
    }

    [Fact]
    public void The_draft_check_alone_comes_before_the_work_folder_is_cleared_and_the_card_is_reached()
    {
        var lines = Text(Script).Split('\n').Select(line => line.Trim()).ToArray();
        var alone = Array.IndexOf(lines, "if ($CheckDraft) {");
        var cleared = Array.FindIndex(lines, line => line.StartsWith("if (Test-Path -LiteralPath $workspace) { Remove-Item", StringComparison.Ordinal));
        var card = Array.FindIndex(lines, line => line.StartsWith("$certificate = Get-CodeSigningCertificates", StringComparison.Ordinal));
        Assert.True(alone >= 0 && cleared > alone && card > alone,
            $"-CheckDraft is decided on line {alone}, the work folder is cleared on {cleared} and the card reached on {card}. It has to be decided first - it reads the folder a signing run left, and signs nothing.");

        var branch = lines[alone..cleared];
        Assert.True(branch.Contains("Confirm-Draft $attested") && branch.Contains("return"),
            "The -CheckDraft branch no longer has the lines that run step 8 and return - without the return it would go on to clear the folder and sign.");
        Assert.True(lines.Last(line => line.Length > 0) == "Confirm-Draft $attested",
            "The ritual no longer ends with a line that runs step 8 - the same function -CheckDraft runs, so the two cannot drift apart.");
    }

    /// <summary>What a draft looks like in a scenario: whether it is one, file a's digest and bytes, the bundles, SHA256SUMS.</summary>
    private sealed record Release(bool IsDraft, string? DigestOfA, string BytesOfA, string[] Bundles, string Sums);

    /// <summary>The draft a finished signing run leaves: everything there, a.zip and b.zip with the bytes that were signed.</summary>
    private static Release Complete() =>
        new(IsDraft: true, DigestOfA: Alpha, BytesOfA: "alpha", Bundles: ["a.zip.sigstore.json", "b.zip.sigstore.json"], Sums: $"{Alpha}  a.zip\n{Beta}  b.zip\n");

    private static string Read(string mode, int times) =>
        Run("read", JsonSerializer.Serialize(new { mode, times, view = new { isDraft = true, assets = Array.Empty<object>() }, files = new Dictionary<string, string>() }));

    private static string Confirm(Release release)
    {
        var assets = new List<object>
        {
            new { name = "a.zip", digest = release.DigestOfA is null ? null : "sha256:" + release.DigestOfA },
            new { name = "b.zip", digest = (string?)("sha256:" + Beta) },
            new { name = "SHA256SUMS", digest = (string?)null },
        };
        assets.AddRange(release.Bundles.Select(bundle => (object)new { name = bundle, digest = (string?)null }));
        var files = new Dictionary<string, string> { ["SHA256SUMS"] = release.Sums, ["a.zip"] = release.BytesOfA };
        return Run("confirm", JsonSerializer.Serialize(new { mode = "answer", times = 0, view = new { isDraft = release.IsDraft, assets }, files }));
    }

    /// <summary>
    /// The four functions cut out of the script and one of them run against the stand-in gh, with what the
    /// driver printed: READ or CONFIRMED, or THREW and the message, then CALLS and how often gh was asked.
    /// </summary>
    private static string Run(string call, string scenario)
    {
        var folder = Directory.CreateTempSubdirectory("bws-signing-");
        try
        {
            File.WriteAllText(Path.Combine(folder.FullName, "gh.cmd"), "@pwsh -NoProfile -File \"%~dp0stand-in.ps1\" %*\r\n@exit /b %errorlevel%\r\n");
            File.WriteAllText(Path.Combine(folder.FullName, "stand-in.ps1"), StandIn);
            File.WriteAllText(Path.Combine(folder.FullName, "driver.ps1"), Driver);
            File.WriteAllText(Path.Combine(folder.FullName, "scenario.json"), scenario);
            File.WriteAllText(Path.Combine(folder.FullName, "a.zip"), "alpha");
            File.WriteAllText(Path.Combine(folder.FullName, "b.zip"), "beta");
            var said = Execute(folder.FullName, call);
            Assert.False(said.Contains("NO FUNCTION", StringComparison.Ordinal), $"{Script} lost a function this class runs:{Environment.NewLine}{said}");
            return said;
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>The driver as its own process, with what it said on both channels.</summary>
    private static string Execute(string folder, string call) =>
        PwshScript.Start(["-NoProfile", "-File", Path.Combine(folder, "driver.ps1"), "-Script", Path.Combine(SourceTree.Root(), Script.Replace('/', Path.DirectorySeparatorChar)), "-Fake", folder, "-Call", call],
            TimeSpan.FromSeconds(90), "The driver").Said;

    private static string Digest(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>A file of the repository with its whole-line comments taken out, so a word in the explanation of a line never counts as the line.</summary>
    private static string Text(string relative)
    {
        var path = Path.Combine(SourceTree.Root(), relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"There is no {relative}.");
        return string.Join('\n', File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith('#')));
    }

    /// <summary>
    /// The gh this class puts first on the path. A view misbehaves for the first `times` calls in the
    /// scenario's mode - fail with a line on the error channel, exit 0 with nothing, or print what is not
    /// JSON - and then answers the scenario's view. A download writes the scenario's file of that name.
    /// </summary>
    private const string StandIn = """
        $scenario = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'scenario.json') | ConvertFrom-Json
        $count = Join-Path $PSScriptRoot 'count'
        $n = 1
        if (Test-Path -LiteralPath $count) { $n += [int](Get-Content -Raw -LiteralPath $count) }
        Set-Content -LiteralPath $count -Value $n
        if ($args[1] -eq 'view') {
            if ($n -le $scenario.times) {
                if ($scenario.mode -eq 'empty') { exit 0 }
                if ($scenario.mode -eq 'garbled') { 'this is not json'; exit 0 }
                [Console]::Error.WriteLine('HTTP 502: Bad Gateway')
                exit 1
            }
            $scenario.view | ConvertTo-Json -Depth 5 -Compress
            exit 0
        }
        if ($args[1] -eq 'download') {
            $pattern = $args[[array]::IndexOf($args, '--pattern') + 1]
            $dir = $args[[array]::IndexOf($args, '--dir') + 1]
            [IO.File]::WriteAllText((Join-Path $dir $pattern), [string]$scenario.files.$pattern)
            exit 0
        }
        [Console]::Error.WriteLine("the stand-in gh was asked: $($args -join ' ')")
        exit 1
        """;

    /// <summary>
    /// The script that finds the four functions in sign-release.ps1 with PowerShell's parser, defines them,
    /// puts the stand-in first on the path and makes one call, under the Stop and StrictMode the ritual runs with.
    /// </summary>
    private const string Driver = """
        param([string] $Script, [string] $Fake, [string] $Call)
        $ErrorActionPreference = 'Stop'
        Set-StrictMode -Version Latest
        $tokens = $null
        $errors = $null
        $tree = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$tokens, [ref]$errors)
        foreach ($wanted in 'Invoke-Step', 'Get-Sha256', 'Read-Release', 'Confirm-Draft') {
            $found = $tree.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $wanted }, $true)
            if (-not $found) { "NO FUNCTION $wanted"; exit 2 }
            . ([scriptblock]::Create($found.Extent.Text))
        }
        $env:PATH = $Fake + [IO.Path]::PathSeparator + $env:PATH
        $Tag = 'v9.9.9'
        $repo = 'nobody/nothing'
        $Wait = 0
        $attestWorkflow = 'attest-signed.yml'
        $workspace = $Fake
        $EXPECTED_ASSETS = @('a.zip', 'b.zip', 'SHA256SUMS')
        try {
            if ($Call -eq 'read') {
                $view = Read-Release 'isDraft' 'in a test' -PauseSeconds 0
                "READ isDraft=$($view.isDraft)"
            }
            else {
                Confirm-Draft @((Join-Path $Fake 'a.zip'), (Join-Path $Fake 'b.zip'))
                'CONFIRMED'
            }
        }
        catch { "THREW $($_.Exception.Message)" }
        "CALLS $((Get-Content -LiteralPath (Join-Path $Fake 'count') -Raw).Trim())"
        """;
}
