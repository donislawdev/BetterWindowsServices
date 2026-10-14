using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace Bws.Architecture.Tests;

/// <summary>
/// The Windows installer, and the scripts and workflows that build, sign, attest and check it.
///
/// <b>What the installer DOES on a machine is not asked here.</b> packaging/test-installer.ps1 asks
/// that of a machine - a clean runner in .github/workflows/installer.yml, and the throwaway virtual
/// machine before that (docs, PROJEKT-MSI, 2026-10-14). These guards hold what that job cannot see
/// coming: the lines each of those measurements rests on, a value that must never change, the
/// refusals of the script that builds it, and the places a release has to name it the same way.
/// Every line below changed would still build a working installer - which is why it needs a guard
/// rather than a build. What breaks is an upgrade, an uninstall, or another account's Start menu, on
/// somebody else's machine.
///
/// <b>The first class of this project that starts a process of PowerShell.</b> The installer source
/// is a template, so its shape is asked of what packaging/build-msi.ps1 RENDERS rather than of the
/// template, the way the guards of the owner's two other projects ask theirs. Every refusal asked
/// here comes before WiX is looked for, so these run on a machine without WiX.
/// </summary>
public sealed class InstallerGuards
{
    /// <summary>
    /// The product's identity in Windows Installer, for good. Every machine that has the program finds
    /// the version it has through it, so a new one would leave the old install beside the new one on
    /// every one of them. Written here a second time on purpose: this is the copy that does not move
    /// when the script does.
    /// </summary>
    private const string UpgradeCode = "FD038501-7A2F-49C9-A587-E8DF3EFB9718";

    private static readonly XNamespace Wix = "http://wixtoolset.org/schemas/v4/wxs";

    private static readonly Lazy<XDocument> Rendered = new(() =>
    {
        var (code, said) = BuildMsi("-Tag", "v1.2.3", "-SourceOnly");
        Assert.True(code == 0, $"build-msi.ps1 refused to render v1.2.3 (exit {code}):{Environment.NewLine}{said}");
        return XDocument.Parse(said);
    });

    [Fact]
    public void The_installer_carries_nothing_but_windows_installer_tables()
    {
        var elements = Rendered.Value.Descendants().ToArray();
        Assert.True(elements.Length >= 10, $"Read {elements.Length} element(s) from the rendered installer, so this guard is not reading it.");

        // Each of these puts code into the package or ends a program: an action of its own, a property
        // or folder set by an action, or a dialog. The owner's decision of 2026-10-14 is that nothing
        // running is ended, and the package carries our files and Windows Installer's tables only.
        string[] forbidden = ["CustomAction", "CustomActionRef", "SetProperty", "SetDirectory", "CloseApplication",
            "InstallExecuteSequence", "InstallUISequence", "UI", "UIRef", "Binary"];
        var found = elements
            .Where(element => element.Name.Namespace != Wix || forbidden.Contains(element.Name.LocalName))
            .Select(element => element.Name.ToString())
            .Concat(elements.SelectMany(element => element.Attributes())
                .Where(attribute => attribute.IsNamespaceDeclaration && attribute.Name.LocalName != "xmlns")
                .Select(attribute => "an extension namespace " + attribute.Value))
            .ToArray();

        Assert.True(found.Length == 0, "The installer brings in " + string.Join(", ", found)
            + ". It uses no extension of WiX, runs no action of its own and shows only Windows Installer's dialogs.");
    }

    [Fact]
    public void The_installer_is_the_product_for_good_and_ends_nothing_that_runs()
    {
        var package = OnlyOne("Package");
        Assert.Equal(UpgradeCode, (string?)package.Attribute("UpgradeCode"));
        Assert.Equal("perMachine", (string?)package.Attribute("Scope"));

        // afterInstallInitialize: the old product goes first, so a rebuild under the same number really
        // replaces its files (measured 2026-10-14 on the virtual machine, "a rebuild" in
        // test-installer.ps1). AllowSameVersionUpgrades: that rebuild replaces the first build rather
        // than installing beside it.
        var upgrade = OnlyOne("MajorUpgrade");
        Assert.Equal("afterInstallInitialize", (string?)upgrade.Attribute("Schedule"));
        Assert.Equal("yes", (string?)upgrade.Attribute("AllowSameVersionUpgrades"));
        Assert.False(string.IsNullOrWhiteSpace((string?)upgrade.Attribute("DowngradeErrorMessage")), "An older version is refused with a sentence.");

        var manager = Rendered.Value.Descendants(Wix + "Property").Where(p => (string?)p.Attribute("Id") == "MSIRESTARTMANAGERCONTROL").ToArray();
        Assert.True(manager.Length == 1 && (string?)manager[0].Attribute("Value") == "Disable",
            "The Restart Manager is off, or an upgrade ends the window in the middle of a plan that stops services. "
            + "It has to be in the package from the first installer on, because an upgrade runs under the OLD package's properties.");
    }

    [Fact]
    public void The_installer_puts_one_folder_on_the_path_and_the_window_in_every_start_menu()
    {
        var path = OnlyOne("Environment");
        foreach (var (attribute, want) in new[] { ("Name", "PATH"), ("System", "yes"), ("Part", "last"), ("Value", "[INSTALLFOLDER]"), ("Permanent", "no") })
        {
            Assert.True((string?)path.Attribute(attribute) == want,
                $"The PATH entry has {attribute}={(string?)path.Attribute(attribute)}, want {want}: the folder goes on the machine's PATH, after everything already there, and comes off again at uninstall.");
        }

        var shortcut = OnlyOne("Shortcut");
        Assert.Equal("[INSTALLFOLDER]" + Register().GetProperty("packages").GetProperty("gui").GetProperty("executable").GetString(), (string?)shortcut.Attribute("Target"));
        Assert.Equal("INSTALLFOLDER", (string?)shortcut.Attribute("WorkingDirectory"));
        Assert.Equal("ProgramMenuFolder", (string?)shortcut.Ancestors(Wix + "StandardDirectory").Single().Attribute("Id"));

        var folder = Rendered.Value.Descendants(Wix + "Directory").Single(d => (string?)d.Attribute("Id") == "INSTALLFOLDER");
        Assert.Equal("ProgramFiles64Folder", (string?)folder.Parent?.Attribute("Id"));
    }

    [Theory]
    [InlineData("v1.0.0-rc.1", "is a release candidate")]
    [InlineData("v1.0.0-beta.2", "is a release candidate")]
    [InlineData("1.0.0", "is not a release tag")]
    [InlineData("v256.0.0", "does not fit an installer version")]
    [InlineData("v0.256.0", "does not fit an installer version")]
    [InlineData("v0.0.65536", "does not fit an installer version")]
    public void A_tag_the_installer_cannot_carry_is_refused_rather_than_cut(string tag, string says)
    {
        var (code, said) = BuildMsi("-Tag", tag, "-SourceOnly");
        Assert.True(code == 1 && said.Contains(says, StringComparison.Ordinal), $"build-msi.ps1 -Tag {tag} exited {code} and said:{Environment.NewLine}{said}{Environment.NewLine}want exit 1 and \"{says}\"");
    }

    [Fact]
    public void The_largest_version_an_installer_holds_is_not_refused()
    {
        // So the refusals above are refused for what they are, and not for something else.
        var (code, said) = BuildMsi("-Tag", "v255.255.65535", "-SourceOnly");
        Assert.True(code == 0, $"build-msi.ps1 refused the largest version an installer holds (exit {code}):{Environment.NewLine}{said}");
    }

    [Fact]
    public void The_installer_is_signed_with_the_name_it_carries()
    {
        // Windows shows the signature's description as the program's name when it asks an administrator
        // to let the installer run, and a string of digits without it.
        var (code, said) = BuildMsi("-Tag", "v1.2.3", "-ProductName");
        Assert.Equal(0, code);
        Assert.Equal((string?)OnlyOne("Package").Attribute("Name"), said.Trim());
    }

    [Fact]
    public void Every_place_a_release_names_the_installer_names_it_the_same_way()
    {
        var name = Register().GetProperty("installer").GetProperty("file").GetString()!;
        Assert.EndsWith(".msi", name, StringComparison.Ordinal);

        // Phase C names the installer once and attests it under that one copy (review of #68).
        (string File, string Line)[] places =
        [
            (".github/workflows/attest-signed.yml", "INSTALLER: " + name),
            (".github/workflows/attest-signed.yml", "subject-path: assets/${{ env.INSTALLER }}"),
            ("README.md", "gh attestation verify " + name + " --repo"),
            ("site/site.json", "\"installer\": \"" + name + "\""),
        ];
        foreach (var (file, line) in places)
        {
            Assert.True(Text(file).Contains(line, StringComparison.Ordinal), $"{file} does not say \"{line}\" - the installer is {name} in packaging/components.json, and one file has one name.");
        }

        Assert.True(Text("README.md").Contains($"(Get-FileHash {name} -Algorithm SHA256)", StringComparison.Ordinal),
            "The README's verification commands do not check the installer against SHA256SUMS, and phase D runs those commands as written.");
    }

    [Fact]
    public void The_signing_asks_first_builds_from_the_signed_packages_and_holds_the_installer_to_the_pin()
    {
        var script = Text("packaging/sign-release.ps1");
        var asked = script.IndexOf("'-Tag', $Tag, '-Check'", StringComparison.Ordinal);
        var card = script.IndexOf("$certificate = Get-CodeSigningCertificates", StringComparison.Ordinal);
        Assert.True(asked > 0 && card > asked, $"sign-release.ps1 asks whether the installer can be built at {asked} and reaches the card at {card}. It asks first, so a machine without WiX stops before anything is signed.");

        var repacked = script.IndexOf("Compress-Archive -Path", StringComparison.Ordinal);
        var built = script.IndexOf("$installerPath = New-Installer", StringComparison.Ordinal);
        Assert.True(repacked > 0 && built > repacked, "sign-release.ps1 builds the installer before the signed archives are repacked - it has to carry the bytes a person unzips.");

        foreach (var line in new[] { "'/d', $productName", "Assert-SignedByPin $installerPath $INSTALLER $pin.certificate_sha256",
            "-PackageId 'installer'", "$EXPECTED_ASSETS += @($INSTALLER, \"$INSTALLER.spdx.json\")", "if ($withInstaller) { $attested += $installerPath }" })
        {
            Assert.True(script.Contains(line, StringComparison.Ordinal), $"sign-release.ps1 has no line saying {line}");
        }
    }

    [Fact]
    public void Every_phase_asks_the_same_question_of_a_candidate_and_of_an_older_tree()
    {
        // One rule: the register of the tagged tree names an installer, and the tag has no hyphen. Two
        // rules would disagree about a tag like v1.0.0-beta, and the one that says "release" would put an
        // installer on a candidate's page - which Windows Installer would then hold against the release.
        Assert.True(Text("packaging/sign-release.ps1").Contains("return [bool]$INSTALLER -and -not $releaseTag.Contains('-')", StringComparison.Ordinal),
            "sign-release.ps1 no longer builds an installer exactly when the register names one and the tag is no candidate.");

        var verify = Text(".github/workflows/verify-release.yml");
        foreach (var line in new[] { "ref: refs/tags/${{ env.TAG }}", "if ($register.PSObject.Properties['installer'])", "if (-not $env:TAG.Contains('-'))",
            "$expected += @($installer, \"$installer.spdx.json\", \"$installer.sigstore.json\")", "Get-AuthenticodeSignature -LiteralPath $installer",
            "$commands = @($commands | Where-Object { -not $_.Contains($installer) })" })
        {
            Assert.True(verify.Contains(line, StringComparison.Ordinal), $"verify-release.yml has no line saying {line}");
        }

        Assert.True(Text(".github/workflows/attest-signed.yml").Contains("if: steps.installer.outputs.present == 'true'", StringComparison.Ordinal),
            "attest-signed.yml no longer attests the installer when the release carries one.");
    }

    [Fact]
    public void The_installer_is_installed_on_a_clean_runner()
    {
        var job = Text(".github/workflows/installer.yml");
        foreach (var line in new[] { "runs-on: windows-latest", "- 'packaging/**'", "- 'src/**'", "run: ./packaging/build-dist.ps1",
            "Select-String -Path packaging/build-msi.ps1 -Pattern", "run: ./packaging/test-installer.ps1 -Window dist/staging/BetterWindowsServices -CommandLine dist/staging/bws" })
        {
            Assert.True(job.Contains(line, StringComparison.Ordinal), $"installer.yml has no line saying {line} - without it the installer is asked by nobody before a release.");
        }
    }

    private static XElement OnlyOne(string name)
    {
        var found = Rendered.Value.Descendants(Wix + name).ToArray();
        Assert.True(found.Length == 1, $"The rendered installer has {found.Length} <{name}>, and it has one.");
        return found[0];
    }

    private static JsonElement Register() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(SourceTree.Root(), "packaging", "components.json"))).RootElement;

    /// <summary>
    /// A file of the repository with its whole-line comments taken out, so a word in the explanation of
    /// a line never counts as the line. Comments in PowerShell and YAML both start with a hash.
    /// </summary>
    private static string Text(string relative)
    {
        var path = Path.Combine(SourceTree.Root(), relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"There is no {relative}.");
        return string.Join('\n', File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith('#')));
    }

    /// <summary>build-msi.ps1 as its own process, the way the signing calls it, with what it said on both channels.</summary>
    private static (int Code, string Said) BuildMsi(params string[] arguments)
    {
        var start = new ProcessStartInfo("pwsh") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(SourceTree.Root(), "packaging", "build-msi.ps1") }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }

        // Both channels are read as they arrive, so the limit below is the one that holds: a read to the
        // end would wait for pwsh to close its output, and a pwsh that hangs never would - the test run
        // would hang with it (review of #68). Each channel has its own builder, and the events of one
        // channel come one after another.
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
            Assert.Fail($"build-msi.ps1 did not finish in 60 s and was ended. It had said:{Environment.NewLine}{output}{error}");
        }

        // Without a limit once it has exited: this is what waits for the last line of both channels.
        process.WaitForExit();
        return (process.ExitCode, output.ToString() + error);
    }
}
