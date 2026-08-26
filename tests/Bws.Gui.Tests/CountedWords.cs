// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Text.RegularExpressions;

namespace Bws.Gui.Tests;

/// <summary>
/// Which words in this product are plural nouns, and the guard that will not let that list go
/// quietly out of date.
///
/// <b>Split out of PluralGuards on 2026-08-26 when the size ratchet fired, and the seam was
/// already there:</b> everything in that file drives the plan panel and reads what it says,
/// while this is about the LANGUAGE FILES and the English in them. Two subjects, one file, and
/// the ratchet is what made somebody look.
///
/// <b>THE WORDS BETWEEN THE NUMBER AND THE NOUN ARE WHY THIS IS NOT <c>\b1 \w+s\b</c>, AND THE
/// MUTATION REGISTER IS WHAT SAID SO.</b> Carried here from PluralGuards with the pattern, because
/// a lesson left behind describing code that moved is prose about nothing. The first version
/// demanded the plural immediately after the one, was written with "1 other entries" quoted as an
/// example it caught, and did not catch it - the entry for the cascade warning came back MISSED,
/// which is the outcome that means a guard does not check what its name claims.
///
/// <b>What this deliberately does NOT catch:</b> a group pronoun standing for a list of one -
/// "these", "those", "they", "them" - which is four of the seven sentences PluralGuards was written
/// for. Those are held by name over there, where an exact string can be checked without guessing at
/// English. The pronouns were tried in this pattern and came out: "them" hits
/// <c>gui.plan.problem.notOperable.one</c> - "This tool shows drivers but does not start or stop
/// them" - where the plural is about drivers in general and is correct.
/// </summary>
internal static class CountedWords
{
    /// <summary>
    /// The nouns this product counts, named rather than guessed at by shape.
    ///
    /// <b>THE SHAPE-BASED PATTERN WAS WRONG AND IT IS MEASURED, not argued.</b> It read
    /// <c>\b1\s+(?:\w+\s+){0,2}\w+s\b</c> - a one, up to two words, then anything ending in s -
    /// and on 2026-08-26 it was checked against five correct sentences and five faulty ones:
    ///
    ///     two words of slack   four false alarms of five, none missed
    ///     one word of slack    the same four
    ///     no slack at all      no false alarms, three of five faults missed
    ///
    /// No setting works, and the reason is structural rather than a matter of tuning: "1 entry
    /// was" and "1 stopped services" have the SAME SHAPE - a one, a word, a word ending in s.
    /// Backlog 225.
    ///
    /// <b>The candidate written in that backlog row - a list of verbs to skip - was measured and
    /// rejected too.</b> Counting what actually follows a number in both English language files
    /// found twelve such words already: is, has, was, does, needs, says, stops, starts, expects -
    /// and three that are not verbs at all. <c>this</c> ends in s. So does <c>ms</c>. So does
    /// <c>process</c>. That side of English is open and will keep growing, so a guard built on it
    /// rots towards the false alarm, which is the direction this guard's own header forbids.
    ///
    /// <b>So the nouns are named instead, and the list rots the other way</b> - towards missing a
    /// fault rather than towards shouting about a correct sentence. That trade is deliberate and
    /// it is not free, which is why <see cref="Every_counted_word_in_the_language_files_is_classified"/>
    /// exists directly below: it will not let a new word appear unclassified.
    ///
    /// Measured occurrences after a count, both files, 2026-08-26: entries 22, instances 2,
    /// steps 1. The rest of this list is the domain vocabulary that has not been counted yet and
    /// would be wrong the day it is.
    /// </summary>
    internal static readonly string[] Nouns =
    [
        "entries", "instances", "steps",
        "services", "drivers", "templates", "dependents", "rows", "columns", "plans"
    ];

    /// <summary>
    /// Words ending in s that follow a count and are NOT a plural noun, so the guard above must
    /// not read them as one.
    ///
    /// Nothing matches against this list - it exists so that the classification guard can tell a
    /// word somebody has already thought about from one nobody has.
    /// </summary>
    internal static readonly string[] NotNouns =
    [
        // Verbs in the third person singular, which is exactly what a correct sentence about ONE
        // thing uses - so every one of these appears in a sentence the old pattern reddened on.
        "is", "was", "has", "does", "needs", "says", "stops", "starts", "expects", "shares",
        // Not verbs, and the reason no shape can do this job. A unit, a determiner and a singular
        // noun that happens to end in s.
        "ms", "this", "process"
    ];

    internal static readonly Regex Plural =
        new(
            @"\b1\s+(?:\w+\s+){0,2}(?:" + string.Join('|', Nouns) + @")\b",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
}

/// <summary>
/// The guard that keeps <see cref="CountedWords"/> from going quietly out of date.
///
/// Its own class because a test has to be an instance member of a public class, and the vocabulary
/// beside it has to be static so the pattern can be built from it once.
/// </summary>
public sealed class CountedWordGuards
{
    /// <summary>
    /// Every word that can follow a count in the language files is either a plural noun this
    /// guard knows or a word somebody has decided is not one.
    ///
    /// <b>THIS IS THE PRICE OF NAMING THE NOUNS, PAID RATHER THAN LEFT UNPAID.</b> A guard built
    /// on a list misses whatever is not on it, and a guard that misses in silence is worse than
    /// one that is merely narrow - backlog 238 is that lesson written down after it cost a real
    /// gate. So the list cannot go quietly out of date: add a sentence saying "{0} sessions" and
    /// this goes red until somebody says which kind of word "sessions" is.
    ///
    /// <b>It reads the files rather than the loaded resources</b>, for the same reason the guard
    /// beside it does: what ships is the file, and a loader that dropped a key would make both of
    /// them agree about nothing.
    /// </summary>
    [Fact]
    public void Every_counted_word_in_the_language_files_is_classified()
    {
        var known = new HashSet<string>(CountedWords.Nouns, StringComparer.Ordinal);
        known.UnionWith(CountedWords.NotNouns);

        var unclassified = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in new[]
                 {
                     Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Resources", "gui.en.json"),
                     Path.Combine(SourceTree.Root(), "src", "Bws.Cli", "Resources", "cli.en.json")
                 })
        {
            // The same window the matching pattern uses - a placeholder, up to two words, then a
            // word ending in s. Anything this finds is something that pattern could have to judge.
            foreach (Match found in Regex.Matches(
                         File.ReadAllText(file),
                         @"\{[0-9]\}\s+(?:\w+\s+){0,2}(\w+s)\b",
                         RegexOptions.None,
                         TimeSpan.FromSeconds(5)))
            {
                var word = found.Groups[1].Value;

                if (!known.Contains(word))
                {
                    unclassified.Add(word);
                }
            }
        }

        Assert.True(
            unclassified.Count == 0,
            "These words follow a count in a language file and this guard does not know what they "
            + "are, so it cannot tell \"1 entries\" from \"1 entry was\". Put each one in "
            + "CountedWords.Nouns if it is a plural noun, or in CountedWords.NotNouns if it is not - "
            + "and if it "
            + "is a plural noun, check first that no sentence pairs it with a one:"
            + Environment.NewLine + string.Join(", ", unclassified));
    }
}
