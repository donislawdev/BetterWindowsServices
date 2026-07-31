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

    private static IReadOnlyList<string> DeclaredProjectReferences(string projectName)
    {
        var projectFile = Path.Combine(
            GuardedAssemblies.RepositoryRoot(), "src", projectName, projectName + ".csproj");

        if (!File.Exists(projectFile))
        {
            throw new InvalidOperationException($"Project file not found: '{projectFile}'.");
        }

        return XDocument.Load(projectFile)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .ToList();
    }
}
