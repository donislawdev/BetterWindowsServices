using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// The WinGet and Chocolatey packages, and the script that renders them (packaging/build-feeds.ps1).
///
/// <b>What a package does on a machine is not asked here.</b> That is measured by installing it with the
/// feed's own client on the throwaway virtual machine (docs, PROJEKT-FEEDY, 2026-11-03). These guards hold
/// what such a measurement cannot see coming: the shape it rests on, the names that are public for good,
/// the one source of the UpgradeCode, and the refusals of the script. Rendering a real release needs its
/// files signed by our certificate, so every refusal asked here comes before the signatures or is theirs,
/// on release folders built here: this assembly as both programs - its product version is the build's -
/// and a copy of the pwsh.exe that runs the script as the installer, validly signed by somebody else.
///
/// The same guard as the owner's ChronoMock (crates/cli/tests/feeds.rs, 2026-11-02), less its check that
/// two lists of signed binaries agree: here the signing, the release check and the packages all read one
/// register, packaging/components.json.
/// </summary>
public sealed class FeedGuards
{
    /// <summary>The installer's stand-in: a file validly signed by somebody else's certificate.</summary>
    private static readonly Lazy<string> SignedBySomebodyElse = new(() => Path.Combine(PwshScript.Home(), "pwsh.exe"));

    /// <summary>
    /// The size an archive's headers declare for its program, where a change has it lie: far past what the
    /// script copies out of an archive, or less than the program it holds - stored, as these archives store
    /// it, so the .NET reader returns every byte whatever the header says.
    /// </summary>
    private static readonly Dictionary<string, uint> Declared = new(StringComparer.Ordinal)
    {
        ["declares-huge:cli"] = 0xF0000000,
        ["declares-less:cli"] = 1000,
    };

    [Fact]
    public void The_window_installs_the_installer_first_and_the_archive_second()
    {
        var (entries, outside) = Installers(Text("packaging/winget/installer.window.yaml.in"));
        Assert.True(entries.Count == 2, $"The window manifest lists {entries.Count} installers, and it lists two:{Environment.NewLine}{string.Join(Environment.NewLine, entries)}");
        foreach (var line in new[] { "Architecture: x64", "InstallerType: wix", "Scope: machine", "ElevationRequirement: elevatesSelf",
            "InstallerUrl: {{MSI_URL}}", "InstallerSha256: {{MSI_SHA256}}", "ProductCode: '{{MSI_PRODUCT_CODE}}'", "UpgradeCode: '{{MSI_UPGRADE_CODE}}'" })
        {
            Assert.True(HasLine(entries[0], line), $"The FIRST installer has no line '{line}' - a fresh install takes the first that applies, and WinGet finds an installed one by its ProductCode:{Environment.NewLine}{entries[0]}");
        }

        // The archive, for --scope user: no scope of its own, and its own folder on PATH rather than a link,
        // so the window restarts as administrator from its own file (Environment.ProcessPath).
        foreach (var line in new[] { "Architecture: x64", "InstallerType: zip", "NestedInstallerType: portable", "RelativeFilePath: {{APP_ZIP_EXE}}",
            "ArchiveBinariesDependOnPath: true", "InstallerUrl: {{APP_ZIP_URL}}", "InstallerSha256: {{APP_ZIP_SHA256}}" })
        {
            Assert.True(HasLine(entries[1], line), $"The SECOND installer has no line '{line}':{Environment.NewLine}{entries[1]}");
        }

        Assert.DoesNotContain("Scope:", entries[1], StringComparison.Ordinal);
        foreach (var field in new[] { "InstallerType", "Scope", "NestedInstallerType", "ArchiveBinariesDependOnPath" })
        {
            Assert.False(outside.Contains(field, StringComparison.Ordinal), $"{field} stands at the top of the window manifest, and it belongs in each installer's entry: the two are of two kinds.");
        }
    }

    [Fact]
    public void The_command_line_is_reached_through_its_own_folder_on_path_and_not_through_a_link_named_bws()
    {
        // A link would be named bws.exe, as the link of Bitwarden Secrets Manager CLI is (backlog 635).
        var manifest = Text("packaging/winget/installer.cli.yaml.in");
        foreach (var line in new[] { "InstallerType: zip", "NestedInstallerType: portable", "RelativeFilePath: {{CLI_ZIP_EXE}}",
            "ArchiveBinariesDependOnPath: true", "InstallerUrl: {{CLI_ZIP_URL}}", "InstallerSha256: {{CLI_ZIP_SHA256}}", "bws" })
        {
            Assert.True(HasLine(manifest, line), $"The command line manifest has no line '{line}':{Environment.NewLine}{manifest}");
        }

        Assert.Single(Installers(manifest).Entries);
        Assert.DoesNotContain("PortableCommandAlias", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void The_chocolatey_window_runs_the_installer_and_finds_it_again_by_its_upgrade_code()
    {
        var install = Text("packaging/chocolatey/tools/chocolateyinstall.window.ps1.in");
        foreach (var line in new[] { "fileType       = 'msi'", "url64bit       = '{{MSI_URL}}'", "checksum64     = '{{MSI_SHA256}}'",
            "checksumType64 = 'sha256'", "validExitCodes = @(0, 3010, 1641)", "Install-ChocolateyPackage @packageArgs" })
        {
            Assert.True(install.Contains(line, StringComparison.Ordinal), $"The window's install script has no line saying {line}");
        }

        // The installer makes the Start menu entry and the PATH entry, so the package makes nothing of its own
        // that it would have to take away again.
        foreach (var absent in new[] { "Install-ChocolateyZipPackage", "Install-ChocolateyShortcut", "Install-BinFile" })
        {
            Assert.DoesNotContain(absent, install, StringComparison.Ordinal);
        }

        var uninstall = Text("packaging/chocolatey/tools/chocolateyuninstall.window.ps1.in");
        foreach (var line in new[] { "$upgradeCode = '{{MSI_UPGRADE_CODE}}'", ".RelatedProducts($upgradeCode)", "-FileType 'msi'" })
        {
            Assert.True(uninstall.Contains(line, StringComparison.Ordinal), $"The window's uninstall script has no line saying {line}");
        }
    }

    [Fact]
    public void The_chocolatey_command_line_unpacks_the_release_archive_and_makes_nothing_else()
    {
        var install = Text("packaging/chocolatey/tools/chocolateyinstall.cli.ps1.in");
        foreach (var line in new[] { "Install-ChocolateyZipPackage", "-Url64bit '{{CLI_ZIP_URL}}'", "-Checksum64 '{{CLI_ZIP_SHA256}}'", "-UnzipLocation $toolsDir" })
        {
            Assert.True(install.Contains(line, StringComparison.Ordinal), $"The command line's install script has no line saying {line}");
        }

        Assert.DoesNotContain("Install-ChocolateyShortcut", install, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(SourceTree.Root(), "packaging", "chocolatey", "tools", "chocolateyuninstall.cli.ps1.in")),
            "The command line package makes nothing outside its own folder, so Chocolatey takes it all away and needs no uninstall script.");
    }

    [Fact]
    public void The_package_names_are_the_published_ones()
    {
        // Public for good: a person installs and upgrades by them, and a new one is a new package beside the
        // old. The owner chose these on 2026-11-03, and the command line's moniker leaves out bws, which
        // Bitwarden Secrets Manager CLI has in WinGet (backlog 635).
        // Each name inside the block of its own package, from its Kind line to the next one: the two blocks
        // with their names swapped would still hold all six, and the window would render as the command line.
        var script = Text("packaging/build-feeds.ps1");
        foreach (var (kind, key, name) in new[] { ("window", "WingetId", "DonislawDev.BetterWindowsServices"), ("cli", "WingetId", "DonislawDev.BetterWindowsServices.CLI"),
            ("window", "ChocoId", "better-windows-services"), ("cli", "ChocoId", "better-windows-services-cli"),
            ("window", "Moniker", "betterwindowsservices"), ("cli", "Moniker", "betterwindowsservices-cli") })
        {
            var pattern = $@"(?m)^\s*Kind\s*=\s*'{kind}'\s*$(?:(?!^\s*Kind\s*=)[\s\S])*?^\s*{key}\s*=\s*'{Regex.Escape(name)}'\s*$";
            Assert.True(Regex.IsMatch(script, pattern, RegexOptions.None, TimeSpan.FromSeconds(5)), $"The {kind} package of build-feeds.ps1 does not say {key} = '{name}'.");
        }
    }

    [Fact]
    public void The_upgrade_code_reaches_the_packages_only_through_the_installer_source()
    {
        var code = Regex.Match(File.ReadAllText(Path.Combine(SourceTree.Root(), "packaging", "build-msi.ps1")), @"(?m)^\$UpgradeCode = '([0-9A-F-]{36})'", RegexOptions.None, TimeSpan.FromSeconds(5)).Groups[1].Value;
        Assert.True(code.Length == 36, "build-msi.ps1 no longer assigns $UpgradeCode, so this guard does not know what to look for.");
        var files = new[] { "build-feeds.ps1" }.Select(name => Path.Combine(SourceTree.Root(), "packaging", name))
            .Concat(new[] { "winget", "chocolatey" }.SelectMany(dir => Directory.EnumerateFiles(Path.Combine(SourceTree.Root(), "packaging", dir), "*", SearchOption.AllDirectories)))
            .ToArray();
        Assert.True(files.Length >= 9, $"Read {files.Length} files, so this guard is not reading the templates.");
        foreach (var file in files)
        {
            Assert.False(File.ReadAllText(file).Contains(code, StringComparison.OrdinalIgnoreCase),
                $"{Path.GetFileName(file)} carries the UpgradeCode itself. It comes from build-msi.ps1 -SourceOnly, so a copy cannot stay behind when the script changes.");
        }
    }

    [Fact]
    public void A_release_that_gets_past_every_other_check_stops_at_the_signatures_and_names_every_file()
    {
        var dir = Case();
        try
        {
            var (code, said) = BuildFeeds(Tag(), ReleaseFolder(dir, "none"), Path.Combine(dir, "out"));
            Assert.True(code == 1 && said.Contains("not signed by the certificate packaging/codesign.json pins", StringComparison.Ordinal), $"The fixture should stop at the signatures, and exited {code}:{Environment.NewLine}{said}");
            Assert.Contains($"{Installer()} (signed by a certificate with SHA-256 ", said, StringComparison.Ordinal);
            foreach (var (_, zip, program) in Programs())
            {
                Assert.Contains($"{program} in {zip} (signature status NotSigned)", said, StringComparison.Ordinal);
            }

            Assert.Equal(["release"], Left(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("remove:SHA256SUMS", "is not there")]
    [InlineData("unlist:BetterWindowsServices-win-x64.msi", "has no line for BetterWindowsServices-win-x64.msi")]
    [InlineData("remove:bws-cli-win-x64.zip", "bws-cli-win-x64.zip is not in")]
    [InlineData("overwrite:BetterWindowsServices-win-x64.zip", "hashes to")]
    [InlineData("append:not a sum", "has a line that is not")]
    [InlineData("twice", "twice")]
    [InlineData("remove-folder", "is not a folder")]
    [InlineData("not-an-archive:cli", "bws-cli-win-x64.zip could not be read as an archive")]
    [InlineData("without:cli", "bws-cli-win-x64.zip holds no bws/bws.exe")]
    [InlineData("as-text:gui", "BetterWindowsServices/BetterWindowsServices.exe in BetterWindowsServices-win-x64.zip is version ''")]
    [InlineData("foreign:gui", "BetterWindowsServices/BetterWindowsServices.exe in BetterWindowsServices-win-x64.zip is version '7.")]
    [InlineData("declares-huge:cli", "bws-cli-win-x64.zip declares bws/bws.exe at 4026531840 bytes")]
    [InlineData("declares-less:cli", "bws-cli-win-x64.zip holds more of bws/bws.exe than the 1000 bytes it declares")]
    public void A_folder_that_is_not_a_published_release_is_refused_and_leaves_nothing(string change, string says)
    {
        var dir = Case();
        try
        {
            var release = ReleaseFolder(dir, change);
            Change(release, change);
            var (code, said) = BuildFeeds(Tag(), release, Path.Combine(dir, "out"));
            Assert.True(code == 1 && said.Contains(says, StringComparison.Ordinal), $"{change}: exited {code} and said:{Environment.NewLine}{said}{Environment.NewLine}want exit 1 and \"{says}\"");
            Assert.DoesNotContain(Left(dir), name => name != "release");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("v0.5.0-rc.1", "is a release candidate")]
    [InlineData("0.5.0", "is not a release tag")]
    public void A_tag_that_is_no_release_gets_no_packages(string tag, string says)
    {
        var (code, said) = BuildFeeds(tag, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "bws-feeds-guard-never-" + Guid.NewGuid().ToString("N")));
        Assert.True(code == 1 && said.Contains(says, StringComparison.Ordinal), $"-Tag {tag} exited {code} and said:{Environment.NewLine}{said}");
    }

    [Fact]
    public void The_packages_are_rendered_neither_into_the_repository_nor_into_a_folder_that_exists()
    {
        var inside = Path.Combine(SourceTree.Root(), "artifacts", "feeds-guard-" + Guid.NewGuid().ToString("N"));
        var (code, said) = BuildFeeds(Tag(), Path.GetTempPath(), inside);
        Assert.True(code == 1 && said.Contains("is inside the repository", StringComparison.Ordinal), $"A destination in the repository exited {code}:{Environment.NewLine}{said}");
        Assert.False(Directory.Exists(inside), "Nothing was to be written into the repository.");

        var taken = Case();
        try
        {
            (code, said) = BuildFeeds(Tag(), Path.GetTempPath(), taken);
            Assert.True(code == 1 && said.Contains("exists already", StringComparison.Ordinal), $"A destination that exists exited {code}:{Environment.NewLine}{said}");
        }
        finally
        {
            Directory.Delete(taken, recursive: true);
        }
    }

    /// <summary>
    /// Builds a release folder that gets as far as the signatures: the installer as a copy of pwsh.exe, each
    /// archive holding its program as this assembly plus its licence, and a SHA256SUMS listing the three as
    /// they are. The change names one thing the folder holds otherwise: an archive that is not one, an
    /// archive without its program, a program of no version, a program of another release, or an archive
    /// whose headers declare a size its program does not have.
    /// </summary>
    private static string ReleaseFolder(string dir, string change)
    {
        var release = Directory.CreateDirectory(Path.Combine(dir, "release")).FullName;
        File.Copy(SignedBySomebodyElse.Value, Path.Combine(release, Installer()));
        foreach (var (half, zip, program) in Programs())
        {
            var path = Path.Combine(release, zip);
            if (change == "not-an-archive:" + half)
            {
                File.WriteAllText(path, "stand-in for " + zip);
                continue;
            }

            var source = change == "foreign:" + half ? SignedBySomebodyElse.Value : change == "as-text:" + half ? null : typeof(FeedGuards).Assembly.Location;
            var licence = (program[..program.IndexOf('/', StringComparison.Ordinal)] + "/LICENSE", (string?)null);
            ReleaseArchive.Write(path, change == "without:" + half ? [licence] : [(program, source), licence]);
            if (Declared.TryGetValue(change, out var declared) && change.EndsWith(":" + half, StringComparison.Ordinal))
            {
                ReleaseArchive.Declare(path, program, declared);
            }
        }

        var names = new[] { Installer() }.Concat(Programs().Select(p => p.Zip));
        File.WriteAllText(Path.Combine(release, "SHA256SUMS"), string.Concat(names.Select(name => $"{Sha256(Path.Combine(release, name))}  {name}\n")));
        return release;
    }

    /// <summary>The changes made to a release folder once it is built, each one thing a published one never is.</summary>
    private static void Change(string release, string change)
    {
        var sums = Path.Combine(release, "SHA256SUMS");
        var colon = change.IndexOf(':', StringComparison.Ordinal);
        var (kind, what) = colon < 0 ? (change, "") : (change[..colon], change[(colon + 1)..]);
        switch (kind)
        {
            case "remove": File.Delete(Path.Combine(release, what)); break;
            case "unlist": File.WriteAllLines(sums, File.ReadAllLines(sums).Where(line => !line.EndsWith(what, StringComparison.Ordinal))); break;
            case "overwrite": File.WriteAllText(Path.Combine(release, what), "other bytes"); break;
            case "append": File.AppendAllText(sums, what + "\n"); break;
            case "twice": File.AppendAllText(sums, File.ReadAllLines(sums)[0] + "\n"); break;
            case "remove-folder": Directory.Delete(release, recursive: true); break;
        }
    }

    /// <summary>Each half of the release as its key in the register, its archive and its program in it.</summary>
    private static (string Half, string Zip, string Program)[] Programs()
    {
        var packages = Register().GetProperty("packages");
        return new[] { "gui", "cli" }.Select(half =>
        {
            var package = packages.GetProperty(half);
            return (half, package.GetProperty("zip").GetString()!, package.GetProperty("folder").GetString() + "/" + package.GetProperty("executable").GetString());
        }).ToArray();
    }

    private static string Installer() => Register().GetProperty("installer").GetProperty("file").GetString()!;

    private static JsonElement Register()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(SourceTree.Root(), "packaging", "components.json")));
        return document.RootElement.Clone();
    }

    /// <summary>The tag of the version this build carries, which the release commit dates in CHANGELOG.md.</summary>
    private static string Tag() =>
        "v" + typeof(FeedGuards).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

    private static (int Code, string Said) BuildFeeds(string tag, string release, string outDir) =>
        PwshScript.Run("packaging/build-feeds.ps1", TimeSpan.FromSeconds(120), "-Tag", tag, "-Release", release, "-OutDir", outDir);

    private static string Case() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bws-feeds-guard-" + Guid.NewGuid().ToString("N"))).FullName;

    private static string[] Left(string dir) => Directory.EnumerateFileSystemEntries(dir).Select(Path.GetFileName).OfType<string>().ToArray();

    private static string Sha256(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    /// <summary>
    /// The entries of the top-level Installers list of a WinGet manifest, each as its own lines, and the lines
    /// outside the list. Enough YAML for the files this project writes, not a parser.
    /// </summary>
    private static (List<string> Entries, string Outside) Installers(string manifest)
    {
        var lines = manifest.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        var start = Array.IndexOf(lines, "Installers:");
        Assert.True(start >= 0, $"The manifest has no Installers list:{Environment.NewLine}{manifest}");
        var end = Array.FindIndex(lines, start + 1, line => line.Length > 0 && line[0] != ' ' && line[0] != '-');
        end = end < 0 ? lines.Length : end;

        // An entry starts with a dash at the start of its line, and every line under it belongs to it.
        var entries = new List<string>();
        foreach (var line in lines[(start + 1)..end])
        {
            if (line.StartsWith("- ", StringComparison.Ordinal) || entries.Count == 0)
            {
                entries.Add(line);
                continue;
            }

            entries[^1] += "\n" + line;
        }

        return (entries, string.Join('\n', lines[..start].Concat(lines[end..])));
    }

    private static bool HasLine(string text, string wanted) =>
        text.Split('\n').Any(line => line.Trim().TrimStart('-').Trim() == wanted);

    /// <summary>A file of the repository with its whole-line comments taken out, so a word in the explanation of a line never counts as the line.</summary>
    private static string Text(string relative) =>
        string.Join('\n', File.ReadAllLines(Path.Combine(SourceTree.Root(), relative.Replace('/', Path.DirectorySeparatorChar))).Where(line => !line.TrimStart().StartsWith('#')));
}
