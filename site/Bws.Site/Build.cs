using System.Globalization;
using System.Text;

namespace Bws.Site;

/// <summary>
/// The whole build: read the repository, write the site, then check what was written.
///
/// The order matters. The link check and the sitemap describe what is ON DISK rather than what
/// was meant, so they run after everything has been written - a check that reads the plan rather
/// than the result is a check that agrees with itself.
/// </summary>
internal sealed class Build
{
    private readonly string _root;
    private readonly string _output;
    private readonly Problems _problems = new();

    internal Build(string root, string output)
    {
        _root = root;
        _output = output;
    }

    internal IReadOnlyList<string> Problems => _problems.Found;

    internal int PagesWritten { get; private set; }

    internal void Run()
    {
        var site = SiteConfig.Load(_root);
        var facts = ProductFacts.Read(_root);
        var words = Translations.Load(_root, site.Languages, _problems);
        var tokens = new Tokens(site, facts, words, _problems);
        var pages = Pages.LoadDefinitions(_root, site, _problems);

        var contents = new Dictionary<(string Id, string Language), PageContent>();
        foreach (var page in pages)
        {
            foreach (var language in site.Languages)
            {
                if (Pages.LoadContent(_root, page, language, _problems) is { } content)
                {
                    contents[(page.Id, language)] = content;
                }
            }
        }

        CheckReachable(pages, site);
        ReadmeCheck.Run(_root, facts, _problems);

        var renderer = new Renderer(site, words, tokens, pages, contents, _problems);

        Fresh(_output);
        CopyAssets();

        foreach (var ((id, language), content) in contents)
        {
            var page = pages.First(candidate => candidate.Id == id);
            var target = Path.Combine(_output, page.OutputPath(language, site).Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, renderer.Render(page, content), Utf8);
            PagesWritten++;
        }

        WriteSitemap(pages, site, contents);
        WriteRobots(site);
        WriteCname(site);

        LinkCheck.Run(_output, site, _problems);
    }

    private static UTF8Encoding Utf8 => new(false);

    /// <summary>
    /// A page nothing links to is a page nobody will find, and on a site of fourteen it happens
    /// by simply forgetting a line. Reachability is counted from the chrome and from the body of
    /// every page, so a guide linked only from a paragraph counts as reachable.
    /// </summary>
    private void CheckReachable(IReadOnlyList<PageDefinition> pages, SiteConfig site)
    {
        var linked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages.Where(page => page.Nav || page.Footer is not null))
        {
            linked.Add(page.Id);
        }

        foreach (var page in pages)
        {
            foreach (var language in site.Languages)
            {
                var file = Path.Combine(_root, "site", "pages", page.Id, language + ".html");
                if (!File.Exists(file))
                {
                    continue;
                }

                var text = File.ReadAllText(file);
                foreach (var other in pages)
                {
                    if (text.Contains("\"" + other.Url(language, site) + "\"", StringComparison.Ordinal))
                    {
                        linked.Add(other.Id);
                    }
                }
            }
        }

        foreach (var orphan in pages.Where(page => page.Id != "home" && !page.IsNotFound && !linked.Contains(page.Id)))
        {
            _problems.Add($"pages: nothing links to '{orphan.Id}'. A page nobody can reach is a page nobody will find.");
        }
    }

    private static void Fresh(string output)
    {
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        Directory.CreateDirectory(output);
    }

    private void CopyAssets()
    {
        var from = Path.Combine(_root, "site", "assets");
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_output, "assets", Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>
    /// The sitemap, with every language of a page named as an alternate of every other. Search
    /// engines take the set as a whole - a page that does not name itself among its alternates
    /// is a set that disagrees with itself - so each entry lists all of them, this one included.
    /// The not-found page is left out, because it is not a page anybody should arrive at.
    /// </summary>
    private void WriteSitemap(IReadOnlyList<PageDefinition> pages, SiteConfig site, Dictionary<(string Id, string Language), PageContent> contents)
    {
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">");

        foreach (var page in pages.Where(page => !page.IsNotFound))
        {
            foreach (var language in site.Languages.Where(language => contents.ContainsKey((page.Id, language))))
            {
                xml.AppendLine("  <url>");
                xml.AppendLine(CultureInfo.InvariantCulture, $"    <loc>{site.Origin}{page.Url(language, site)}</loc>");
                foreach (var other in site.Languages.Where(other => contents.ContainsKey((page.Id, other))))
                {
                    xml.AppendLine(CultureInfo.InvariantCulture, $"    <xhtml:link rel=\"alternate\" hreflang=\"{other}\" href=\"{site.Origin}{page.Url(other, site)}\"/>");
                }

                xml.AppendLine("  </url>");
            }
        }

        xml.AppendLine("</urlset>");
        File.WriteAllText(Path.Combine(_output, "sitemap.xml"), xml.ToString(), Utf8);
    }

    private void WriteRobots(SiteConfig site)
    {
        var text = string.Join(
            "\n",
            "User-agent: *",
            "Allow: /",
            "",
            $"Sitemap: {site.Origin}/sitemap.xml",
            "");

        File.WriteAllText(Path.Combine(_output, "robots.txt"), text, Utf8);
    }

    /// <summary>
    /// The custom domain, which GitHub Pages reads from a file at the root of what is published.
    /// The workflow holds this against the domain configured in the repository's settings and
    /// refuses to publish when they differ - a mismatch takes the site off the air, and a file
    /// in a repository and a field in a web page are two places for one fact.
    /// </summary>
    private void WriteCname(SiteConfig site) =>
        File.WriteAllText(Path.Combine(_output, "CNAME"), site.Host + "\n", Utf8);
}
