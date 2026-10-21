using System.Text.RegularExpressions;

namespace Bws.Site;

/// <summary>
/// One page of the site as it is kept in the repository: <c>site/pages/{id}/page.json</c> beside
/// one HTML fragment per language.
///
/// <b>The slug is an address and addresses do not change.</b> GitHub Pages cannot redirect, so a
/// renamed slug is a dead link in every search result and every bookmark that ever pointed at
/// the page. The file is named after the id and the id never appears in a URL, so the id may be
/// renamed and the slug may not.
///
/// <b>Everything a person reads is in the fragment, including the title and the description.</b>
/// A <c>page.json</c> with Polish text in it would be swept by the architecture guard that holds
/// every *.json in the repository to ASCII, and a permission per page would be a list nobody
/// maintains. So the fragment opens with three <c>&lt;meta&gt;</c> lines the generator lifts into
/// the head, and the JSON stays a description of the page's place rather than its words.
/// </summary>
internal sealed record PageDefinition
{
    public required string Id { get; init; }

    public required int Order { get; init; }

    /// <summary>Whether the page is in the header navigation.</summary>
    public bool Nav { get; init; }

    /// <summary>The footer group the page is listed in, or null for none: <c>product</c> or <c>guides</c>.</summary>
    public string? Footer { get; init; }

    /// <summary>Structured data to emit: <c>software</c> on the front page, <c>faq</c> on a question page, or null.</summary>
    public string? Schema { get; init; }

    /// <summary>
    /// The slug per language that has slugs of its own - the registry says which. Empty for the
    /// front page. ASCII, lower case, hyphens. A language the registry marks <c>english</c> is
    /// NOT listed here, and listing it is a problem: it would be a slug nothing ever uses.
    /// </summary>
    public required Dictionary<string, string> Slug { get; init; }

    /// <summary>The not-found page is written to <c>404.html</c> rather than to a folder, because that is where GitHub Pages looks.</summary>
    public bool IsNotFound => Id == "404";

    /// <summary>
    /// The slug a language uses for this page. A missing own slug falls back to the default
    /// language's, so the build can go on and list every other problem instead of stopping on a
    /// key lookup - <c>Pages.CheckSlugs</c> has already said the slug is missing.
    /// </summary>
    internal string SlugFor(string language, SiteConfig site) =>
        site.Language(language).Slugs == "own" && Slug.TryGetValue(language, out var own)
            ? own
            : Slug[site.DefaultLanguage];

    internal string Url(string language, SiteConfig site)
    {
        var prefix = language == site.DefaultLanguage ? "/" : "/" + language + "/";
        if (IsNotFound)
        {
            return prefix + "404.html";
        }

        var slug = SlugFor(language, site);
        return slug.Length == 0 ? prefix : prefix + slug + "/";
    }

    internal string OutputPath(string language, SiteConfig site)
    {
        var url = Url(language, site).TrimStart('/');
        return url.EndsWith('/') || url.Length == 0 ? url + "index.html" : url;
    }
}

/// <summary>
/// A page's words in one language: the head lines and the inner HTML of <c>&lt;main&gt;</c>.
/// <see cref="TranslatedFrom"/> is the stamp of the default language's page this one follows,
/// which only a translation carries.
/// </summary>
internal sealed record PageContent(string Language, string Title, string Description, string NavText, string Body, string? TranslatedFrom);

internal static class Pages
{
    private static readonly Regex HeadLine = new(
        @"^\s*<meta name=""(?<name>title|description|nav|translated-from)"" content=""(?<value>[^""]*)"">\s*$",
        RegexOptions.Multiline | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex SlugShape = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.None, TimeSpan.FromSeconds(1));

    internal static List<PageDefinition> LoadDefinitions(string root, SiteConfig site, Problems problems)
    {
        var pages = new List<PageDefinition>();
        foreach (var folder in Directory.EnumerateDirectories(Path.Combine(root, "site", "pages")).Order(StringComparer.Ordinal))
        {
            var path = Path.Combine(folder, "page.json");
            var page = Json.Read<PageDefinition>(path);

            if (page.Id != Path.GetFileName(folder))
            {
                problems.Add($"pages: {path} says id '{page.Id}' but sits in folder '{Path.GetFileName(folder)}'.");
            }

            CheckSlugs(page, site, path, problems);
            pages.Add(page);
        }

        CheckSlugsAreDistinct(pages, site, problems);
        return pages.OrderBy(page => page.Order).ThenBy(page => page.Id, StringComparer.Ordinal).ToList();
    }

    internal static PageContent? LoadContent(string root, PageDefinition page, string language, Problems problems)
    {
        var path = Path.Combine(root, "site", "pages", page.Id, language + ".html");
        if (!File.Exists(path))
        {
            problems.Add($"pages: {page.Id} has no {language}.html.");
            return null;
        }

        var text = File.ReadAllText(path);
        var head = new Dictionary<string, string>(StringComparer.Ordinal);
        var body = HeadLine.Replace(text, match =>
        {
            head[match.Groups["name"].Value] = match.Groups["value"].Value;
            return "";
        }).Trim();

        foreach (var required in new[] { "title", "description" })
        {
            if (!head.ContainsKey(required))
            {
                problems.Add($"pages: {page.Id}/{language}.html does not open with <meta name=\"{required}\" content=\"...\">.");
            }
        }

        if ((page.Nav || page.Footer is not null) && !head.ContainsKey("nav"))
        {
            problems.Add($"pages: {page.Id}/{language}.html is linked from the chrome and has no <meta name=\"nav\">.");
        }

        var title = head.GetValueOrDefault("title", "");
        var description = head.GetValueOrDefault("description", "");
        CheckLengths(page, language, title, description, problems);

        return new PageContent(language, title, description, head.GetValueOrDefault("nav", title), body, head.GetValueOrDefault("translated-from"));
    }

    /// <summary>
    /// The lengths search engines show. A title past about sixty characters is cut in the
    /// result, a description outside roughly fifty to a hundred and sixty is either replaced
    /// with a snippet of the page or cut. The limits here are looser than the advice so that a
    /// long product name does not fail a build, and tighter than nothing.
    /// </summary>
    private static void CheckLengths(PageDefinition page, string language, string title, string description, Problems problems)
    {
        if (title.Length is < 15 or > 70)
        {
            problems.Add($"pages: {page.Id}/{language}.html title is {title.Length} characters, wanted 15 to 70: '{title}'.");
        }

        if (description.Length is < 50 or > 165)
        {
            problems.Add($"pages: {page.Id}/{language}.html description is {description.Length} characters, wanted 50 to 165.");
        }
    }

    /// <summary>
    /// A published language with slugs of its own needs one for every page. A language set to
    /// english must have none, because the one it would have is never read. A slug for a language
    /// that is registered and not yet published IS held to the same shape, so a typo is found
    /// while the language is being written and not on the day it goes live.
    /// </summary>
    private static void CheckSlugs(PageDefinition page, SiteConfig site, string path, Problems problems)
    {
        foreach (var language in site.Languages.Where(language => site.Language(language).Slugs == "own" && !page.Slug.ContainsKey(language)))
        {
            problems.Add($"pages: {path} has no slug for '{language}'.");
        }

        foreach (var (language, slug) in page.Slug)
        {
            if (!site.LanguageRegistry.TryGetValue(language, out var info))
            {
                problems.Add($"pages: {path} has a slug for '{language}', which site.json's language_registry does not list.");
                continue;
            }

            if (info.Slugs != "own")
            {
                problems.Add($"pages: {path} has a slug for '{language}', whose registry entry says slugs are english, so it would never be used.");
                continue;
            }

            var allowedEmpty = page.Id == "home" || page.IsNotFound;
            if (slug.Length == 0 ? !allowedEmpty : !SlugShape.IsMatch(slug))
            {
                problems.Add($"pages: {path} slug '{slug}' for '{language}' is not lower case words joined by hyphens.");
            }
        }
    }

    private static void CheckSlugsAreDistinct(List<PageDefinition> pages, SiteConfig site, Problems problems)
    {
        foreach (var language in site.Languages)
        {
            var clashes = pages
                .GroupBy(page => page.Url(language, site), StringComparer.Ordinal)
                .Where(group => group.Count() > 1);

            foreach (var clash in clashes)
            {
                problems.Add($"pages: {string.Join(", ", clash.Select(page => page.Id))} share the address {clash.Key} in '{language}'.");
            }
        }
    }
}
