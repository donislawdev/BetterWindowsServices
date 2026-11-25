using System.Text.RegularExpressions;

namespace Bws.Site.Tests;

/// <summary>
/// Every verb the reference page prints beside a switch is one the command line reads.
///
/// <b>Written on 2026-11-25, when the page was found printing "setrecovery"</b> - a word nobody can type -
/// on all 22 languages for a day: the recovery verb arrived and the page's own spelling table did not
/// learn it. The page reads the switch table out of OptionSurface.cs as text, so this reads the verb list
/// out of the same file the same way, and the two cannot be checked by a compiler.
/// </summary>
public sealed class VerbSpellingGuards
{
    [Fact]
    public void Every_verb_beside_a_switch_is_a_word_the_command_line_reads()
    {
        var verbs = Verbs();
        var printed = ProductFacts.Read(Repository.Root()).Switches.SelectMany(fact => fact.Verbs).Distinct().ToList();

        Assert.NotEmpty(printed);
        Assert.All(printed, word => Assert.True(
            verbs.Contains(word.Split(' ')[0]),
            $"The reference page prints '{word}' beside a switch, and the command line has no verb '{word.Split(' ')[0]}'."));
    }

    [Fact]
    public void A_kind_the_spelling_table_does_not_know_comes_out_as_a_word_the_check_refuses()
    {
        // The canary: without it the check above could pass by reading an empty or wrong list.
        Assert.DoesNotContain(ProductFacts.Spelling("SetSomethingNew"), Verbs());
    }

    private static HashSet<string> Verbs()
    {
        var text = File.ReadAllText(Path.Combine(Repository.Root(), "src", "Bws.Cli", "OptionSurface.cs"));
        var list = Regex.Match(text, @"IReadOnlyList<string> Verbs =>\s*\[(?<words>[^\]]*)\]", RegexOptions.None, TimeSpan.FromSeconds(1));

        Assert.True(list.Success, "The verb list was not found in OptionSurface.cs - its declaration changed shape.");

        var words = Regex.Matches(list.Groups["words"].Value, "\"(?<word>[a-z-]+)\"", RegexOptions.None, TimeSpan.FromSeconds(1))
            .Select(match => match.Groups["word"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("stop", words);
        return words;
    }
}
