using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bws.Site;

/// <summary>
/// The two blocks of structured data this site emits, and nothing else.
///
/// <b>Marked up only where there is something true to say.</b> The front page describes the
/// program, and the questions page marks its questions as questions. A guide marked up as
/// software would tell an indexer there are twelve programs here, and a page claiming an
/// aggregate rating nobody gave is the kind of thing that gets a site's markup ignored
/// wholesale. Both blocks describe what a visitor can see on the page they sit on.
///
/// <b>The questions are read out of the rendered page rather than kept beside it</b>, which is
/// the same rule the rest of this generator keeps: one source. A list of questions in a JSON
/// file would be a second copy of the page, in a second language, drifting quietly - and search
/// engines require the marked-up text to match what the visitor reads.
/// </summary>
internal static class StructuredData
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(5);

    /// <summary>A question and its answer inside the page's question block.</summary>
    private static readonly Regex Pair = new(
        @"<h3>(?<question>.*?)</h3>\s*(?<answer>(?:\s*<p>.*?</p>)+)",
        RegexOptions.Singleline | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(5));

    private static readonly Regex QuestionBlock = new(
        @"<div class=""qa"">(?<inner>.*?)\n\s*</div>\s*\n\s*</div>",
        RegexOptions.Singleline | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(5));

    private static readonly Regex Tag = new("<[^>]+>", RegexOptions.None, TimeSpan.FromSeconds(5));

    internal static string Software(SiteConfig site, string version, string description)
    {
        var json = new StringBuilder();
        json.AppendLine("<script type=\"application/ld+json\">");
        json.AppendLine("{");
        json.AppendLine("  \"@context\": \"https://schema.org\",");
        json.AppendLine("  \"@type\": \"SoftwareApplication\",");
        json.AppendLine(CultureInfo.InvariantCulture, $"  \"name\": \"{Html.Json(site.Product)}\",");
        json.AppendLine("  \"applicationCategory\": \"DeveloperApplication\",");
        json.AppendLine("  \"operatingSystem\": \"Windows 10 1809, Windows Server 2019, and later, 64-bit\",");
        json.AppendLine(CultureInfo.InvariantCulture, $"  \"softwareVersion\": \"{Html.Json(version)}\",");
        json.AppendLine(CultureInfo.InvariantCulture, $"  \"description\": \"{Html.Json(description)}\",");
        json.AppendLine(CultureInfo.InvariantCulture, $"  \"url\": \"{site.Origin}/\",");
        json.AppendLine(CultureInfo.InvariantCulture, $"  \"downloadUrl\": \"{site.Repo}/releases/latest\",");
        json.AppendLine(CultureInfo.InvariantCulture, $"  \"codeRepository\": \"{site.Repo}\",");
        json.AppendLine("  \"license\": \"https://www.gnu.org/licenses/gpl-3.0.html\",");
        json.AppendLine("  \"isAccessibleForFree\": true,");
        json.AppendLine("  \"offers\": { \"@type\": \"Offer\", \"price\": \"0\", \"priceCurrency\": \"USD\" },");
        json.AppendLine("  \"author\": { \"@type\": \"Person\", \"name\": \"DonislawDev\" }");
        json.AppendLine("}");
        json.Append("</script>");
        return json.ToString();
    }

    /// <summary>
    /// The questions the page actually asks. An empty result is a problem rather than an empty
    /// block: a page that declares itself a question page and emits no questions has either lost
    /// its markup or changed its shape, and either way the silence would last until somebody
    /// looked at the published source.
    /// </summary>
    internal static string? Questions(string body, string where, Problems problems)
    {
        var block = QuestionBlock.Match(body);
        if (!block.Success)
        {
            problems.Add($"schema: {where} is marked as a question page and has no <div class=\"qa\"> block.");
            return null;
        }

        var pairs = Pair.Matches(block.Groups["inner"].Value);
        if (pairs.Count == 0)
        {
            problems.Add($"schema: {where} has a question block with no heading and paragraph pairs in it.");
            return null;
        }

        var json = new StringBuilder();
        json.AppendLine("<script type=\"application/ld+json\">");
        json.AppendLine("{");
        json.AppendLine("  \"@context\": \"https://schema.org\",");
        json.AppendLine("  \"@type\": \"FAQPage\",");
        json.AppendLine("  \"mainEntity\": [");

        for (var index = 0; index < pairs.Count; index++)
        {
            var question = Plain(pairs[index].Groups["question"].Value);
            var answer = Plain(pairs[index].Groups["answer"].Value);
            var comma = index == pairs.Count - 1 ? "" : ",";
            json.AppendLine("    {");
            json.AppendLine("      \"@type\": \"Question\",");
            json.AppendLine(CultureInfo.InvariantCulture, $"      \"name\": \"{Html.Json(question)}\",");
            json.AppendLine("      \"acceptedAnswer\": {");
            json.AppendLine("        \"@type\": \"Answer\",");
            json.AppendLine(CultureInfo.InvariantCulture, $"        \"text\": \"{Html.Json(answer)}\"");
            json.AppendLine("      }");
            json.AppendLine(CultureInfo.InvariantCulture, $"    }}{comma}");
        }

        json.AppendLine("  ]");
        json.AppendLine("}");
        json.Append("</script>");
        return json.ToString();
    }

    /// <summary>
    /// The text a person reads, without the markup around it. The entities are turned back into
    /// characters because this goes into a JSON string rather than into HTML - leaving
    /// <c>&amp;amp;</c> in it would show a visitor an ampersand and a search engine the letters.
    /// </summary>
    private static string Plain(string html)
    {
        var text = Tag.Replace(html, " ");
        text = Regex.Replace(text, @"\s+", " ", RegexOptions.None, Ceiling).Trim();
        return text
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&middot;", "\u00B7", StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal);
    }
}
