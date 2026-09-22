using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bws.Site;

/// <summary>
/// The <c>{{name}}</c> placeholders a page may carry, and the tables built out of the program.
///
/// <b>An unresolved token is a problem rather than text left on the page.</b> A page that says
/// <c>{{releases}}</c> to a visitor is worse than a build that refuses, and a typo in a token
/// name is otherwise invisible until somebody reads the published page.
/// </summary>
internal sealed class Tokens
{
    private static readonly Regex Placeholder = new(@"\{\{\s*(?<name>[a-z0-9:_-]+)\s*\}\}", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(2));

    private readonly Dictionary<string, Func<string, string>> _byName;
    private readonly Problems _problems;

    internal Tokens(SiteConfig site, ProductFacts facts, Translations words, Problems problems)
    {
        _problems = problems;
        _byName = new Dictionary<string, Func<string, string>>(StringComparer.Ordinal)
        {
            ["product"] = _ => site.Product,
            ["host"] = _ => site.Host,
            ["origin"] = _ => site.Origin,
            ["repo"] = _ => site.Repo,
            ["releases"] = _ => site.Repo + "/releases/latest",
            ["all_releases"] = _ => site.Repo + "/releases",
            ["issues"] = _ => site.Repo + "/issues",
            ["support"] = _ => site.Support,
            ["version"] = _ => facts.Version,
            ["archive_window"] = _ => site.Archives.Window,
            ["archive_cli"] = _ => site.Archives.Cli,
            ["field_count"] = _ => facts.Fields.Count.ToString(CultureInfo.InvariantCulture),
            ["exit_code_count"] = _ => facts.ExitCodes.Count.ToString(CultureInfo.InvariantCulture),
            ["switch_count"] = _ => facts.Switches.Count.ToString(CultureInfo.InvariantCulture),
            ["table:exit-codes"] = language => ExitCodeTable(facts, words, language, problems),
            ["table:switches"] = language => SwitchTable(facts, words, language, problems),
            ["table:query-fields"] = language => FieldTable(facts, words, language, problems),
            ["list:reserved-words"] = language => ReservedWords(words, language, problems),
        };
    }

    internal string Resolve(string text, string language, string where) =>
        Placeholder.Replace(text, match =>
        {
            var name = match.Groups["name"].Value;
            if (_byName.TryGetValue(name, out var value))
            {
                return value(language);
            }

            _problems.Add($"tokens: {where} uses {{{{{name}}}}}, which is not a token.");
            return match.Value;
        });

    /// <summary>
    /// The exit code table, one row per constant in ExitCode.cs. A code whose sentence is
    /// missing is named in the problems, and so is a sentence about a code that no longer
    /// exists - the bridge runs both ways, which is what makes it worth having.
    /// </summary>
    private static string ExitCodeTable(ProductFacts facts, Translations words, string language, Problems problems)
    {
        var rows = new StringBuilder();
        foreach (var code in facts.ExitCodes)
        {
            var key = "exit." + code.Name.ToLowerInvariant();
            rows.Append(CultureInfo.InvariantCulture, $"<tr><td><code>{code.Value}</code></td><td>{words.Get(language, key, problems)}</td></tr>");
        }

        foreach (var stale in words.Keys
            .Where(key => key.StartsWith("exit.", StringComparison.Ordinal))
            .Where(key => !facts.ExitCodes.Any(code => "exit." + code.Name.ToLowerInvariant() == key)))
        {
            problems.Add($"tokens: i18n has '{stale}' and ExitCode.cs has no such code.");
        }

        return Table(words.Get(language, "table.head.code", problems), words.Get(language, "table.head.meaning", problems), rows.ToString());
    }

    private static string SwitchTable(ProductFacts facts, Translations words, string language, Problems problems)
    {
        var rows = new StringBuilder();
        foreach (var option in facts.Switches)
        {
            var key = "switch." + option.Name.TrimStart('-');
            var verbs = string.Join(", ", option.Verbs.Select(verb => $"<code>{verb}</code>"));
            rows.Append(CultureInfo.InvariantCulture, $"<tr><td><code>{option.Name}</code></td><td>{words.Get(language, key, problems)}</td><td class=\"verbs\">{verbs}</td></tr>");
        }

        foreach (var stale in words.Keys
            .Where(key => key.StartsWith("switch.", StringComparison.Ordinal))
            .Where(key => !facts.Switches.Any(option => "switch." + option.Name.TrimStart('-') == key)))
        {
            problems.Add($"tokens: i18n has '{stale}' and OptionSurface.cs has no such switch.");
        }

        return Table(
            words.Get(language, "table.head.switch", problems),
            words.Get(language, "table.head.what", problems),
            rows.ToString(),
            words.Get(language, "table.head.where", problems));
    }

    /// <summary>
    /// Every field of the query language, its sentence, and the values it accepts. A field that
    /// takes text, a number or a size has no list of values, and the cell says which of the four
    /// it is rather than being left empty - empty would read as "this field accepts nothing".
    /// </summary>
    private static string FieldTable(ProductFacts facts, Translations words, string language, Problems problems)
    {
        var rows = new StringBuilder();
        foreach (var field in facts.Fields)
        {
            var values = field.Values.Count > 0
                ? string.Join(", ", field.Values.Select(value => $"<code>{value}</code>"))
                : words.Get(language, "field.open." + field.Name, problems);

            rows.Append(CultureInfo.InvariantCulture, $"<tr><td><code>{field.Name}</code></td><td>{words.Get(language, "field." + field.Name, problems)}</td><td>{values}</td></tr>");
        }

        foreach (var stale in words.Keys
            .Where(key => key.StartsWith("field.", StringComparison.Ordinal) && !key.StartsWith("field.open.", StringComparison.Ordinal))
            .Where(key => !facts.Fields.Any(field => "field." + field.Name == key)))
        {
            problems.Add($"tokens: i18n has '{stale}' and the query language has no such field.");
        }

        return Table(
            words.Get(language, "table.head.field", problems),
            words.Get(language, "table.head.asks", problems),
            rows.ToString(),
            words.Get(language, "table.head.values", problems));
    }

    private static string ReservedWords(Translations words, string language, Problems problems)
    {
        var items = ProductFacts.ReservedWords
            .Select(word => $"<li><code>{Html.Escape(word)}</code> - {words.Get(language, "reserved." + Key(word), problems)}</li>");

        return "<ul class=\"prose-list\">" + string.Join("", items) + "</ul>";
    }

    private static string Key(string word) => word == "?" ? "unreadable" : word;

    private static string Table(string first, string second, string rows, string? third = null)
    {
        var head = third is null
            ? $"<tr><th>{first}</th><th>{second}</th></tr>"
            : $"<tr><th>{first}</th><th>{second}</th><th>{third}</th></tr>";

        return $"<div class=\"table-wrap\"><table><thead>{head}</thead><tbody>{rows}</tbody></table></div>";
    }
}
