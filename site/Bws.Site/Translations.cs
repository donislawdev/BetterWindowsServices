using System.Text.Json;

namespace Bws.Site;

/// <summary>
/// The words of the site's chrome and reference tables, per language, from <c>site/i18n/{code}.json</c>.
///
/// <b>Every language file has to carry exactly the same keys, and a missing one fails the build
/// rather than falling back to English.</b> A fallback is a page that is half in one language
/// and half in another and looks finished. ChronoMock's site keeps the same rule and it is the
/// right one: the Polish file is either complete or the build says which line is missing.
///
/// Only these files may hold text outside ASCII among the site's JSON - the architecture guard
/// that sweeps every *.json in the repository lists them by name with the reason.
/// </summary>
internal sealed class Translations
{
    private readonly Dictionary<string, Dictionary<string, string>> _byLanguage;

    private Translations(Dictionary<string, Dictionary<string, string>> byLanguage)
    {
        _byLanguage = byLanguage;
    }

    internal IEnumerable<string> Keys => _byLanguage.Values.First().Keys;

    internal static Translations Load(string root, IEnumerable<string> languages, Problems problems)
    {
        var byLanguage = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var language in languages)
        {
            var path = Path.Combine(root, "site", "i18n", language + ".json");
            if (!File.Exists(path))
            {
                problems.Add($"i18n: {path} is missing.");
                byLanguage[language] = new Dictionary<string, string>(StringComparer.Ordinal);
                continue;
            }

            byLanguage[language] = Read(path, problems);
        }

        CheckParity(byLanguage, problems);
        return new Translations(byLanguage);
    }

    /// <summary>The text for a key, or the key itself in brackets - and a problem - when there is none.</summary>
    internal string Get(string language, string key, Problems problems)
    {
        if (_byLanguage.TryGetValue(language, out var words) && words.TryGetValue(key, out var text))
        {
            return text;
        }

        problems.Add($"i18n: {language}.json has no key '{key}'.");
        return "[" + key + "]";
    }

    internal bool Has(string language, string key) =>
        _byLanguage.TryGetValue(language, out var words) && words.ContainsKey(key);

    private static Dictionary<string, string> Read(string path, Problems problems)
    {
        var words = new Dictionary<string, string>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Name.StartsWith('_'))
            {
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                problems.Add($"i18n: {path} key '{property.Name}' is not a string.");
                continue;
            }

            words[property.Name] = property.Value.GetString() ?? "";
        }

        return words;
    }

    private static void CheckParity(Dictionary<string, Dictionary<string, string>> byLanguage, Problems problems)
    {
        var all = byLanguage.Values.SelectMany(words => words.Keys).ToHashSet(StringComparer.Ordinal);
        foreach (var (language, words) in byLanguage)
        {
            foreach (var key in all.Where(key => !words.ContainsKey(key)).Order(StringComparer.Ordinal))
            {
                problems.Add($"i18n: {language}.json lacks '{key}', which another language has.");
            }
        }
    }
}
