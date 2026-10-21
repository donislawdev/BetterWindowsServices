namespace Bws.Site.Tests;

/// <summary>
/// The sandbox every generator test works in: a copy of the real site in a temporary directory,
/// which a test damages in one place before asking the build whether it noticed.
///
/// <b>Split out of GeneratorTests on 2026-10-21</b> when the languages' tests took that file over
/// the ceiling the size ratchet holds test files to. The ceiling says split it, and the seam is
/// the one the file already had: what a test needs in order to break something, and the tests that
/// do the breaking.
/// </summary>
public abstract class SiteTestBase : IDisposable
{
    protected SiteTestBase()
    {
        Copy = Path.Combine(Path.GetTempPath(), "bws-site-tests", Guid.NewGuid().ToString("n"));
        Output = Path.Combine(Copy, "out");
        CopySite(Repository.Root(), Copy);
    }

    /// <summary>The temporary copy of the repository's site this test works on.</summary>
    protected string Copy { get; }

    /// <summary>Where the build writes.</summary>
    protected string Output { get; }

    public void Dispose()
    {
        if (Directory.Exists(Copy))
        {
            Directory.Delete(Copy, recursive: true);
        }
    }

    protected IReadOnlyList<string> Build()
    {
        var build = new Build(Copy, Output);
        build.Run();
        return build.Problems;
    }

    /// <summary>Adds a language to the published list, whatever the list holds now.</summary>
    protected void Publish(string code) =>
        EditSite(text => System.Text.RegularExpressions.Regex.Replace(
            text,
            "(\"languages\"\\s*:\\s*\\[[^\\]]*)\\]",
            "$1, \"" + code + "\"]",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1)));

    /// <summary>Adds an invented language to the registry, so a test never depends on a real one being published or not.</summary>
    protected void Register(string code, string dir, string slugs) =>
        EditSite(text => text.Replace(
            "\"language_registry\": {",
            "\"language_registry\": {\n    \"" + code + "\": { \"tag\": \"" + code + "\", \"locale\": \"" + code + "_XX\", \"name\": \"Test language\", \"short\": \"" + code.ToUpperInvariant() + "\", \"dir\": \"" + dir + "\", \"slugs\": \"" + slugs + "\" },",
            StringComparison.Ordinal));

    protected void EditSite(Func<string, string> edit)
    {
        var path = Path.Combine(Copy, "site", "site.json");
        var before = File.ReadAllText(path);
        var after = edit(before);

        // Same rule as the page damage: an edit that changed nothing would let the test pass
        // against a generator that checks nothing.
        Assert.NotEqual(before, after);
        File.WriteAllText(path, after);
    }

    protected string StampLine(string id, string language)
    {
        var text = File.ReadAllText(Path.Combine(Copy, "site", "pages", id, language + ".html"));
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            "<meta name=\"translated-from\" content=\"[^\"]*\">",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1));
        Assert.True(match.Success, $"{id}/{language}.html carries no stamp to remove");
        return match.Value;
    }

    /// <summary>
    /// Breaks one thing in one page fragment.
    ///
    /// <b>The path is built from segments rather than written as one string</b>, and that is not
    /// a style preference. A page folder in this project is named after the page, one of them is
    /// the front page, and a slash-separated path through that folder ending in a file carries
    /// the shape of a unix home directory inside it - which PublicSurfaceGuards sweeps every
    /// published file for. It went red the first run this file reached CI, and red again on the
    /// comment that first tried to explain it by quoting the path. The guard is right both
    /// times: weakening a privacy sweep to fit a test path would be the wrong half of the trade.
    /// </summary>
    protected void DamagePage(string id, string language, string from, string to)
    {
        var path = Path.Combine(Copy, "site", "pages", id, language + ".html");
        var text = File.ReadAllText(path);

        // The damage has to land, or the test would pass against a generator that checks
        // nothing - the assertion would simply never be reached with anything broken.
        Assert.Contains(from, text, StringComparison.Ordinal);
        File.WriteAllText(path, text.Replace(from, to, StringComparison.Ordinal));
    }

    private static void CopySite(string root, string target)
    {
        foreach (var relative in new[] { "site", "README.md", "Directory.Build.props", "BetterWindowsServices.slnx" })
        {
            var source = Path.Combine(root, relative);
            if (File.Exists(source))
            {
                Directory.CreateDirectory(target);
                File.Copy(source, Path.Combine(target, relative));
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                // bin and obj under site/Bws.Site are the generator's own build output and
                // copying them would take seconds and megabytes for nothing.
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                var destination = Path.Combine(target, Path.GetRelativePath(root, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
        }

        // The exit codes and the switches are read out of the shipped source as text, so the
        // copy needs those two files and nothing else from src.
        foreach (var relative in new[] { "src/Bws.Cli/ExitCode.cs", "src/Bws.Cli/OptionSurface.cs" })
        {
            var destination = Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), destination);
        }
    }
}

internal static class Repository
{
    internal static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BetterWindowsServices.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No repository root above " + AppContext.BaseDirectory);
    }
}
