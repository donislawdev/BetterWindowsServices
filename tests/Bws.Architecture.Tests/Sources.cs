namespace Bws.Architecture.Tests;

/// <summary>
/// Which files the guards read.
///
/// One place, because two guards were each carrying their own copy of "everything under src that
/// is not in obj or bin", and two more were about to. Every copy of a filter is another chance
/// for one of them to quietly stop covering a folder, and a guard reading the wrong set of
/// files passes for the same reason a guard reading no files passes.
/// </summary>
internal static class Sources
{
    /// <summary>Every source file that ends up in the product.</summary>
    internal static IEnumerable<string> Shipped() => Under("src");

    /// <summary>Every source file that tests the product.</summary>
    internal static IEnumerable<string> Testing() => Under("tests");

    private static IEnumerable<string> Under(string folder) =>
        Directory
            .EnumerateFiles(Path.Combine(GuardedAssemblies.RepositoryRoot(), folder), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
