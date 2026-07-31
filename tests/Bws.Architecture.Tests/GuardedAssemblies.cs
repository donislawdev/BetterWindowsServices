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

    internal static string PathOf(string projectName)
    {
        var binDirectory = Path.Combine(RepositoryRoot(), "src", projectName, "bin");

        if (!Directory.Exists(binDirectory))
        {
            throw new InvalidOperationException(
                $"No build output for {projectName} under '{binDirectory}'. " +
                "Build the whole solution before running the architecture guards.");
        }

        // The exact path carries configuration, target framework and sometimes a runtime
        // identifier. Globbing and taking the freshest match survives all three changing.
        var newest = Directory
            .EnumerateFiles(binDirectory, projectName + ".dll", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return newest ?? throw new InvalidOperationException(
            $"Found no {projectName}.dll under '{binDirectory}'. " +
            "Build the whole solution before running the architecture guards.");
    }

    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException(
            "Repository root not found: no .git directory above the test output directory.");
    }
}
