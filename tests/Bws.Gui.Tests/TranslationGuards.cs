using System.Globalization;
using System.Text.Json;

namespace Bws.Gui.Tests;

/// <summary>
/// Every language built into the window, held to English: the same keys, the same values in each
/// sentence, and rule 12 in the words.
///
/// <b>Written 2026-11-17, the day Polish became the second language built in.</b> Until then there
/// was nothing to compare, and every other guard that reads a sentence reads English only - this
/// test host pins the window to English (LanguagePinGuards), so a test that asks Texts for a sentence
/// never sees another language. A translated sentence that lost its {1} would show on screen without
/// the entry it names, and nothing in this suite would ever have asked for it.
///
/// <b>Complete, not merely correct.</b> ADR-21 lets a file dropped beside the program be unfinished,
/// because an unfinished translation should be usable. A language BUILT IN is a different thing: it
/// ships in every release, and a sentence added to English and not to it reaches a person in English
/// without anybody choosing that. So a built-in language holds every English key, and a new English
/// sentence brings its translations with it in the same change.
///
/// <b>What this does NOT check:</b> that a translation is good - whether a word is the one Windows
/// uses there, or a sentence reads naturally, is for a person reading the window.
/// tools/i18n/coverage.ps1 says how many sentences are word for word English, which is the shape of
/// one nobody touched.
/// </summary>
public sealed class TranslationGuards
{
    [Fact]
    public void Every_language_built_in_says_everything_english_says_with_the_same_values()
    {
        var english = Strings(Document(Texts.Fallback));
        var others = Built().Where(code => code != Texts.Fallback).ToList();

        // Without a second language the loop below compares nothing and passes, which is the guard
        // that cannot fail the coverage tool's header warns about.
        Assert.NotEmpty(others);

        var problems = others.SelectMany(code => Problems(code, Document(code), english)).ToList();

        Assert.True(
            problems.Count == 0,
            "A translation built into the window disagrees with English:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void No_sentence_in_any_language_built_in_carries_a_semicolon_or_a_long_dash()
    {
        var found = Built()
            .SelectMany(code => Strings(Document(code)).Where(pair => Forbidden(pair.Value)).Select(pair => code + ": " + pair.Key))
            .ToList();

        Assert.True(
            found.Count == 0,
            "Rule 12 holds on screen too - a flat hyphen, and no semicolons:" + Environment.NewLine
            + string.Join(Environment.NewLine, found));
    }

    /// <summary>
    /// The canary for the test above it: each kind of fault, planted in a translation invented here,
    /// has to be named. A comparison that quietly returned nothing would pass every real file.
    /// </summary>
    [Theory]
    [InlineData("""{ "_meta": { "code": "xx" }, "a": "Zatrzymaj {0}", "b": "Plik {1} z {0}" }""", "")]
    [InlineData("""{ "_meta": { "code": "xx" }, "a": "Zatrzymaj", "b": "Plik {1} z {0}" }""", "a holds")]
    [InlineData("""{ "_meta": { "code": "xx" }, "a": "Zatrzymaj {0} {2}", "b": "Plik {1} z {0}" }""", "a holds")]
    [InlineData("""{ "_meta": { "code": "xx" }, "a": "Zatrzymaj {0}" }""", "b is missing")]
    [InlineData("""{ "_meta": { "code": "xx" }, "a": "Zatrzymaj {0}", "b": "Plik {1} z {0}", "c": "Nikt" }""", "c is not in English")]
    [InlineData("""{ "_meta": { "code": "yy" }, "a": "Zatrzymaj {0}", "b": "Plik {1} z {0}" }""", "names itself yy")]
    [InlineData("""{ "a": "Zatrzymaj {0}", "b": "Plik {1} z {0}" }""", "names itself nothing")]
    [InlineData("""{ "_meta": { "code": "xx" }, "a": "Zatrzymaj {{0}}", "b": "Plik {1} z {0}" }""", "a holds")]
    [InlineData("""{ "_meta": { "code": "xx" }, "a": "Zatrzymaj {{{0}}}", "b": "Plik {1,4} z {0:N0}" }""", "")]
    public void The_comparison_names_each_kind_of_fault_it_is_written_for(string translation, string expected)
    {
        var english = new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "Stop {0}", ["b"] = "File {0} of {1}" };

        var problems = Problems("xx", Parse(translation), english);

        if (expected.Length == 0)
        {
            Assert.Empty(problems);
        }
        else
        {
            Assert.Contains(problems, problem => problem.Contains(expected, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void A_semicolon_and_both_long_dashes_are_found_and_a_flat_hyphen_is_not()
    {
        Assert.True(Forbidden("Stop; then start"));
        Assert.True(Forbidden("Stop " + (char)0x2013 + " start"));
        Assert.True(Forbidden("Stop " + (char)0x2014 + " start"));
        Assert.False(Forbidden("Stop - start, then look"));
    }

    /// <summary>What one translation gets wrong against English, each fault as a line naming its key.</summary>
    internal static List<string> Problems(string code, JsonElement translation, IReadOnlyDictionary<string, string> english)
    {
        var problems = new List<string>();
        var named = NamedCode(translation);

        if (!string.Equals(named, code, StringComparison.Ordinal))
        {
            problems.Add(code + ": the file names itself " + (named ?? "nothing"));
        }

        var strings = Strings(translation);

        problems.AddRange(strings.Select(pair => Fault(code + ": " + pair.Key, pair.Value, english.GetValueOrDefault(pair.Key))).OfType<string>());
        problems.AddRange(english.Keys.Where(key => !strings.ContainsKey(key)).Select(key => code + ": " + key + " is missing"));

        return problems;
    }

    /// <summary>The code a file gives itself under _meta, or nothing when it gives none.</summary>
    private static string? NamedCode(JsonElement file) =>
        file.TryGetProperty("_meta", out var meta)
        && meta.ValueKind == JsonValueKind.Object
        && meta.TryGetProperty("code", out var said)
        && said.ValueKind == JsonValueKind.String
            ? said.GetString()
            : null;

    /// <summary>What is wrong with one translated sentence against its English one, or nothing.</summary>
    private static string? Fault(string where, string text, string? original)
    {
        if (original is null)
        {
            return where + " is not in English";
        }

        return Holes(text) == Holes(original)
            ? null
            : where + " holds [" + Holes(text) + "] where English holds [" + Holes(original) + "]";
    }

    private static bool Forbidden(string text) => text.IndexOfAny([';', (char)0x2013, (char)0x2014]) >= 0;

    /// <summary>
    /// The values a sentence takes, as string.Format reads it: a doubled brace is a brace on screen and
    /// takes nothing, so "{{0}}" holds no value while "{{{0}}}" holds one between two braces.
    /// </summary>
    private static string Holes(string text)
    {
        var holes = new SortedSet<int>();

        for (var at = 0; at < text.Length; at++)
        {
            if (text[at] != '{')
            {
                continue;
            }

            if (at + 1 < text.Length && text[at + 1] == '{')
            {
                at++;
                continue;
            }

            var digits = new string(text[(at + 1)..].TakeWhile(char.IsAsciiDigit).ToArray());

            if (digits.Length > 0)
            {
                holes.Add(int.Parse(digits, CultureInfo.InvariantCulture));
            }
        }

        return string.Join(",", holes);
    }

    /// <summary>The sentences of one language built in, by key - for a guard that lays them over a window.</summary>
    internal static Dictionary<string, string> Sentences(string code) => Strings(Document(code));

    /// <summary>The languages this build carries inside itself, read from its resources rather than listed.</summary>
    internal static List<string> Built() =>
        typeof(Texts).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Texts.Prefix, StringComparison.Ordinal) && name.EndsWith(Texts.Suffix, StringComparison.Ordinal))
            .Select(name => name[Texts.Prefix.Length..^Texts.Suffix.Length])
            .Order(StringComparer.Ordinal)
            .ToList();

    private static JsonElement Document(string code)
    {
        using var stream = typeof(Texts).Assembly.GetManifestResourceStream(Texts.Prefix + code + Texts.Suffix)
            ?? throw new InvalidOperationException("No language " + code + " is built into the window.");
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.Clone();
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    private static Dictionary<string, string> Strings(JsonElement file) =>
        file.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
}
