using Bws.Cli;

namespace Bws.Cli.Tests;

/// <summary>
/// What the command line turns back before it opens anything.
///
/// <b>The property being asserted is WHERE the answer comes from, not what it says.</b> Every
/// refusal here was already the right sentence with the right code - two of them were simply given
/// after the service control manager had been read, which is about half a second spent to tell
/// somebody they left a word out. Refusals exists in front of the expensive work for exactly that,
/// and asking it directly is how a test can tell the two positions apart at all. A test that ran
/// the program and checked the exit code would pass either way.
/// </summary>
public sealed class ArgumentRefusalTests
{
    [Fact]
    public void Show_without_a_name_is_answered_before_the_manager_is_opened()
    {
        Assert.Equal(ExitCode.Usage, Answer("show"));
    }

    [Fact]
    public void A_comparison_with_one_side_is_answered_before_the_manager_is_opened()
    {
        Assert.Equal(ExitCode.Usage, Answer("snapshot", "diff", "before.json"));
    }

    [Fact]
    public void A_comparison_with_three_sides_is_answered_before_the_manager_is_opened()
    {
        // Refused rather than resolved by picking one, because either choice silently ignores
        // something the person typed.
        Assert.Equal(ExitCode.Usage, Answer("snapshot", "diff", "before.json", "after.json", "--live"));
    }

    [Fact]
    public void A_comparison_with_two_files_has_nothing_wrong_with_it()
    {
        // The half that makes the three above mean something: this must reach the machine.
        Assert.Null(Answer("snapshot", "diff", "before.json", "after.json"));
    }

    /// <summary>
    /// An empty query is a mistake, and no query at all is not - owner's decision, 2026-08-26.
    ///
    /// <b>The fourth spelling of one mistake.</b> The language already turns back <c>!!!</c>,
    /// <c>name:""</c> and a field with nothing after it, every time with the same sentence: a
    /// script with a typo in its query must not be handed the whole machine and a code of success.
    /// This spelling never reached the parser as a term - the empty text arrived as an empty query,
    /// and an empty query legitimately means everything.
    /// </summary>
    [Theory]
    [InlineData("--query=")]
    [InlineData("--query= ")]
    public void An_empty_query_written_with_an_equals_sign_is_incomplete(string spelling)
    {
        Assert.Contains("--query", CommandLine.Read(["list", spelling]).Incomplete, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_query_written_as_a_separate_word_is_incomplete(string value)
    {
        Assert.Contains("--query", CommandLine.Read(["list", "--query", value]).Incomplete, StringComparer.Ordinal);
    }

    [Fact]
    public void No_query_at_all_is_not_a_mistake()
    {
        // The distinction the whole change rests on. Leaving the switch off says nothing about
        // narrowing. Writing it with nothing after it says somebody meant to narrow and what they
        // wrote came to nothing - which is what a shell does with a variable that expanded to
        // nothing.
        Assert.Empty(CommandLine.Read(["list"]).Incomplete);
        Assert.Empty(CommandLine.Read(["list", "--query", "status:running"]).Incomplete);
    }


    /// <summary>
    /// A word a verb has no room for is kept apart from the options nobody knows.
    ///
    /// <b>Backlog 233, and the parser is where the two have to part company.</b> Until 2026-09-01
    /// both went into one list, so <c>bws start type Spooler manual</c> answered "Unknown option:
    /// Spooler, manual" - naming as options two words that are spelled perfectly and are not
    /// options at all, and sending somebody to hunt for a typo they had not made.
    ///
    /// <b>The verb still takes its name, and that is what makes the rest extra.</b> Asserting only
    /// that the list is not empty would pass just as well against a parser that gave up on the
    /// whole line.
    /// </summary>
    [Fact]
    public void A_word_a_verb_has_no_room_for_is_not_an_unknown_option()
    {
        var typed = CommandLine.Read(["start", "type", "Spooler", "manual"]);

        Assert.Empty(typed.Rejected);
        Assert.Equal(["Spooler", "manual"], typed.Extra);
        Assert.Equal("type", typed.ServiceName);
    }

    /// <summary>
    /// The fault is older than the verb that made it easy to meet, so the guard covers both.
    ///
    /// <c>bws stop A B</c> has answered "unknown option" since the day stop was built, and
    /// <c>list</c> takes no bare word at all - a second shape for the same rule, which is what
    /// rule 12 asks of a criterion before it is written down.
    /// </summary>
    [Fact]
    public void A_second_name_and_a_name_where_none_belongs_are_both_extra_words()
    {
        Assert.Equal(["B"], CommandLine.Read(["stop", "A", "B"]).Extra);
        Assert.Equal(["Spooler"], CommandLine.Read(["list", "Spooler"]).Extra);

        // An option nobody knows is still an option nobody knows, which is the half that keeps
        // this from being a parser that stopped rejecting anything.
        Assert.Equal(["--nope"], CommandLine.Read(["list", "--nope"]).Rejected);
    }
    private static int? Answer(params string[] arguments) => Refusals.Answer(CommandLine.Read(arguments));
}
