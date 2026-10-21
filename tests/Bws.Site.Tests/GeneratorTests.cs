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
    // The session on the front page, which the poster tests plant a video in front of.
    private const string SessionImage = "<img src=\"/assets/bws-in-action.gif\"";

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

        // The number of languages is read from the file that says it, not written here: this
        // assertion used to say "two" and would have had to be edited for every language added.
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(_copy, "site", "site.json")));
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

    [Fact]
    public void A_translation_with_a_different_shape_from_the_original_is_reported()
    {
        // A class is the cheapest damage that is still a difference in structure: the page
        // looks the same to a reader and is not the page the original is.
        DamagePage("faq", "pl", "<section class=\"band page-title\">", "<section class=\"band page-title wrong\">");

        Assert.Contains(Build(), problem => problem.Contains("parity: faq/pl.html does not have the shape", StringComparison.Ordinal));
    }

    [Fact]
    public void A_translation_that_changes_a_command_is_reported()
    {
        DamagePage("faq", "pl", "<code>start:auto !status:running</code>", "<code>start:auto !status:runing</code>");

        Assert.Contains(Build(), problem => problem.Contains("parity: faq/pl.html has different code", StringComparison.Ordinal));
    }

    [Fact]
    public void A_translation_that_loses_a_token_is_reported()
    {
        DamagePage("home", "pl", "{{releases}}", "https://github.com");

        Assert.Contains(Build(), problem => problem.Contains("parity: home/pl.html uses different tokens", StringComparison.Ordinal));
    }

    [Fact]
    public void A_translation_without_a_stamp_is_a_problem()
    {
        DamagePage("home", "pl", StampLine("home", "pl"), "");

        Assert.Contains(Build(), problem => problem.Contains("freshness: home/pl.html has no translated-from stamp", StringComparison.Ordinal));
    }

    /// <summary>
    /// The owner's decision of 2026-10-21 in one test: English moves on, the translation is
    /// LISTED and the build is still clean - and the same list is a failure when asked for.
    /// </summary>
    [Fact]
    public void A_translation_older_than_the_english_is_listed_and_fails_only_when_asked()
    {
        var english = Path.Combine(_copy, "site", "pages", "home", "en.html");
        File.AppendAllText(english, "<!-- the English moved on -->");

        var plain = new Build(_copy, _output);
        plain.Run();
        Assert.Empty(plain.Problems);
        Assert.Contains(plain.Stale, line => line.Contains("home/pl.html", StringComparison.Ordinal));

        var strict = new Build(_copy, _output, requireFresh: true);
        strict.Run();
        Assert.Contains(strict.Problems, problem => problem.Contains("freshness: home/pl.html", StringComparison.Ordinal));
    }

    [Fact]
    public void Stamping_a_language_makes_it_fresh_and_a_second_stamp_changes_nothing()
    {
        File.AppendAllText(Path.Combine(_copy, "site", "pages", "home", "en.html"), "<!-- the English moved on -->");

        var site = SiteConfig.Load(_copy);
        var problems = new Problems();
        var pages = Pages.LoadDefinitions(_copy, site, problems);

        var first = Freshness.Stamp(_copy, site, "all", pages, out _);
        var second = Freshness.Stamp(_copy, site, "all", pages, out _);

        var build = new Build(_copy, _output, requireFresh: true);
        build.Run();

        // One file per translation: only the English front page moved, so only its translations
        // were stamped again. The other eleven pages and the language files already carried the
        // right stamp. The number of translations is counted, not written, so adding a language
        // does not break this.
        var translations = Directory.EnumerateFiles(Path.Combine(_copy, "site", "pages", "home"), "*.html").Count() - 1;

        Assert.Empty(problems.Found);
        Assert.Equal(translations, first);
        Assert.Equal(0, second);
        Assert.Empty(build.Problems);
    }

    /// <summary>
    /// The working tree on the owner's machine is CRLF and the repository is LF. A hash that saw
    /// the difference would call every translation stale on one of the two, and the list would
    /// be wrong in a way nobody could act on.
    /// </summary>
    [Fact]
    public void A_difference_in_line_endings_alone_does_not_make_a_translation_stale()
    {
        var english = Path.Combine(_copy, "site", "pages", "home", "en.html");
        var text = File.ReadAllText(english).Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(english, text.Replace("\n", "\r\n", StringComparison.Ordinal));

        var build = new Build(_copy, _output);
        build.Run();

        Assert.Empty(build.Problems);
        Assert.Empty(build.Stale);
    }

    [Fact]
    public void A_file_for_a_language_the_registry_does_not_list_is_reported()
    {
        File.Copy(
            Path.Combine(_copy, "site", "pages", "faq", "pl.html"),
            Path.Combine(_copy, "site", "pages", "faq", "xx.html"));

        Assert.Contains(Build(), problem => problem.Contains("faq/xx.html belongs to no language", StringComparison.Ordinal));
    }

    [Fact]
    public void A_published_language_with_no_registry_entry_stops_the_build_with_a_reason()
    {
        Publish("xx");

        Assert.Contains(Build(), problem => problem.Contains("'xx' is published under languages and has no entry", StringComparison.Ordinal));
    }

    [Fact]
    public void A_direction_that_is_neither_ltr_nor_rtl_is_reported()
    {
        EditSite(text => text.Replace("\"dir\": \"rtl\"", "\"dir\": \"sideways\"", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("sideways", StringComparison.Ordinal));
    }

    [Fact]
    public void A_slug_for_a_language_whose_slugs_are_english_is_reported()
    {
        Register("xe", "ltr", "english");
        Publish("xe");
        var path = Path.Combine(_copy, "site", "pages", "faq", "page.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"pl\": \"faq\"", "\"pl\": \"faq\", \"xe\": \"faq\"", StringComparison.Ordinal));

        Assert.Contains(Build(), problem => problem.Contains("has a slug for 'xe', whose registry entry says slugs are english", StringComparison.Ordinal));
    }

    /// <summary>
    /// What a right-to-left language gets, through the real generator. The language is invented
    /// for the test and made out of the Polish files - the same shape, the same stamps, an
    /// invented registry entry - which is exactly enough for the generator to have nothing to
    /// complain about and a page to be written for it. It is invented, not Arabic, so the test
    /// does not change the day Arabic is published.
    /// </summary>
    [Fact]
    public void A_right_to_left_language_is_written_with_its_direction_and_listed_in_the_switch()
    {
        Register("xr", "rtl", "english");
        Publish("xr");
        File.Copy(Path.Combine(_copy, "site", "i18n", "pl.json"), Path.Combine(_copy, "site", "i18n", "xr.json"));
        foreach (var folder in Directory.EnumerateDirectories(Path.Combine(_copy, "site", "pages")))
        {
            File.Copy(Path.Combine(folder, "pl.html"), Path.Combine(folder, "xr.html"));
        }

        var problems = Build();

        var rtl = File.ReadAllText(Path.Combine(_output, "xr", "faq", "index.html"));
        var english = File.ReadAllText(Path.Combine(_output, "faq", "index.html"));

        Assert.Empty(problems);
        Assert.Contains("<html lang=\"xr\" dir=\"rtl\">", rtl, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"en\">", english, StringComparison.Ordinal);
        Assert.Contains("hreflang=\"xr\" href=\"https://betterwindowsservices.donislawdev.com/xr/faq/\"", english, StringComparison.Ordinal);
        Assert.Contains(">Test language<", english, StringComparison.Ordinal);
    }

    [Fact]
    public void A_link_written_as_a_url_token_resolves_and_a_mistyped_one_is_reported()
    {
        DamagePage("faq", "pl", "href=\"/pl/sc-exe-i-powershell/\"", "href=\"{{url:sc-exe-and-powershell}}\"");
        Assert.Empty(Build());

        // Resolved in the language of the PAGE THAT WRITES IT. The address of the English page
        // exists too, so the link check alone would pass a token that resolved to the wrong one.
        // The Polish address is also in this page's navigation and footer, so asserting that it
        // is present would pass either way - what only a wrong resolution can put on a Polish
        // page is the English address.
        var polish = File.ReadAllText(Path.Combine(_output, "pl", "faq", "index.html"));
        Assert.Contains("href=\"/pl/sc-exe-i-powershell/\"", polish, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/sc-exe-and-powershell/\"", polish, StringComparison.Ordinal);

        DamagePage("faq", "pl", "{{url:sc-exe-and-powershell}}", "{{url:sc-exe-and-powershel}}");
        Assert.Contains(Build(), problem => problem.Contains("no page has the id 'sc-exe-and-powershel'", StringComparison.Ordinal));
    }

    private IReadOnlyList<string> Build()
    {
        var build = new Build(_copy, _output);
        build.Run();
        return build.Problems;
    }

    /// <summary>Adds a language to the published list, whatever the list holds now.</summary>
    private void Publish(string code) =>
        EditSite(text => System.Text.RegularExpressions.Regex.Replace(
            text,
            "(\"languages\"\\s*:\\s*\\[[^\\]]*)\\]",
            "$1, \"" + code + "\"]",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1)));

    /// <summary>Adds an invented language to the registry, so a test never depends on a real one being published or not.</summary>
    private void Register(string code, string dir, string slugs) =>
        EditSite(text => text.Replace(
            "\"language_registry\": {",
            "\"language_registry\": {\n    \"" + code + "\": { \"tag\": \"" + code + "\", \"locale\": \"" + code + "_XX\", \"name\": \"Test language\", \"short\": \"" + code.ToUpperInvariant() + "\", \"dir\": \"" + dir + "\", \"slugs\": \"" + slugs + "\" },",
            StringComparison.Ordinal));

    private void EditSite(Func<string, string> edit)
    {
        var path = Path.Combine(_copy, "site", "site.json");
        var before = File.ReadAllText(path);
        var after = edit(before);

        // Same rule as the page damage: an edit that changed nothing would let the test pass
        // against a generator that checks nothing.
        Assert.NotEqual(before, after);
        File.WriteAllText(path, after);
    }

    private string StampLine(string id, string language)
    {
        var text = File.ReadAllText(Path.Combine(_copy, "site", "pages", id, language + ".html"));
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
