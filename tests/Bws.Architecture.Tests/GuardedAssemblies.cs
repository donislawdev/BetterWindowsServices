using System.Xml.Linq;

namespace Bws.Architecture.Tests;

/// <summary>
/// Locates the build output of the projects that ship to users.
///
/// The guards read compiled files instead of referencing the projects on purpose.
/// A test project that referenced them would add the very reference edges it exists
/// to forbid, and it could not inspect the WPF project at all, because that one
/// targets net10.0-windows while the test host runs on net10.0.
/// </summary>
internal static class GuardedAssemblies
{
    /// <summary>Projects whose output reaches users. tests/ and tools/ are out of scope.</summary>
    internal static readonly string[] Shipped = ["Bws.Core", "Bws.Cli", "Bws.Gui"];

    /// <summary>
    /// The name the compiled assembly carries, which since 2026-09-22 is not the project's for two
    /// of the three: <c>Bws.Cli</c> builds <c>bws</c> and <c>Bws.Gui</c> builds
    /// <c>BetterWindowsServices</c>, because those are the file names a user meets.
    ///
    /// Read out of the project file rather than written here a second time. A copy would agree
    /// with the project file on the day it was written and nothing would say when it stopped -
    /// and a layering guard asserting that the core never references <c>"Bws.Cli"</c> would then
    /// pass for the wrong reason, because no assembly of that name exists to be referenced.
    /// </summary>
    internal static string AssemblyNameOf(string projectName)
    {
        var projectFile = Path.Combine(SourceTree.Root(), "src", projectName, projectName + ".csproj");

        if (!File.Exists(projectFile))
        {
            throw new InvalidOperationException($"Project file not found: '{projectFile}'.");
        }

        var declared = XDocument.Load(projectFile)
            .Descendants()
            .Where(node => node.Name.LocalName == "AssemblyName")
            .Select(node => node.Value.Trim())
            .LastOrDefault(value => value.Length > 0);

        // MSBuild's own default: an assembly with no AssemblyName is called after its project.
        return declared ?? projectName;
    }

    internal static string PathOf(string projectName)
    {
        var binDirectory = Path.Combine(SourceTree.Root(), "src", projectName, "bin");

        if (!Directory.Exists(binDirectory))
        {
            throw new InvalidOperationException(
                $"No build output for {projectName} under '{binDirectory}'. " +
                "Build the whole solution before running the architecture guards.");
        }

        var fileName = AssemblyNameOf(projectName) + ".dll";

        // The exact path carries configuration, target framework and sometimes a runtime
        // identifier. Globbing and taking the freshest match survives all three changing.
        var newest = Directory
            .EnumerateFiles(binDirectory, fileName, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return newest ?? throw new InvalidOperationException(
            $"Found no {fileName} under '{binDirectory}'. " +
            "Build the whole solution before running the architecture guards.");
    }
}
