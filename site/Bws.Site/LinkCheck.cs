using System.Text.RegularExpressions;

namespace Bws.Site;

/// <summary>
/// Every <c>href</c>, <c>src</c> and <c>poster</c> the built site carries, held against what the
/// site actually contains. A poster is loaded like a src, so it is refused from another host the
/// same way.
///
/// <b>A dead internal link is the failure this site is most likely to have</b>, because the
/// addresses are written by hand in fragments while the pages they point at are declared in JSON,
/// and nothing else would notice. GitHub Pages cannot redirect, so a wrong address is a 404 for
/// as long as it stands.
///
/// <b>An external host outside the allowed list fails too</b>, and that is a promise rather than
/// tidiness: the product never talks to the internet, and the site says it sets no cookies and
/// loads nothing from anywhere else. A stylesheet or a script from another host would make that
/// sentence false, and a link to a host nobody listed is usually a typo in a domain.
/// </summary>
internal static class LinkCheck
{
    // Every spelling HTML takes: the name in any case, space around the equals sign, and the value
    // in double quotes, single quotes or none. The pages are written by hand, and a check that
    // reads one spelling passes the others without a word - which is the failure it exists for.
    //
    // WHITESPACE BEFORE THE NAME, NOT \b. An attribute starts after whitespace, and \b also stands
    // between the hyphen and the name in data-src and data-href - which read their values as
    // addresses and failed a strict build on whatever a page keeps there.
    private static readonly Regex Reference = new(
        @"(?<=\s)(?<what>href|src|poster)\s*=\s*(?:""(?<target>[^""]+)""|'(?<target>[^']+)'|(?<target>[^\s""'=<>`]+))",
        RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    internal static void Run(string outputRoot, SiteConfig site, Problems problems)
    {
        var files = Directory.EnumerateFiles(outputRoot, "*.html", SearchOption.AllDirectories).ToList();
        var present = files
            .Select(file => "/" + Path.GetRelativePath(outputRoot, file).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var asset in Directory.EnumerateFiles(Path.Combine(outputRoot, "assets"), "*", SearchOption.AllDirectories))
        {
            present.Add("/" + Path.GetRelativePath(outputRoot, asset).Replace('\\', '/'));
        }

        foreach (var file in files)
        {
            var where = Path.GetRelativePath(outputRoot, file).Replace('\\', '/');
            foreach (var match in Reference.Matches(File.ReadAllText(file)).Cast<Match>())
            {
                Check(match.Groups["target"].Value, where, match.Groups["what"].Value, present, site, problems);
            }
        }
    }

    private static void Check(string target, string where, string what, HashSet<string> present, SiteConfig site, Problems problems)
    {
        if (target.StartsWith('#') || target.StartsWith("mailto:", StringComparison.Ordinal))
        {
            return;
        }

        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // Reported rather than thrown: an exception here stopped the whole build on the first
            // bad address and named none of the others.
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
            {
                problems.Add($"links: {where} has the address '{target}', which is not an address a browser can follow.");
                return;
            }

            var host = uri.Host;
            if (!site.LinkHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add($"links: {where} points at {host}, which site.json does not list under link_hosts.");
            }

            if (!string.Equals(what, "href", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"links: {where} loads {target} from another host. This site loads nothing from anywhere else.");
            }

            return;
        }

        if (!target.StartsWith('/'))
        {
            problems.Add($"links: {where} has the relative address '{target}'. Addresses on this site start at the root, because a page at /a/b/ and one at /a/ would read it differently.");
            return;
        }

        var path = target.Split('#')[0].Split('?')[0];
        var wanted = path.EndsWith('/') ? path + "index.html" : path;
        if (!present.Contains(wanted))
        {
            problems.Add($"links: {where} points at {target}, and the built site has no {wanted}.");
        }
    }
}
