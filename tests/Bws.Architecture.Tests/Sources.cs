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

    /// <summary>Every C# file that ends up in the product.</summary>
    internal static IEnumerable<string> Shipped() => Under("src", "*.cs");

    /// <summary>Every C# file that tests the product.</summary>
    internal static IEnumerable<string> Testing() => Under("tests", "*.cs");

    /// <summary>
    /// Every XAML file that ends up in the product.
    ///
    /// Kept apart from <see cref="Shipped"/> rather than folded into it, because the two are held
    /// to different ceilings - the longest markup file here is two hundred and sixty lines past the
    /// longest C# one, so one set would either forgive the C# number or demand the theme be split
    /// on the day markup started being counted. Owner's decision 2026-08-10, backlog 132.
    ///
    /// <b>This method is why the name of the one above changed.</b> "Every source file that ends up
    /// in the product" was true of neither once markup was in the tree, and a filter whose name
    /// overstates its reach is how a guard ends up reading the wrong set of files.
    /// </summary>
    internal static IEnumerable<string> ShippedMarkup() => Under("src", "*.xaml");

    /// <summary>
    /// Every file that can carry an MSBuild property for this product.
    ///
    /// The whole tree rather than src/ alone, and that is deliberate: a test project pulling a
    /// vulnerable package in, or switching an analyser off, is the same problem wearing a different
    /// hat, and this repository has six of them against three shipped projects. Moved here from
    /// SupplyChainGuards on 2026-09-23, when AnalyzerRuleGuards became the second guard reading it.
    /// </summary>
    internal static IEnumerable<string> BuildFiles() =>
        new[] { "*.csproj", "*.props", "*.targets" }
            .SelectMany(pattern => Directory.EnumerateFiles(SourceTree.Root(), pattern, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            // tools/ is outside version control and outside the product, and its probe projects
            // are throwaway. A guard reading them would fail a clone that has no tools directory
            // at all, which is every clone but this one.
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tools{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static IEnumerable<string> Under(string folder, string pattern) =>
        Directory
            .EnumerateFiles(Path.Combine(SourceTree.Root(), folder), pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
