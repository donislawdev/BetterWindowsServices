using System.Xml.Linq;

namespace Bws.Architecture.Tests;

/// <summary>
/// Guards the dependency direction as it is DECLARED in the project files.
///
/// This exists because of a hole found by breaking the rule on purpose, on
/// 2026-08-01: a project reference was added from Bws.Gui to Bws.Cli, the build
/// stayed green, and every metadata guard still passed. The reason is that the C#
/// compiler omits assembly references the assembly does not actually use. Nothing
/// from Bws.Cli was called yet, so the compiled file carried no trace of it.
///
/// Metadata guards and declaration guards catch different things and neither one
/// replaces the other:
///   - metadata catches real usage, including paths nobody declared on purpose,
///   - declarations catch the reference that was added but not used yet, which is
///     exactly how this rule would erode: one harmless-looking reference at a time.
/// </summary>
public sealed class DeclaredReferenceGuards
{
    /// <summary>What each shipped project is allowed to reference, by project name.</summary>
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["Bws.Core"] = [],
        ["Bws.Cli"] = ["Bws.Core"],
        ["Bws.Gui"] = ["Bws.Core"]
    };

    [Theory]
    [InlineData("Bws.Core")]
    [InlineData("Bws.Cli")]
    [InlineData("Bws.Gui")]
    public void A_shipped_project_declares_only_the_references_it_is_allowed(string projectName)
    {
        var declared = DeclaredProjectReferences(projectName);
        var permitted = Allowed[projectName];

        var forbidden = declared.Except(permitted, StringComparer.OrdinalIgnoreCase).ToArray();

        Assert.True(
            forbidden.Length == 0,
            $"{projectName} declares a reference it must not have: {string.Join(", ", forbidden)}. " +
            $"Allowed: [{string.Join(", ", permitted)}]. " +
            "Direction of dependencies is fixed by ADR-3 and 06-STRUKTURA-I-KONWENCJE, part 2.");
    }

    /// <summary>
    /// What each shipped project may take from outside, by package name.
    ///
    /// Dependencies are rule 6 of `CLAUDE.md` and `ADR-15`: this tool runs with administrator
    /// rights on production machines, so a short list is an argument rather than housekeeping.
    /// The reference guard above only reads project references, so before this existed a
    /// package could arrive in the core and nothing would say a word.
    ///
    /// Added 2026-08-02, on the day a test-only package went in and the claim "it never
    /// reaches anything that ships" was made out loud. A claim like that needs something
    /// keeping it true.
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedPackages = new()
    {
        ["Bws.Core"] = ["Microsoft.Windows.CsWin32"],
        ["Bws.Cli"] = [],

        // WPF-UI, MIT, added 2026-08-02 with the owner's decision to build this window's
        // appearance on it. Registered in ADR-15 first and listed here second, which is the
        // order this guard exists to force - and it did force it, going red on the build that
        // added the package before the document was written.
        //
        // It is the FIRST package this project ships. The two before it are a source generator
        // and an analyser, neither of which leaves anything behind at run time. This one is
        // 6.3 MB of assembly inside the executable, on a machine where the tool runs with
        // administrator rights. Its licence was read in the repository's own LICENSE file, and
        // what it costs is measured in docs/10-WPF-UI.md rather than assumed.
        ["Bws.Gui"] = ["WPF-UI"]
    };

    [Theory]
    [InlineData("Bws.Core")]
    [InlineData("Bws.Cli")]
    [InlineData("Bws.Gui")]
    public void A_shipped_project_takes_only_the_packages_it_is_allowed(string projectName)
    {
        // The build-wide analyser is deliberately not on this list. It lives in
        // Directory.Build.props with PrivateAssets, produces no assembly and ends up in
        // nothing - so counting it here would make the list about where a line of XML sits
        // rather than about what the product carries.
        var declared = Declared(projectName, "PackageReference");
        var permitted = AllowedPackages[projectName];

        var forbidden = declared.Except(permitted, StringComparer.OrdinalIgnoreCase).ToArray();

        Assert.True(
            forbidden.Length == 0,
            $"{projectName} takes a package it is not allowed: {string.Join(", ", forbidden)}. " +
            $"Allowed: [{string.Join(", ", permitted)}]. A dependency here runs with " +
            "administrator rights on somebody's production machine, so it needs an entry in " +
            "ADR-15 and a line on this list, in that order.");
    }

    [Fact]
    public void The_core_declares_no_project_references_at_all()
    {
        // Stated separately from the table above because it is the rule that erodes
        // first and the one whose breach is hardest to notice by reading code.
        var declared = DeclaredProjectReferences("Bws.Core");

        Assert.True(
            declared.Count == 0,
            $"Bws.Core references other projects: {string.Join(", ", declared)}. " +
            "The core sits at the bottom and knows nothing above it (ADR-3).");
    }

    private static IReadOnlyList<string> DeclaredProjectReferences(string projectName) =>
        [.. Declared(projectName, "ProjectReference")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))];

    private static IReadOnlyList<string> Declared(string projectName, string element)
    {
        var projectFile = Path.Combine(
            GuardedAssemblies.RepositoryRoot(), "src", projectName, projectName + ".csproj");

        if (!File.Exists(projectFile))
        {
            throw new InvalidOperationException($"Project file not found: '{projectFile}'.");
        }

        return XDocument.Load(projectFile)
            .Descendants()
            .Where(node => node.Name.LocalName == element)
            .Select(node => (string?)node.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .ToList();
    }
}
