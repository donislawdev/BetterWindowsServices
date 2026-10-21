using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Bws.Site;

/// <summary>
/// Which translations are older than the English they follow - and the stamp that says so.
///
/// <b>The mechanism.</b> A translation carries <c>&lt;meta name="translated-from" content="..."&gt;</c>
/// (a language file carries <c>"_translated_from"</c>), which is a short hash of the default
/// language's file AS IT WAS when the translation was made. The build hashes the default file now
/// and compares. Different means the English moved on.
///
/// <b>Why a report and not a failure, by the owner's decision of 2026-10-21.</b> The program is
/// developed daily and the English text follows it. A build that failed on every English edit
/// until twenty-one languages were redone would be switched off within a week - or worse, kept
/// green by re-stamping a page nobody retranslated, which is a lie the check cannot see. So the
/// list is printed on every build, and <c>--require-fresh</c> turns it into failures for the
/// moment that wants it, such as the day before a release.
///
/// <b>What a missing stamp is.</b> A problem, always: without it there is nothing to compare, and
/// a translation that cannot say what it follows is a translation nobody can ever call stale.
///
/// <b>The hash ignores line endings and surrounding whitespace.</b> The working tree on the
/// owner's machine is CRLF and the repository is LF; a hash that saw the difference would call
/// every translation stale on one of the two.
///
/// <b>What it cannot see, said here rather than found out.</b> That a page was retranslated
/// properly. <c>--stamp</c> writes whatever the English hash is now, so stamping a file nobody
/// touched is possible, and nothing here can tell. The stamp is a promise by whoever stamps it.
/// Also: the language files are stamped as a whole, so one changed English sentence marks every
/// language file stale, not just the key.
/// </summary>
internal static class Freshness
{
    private static readonly Regex StampLine = new(
        @"<meta name=""translated-from"" content=""[^""]*"">",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    private static readonly Regex JsonStampLine = new(
        @"""_translated_from""\s*:\s*""[^""]*""",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    private static readonly Regex HeadLine = new(
        @"^[ \t]*<meta name=""(?:title|description|nav)"" content=""[^""]*"">[ \t]*\r?$",
        RegexOptions.Multiline,
        TimeSpan.FromSeconds(1));

    /// <summary>Twelve hex digits: 48 bits is not a security property, it is enough that two edits never collide by accident.</summary>
    internal static string Hash(string text)
    {
        var normal = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normal)))[..12];
    }

    internal static string PagePath(string root, string pageId, string language) =>
        Path.Combine(root, "site", "pages", pageId, language + ".html");

    internal static string WordsPath(string root, string language) =>
        Path.Combine(root, "site", "i18n", language + ".json");

    internal static void Check(
        string root,
        SiteConfig site,
        IReadOnlyList<PageDefinition> pages,
        Dictionary<(string Id, string Language), PageContent> contents,
        Translations words,
        Problems problems,
        List<string> stale)
    {
        var source = site.DefaultLanguage;

        foreach (var page in pages)
        {
            if (!contents.TryGetValue((page.Id, source), out var original))
            {
                continue;
            }

            if (original.TranslatedFrom is not null)
            {
                problems.Add($"freshness: {page.Id}/{source}.html is the original and carries a translated-from stamp, which only a translation has.");
            }

            var current = Hash(File.ReadAllText(PagePath(root, page.Id, source)));
            foreach (var language in site.Languages.Where(language => language != source))
            {
                if (!contents.TryGetValue((page.Id, language), out var translation))
                {
                    continue;
                }

                Judge($"{page.Id}/{language}.html", translation.TranslatedFrom, current, language, problems, stale);
            }
        }

        var wordsNow = Hash(File.ReadAllText(WordsPath(root, source)));
        foreach (var language in site.Languages.Where(language => language != source))
        {
            Judge($"i18n/{language}.json", words.StampOf(language), wordsNow, language, problems, stale);
        }
    }

    private static void Judge(string file, string? stamp, string current, string language, Problems problems, List<string> stale)
    {
        if (stamp is null)
        {
            problems.Add($"freshness: {file} has no translated-from stamp. Once it really follows the English, run: dotnet run --project site/Bws.Site -- --stamp {language}");
        }
        else if (!string.Equals(stamp, current, StringComparison.Ordinal))
        {
            stale.Add($"{file} follows English {stamp}, and the English is now {current}.");
        }
    }

    /// <summary>
    /// Files for a language the registry has never heard of. A language that is registered and
    /// not published is allowed to have files - that is how it is prepared - but a file named
    /// after a code that is nowhere is a typo (<c>pt.html</c> for <c>pt-br.html</c>) and would
    /// sit in the repository for ever, built by nothing.
    /// </summary>
    internal static void CheckForStrays(string root, SiteConfig site, Problems problems)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "site", "i18n"), "*.json"))
        {
            var code = Path.GetFileNameWithoutExtension(file);
            if (!site.LanguageRegistry.ContainsKey(code))
            {
                problems.Add($"languages: site/i18n/{code}.json belongs to no language in site.json's language_registry.");
            }
        }

        foreach (var folder in Directory.EnumerateDirectories(Path.Combine(root, "site", "pages")))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.html"))
            {
                var code = Path.GetFileNameWithoutExtension(file);
                if (!site.LanguageRegistry.ContainsKey(code))
                {
                    problems.Add($"languages: site/pages/{Path.GetFileName(folder)}/{code}.html belongs to no language in site.json's language_registry.");
                }
            }
        }
    }

    /// <summary>
    /// Writes the stamp into every file of a language (or of all of them). Returns the number of
    /// files written. A file is rewritten only when the stamp is not already right, so running it
    /// twice changes nothing the second time.
    /// </summary>
    internal static int Stamp(string root, SiteConfig site, string selector, IReadOnlyList<PageDefinition> pages, out string message)
    {
        var languages = selector == "all"
            ? site.LanguageRegistry.Keys.Where(code => code != site.DefaultLanguage).ToList()
            : [selector];

        if (selector != "all" && (selector == site.DefaultLanguage || !site.LanguageRegistry.ContainsKey(selector)))
        {
            message = $"'{selector}' is not a translation in site.json's language_registry (the default language is the original and is not stamped).";
            return -1;
        }

        var written = 0;
        foreach (var language in languages)
        {
            foreach (var page in pages)
            {
                var original = PagePath(root, page.Id, site.DefaultLanguage);
                var path = PagePath(root, page.Id, language);
                if (!File.Exists(original) || !File.Exists(path))
                {
                    continue;
                }

                written += StampPage(path, Hash(File.ReadAllText(original))) ? 1 : 0;
            }

            var words = WordsPath(root, language);
            if (File.Exists(words))
            {
                written += StampWords(words, Hash(File.ReadAllText(WordsPath(root, site.DefaultLanguage)))) ? 1 : 0;
            }
        }

        message = $"stamped {written} file(s) for {(selector == "all" ? "every translation" : selector)}";
        return written;
    }

    private static bool StampPage(string path, string hash)
    {
        var text = File.ReadAllText(path);
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var line = $"<meta name=\"translated-from\" content=\"{hash}\">";

        string updated;
        if (StampLine.IsMatch(text))
        {
            updated = StampLine.Replace(text, line, 1);
        }
        else
        {
            // After the last of the head lines, so the stamp sits with them and never in the body.
            var last = HeadLine.Matches(text).LastOrDefault();
            if (last is null)
            {
                return false;
            }

            var end = last.Index + last.Length;
            var tail = text[end..];
            updated = text[..end].TrimEnd('\r') + newline + line + (text[(end - 1)..end] == "\r" ? "\r" : "") + tail;
        }

        if (string.Equals(updated, text, StringComparison.Ordinal))
        {
            return false;
        }

        File.WriteAllText(path, updated, new UTF8Encoding(false));
        return true;
    }

    private static bool StampWords(string path, string hash)
    {
        var text = File.ReadAllText(path);
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var entry = $"\"{Translations.StampKey}\": \"{hash}\"";

        string updated;
        if (JsonStampLine.IsMatch(text))
        {
            updated = JsonStampLine.Replace(text, entry, 1);
        }
        else
        {
            var open = text.IndexOf('{', StringComparison.Ordinal);
            updated = text[..(open + 1)] + newline + "  " + entry + "," + text[(open + 1)..];
        }

        if (string.Equals(updated, text, StringComparison.Ordinal))
        {
            return false;
        }

        File.WriteAllText(path, updated, new UTF8Encoding(false));
        return true;
    }
}
