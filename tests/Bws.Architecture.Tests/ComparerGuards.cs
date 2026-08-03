using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Every collection keyed by a string says which comparison it wants.
///
/// <b>Written 2026-08-03 as the answer to a question an audit asked about `MA0002`</b>, which is
/// the analyser rule that would demand exactly this and is switched off in `.editorconfig` with
/// a measurement beside it: turning it on produces 127 hits, and a guard that shouts at correct
/// code is one people learn to skip.
///
/// The rule underneath it is not optional, though, and it is rule 3 of the untouchable list.
/// <b>A service name is an identity and Windows compares it without case.</b> A dictionary keyed
/// by one that forgets <c>OrdinalIgnoreCase</c> does not fall over - it quietly fails to match
/// two spellings of one service, which in an audit tool means a wrong answer that looks like a
/// right one. `ADR-14` exists for that failure and this is it, one layer down.
///
/// So the shape here is narrower than the analyser's and answers the same question: not "which
/// comparison is right", which nothing mechanical can know, but <b>"was one chosen at all"</b>.
/// The default for a string key is ordinal and case sensitive, and it is right often enough that
/// nobody notices the times it is not.
///
/// <b>Only the product, and only the core plus what ships.</b> Tests build dictionaries to hold
/// expectations, and there the default is the point.
///
/// False alarm estimate, as `docs/04` requires before a guard is built: measured over the whole
/// product at the moment this was written, every one of the matching sites already passes a
/// comparer. So zero on the day, by construction, exactly like the size ratchet - and every hit
/// from here on is a decision somebody skipped.
/// </summary>
public sealed class ComparerGuards
{
    /// <summary>
    /// Building a collection over string keys without saying how the keys compare.
    ///
    /// Deliberately narrow. It looks for the constructions this product actually uses and does
    /// not try to catch every way one could be written - a pattern that reaches for everything
    /// over C# source is a pattern that starts matching comments and strings, and a guard with
    /// false alarms is worth less than none.
    /// </summary>
    private static readonly Regex Unspecified = new(
        @"new\s+(Dictionary|HashSet|SortedSet|SortedDictionary)<\s*string\s*[,>][^(]*\(\s*\)"
        + @"|\.To(Dictionary|HashSet|Lookup)\(\s*[^)]*\)\s*;",
        RegexOptions.Compiled,
        Sources.Ceiling);

    /// <summary>
    /// A comparer, in any of the spellings this product uses for one.
    ///
    /// Checked on the same line rather than inside the match, because <c>ToDictionary</c> takes
    /// its comparer as a last argument and the pattern above stops before it.
    /// </summary>
    private static readonly Regex Chosen = new(
        @"StringComparer\.|StringComparison\.|IEqualityComparer<string>",
        RegexOptions.Compiled,
        Sources.Ceiling);

    [Fact]
    public void Every_collection_over_string_keys_says_how_the_keys_compare()
    {
        var silent = new List<string>();

        foreach (var file in Sources.Shipped())
        {
            var lines = File.ReadAllLines(file);

            for (var number = 0; number < lines.Length; number++)
            {
                var line = lines[number];

                if (Unspecified.IsMatch(line) && !Chosen.IsMatch(line))
                {
                    silent.Add($"{Path.GetFileName(file)}:{number + 1}  {line.Trim()}");
                }
            }
        }

        Assert.True(
            silent.Count == 0,
            "A collection over string keys was built without saying how the keys compare. The "
            + "default is ordinal and case sensitive, which is right often enough that the times "
            + "it is not go unnoticed - and a service name is compared WITHOUT case, so a "
            + "dictionary keyed by one and left to the default silently fails to match two "
            + "spellings of one service. Say which comparison you want, even when it is the "
            + "default one:\n" + string.Join("\n", silent));
    }
}
