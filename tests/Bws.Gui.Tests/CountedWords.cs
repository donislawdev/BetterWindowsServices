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
    /// it is not free, which is why <see cref="CountedWordGuards.Every_counted_word_in_the_language_files_is_classified"/>
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
        // Two more on 2026-09-01, both from the command line repairs of that day and both caught
        // by the guard below on their first run. "{0} entry carries a field one snapshot never
        // read" is the singular half doing its job, and "{0} takes one name" counts nothing at all
        // - the placeholder there is a verb, not a number.
        "carries", "takes",
        // One more on 2026-09-06, from the sentence about who dies when a process is ended:
        // "Ending the process behind {0} also ends {1} other entries". The placeholder before it
        // is a service name rather than a number, so the verb after it stays singular whatever the
        // count of the OTHER placeholder in the same sentence is.
        "ends",
        // One more on 2026-09-08, from rung five's refusal: "end process {1}, which is the process
        // {0} runs in". Both placeholders in that sentence are names rather than numbers - one is
        // a process and one is a service - so the verb after the second stays singular no matter
        // what. This guard cannot tell a name from a count and is not meant to: it asks, and this
        // list is where the answer goes.
        "runs",
        // Two more on 2026-09-24, from package 5 of the UX audit, and both caught only by CI because
        // the session ran the window's tests filtered to its own classes. "{0} belongs to the load
        // order group {1}" puts a service name before the verb, and "could not read some of these
        // fields: {0} Press F5" puts a list of fields before the start of the next sentence.
        "belongs", "Press",
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

    /// <summary>
    /// Every sentence that counts something has a singular beside it.
    ///
    /// <b>BACKLOG 207, AND THIS IS THE GUARD THAT WOULD HAVE CAUGHT ALL NINE.</b> The command line
    /// carried nine keys counting entries with no singular anywhere - "Read 1 entries in 12 ms",
    /// "1 entries were judged on a field that could not be read" - and the window carried four.
    /// They were found by reading the language file with a pattern, by hand, twice. Nothing in this
    /// project would have gone red, which is the sentence <see cref="PluralGuards"/> opens with.
    ///
    /// <b>It asks about KEYS rather than about sentences, and that is the whole difference from the
    /// guard above.</b> That one holds the vocabulary honest - every word following a count is one
    /// somebody has classified. This one holds the SHAPE honest: a sentence that counts a noun this
    /// project pluralises has to live in a pair, so the code has a singular to reach for. A key
    /// ending in neither half is a sentence with only one way to be said.
    ///
    /// <b>Both files, because the fault crossed between them in both directions.</b> The pair for
    /// the shared process warning existed on the command line first and the window arrived without
    /// it. The pair for the counted entries existed nowhere.
    ///
    /// <b>What it deliberately does not catch, said rather than left to be found:</b> a plural with
    /// no number in it at all - "these drivers", "Those keep running". A pattern looking for a
    /// count cannot see one, which is written up in the header above and is why those sentences are
    /// held by name in <see cref="PluralGuards"/> instead.
    /// </summary>
    [Fact]
    public void Every_sentence_that_counts_something_is_one_half_of_a_pair()
    {
        var alone = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in new[]
                 {
                     Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Resources", "gui.en.json"),
                     Path.Combine(SourceTree.Root(), "src", "Bws.Cli", "Resources", "cli.en.json")
                 })
        {
            using var language = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));

            var keys = language.RootElement.EnumerateObject()
                .Where(entry => entry.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                .ToDictionary(entry => entry.Name, entry => entry.Value.GetString() ?? string.Empty, StringComparer.Ordinal);

            foreach (var (key, sentence) in keys)
            {
                // The same window the two guards above use - a placeholder, up to two words, then
                // a noun this project counts.
                if (!Regex.IsMatch(
                        sentence,
                        @"\{[0-9]\}\s+(?:\w+\s+){0,2}(?:" + string.Join('|', CountedWords.Nouns) + @")\b",
                        RegexOptions.None,
                        TimeSpan.FromSeconds(5)))
                {
                    continue;
                }

                if (Partner(key) is not { } half || !keys.ContainsKey(half))
                {
                    alone.Add(key);
                }
            }
        }

        Assert.True(
            alone.Count == 0,
            "These sentences count something and have no singular to fall back on, so a machine "
            + "holding one of it reads \"1 entries\". Give each a .one and a .many, and pick "
            + "between them at the call - a helper that appends the half is invisible to the guard "
            + "that checks a key is reachable:"
            + Environment.NewLine + string.Join(Environment.NewLine, alone));
    }

    /// <summary>
    /// The other half of a paired key, or nothing when the key is not one half of a pair.
    ///
    /// <b>THE HALF IS NOT ALWAYS THE LAST WORD, AND THAT WAS THIS GUARD'S ONE FALSE ALARM ON ITS
    /// FIRST RUN.</b> <c>gui.rollup.many.running</c> has had <c>gui.rollup.one.running</c> beside
    /// it since the day it was written, and a check that only looked at the end of the key called
    /// a correct pair a fault. `docs/06` asks for that number to be measured rather than found
    /// later: one false alarm, in the first run, fixed here.
    ///
    /// So the segment is swapped wherever it sits. A key holding neither segment is nothing rather
    /// than a guess, which is what makes it a fault above.
    /// </summary>
    private static string? Partner(string key)
    {
        var parts = key.Split('.');

        var half = Array.FindIndex(parts, part =>
            string.Equals(part, "one", StringComparison.Ordinal)
            || string.Equals(part, "many", StringComparison.Ordinal));

        if (half < 0)
        {
            return null;
        }

        parts[half] = parts[half] == "one" ? "many" : "one";

        return string.Join('.', parts);
    }
}
