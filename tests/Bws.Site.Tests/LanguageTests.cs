namespace Bws.Site.Tests;

/// <summary>
/// The site's languages, asked whether the generator NOTICES what goes wrong with one.
///
/// Same rule as <see cref="GeneratorTests"/>: every test copies the real site, damages one thing
/// and asserts the build says so. These are the ones a language can break - its registry entry, its
/// slugs, its shape against the English page, the stamp that says which English it follows, and what
/// the generator writes for a right-to-left language. None of them names a language that is really
/// published, so adding one never edits this file.
/// </summary>
public sealed class LanguageTests : SiteTestBase
{
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
        var english = Path.Combine(Copy, "site", "pages", "home", "en.html");
        File.AppendAllText(english, "<!-- the English moved on -->");

        var plain = new Build(Copy, Output);
        plain.Run();
        Assert.Empty(plain.Problems);
        Assert.Contains(plain.Stale, line => line.Contains("home/pl.html", StringComparison.Ordinal));

        var strict = new Build(Copy, Output, requireFresh: true);
        strict.Run();
        Assert.Contains(strict.Problems, problem => problem.Contains("freshness: home/pl.html", StringComparison.Ordinal));
    }

    [Fact]
    public void Stamping_a_language_makes_it_fresh_and_a_second_stamp_changes_nothing()
    {
        File.AppendAllText(Path.Combine(Copy, "site", "pages", "home", "en.html"), "<!-- the English moved on -->");

        var site = SiteConfig.Load(Copy);
        var problems = new Problems();
        var pages = Pages.LoadDefinitions(Copy, site, problems);

        var first = Freshness.Stamp(Copy, site, "all", pages, out _);
        var second = Freshness.Stamp(Copy, site, "all", pages, out _);

        var build = new Build(Copy, Output, requireFresh: true);
        build.Run();

        // One file per translation: only the English front page moved, so only its translations
        // were stamped again. The other eleven pages and the language files already carried the
        // right stamp. The number of translations is counted, not written, so adding a language
        // does not break this.
        var translations = Directory.EnumerateFiles(Path.Combine(Copy, "site", "pages", "home"), "*.html").Count() - 1;

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
        var english = Path.Combine(Copy, "site", "pages", "home", "en.html");
        var text = File.ReadAllText(english).Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(english, text.Replace("\n", "\r\n", StringComparison.Ordinal));

        var build = new Build(Copy, Output);
        build.Run();

        Assert.Empty(build.Problems);
        Assert.Empty(build.Stale);
    }

    /// <summary>
    /// French writes a no-break space before a colon and a question mark, as an entity. The
    /// structured data is JSON read by a machine, so the entity has to be turned into the
    /// character there - what it says to a search engine otherwise is the letters.
    /// </summary>
    [Fact]
    public void A_no_break_space_in_a_question_reaches_the_structured_data_as_the_character()
    {
        DamagePage("faq", "en", "Does it need the internet?", "Does it need the internet&nbsp;?");

        Assert.Empty(Build());

        var page = File.ReadAllText(Path.Combine(Output, "faq", "index.html"));
        var start = page.IndexOf("application/ld+json", StringComparison.Ordinal);
        Assert.True(start >= 0, "the questions page should carry structured data");

        var data = page[start..page.IndexOf("</script>", start, StringComparison.Ordinal)];
        Assert.Contains("the internet\u00A0?", data, StringComparison.Ordinal);
        Assert.DoesNotContain("&nbsp;", data, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_for_a_language_the_registry_does_not_list_is_reported()
    {
        File.Copy(
            Path.Combine(Copy, "site", "pages", "faq", "pl.html"),
            Path.Combine(Copy, "site", "pages", "faq", "xx.html"));

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
        var path = Path.Combine(Copy, "site", "pages", "faq", "page.json");
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
        File.Copy(Path.Combine(Copy, "site", "i18n", "pl.json"), Path.Combine(Copy, "site", "i18n", "xr.json"));
        foreach (var folder in Directory.EnumerateDirectories(Path.Combine(Copy, "site", "pages")))
        {
            File.Copy(Path.Combine(folder, "pl.html"), Path.Combine(folder, "xr.html"));
        }

        var problems = Build();

        var rtl = File.ReadAllText(Path.Combine(Output, "xr", "faq", "index.html"));
        var english = File.ReadAllText(Path.Combine(Output, "faq", "index.html"));

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
        var polish = File.ReadAllText(Path.Combine(Output, "pl", "faq", "index.html"));
        Assert.Contains("href=\"/pl/sc-exe-i-powershell/\"", polish, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/sc-exe-and-powershell/\"", polish, StringComparison.Ordinal);

        DamagePage("faq", "pl", "{{url:sc-exe-and-powershell}}", "{{url:sc-exe-and-powershel}}");
        Assert.Contains(Build(), problem => problem.Contains("no page has the id 'sc-exe-and-powershel'", StringComparison.Ordinal));
    }
}
