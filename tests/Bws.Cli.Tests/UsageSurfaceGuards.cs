using System.Text.RegularExpressions;

namespace Bws.Cli.Tests;

/// <summary>
/// That the help mentions everything this tool actually accepts.
///
/// <b>Written 2026-09-06 to make a sentence true that had been false for a package.</b>
/// <c>UsageContractTests</c> in the integration tests holds a hand-written list of switches and
/// checks the help against it in both directions, and its own comment promised that adding a
/// switch to the tool without adding it there would fail. Nothing compared that list against the
/// tool. So <c>--restart</c> arrived in the parser, in the switch table and in a plan the core
/// renders, with the help saying nothing about it and every check green.
///
/// <b>Derived here on purpose, where that file is hand-written on purpose, and both are right.</b>
/// A list somebody maintains is what catches a switch that exists and is undocumented in
/// <c>docs/02</c> - a derived one would agree with the code whatever the code did. What a derived
/// check catches is the opposite fault: a surface that grew and a help that did not. Two questions,
/// two mechanisms, and the one this file asks needs to see <c>OptionSurface</c>, which only the
/// command line's own tests can.
///
/// <b>It reads the text rather than running the tool</b>, because the help is a resource string and
/// the process would add a second of nothing. The integration file already runs the real thing.
/// </summary>
public sealed class UsageSurfaceGuards
{
    [Fact]
    public void Every_switch_this_tool_accepts_is_mentioned_in_the_help()
    {
        var usage = Texts.Of("cli.usage");

        var missing = OptionSurface.Surface
            .Select(entry => entry.Option)
            .Where(option => !usage.Contains(option, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "These switches are accepted and the help says nothing about them, so somebody can only "
            + "find them by reading the source: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_verb_this_tool_accepts_is_mentioned_in_the_help()
    {
        var usage = Texts.Of("cli.usage");

        // The word as somebody types it, which is not the name of the enumeration value - see
        // OptionSurface.Spelling for the time that difference sent people looking for a command
        // called "snapshotcreate".
        var missing = OptionSurface.Verbs
            // A WHOLE WORD, NOT A SUBSTRING AND NOT "bws " AND THE VERB. The help writes three
            // of them on one line - bws stop|start|restart NAME - so demanding the tool name in
            // front of each would report a verb that is documented. And a bare substring would
            // let "start" be satisfied by "start-type", which is a different command.
            .Where(verb => !Regex.IsMatch(
                usage, @"(?<![\w-])" + Regex.Escape(verb) + @"(?![\w-])", RegexOptions.None,
                TimeSpan.FromSeconds(5)))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "These verbs exist and the help does not show how to use them: " + string.Join(", ", missing));
    }

    [Fact]
    public void The_help_was_actually_read()
    {
        // A GUARD THAT FINDS NOTHING MUST GO RED RATHER THAN GREEN. Both checks above pass happily
        // against an empty string, and an empty string is exactly what Texts.Of hands back for a key
        // nobody wrote - so a renamed resource would leave this file reporting no problems.
        var usage = Texts.Of("cli.usage");

        Assert.NotEqual("cli.usage", usage);
        Assert.Contains("bws list", usage, StringComparison.Ordinal);
    }
}
