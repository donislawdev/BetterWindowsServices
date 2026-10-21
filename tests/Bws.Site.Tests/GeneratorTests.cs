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
public sealed class GeneratorTests : SiteTestBase
{
    // The session on the front page, which the poster tests plant a video in front of.
    private const string SessionImage = "<img src=\"/assets/bws-in-action.gif\"";

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

        var pages = Directory.EnumerateDirectories(Path.Combine(Copy, "site", "pages")).Count();
        var written = Directory.EnumerateFiles(Output, "*.html", SearchOption.AllDirectories).Count();

        // The number of languages is read from the file that says it, not written here: this
        // assertion used to say "two" and would have had to be edited for every language added.
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(Copy, "site", "site.json")));
        var languages = config.RootElement.GetProperty("languages").GetArrayLength();

        Assert.Equal(pages * languages, written);
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
    /// A video's still frame is an address too, and the check read only href and src until the
    /// front page got a video on 2026-09-24 - a poster pointing at nothing would have shipped as
    /// a black box with a play button on it.
    ///
    /// <b>The video is planted, since 2026-10-07</b>, when the front page went back to the GIF the
    /// README carries, by the owner's decision. The check still reads posters, so a video can come
    /// back without this guard having to be found again - and a test that relied on the page to
    /// hold one would have nothing left to damage.
    /// </summary>
    [Fact]
    public void A_poster_pointing_at_nothing_is_reported()
    {
        DamagePage("home", "en", SessionImage, "<video poster=\"/assets/bws-in-action.jpg\"></video>" + SessionImage);

        Assert.Contains(Build(), problem => problem.Contains("/assets/bws-in-action.jpg", StringComparison.Ordinal));
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
            "<img src=\"/assets/window-plan.png\"",
            "<img src=\"https://cdn.example.com/window-plan.png\"");

        var problems = Build();

        // The LOAD sentence, not the host name alone: an unlisted host is reported by a second
        // sentence that names it too, so "cdn.example.com" somewhere in the problems passed even
        // with the load check gone.
        Assert.Contains(problems, problem => problem.Contains("loads https://cdn.example.com/window-plan.png from another host", StringComparison.Ordinal));
    }

    /// <summary>A video's still frame is loaded like an image, so it is refused from another host the same way.</summary>
    [Fact]
    public void A_poster_loaded_from_another_host_is_refused()
    {
        DamagePage("home", "en", SessionImage, "<video poster=\"https://cdn.example.com/bws-in-action.png\"></video>" + SessionImage);

        Assert.Contains(Build(), problem => problem.Contains("loads https://cdn.example.com/bws-in-action.png from another host", StringComparison.Ordinal));
    }

    /// <summary>
    /// HTML takes an attribute name in any case and a value in single quotes as well as double,
    /// and a check that reads only one spelling passes the others without a word.
    /// </summary>
    [Fact]
    public void An_address_in_capitals_and_single_quotes_is_still_checked()
    {
        DamagePage("home", "en", "src=\"/assets/bws-in-action.gif\"", "SRC='/assets/bws-in-action.jpg'");

        Assert.Contains(Build(), problem => problem.Contains("/assets/bws-in-action.jpg", StringComparison.Ordinal));
    }

    /// <summary>An address that is not an address is a problem to report, not an exception that stops the build.</summary>
    [Fact]
    public void A_malformed_address_is_reported_rather_than_thrown()
    {
        DamagePage("home", "en", "href=\"/download/\"", "href=\"https://\"");

        // The sentence of THIS branch. "'https://'" alone is also what the relative-address branch
        // would print, so it passed whether or not the scheme was recognised at all.
        Assert.Contains(Build(), problem => problem.Contains("'https://', which is not an address a browser can follow", StringComparison.Ordinal));
    }

    /// <summary>
    /// A data attribute ends in src or href and is not an address - it is whatever a page keeps
    /// there. Read as one, a value that is not a path failed a strict build and with it the site.
    /// </summary>
    [Fact]
    public void A_data_attribute_is_not_read_as_an_address()
    {
        DamagePage("home", "en", "<img src=\"/assets/window-plan.png\"", "<img data-src=\"not-an-address\" data-href=\"#\" src=\"/assets/window-plan.png\"");

        Assert.DoesNotContain(Build(), problem => problem.Contains("not-an-address", StringComparison.Ordinal));
    }

    [Fact]
    public void A_key_missing_from_one_language_fails_rather_than_falling_back()
    {
        var path = Path.Combine(Copy, "site", "i18n", "pl.json");
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
            var path = Path.Combine(Copy, "site", "i18n", language + ".json");
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
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Copy, "site", "pages"), "*.html", SearchOption.AllDirectories))
        {
            File.WriteAllText(file, File.ReadAllText(file)
                .Replace("/sc-exe-and-powershell/", "/cli-reference/", StringComparison.Ordinal)
                .Replace("/pl/sc-exe-i-powershell/", "/pl/dokumentacja-cli/", StringComparison.Ordinal));
        }

        var definition = Path.Combine(Copy, "site", "pages", "sc-exe-and-powershell", "page.json");
        File.WriteAllText(definition, File.ReadAllText(definition).Replace("\"footer\": \"guides\",", "", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("sc-exe-and-powershell", StringComparison.Ordinal));
    }

    [Fact]
    public void Two_pages_sharing_an_address_are_reported()
    {
        var definition = Path.Combine(Copy, "site", "pages", "faq", "page.json");
        File.WriteAllText(definition, File.ReadAllText(definition).Replace("\"en\": \"faq\"", "\"en\": \"download\"", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("share the address", StringComparison.Ordinal));
    }

    [Fact]
    public void A_title_too_long_for_a_search_result_is_reported()
    {
        DamagePage(
            "download",
            "en",
            "<meta name=\"title\" content=\"Download Better Windows Services for Windows - free, zip or MSI\">",
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
        var path = Path.Combine(Copy, "README.md");
        var text = File.ReadAllText(path);
        var row = text.Split('\n').First(candidate => candidate.StartsWith("| `--dry-run`", StringComparison.Ordinal));
        File.WriteAllText(path, text.Replace(row + "\n", "", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("--dry-run", StringComparison.Ordinal) && problem.Contains("readme", StringComparison.Ordinal));
    }

    [Fact]
    public void An_exit_code_missing_from_the_readme_table_is_reported()
    {
        var path = Path.Combine(Copy, "README.md");
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
        var path = Path.Combine(Copy, "site", "pages", "faq", "en.html");
        File.WriteAllText(path, File.ReadAllText(path).Replace("<div class=\"qa\">", "<div class=\"questions\">", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("question page", StringComparison.Ordinal));
    }

    [Fact]
    public void The_structured_data_on_the_two_pages_that_carry_it_is_valid_json()
    {
        Build();

        foreach (var (file, type) in new[]
        {
            (Path.Combine(Output, "index.html"), "SoftwareApplication"),
            (Path.Combine(Output, "faq", "index.html"), "FAQPage"),
            (Path.Combine(Output, "pl", "index.html"), "SoftwareApplication"),
            (Path.Combine(Output, "pl", "faq", "index.html"), "FAQPage"),
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

        var sitemap = File.ReadAllText(Path.Combine(Output, "sitemap.xml"));
        var written = Directory
            .EnumerateFiles(Output, "*.html", SearchOption.AllDirectories)
            .Select(file => "/" + Path.GetRelativePath(Output, file).Replace('\\', '/'))
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

        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", File.ReadAllText(Path.Combine(Output, "404.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_custom_domain_is_written_where_pages_looks_for_it()
    {
        Build();

        Assert.Equal("betterwindowsservices.donislawdev.com\n", File.ReadAllText(Path.Combine(Output, "CNAME")));
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

        foreach (var file in Directory.EnumerateFiles(Output, "*.html", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("src=\"http", text, StringComparison.Ordinal);
            Assert.DoesNotContain("<link rel=\"stylesheet\" href=\"http", text, StringComparison.Ordinal);
        }
    }
}
