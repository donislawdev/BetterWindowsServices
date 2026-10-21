using System.Text.RegularExpressions;

namespace Bws.Site;

/// <summary>
/// A translation held against the page in the default language it follows - not for the quality
/// of its sentences, which nothing here can judge, but for the three ways a translation goes
/// wrong that a build CAN see.
///
/// <b>The shape.</b> The block structure has to be the English page's: the same sections, the
/// same headings, paragraphs, lists and table cells, in the same order, with the same classes and
/// ids, linking to the same pages and the same outside addresses. A translator who drops a
/// paragraph, merges two list items or leaves a closing tag off produces a page that looks
/// finished and is not the page it claims to be. Inline formatting (<c>em</c>, <c>strong</c>,
/// <c>code</c>, <c>span</c>, <c>wbr</c>) is NOT structure: a language may emphasise a different
/// word, so it is left out of the comparison.
///
/// <b>The commands.</b> Every <c>&lt;code&gt;</c> has to hold what the English one holds, minus the
/// comment spans of a code block, which are prose and are translated, and except for the word
/// inside an angle-bracket placeholder, which is the reader's to fill in. A command the visitor will
/// paste into a shell is the one thing on the page that must not be a translation.
///
/// <b>The tokens.</b> The same <c>{{...}}</c> placeholders, the same number of times. A page that
/// loses <c>{{releases}}</c> loses the link to the download, and one that gains a token the
/// English page does not use is a sentence about something the English page does not say.
///
/// <b>Written 2026-10-21, measured first.</b> English and Polish agree on all of this on ten of
/// twelve pages; the two that did not (the question about the program's own language) differed
/// in CONTENT, not by a slip, and were reconciled in the English source before this check went in.
/// </summary>
internal static class Parity
{
    private static readonly HashSet<string> Inline = new(StringComparer.Ordinal)
    {
        "em", "strong", "code", "span", "wbr", "br", "b", "i", "sub", "sup", "kbd", "abbr", "mark", "small",
    };

    private static readonly Regex Tag = new(
        @"<(?<close>/)?(?<name>[a-zA-Z][a-zA-Z0-9-]*)(?<attributes>(?:[^>""']|""[^""]*""|'[^']*')*)>",
        RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(5));

    private static readonly Regex Attribute = new(
        @"(?<=\s)(?<key>class|id|href|src)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')",
        RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private static readonly Regex CodeElement = new(
        @"<code[^>]*>(?<text>.*?)</code>",
        RegexOptions.ExplicitCapture | RegexOptions.Singleline,
        TimeSpan.FromSeconds(5));

    private static readonly Regex CommentSpan = new(
        @"<span class=""c"">.*?</span>",
        RegexOptions.Singleline,
        TimeSpan.FromSeconds(5));

    private static readonly Regex AnyTag = new("<[^>]*>", RegexOptions.None, TimeSpan.FromSeconds(2));

    private static readonly Regex Token = new(
        @"\{\{\s*(?<name>[a-z0-9:_-]+)\s*\}\}",
        RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(2));

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.None, TimeSpan.FromSeconds(1));

    // A word in angle brackets is the part of a command the reader replaces - <code> in a file
    // name, <name> in a pattern - and the one part of a command that a translation SHOULD
    // translate, because the reader has to understand what to put there. The brackets stay, the
    // word between them is not compared.
    private static readonly Regex Placeholder = new(@"&lt;[^&]*?&gt;", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>The address of every page in every published language, mapped to the id of the page, so that a link means the same page whatever its spelling.</summary>
    internal static Dictionary<string, string> AddressBook(IReadOnlyList<PageDefinition> pages, SiteConfig site)
    {
        var book = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            foreach (var language in site.Languages)
            {
                book[page.Url(language, site)] = page.Id;
            }
        }

        return book;
    }

    internal static void Check(
        PageDefinition page,
        PageContent original,
        PageContent translation,
        Dictionary<string, string> addresses,
        Problems problems)
    {
        var where = $"{page.Id}/{translation.Language}.html";

        var wanted = Skeleton(original.Body, addresses);
        var found = Skeleton(translation.Body, addresses);
        var firstDifference = FirstDifference(wanted, found);
        if (firstDifference >= 0)
        {
            problems.Add(
                $"parity: {where} does not have the shape of the {original.Language} page. "
                + $"Element {firstDifference + 1} of {wanted.Count} should be '{At(wanted, firstDifference)}' and is '{At(found, firstDifference)}'"
                + (wanted.Count == found.Count ? "." : $" - the English page has {wanted.Count} and this one has {found.Count}."));
        }

        var wantedCode = Commands(original.Body);
        var foundCode = Commands(translation.Body);
        var missing = Without(wantedCode, foundCode);
        var extra = Without(foundCode, wantedCode);
        if (missing.Count > 0 || extra.Count > 0)
        {
            problems.Add(
                $"parity: {where} has different code from the {original.Language} page"
                + (missing.Count > 0 ? $" - missing: {Quote(missing)}" : "")
                + (extra.Count > 0 ? $" - not in the original: {Quote(extra)}" : "")
                + ".");
        }

        var wantedTokens = TokenNames(original.Body);
        var foundTokens = TokenNames(translation.Body);
        var tokenMissing = Without(wantedTokens, foundTokens);
        var tokenExtra = Without(foundTokens, wantedTokens);
        if (tokenMissing.Count > 0 || tokenExtra.Count > 0)
        {
            problems.Add(
                $"parity: {where} uses different tokens from the {original.Language} page"
                + (tokenMissing.Count > 0 ? $" - missing: {string.Join(", ", tokenMissing)}" : "")
                + (tokenExtra.Count > 0 ? $" - not in the original: {string.Join(", ", tokenExtra)}" : "")
                + ".");
        }
    }

    /// <summary>The block structure of a fragment as one line per element, with the attributes that make it the same element.</summary>
    internal static List<string> Skeleton(string body, Dictionary<string, string> addresses)
    {
        var items = new List<string>();
        foreach (var match in Tag.Matches(body).Cast<Match>())
        {
            var name = match.Groups["name"].Value.ToLowerInvariant();
            if (Inline.Contains(name))
            {
                continue;
            }

            if (match.Groups["close"].Success)
            {
                items.Add("/" + name);
                continue;
            }

            var line = name;
            foreach (var attribute in Attribute.Matches(match.Groups["attributes"].Value).Cast<Match>())
            {
                var key = attribute.Groups["key"].Value.ToLowerInvariant();
                var value = attribute.Groups["value"].Value;
                line += key switch
                {
                    "class" => "." + string.Join(".", value.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
                    "id" => "#" + value,
                    "href" => " ->" + Destination(value, addresses),
                    _ => " " + key + "=" + value,
                };
            }

            items.Add(line);
        }

        return items;
    }

    /// <summary>
    /// A link's destination as the page it means, so <c>/faq/</c>, <c>/de/haeufige-fragen/</c> and
    /// <c>{{url:faq}}</c> are all 'the FAQ'. An outside address and an in-page anchor stay as written.
    /// </summary>
    private static string Destination(string href, Dictionary<string, string> addresses)
    {
        var hash = href.IndexOf('#', StringComparison.Ordinal);
        var path = hash < 0 ? href : href[..hash];
        var fragment = hash < 0 ? "" : href[hash..];

        var token = Token.Match(path);
        if (token.Success && token.Value.Length == path.Length && token.Groups["name"].Value.StartsWith("url:", StringComparison.Ordinal))
        {
            return "page:" + token.Groups["name"].Value["url:".Length..] + fragment;
        }

        return addresses.TryGetValue(path, out var id) ? "page:" + id + fragment : href;
    }

    private static List<string> Commands(string body) =>
        CodeElement.Matches(body).Cast<Match>()
            .Select(match => CommentSpan.Replace(match.Groups["text"].Value, ""))
            .Select(text => Placeholder.Replace(Whitespace.Replace(AnyTag.Replace(text, ""), " ").Trim(), "&lt;&gt;"))
            .Where(text => text.Length > 0)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static List<string> TokenNames(string body) =>
        Token.Matches(body).Cast<Match>()
            .Select(match => match.Groups["name"].Value)
            .Where(name => !name.StartsWith("url:", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static int FirstDifference(List<string> wanted, List<string> found)
    {
        var shared = Math.Min(wanted.Count, found.Count);
        for (var index = 0; index < shared; index++)
        {
            if (!string.Equals(wanted[index], found[index], StringComparison.Ordinal))
            {
                return index;
            }
        }

        return wanted.Count == found.Count ? -1 : shared;
    }

    private static string At(List<string> items, int index) => index < items.Count ? items[index] : "(nothing)";

    /// <summary>The elements of <paramref name="from"/> that <paramref name="other"/> has fewer of, counting repeats.</summary>
    private static List<string> Without(List<string> from, List<string> other)
    {
        var remaining = other.GroupBy(item => item, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var item in from)
        {
            if (remaining.TryGetValue(item, out var count) && count > 0)
            {
                remaining[item] = count - 1;
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private static string Quote(List<string> items)
    {
        const int Shown = 3;
        var shown = items.Take(Shown).Select(item => "'" + (item.Length > 60 ? item[..60] + "..." : item) + "'");
        return string.Join(", ", shown) + (items.Count > Shown ? $" and {items.Count - Shown} more" : "");
    }
}
