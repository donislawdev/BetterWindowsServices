using System.Globalization;
using System.Text;

namespace Bws.Site;

/// <summary>
/// One page's fragment, wrapped in the head, the header and the footer that every page shares.
///
/// <b>The chrome is written here and nowhere else</b>, which is the site's version of GUI rule 5
/// in CLAUDE.md: a change to the navigation is one change, not fourteen files and a language
/// multiplier. What a page owns is its own words, and it owns all of them - the title and the
/// description come out of the fragment too, so nothing a visitor reads lives in a JSON file
/// that an architecture guard holds to ASCII.
/// </summary>
internal sealed class Renderer
{
    private readonly SiteConfig _site;
    private readonly Translations _words;
    private readonly Tokens _tokens;
    private readonly Problems _problems;
    private readonly IReadOnlyList<PageDefinition> _pages;
    private readonly Dictionary<(string Id, string Language), PageContent> _contents;

    internal Renderer(
        SiteConfig site,
        Translations words,
        Tokens tokens,
        IReadOnlyList<PageDefinition> pages,
        Dictionary<(string Id, string Language), PageContent> contents,
        Problems problems)
    {
        _site = site;
        _words = words;
        _tokens = tokens;
        _pages = pages;
        _contents = contents;
        _problems = problems;
    }

    internal string Render(PageDefinition page, PageContent content)
    {
        var language = content.Language;
        var url = _site.Origin + page.Url(language, _site);
        var title = _tokens.Resolve(content.Title, language, $"{page.Id}/{language} title");
        var description = _tokens.Resolve(content.Description, language, $"{page.Id}/{language} description");
        var body = _tokens.Resolve(content.Body, language, $"{page.Id}/{language}");

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine(CultureInfo.InvariantCulture, $"<html lang=\"{language}\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<title>{Html.Escape(title)}</title>");
        html.AppendLine(CultureInfo.InvariantCulture, $"<meta name=\"description\" content=\"{Html.Escape(description)}\">");

        // A page that does not exist must not be indexed under whatever address produced it.
        if (page.IsNotFound)
        {
            html.AppendLine("<meta name=\"robots\" content=\"noindex\">");
        }

        html.AppendLine(CultureInfo.InvariantCulture, $"<link rel=\"canonical\" href=\"{url}\">");
        AppendAlternates(html, page);

        html.AppendLine("<link rel=\"icon\" href=\"/assets/icon.svg\" type=\"image/svg+xml\">");

        // The .ico is for the browsers that ask for one by habit rather than by link,
        // and it is the program's own file copied, not a second drawing.
        html.AppendLine("<link rel=\"icon\" href=\"/assets/favicon.ico\" sizes=\"any\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<meta name=\"theme-color\" content=\"{_site.ThemeColor}\">");
        AppendOpenGraph(html, page, language, title, description, url);

        if (_site.SearchConsoleToken.Length > 0)
        {
            html.AppendLine(CultureInfo.InvariantCulture, $"<meta name=\"google-site-verification\" content=\"{Html.Escape(_site.SearchConsoleToken)}\">");
        }

        html.AppendLine("<link rel=\"stylesheet\" href=\"/assets/style.css\">");
        AppendStructuredData(html, page, language, description, body);
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        AppendHeader(html, page, language);
        html.AppendLine("<main id=\"main\">");
        html.AppendLine(body);
        html.AppendLine("</main>");
        AppendFooter(html, language);
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    /// <summary>
    /// The same page in the other languages, plus <c>x-default</c> for a visitor whose language
    /// is neither. Every alternate names every language INCLUDING this one - search engines take
    /// the set as a whole and a page that leaves itself out is a set that does not agree with
    /// itself. The not-found page gets none: it is one address for many missing pages.
    /// </summary>
    private void AppendAlternates(StringBuilder html, PageDefinition page)
    {
        if (page.IsNotFound)
        {
            return;
        }

        foreach (var language in _site.Languages)
        {
            html.AppendLine(CultureInfo.InvariantCulture, $"<link rel=\"alternate\" hreflang=\"{language}\" href=\"{_site.Origin}{page.Url(language, _site)}\">");
        }

        html.AppendLine(CultureInfo.InvariantCulture, $"<link rel=\"alternate\" hreflang=\"x-default\" href=\"{_site.Origin}{page.Url(_site.DefaultLanguage, _site)}\">");
    }

    private void AppendOpenGraph(StringBuilder html, PageDefinition page, string language, string title, string description, string url)
    {
        html.AppendLine("<meta property=\"og:type\" content=\"website\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<meta property=\"og:site_name\" content=\"{Html.Escape(_site.Product)}\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<meta property=\"og:locale\" content=\"{Locale(language)}\">");
        foreach (var other in _site.Languages.Where(other => other != language))
        {
            html.AppendLine(CultureInfo.InvariantCulture, $"<meta property=\"og:locale:alternate\" content=\"{Locale(other)}\">");
        }

        html.AppendLine(CultureInfo.InvariantCulture, $"<meta property=\"og:title\" content=\"{Html.Escape(title)}\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<meta property=\"og:description\" content=\"{Html.Escape(description)}\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<meta property=\"og:url\" content=\"{url}\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<meta property=\"og:image\" content=\"{_site.Origin}{_site.SocialImage.Path}\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<meta property=\"og:image:width\" content=\"{_site.SocialImage.Width}\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<meta property=\"og:image:height\" content=\"{_site.SocialImage.Height}\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<meta property=\"og:image:alt\" content=\"{Html.Escape(_site.SocialImage.Alt)}\">");
        html.AppendLine("<meta name=\"twitter:card\" content=\"summary_large_image\">");

        // og:type is "website" everywhere, so the front page is the only one that also says what
        // the thing on it is. Repeating the software block on every page would tell an indexer
        // there are fourteen programs here.
        _ = page;
    }

    private static string Locale(string language) => language switch
    {
        "en" => "en_US",
        "pl" => "pl_PL",
        _ => language,
    };

    /// <summary>
    /// Structured data, on the two pages where there is something true to say: the front page
    /// describes the program, and the questions page marks its questions up as questions.
    /// Nowhere else - marking a guide up as software would be a claim about a thing that is not
    /// on that page. An unrecognised value is a problem rather than silence, because a typo
    /// would otherwise leave a page without the markup it asked for and say nothing.
    /// </summary>
    private void AppendStructuredData(StringBuilder html, PageDefinition page, string language, string description, string body)
    {
        var where = $"{page.Id}/{language}";
        switch (page.Schema)
        {
            case null:
                break;

            case "software":
                html.AppendLine(StructuredData.Software(_site, _tokens.Resolve("{{version}}", language, where), description));
                break;

            case "faq":
                if (StructuredData.Questions(body, where, _problems) is { } questions)
                {
                    html.AppendLine(questions);
                }

                break;

            default:
                _problems.Add($"schema: {where} asks for '{page.Schema}', which is not something this generator emits.");
                break;
        }
    }

    private void AppendHeader(StringBuilder html, PageDefinition current, string language)
    {
        html.AppendLine(CultureInfo.InvariantCulture, $"<a class=\"skip\" href=\"#main\">{_words.Get(language, "chrome.skip", _problems)}</a>");
        html.AppendLine("<header class=\"site-header\">");
        html.AppendLine("<div class=\"wrap\">");
        html.AppendLine(CultureInfo.InvariantCulture, $"<a class=\"brand\" href=\"{Home(language)}\"><img src=\"/assets/icon.svg\" alt=\"\" width=\"28\" height=\"28\">{Html.Escape(_site.Product)}</a>");
        html.AppendLine(CultureInfo.InvariantCulture, $"<nav class=\"site-nav\" aria-label=\"{_words.Get(language, "chrome.nav", _problems)}\">");

        foreach (var page in _pages.Where(page => page.Nav))
        {
            var here = page.Id == current.Id ? " aria-current=\"page\"" : "";
            html.AppendLine(CultureInfo.InvariantCulture, $"<a href=\"{page.Url(language, _site)}\"{here}>{Html.Escape(NavText(page, language))}</a>");
        }

        foreach (var other in _site.Languages.Where(other => other != language))
        {
            var target = current.IsNotFound ? Home(other) : current.Url(other, _site);
            html.AppendLine(CultureInfo.InvariantCulture, $"<a class=\"lang\" href=\"{target}\" lang=\"{other}\" hreflang=\"{other}\">{_words.Get(language, "chrome.language." + other, _problems)}</a>");
        }

        html.AppendLine("</nav>");
        html.AppendLine("</div>");
        html.AppendLine("</header>");
    }

    private void AppendFooter(StringBuilder html, string language)
    {
        html.AppendLine("<footer class=\"site-footer\">");
        html.AppendLine("<div class=\"wrap\">");
        html.AppendLine("<div class=\"footer-groups\">");

        foreach (var group in new[] { "product", "guides" })
        {
            html.AppendLine("<div>");
            html.AppendLine(CultureInfo.InvariantCulture, $"<h2>{_words.Get(language, "chrome.footer." + group, _problems)}</h2>");
            html.AppendLine("<ul>");
            foreach (var page in _pages.Where(page => string.Equals(page.Footer, group, StringComparison.Ordinal)))
            {
                html.AppendLine(CultureInfo.InvariantCulture, $"<li><a href=\"{page.Url(language, _site)}\">{Html.Escape(NavText(page, language))}</a></li>");
            }

            html.AppendLine("</ul>");
            html.AppendLine("</div>");
        }

        html.AppendLine("<div>");
        html.AppendLine(CultureInfo.InvariantCulture, $"<h2>{_words.Get(language, "chrome.footer.project", _problems)}</h2>");
        html.AppendLine("<ul>");
        foreach (var (key, href) in new[]
        {
            ("chrome.footer.source", _site.Repo),
            ("chrome.footer.releases", _site.Repo + "/releases"),
            ("chrome.footer.issues", _site.Repo + "/issues"),
            ("chrome.footer.security", _site.Repo + "/blob/main/SECURITY.md"),
            ("chrome.footer.support", _site.Support),
        })
        {
            html.AppendLine(CultureInfo.InvariantCulture, $"<li><a href=\"{href}\">{_words.Get(language, key, _problems)}</a></li>");
        }

        html.AppendLine("</ul>");
        html.AppendLine("</div>");
        html.AppendLine("</div>");

        html.AppendLine("<div class=\"footer-legal\">");
        foreach (var key in new[] { "chrome.legal.licence", "chrome.legal.trademark", "chrome.legal.privacy" })
        {
            html.AppendLine(CultureInfo.InvariantCulture, $"<p>{_tokens.Resolve(_words.Get(language, key, _problems), language, key)}</p>");
        }

        html.AppendLine("</div>");
        html.AppendLine("</div>");
        html.AppendLine("</footer>");
    }

    private string Home(string language) => language == _site.DefaultLanguage ? "/" : "/" + language + "/";

    private string NavText(PageDefinition page, string language) =>
        _contents.TryGetValue((page.Id, language), out var content) ? content.NavText : page.Id;
}
