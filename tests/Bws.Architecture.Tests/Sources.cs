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
    /// <summary>
    /// How long any guard may spend matching one line before it gives up.
    ///
    /// These patterns run over our own files, so nothing hostile reaches them and in principle
    /// none of them can run away. The ceiling is here anyway, and the reason is the one this
    /// project already paid for once: without a limit, a mistake in a pattern <b>hangs the test
    /// run instead of reddening it</b>, and a run that never ends reports nothing at all.
    ///
    /// Generous on purpose. The job is to stop a hang, not to time anything.
    /// </summary>
    internal static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(5);

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
