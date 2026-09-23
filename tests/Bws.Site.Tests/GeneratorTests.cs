using System.Text.Json;

namespace Bws.Site.Tests;

/// <summary>
/// The website generator, asked whether it NOTICES.
///
/// <b>Every test here breaks something on purpose.</b> A check that agrees with a correct input
/// proves nothing - it would pass just as happily if it examined nothing at all, which is
/// exactly how tools/audit/audit.ps1 went green for weeks in this repository after the thing it
/// read moved to another file. So each test copies the real site into a temporary directory,
/// damages one thing, and asserts the build says so.
///
/// The one test that does not break anything is the first: the site as it stands builds clean.
/// Without it, every other test here could pass against a generator that reports problems
/// unconditionally.
/// </summary>
public sealed class GeneratorTests : IDisposable
{
    private readonly string _copy;
    private readonly string _output;

    public GeneratorTests()
    {
        _copy = Path.Combine(Path.GetTempPath(), "bws-site-tests", Guid.NewGuid().ToString("n"));
        _output = Path.Combine(_copy, "out");
        CopySite(Repository.Root(), _copy);
    }

    public void Dispose()
    {
        if (Directory.Exists(_copy))
        {
            Directory.Delete(_copy, recursive: true);
        }
    }

    [Fact]
    public void The_site_as_it_stands_builds_with_nothing_to_report()
    {
        var problems = Build();

        Assert.True(problems.Count == 0, "The site in this repository has problems:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Every_page_is_written_in_every_language()
    {
        Build();

        var pages = Directory.EnumerateDirectories(Path.Combine(_copy, "site", "pages")).Count();
        var written = Directory.EnumerateFiles(_output, "*.html", SearchOption.AllDirectories).Count();

        Assert.Equal(pages * 2, written);
    }

    [Fact]
    public void A_token_nobody_defined_is_reported_rather_than_left_on_the_page()
    {
        DamagePage("home", "en", "{{releases}}", "{{releases_page}}");

        Assert.Contains(Build(), problem => problem.Contains("{{releases_page}}", StringComparison.Ordinal));
    }

    [Fact]
    public void A_link_to_a_page_that_does_not_exist_is_reported()
    {
        DamagePage("home", "en", "/services-msc-alternative/", "/services-msc-alternatives/");

        Assert.Contains(Build(), problem => problem.Contains("/services-msc-alternatives/", StringComparison.Ordinal));
    }

    /// <summary>
    /// The promise on every page's footer - that the site loads nothing from anywhere else - is
    /// the one this check exists for. A font or a script from another host would make that
    /// sentence false without changing how the page looks.
    /// </summary>
    [Fact]
    public void An_asset_loaded_from_another_host_is_refused()
    {
        DamagePage(
            "home",
            "en",
            "<img src=\"/assets/icon.svg\" alt=\"\" width=\"16\" height=\"16\">",
            "<img src=\"https://cdn.example.com/icon.svg\" alt=\"\" width=\"16\" height=\"16\">");

        var problems = Build();

        Assert.Contains(problems, problem => problem.Contains("cdn.example.com", StringComparison.Ordinal));
    }

    [Fact]
    public void A_key_missing_from_one_language_fails_rather_than_falling_back()
    {
        var path = Path.Combine(_copy, "site", "i18n", "pl.json");
        var text = File.ReadAllText(path);
        var line = text.Split('\n').First(candidate => candidate.Contains("\"exit.ok\"", StringComparison.Ordinal));
        File.WriteAllText(path, text.Replace(line + "\n", "", StringComparison.Ordinal));

        var problems = Build();

        Assert.Contains(problems, problem => problem.Contains("exit.ok", StringComparison.Ordinal));
    }

    /// <summary>
    /// The bridge to the program, in the direction that matters: a sentence about an exit code
    /// the program no longer has. The other direction - a code with no sentence - is covered by
    /// the missing-key test above, since the table asks for one key per code.
    /// </summary>
    [Fact]
    public void A_sentence_about_an_exit_code_the_program_does_not_have_is_reported()
    {
        foreach (var language in new[] { "en", "pl" })
        {
            var path = Path.Combine(_copy, "site", "i18n", language + ".json");
            File.WriteAllText(
                path,
                File.ReadAllText(path).Replace(
                    "\"exit.ok\":",
                    "\"exit.gonebad\": \"a code nobody has\",\n  \"exit.ok\":",
                    StringComparison.Ordinal));
        }

        Assert.Contains(Build(), problem => problem.Contains("exit.gonebad", StringComparison.Ordinal));
    }

    [Fact]
    public void A_page_nothing_links_to_is_reported()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(_copy, "site", "pages"), "*.html", SearchOption.AllDirectories))
        {
            File.WriteAllText(file, File.ReadAllText(file)
                .Replace("/sc-exe-and-powershell/", "/cli-reference/", StringComparison.Ordinal)
                .Replace("/pl/sc-exe-i-powershell/", "/pl/dokumentacja-cli/", StringComparison.Ordinal));
        }

        var definition = Path.Combine(_copy, "site", "pages", "sc-exe-and-powershell", "page.json");
        File.WriteAllText(definition, File.ReadAllText(definition).Replace("\"footer\": \"guides\",", "", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("sc-exe-and-powershell", StringComparison.Ordinal));
    }

    [Fact]
    public void Two_pages_sharing_an_address_are_reported()
    {
        var definition = Path.Combine(_copy, "site", "pages", "faq", "page.json");
        File.WriteAllText(definition, File.ReadAllText(definition).Replace("\"en\": \"faq\"", "\"en\": \"download\"", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("share the address", StringComparison.Ordinal));
    }

    [Fact]
    public void A_title_too_long_for_a_search_result_is_reported()
    {
        DamagePage(
            "download",
            "en",
            "<meta name=\"title\" content=\"Download Better Windows Services for Windows - free, no installer\">",
            "<meta name=\"title\" content=\"" + new string('x', 120) + "\">");

        Assert.Contains(Build(), problem => problem.Contains("title is 120 characters", StringComparison.Ordinal));
    }

    /// <summary>
    /// Backlog 391: the README's copy of two frozen contracts, which nothing read until this
    /// generator did.
    /// </summary>
    [Fact]
    public void A_switch_missing_from_the_readme_table_is_reported()
    {
        var path = Path.Combine(_copy, "README.md");
        var text = File.ReadAllText(path);
        var row = text.Split('\n').First(candidate => candidate.StartsWith("| `--dry-run`", StringComparison.Ordinal));
        File.WriteAllText(path, text.Replace(row + "\n", "", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("--dry-run", StringComparison.Ordinal) && problem.Contains("readme", StringComparison.Ordinal));
    }

    [Fact]
    public void An_exit_code_missing_from_the_readme_table_is_reported()
    {
        var path = Path.Combine(_copy, "README.md");
        var text = File.ReadAllText(path);
        var row = text.Split('\n').First(candidate => candidate.StartsWith("| `5` |", StringComparison.Ordinal));
        File.WriteAllText(path, text.Replace(row + "\n", "", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("exit code table", StringComparison.Ordinal));
    }

    /// <summary>
    /// The page's structured data has to describe what the visitor reads, and the questions are
    /// therefore lifted out of the rendered page. A question block that stops matching would
    /// leave a page declaring itself a question page and carrying no questions.
    /// </summary>
    [Fact]
    public void A_question_page_with_no_questions_left_in_it_is_reported()
    {
        var path = Path.Combine(_copy, "site", "pages", "faq", "en.html");
        File.WriteAllText(path, File.ReadAllText(path).Replace("<div class=\"qa\">", "<div class=\"questions\">", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("question page", StringComparison.Ordinal));
    }

    [Fact]
    public void The_structured_data_on_the_two_pages_that_carry_it_is_valid_json()
    {
        Build();

        foreach (var (file, type) in new[]
        {
            (Path.Combine(_output, "index.html"), "SoftwareApplication"),
            (Path.Combine(_output, "faq", "index.html"), "FAQPage"),
            (Path.Combine(_output, "pl", "index.html"), "SoftwareApplication"),
            (Path.Combine(_output, "pl", "faq", "index.html"), "FAQPage"),
        })
        {
            var text = File.ReadAllText(file);
            var start = text.IndexOf("<script type=\"application/ld+json\">", StringComparison.Ordinal);
            Assert.True(start >= 0, $"{file} carries no structured data.");

            var body = text[(start + "<script type=\"application/ld+json\">".Length)..];
            body = body[..body.IndexOf("</script>", StringComparison.Ordinal)];

            using var document = JsonDocument.Parse(body);
            Assert.Equal(type, document.RootElement.GetProperty("@type").GetString());
        }
    }

    /// <summary>
    /// Everything a page says about which addresses exist has to agree: the canonical, the
    /// alternates and the sitemap. They are written by three different pieces of code from one
    /// set of definitions, and a disagreement between them is what tells a search engine the
    /// site is two sites.
    /// </summary>
    [Fact]
    public void The_sitemap_names_every_page_it_wrote_and_nothing_else()
    {
        Build();

        var sitemap = File.ReadAllText(Path.Combine(_output, "sitemap.xml"));
        var written = Directory
            .EnumerateFiles(_output, "*.html", SearchOption.AllDirectories)
            .Select(file => "/" + Path.GetRelativePath(_output, file).Replace('\\', '/'))
            .Select(path => path.EndsWith("/index.html", StringComparison.Ordinal) ? path[..^"index.html".Length] : path)
            .ToList();

        foreach (var page in written.Where(page => !page.EndsWith("404.html", StringComparison.Ordinal)))
        {
            Assert.Contains("<loc>https://betterwindowsservices.donislawdev.com" + page + "</loc>", sitemap, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("404", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void The_not_found_page_refuses_to_be_indexed()
    {
        Build();

        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", File.ReadAllText(Path.Combine(_output, "404.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_custom_domain_is_written_where_pages_looks_for_it()
    {
        Build();

        Assert.Equal("betterwindowsservices.donislawdev.com\n", File.ReadAllText(Path.Combine(_output, "CNAME")));
    }

    /// <summary>
    /// The product promises it never talks to the internet, and the site has to be able to say
    /// the same about itself. A stylesheet, a font or a script from anywhere else would be the
    /// quiet way that stops being true.
    /// </summary>
    [Fact]
    public void No_built_page_loads_anything_from_another_host()
    {
        Build();

        foreach (var file in Directory.EnumerateFiles(_output, "*.html", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("src=\"http", text, StringComparison.Ordinal);
            Assert.DoesNotContain("<link rel=\"stylesheet\" href=\"http", text, StringComparison.Ordinal);
        }
    }

    private IReadOnlyList<string> Build()
    {
        var build = new Build(_copy, _output);
        build.Run();
        return build.Problems;
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
    private void DamagePage(string id, string language, string from, string to)
    {
        var path = Path.Combine(_copy, "site", "pages", id, language + ".html");
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
