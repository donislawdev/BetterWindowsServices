using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// The register of what this product ships that somebody else wrote, held against the build.
///
/// <b>packaging/components.json is one file with three renderings, and that is what makes it
/// worth guarding.</b> The SPDX document published beside every release archive is generated
/// from it, THIRD-PARTY-NOTICES.md carries the same set with the full licence texts, and
/// <c>bws license --components</c> prints it from inside the executable for a machine with no
/// internet. One register means they cannot disagree. It also means a mistake in it is a
/// mistake in all three.
///
/// <b>Why the list is not taken from a scan of the release, which is the obvious alternative.</b>
/// Both programs publish as a SINGLE self-contained file. WPF, the .NET runtime and WPF-UI are
/// inside that file with no package metadata left anywhere, so a scanner over the archive would
/// report two executables and assign a licence to neither. A curated register is the only thing
/// that can carry the licences, and the job of every check here is to police it rather than to
/// replace it.
///
/// <b>What this cannot do, said so a green run is not read as more than it is.</b> It compares
/// names, versions and bytes. It does not read anybody's licence and cannot tell whether the
/// terms changed between versions - the same limit LicenceNoticeGuards states about itself, and
/// the same answer: that is a question for a person, and the notices file says when one last
/// looked. The register-against-the-publish-manifest direction lives in
/// <c>packaging/build-dist.ps1</c>, because only a real publish writes that manifest.
/// </summary>
public sealed class ComponentRegisterGuards
{
    private const string RegisterPath = "packaging/components.json";
    private const string Notices = "THIRD-PARTY-NOTICES.md";

    private static JsonDocument Register()
    {
        var path = Path.Combine(SourceTree.Root(), RegisterPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(
            File.Exists(path),
            $"There is no {RegisterPath}. It is the source of the bill of materials attached to "
            + "every release, of the notices file, and of what the program answers about itself.");

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static IEnumerable<JsonElement> Components(JsonDocument register) =>
        register.RootElement.GetProperty("components").EnumerateArray();

    [Fact]
    public void Every_component_carries_what_a_bill_of_materials_needs()
    {
        using var register = Register();
        var missing = new List<string>();

        foreach (var component in Components(register))
        {
            var name = component.GetProperty("name").GetString() ?? "(unnamed)";

            foreach (var field in new[] { "name", "kind", "license_declared", "license_concluded", "supplier", "source", "notice", "in" })
            {
                if (!component.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
                {
                    missing.Add($"  {name} has no {field}");
                }
            }

            // EXACTLY ONE OF THE TWO, and the distinction is the reason this register can be
            // true on two machines at once. A component we reference states its version, because
            // we chose it and a bump has to be noticed. One the SDK resolves does not, because
            // its version is whatever .NET built the file - and a literal there would be a
            // number that is right here and wrong on the build agent.
            var stated = component.TryGetProperty("version", out _);
            var fromBuild = component.TryGetProperty("version_from", out _);

            if (stated == fromBuild)
            {
                missing.Add($"  {name} must carry exactly one of version and version_from");
            }
        }

        Assert.True(
            missing.Count == 0,
            $"{RegisterPath} is the source of a document that goes out with a release, and a "
            + "malformed one is worse than none because it looks like an answer:"
            + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Every_package_that_ships_is_in_the_register()
    {
        using var register = Register();

        var named = Components(register)
            .Select(component => component.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // THE DIRECTION THAT FINDS THINGS. A package we chose is in a project file and hard to
        // forget. One arriving as somebody else's dependency arrives with no prompt at all,
        // ships, and creates the same obligation - WPF-UI.Abstractions is in this register
        // because a person noticed, not because anything asked.
        //
        // Asked of LicenceNoticeGuards rather than worked out again here: "which packages
        // actually ship" is a question with four false answers in this tree, and one
        // implementation is the only way both checks keep giving the same one.
        var shipping = new[] { "Bws.Core", "Bws.Cli", "Bws.Gui" }
            .SelectMany(LicenceNoticeGuards.ShippingAssetsOf)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(package => !named.Contains(package))
            .ToList();

        Assert.True(
            shipping.Count == 0,
            "These packages put an assembly into the build output and are not in "
            + $"{RegisterPath}. Read the licence in the package on disk - not the label on its "
            + "listing - then add it to the register AND to the notices:"
            + Environment.NewLine + string.Join(Environment.NewLine, shipping.Select(name => "  " + name)));
    }

    [Fact]
    public void The_register_names_no_package_that_stopped_shipping()
    {
        using var register = Register();

        var shipping = new[] { "Bws.Core", "Bws.Cli", "Bws.Gui" }
            .SelectMany(LicenceNoticeGuards.ShippingAssetsOf)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // The other direction, and the one that rots quietly. Only the components that arrive as
        // packages can be checked this way: a runtime pack is resolved by the SDK and is in no
        // package graph at all, which is what build-dist.ps1 checks against the real manifest.
        var gone = Components(register)
            .Where(component => component.GetProperty("kind").GetString() == "nuget")
            .Select(component => component.GetProperty("name").GetString()!)
            .Where(name => !shipping.Contains(name))
            .ToList();

        Assert.True(
            gone.Count == 0,
            $"{RegisterPath} lists packages that no shipped project carries any more. A register "
            + "describing a build nobody makes is worse than none:"
            + Environment.NewLine + string.Join(Environment.NewLine, gone.Select(name => "  " + name)));
    }

    [Fact]
    public void Every_stated_version_is_the_version_the_build_resolved()
    {
        using var register = Register();
        var resolved = ResolvedPackages();
        var wrong = new List<string>();

        foreach (var component in Components(register))
        {
            if (!component.TryGetProperty("version", out var stated))
            {
                continue;
            }

            var name = component.GetProperty("name").GetString()!;

            if (!resolved.TryGetValue(name, out var built))
            {
                wrong.Add($"  {name} states {stated.GetString()} and nothing in this tree resolves it");
                continue;
            }

            if (!string.Equals(stated.GetString(), built, StringComparison.OrdinalIgnoreCase))
            {
                wrong.Add($"  {name}: the register says {stated.GetString()} and the build resolved {built}");
            }
        }

        Assert.True(
            wrong.Count == 0,
            "A bill of materials describing a different build than the one in the archive is "
            + "worse than no bill of materials. Update the register:"
            + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    public void Every_pinned_binary_still_hashes_to_what_the_register_pins()
    {
        using var register = Register();
        var resolved = ResolvedPackages();
        var packageRoot = PackageFolder();
        var checkedFiles = 0;
        var wrong = new List<string>();

        foreach (var component in Components(register))
        {
            if (!component.TryGetProperty("files", out var files))
            {
                continue;
            }

            var name = component.GetProperty("name").GetString()!;

            // The version from the BUILD rather than from the register, so that a bump moves this
            // check to the file that moved instead of leaving it on one nobody ships. The version
            // itself is held to the register by the guard above, so the two cannot drift apart
            // without one of them going red.
            Assert.True(resolved.ContainsKey(name), $"{name} carries a pin and nothing in this tree resolves it.");

            foreach (var file in files.EnumerateArray())
            {
                var relative = file.GetProperty("package_path").GetString()!;
                var path = Path.Combine(
                    packageRoot,
                    name.ToLowerInvariant(),
                    resolved[name],
                    relative.Replace('/', Path.DirectorySeparatorChar));

                // FAILS CLOSED. A pin that cannot find its file is a check that passes by reading
                // nothing, which is the failure mode this repository refuses everywhere else.
                Assert.True(
                    File.Exists(path),
                    $"The register pins {relative} of {name} {resolved[name]} and there is no such "
                    + $"file at '{path}'. Restore writes it, so either the package layout changed "
                    + "or these guards are running against a tree nobody restored.");

                var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

                if (!string.Equals(actual, file.GetProperty("sha256").GetString(), StringComparison.Ordinal))
                {
                    wrong.Add($"  {name} {resolved[name]} {relative}"
                        + $"{Environment.NewLine}    pinned {file.GetProperty("sha256").GetString()}"
                        + $"{Environment.NewLine}    actual {actual}");
                }

                checkedFiles++;
            }
        }

        // A sweep that read nothing finds nothing and reports it in the same green as one that
        // read everything. Two files carry a pin today and the register says why only those two.
        Assert.True(checkedFiles > 0, "No pinned file was checked at all, so this guard proved nothing.");

        Assert.True(
            wrong.Count == 0,
            "A shipped binary does not hash to what this repository pins. Either the version moved "
            + "and the register has not - take the new hash from the package on disk - or somebody "
            + "replaced a file on this machine:"
            + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    [Fact]
    public void Every_component_is_named_in_the_notices()
    {
        using var register = Register();
        var text = File.ReadAllText(Path.Combine(SourceTree.Root(), Notices));
        var missing = new List<string>();

        foreach (var component in Components(register))
        {
            foreach (var field in new[] { "name", "notice" })
            {
                var value = component.GetProperty(field).GetString()!;

                if (!text.Contains(value, StringComparison.OrdinalIgnoreCase))
                {
                    missing.Add($"  {value} (the {field} of {component.GetProperty("name").GetString()})");
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            $"The register ships these and {Notices} does not name them. The licences on the "
            + "borrowed code are what require the notice to travel with the program, and the "
            + "register is not that notice - it is a list:"
            + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Every_licence_identifier_that_is_not_on_the_spdx_list_is_defined()
    {
        using var register = Register();
        var defined = register.RootElement.GetProperty("license_refs")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        var used = Components(register)
            .SelectMany(component => new[]
            {
                component.GetProperty("license_declared").GetString()!,
                component.GetProperty("license_concluded").GetString()!
            })
            .Where(licence => licence.StartsWith("LicenseRef-", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Where(licence => !defined.Contains(licence))
            .ToList();

        Assert.True(
            used.Count == 0,
            "A document using one of these would not validate, and an invalid bill of materials "
            + "looks like an answer:" + Environment.NewLine + string.Join(Environment.NewLine, used));
    }

    [Fact]
    public void Every_package_the_register_describes_is_a_project_that_exists()
    {
        using var register = Register();
        var wrong = new List<string>();

        foreach (var package in register.RootElement.GetProperty("packages").EnumerateObject())
        {
            var project = package.Value.GetProperty("project").GetString()!;
            var path = Path.Combine(SourceTree.Root(), project.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(path))
            {
                wrong.Add($"  {package.Name}: there is no project at {project}");
                continue;
            }

            // THE EXECUTABLE NAME IS A FROZEN CONTRACT - docs/02 lists it, and scripts and
            // scheduled tasks call these files by name. The register writes it down to build the
            // archive, which makes it a second copy, so it is held against the one the assembly
            // actually carries.
            var expected = Regex
                .Match(File.ReadAllText(path), @"<AssemblyName>([^<]+)</AssemblyName>", RegexOptions.None, Sources.Ceiling)
                .Groups[1].Value;
            var stated = package.Value.GetProperty("executable").GetString()!;

            if (!string.Equals(stated, expected + ".exe", StringComparison.OrdinalIgnoreCase))
            {
                wrong.Add($"  {package.Name}: the register packs {stated} and {project} builds {expected}.exe");
            }
        }

        Assert.True(
            wrong.Count == 0,
            "The register would build an archive around a file that is not there:"
            + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    /// <summary>
    /// Every package the restore resolved, as name to version.
    ///
    /// Read out of the assets file the build writes, which is the only place the resolved graph
    /// is written down. Runtime packs are deliberately absent from it: the SDK resolves those and
    /// they are in no package graph, which is why the register marks their versions as the
    /// build's to decide and build-dist.ps1 checks them against the publish manifest instead.
    /// </summary>
    private static Dictionary<string, string> ResolvedPackages()
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in new[] { "Bws.Core", "Bws.Cli", "Bws.Gui" })
        {
            var assets = Path.Combine(SourceTree.Root(), "src", project, "obj", "project.assets.json");

            Assert.True(File.Exists(assets), $"There is no '{assets}'. Restore writes it, so this guard would read nothing.");

            foreach (Match match in Regex.Matches(
                File.ReadAllText(assets),
                @"""([A-Za-z][A-Za-z0-9._-]*)/(\d+\.\d+\.\d+[^""]*)""\s*:\s*\{",
                RegexOptions.None,
                Sources.Ceiling))
            {
                resolved[match.Groups[1].Value] = match.Groups[2].Value;
            }
        }

        return resolved;
    }

    /// <summary>
    /// Where NuGet put the packages, read out of the assets file rather than assembled from a
    /// profile path - a build agent moves it with NUGET_PACKAGES, and a guessed path would make
    /// the hash check quietly check nothing.
    /// </summary>
    private static string PackageFolder()
    {
        var assets = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "obj", "project.assets.json");

        using var document = JsonDocument.Parse(File.ReadAllText(assets));

        foreach (var folder in document.RootElement.GetProperty("packageFolders").EnumerateObject())
        {
            if (Directory.Exists(folder.Name))
            {
                return folder.Name;
            }
        }

        Assert.Fail($"'{assets}' names no package folder that exists on this machine.");

        return string.Empty;
    }
}
