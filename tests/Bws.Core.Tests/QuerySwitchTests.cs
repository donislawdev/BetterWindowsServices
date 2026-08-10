using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// The two things the language gained for the window: a bare word read as an expression, and
/// a query able to say whether it carries a particular exclusion.
///
/// Both exist because of a switch beside the search box, and both live here rather than in the
/// window for the same reason: working out which part of the text is a bare word, and which
/// member a switch stands for, is the language's job. A second copy of either in the interface
/// would drift from this one without a sound.
/// </summary>
public sealed class QuerySwitchTests
{
    [Fact]
    public void A_bare_word_is_text_to_be_contained_when_the_switch_is_down()
    {
        var query = QueryParserTests.Valid("^spooler$");

        // The characters are part of what is being looked for, so nothing has them.
        Assert.False(query.Match(Entries.Any).Matched);
    }

    [Fact]
    public void A_bare_word_is_an_expression_when_the_switch_is_up()
    {
        var query = Expression("^spooler$");

        Assert.True(query.Match(Entries.Any).Matched);
    }

    [Fact]
    public void The_marks_leave_a_member_with_a_field_alone()
    {
        // Only the search half is affected. Somebody who learned that a star is a wildcard does
        // not have to find out that marks elsewhere in the line quietly made it a quantifier.
        Assert.True(QueryParserTests.Valid("name:spool*").Match(Entries.Any).Matched);

        // And the same spelling read as an expression would match "Spoole" followed by any
        // number of r, which this entry is not - so the two readings are genuinely different
        // and this test is not passing by them agreeing.
        Assert.False(Expression("spool*x").Match(Entries.Any).Matched);
    }

    [Fact]
    public void An_expression_that_will_not_compile_is_refused_with_the_reason()
    {
        var parsed = QueryParser.Parse("/spooler(/");

        Assert.False(parsed.IsValid);

        var problem = Assert.Single(parsed.Problems);

        Assert.Equal(QueryProblemKind.BadPattern, problem.Kind);
        Assert.Equal("spooler(", problem.Text);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    [Fact]
    public void The_switch_being_up_does_not_make_ordinary_words_stop_working()
    {
        // Most words are also expressions that mean themselves, which is what makes the switch
        // safe to leave on.
        Assert.True(Expression("spooler").Match(Entries.Any).Matched);
    }

    [Fact]
    public void A_query_says_whether_it_excludes_a_value()
    {
        Assert.True(QueryParserTests.Valid("!type:driver").Excludes("type", "driver"));
        Assert.False(QueryParserTests.Valid("type:driver").Excludes("type", "driver"));
        Assert.False(QueryParserTests.Valid("").Excludes("type", "driver"));
        Assert.False(QueryParserTests.Valid("!status:running").Excludes("type", "driver"));
    }

    [Fact]
    public void The_question_folds_spelling_the_way_the_language_does()
    {
        // Asked of a query somebody typed, so it has to accept the spellings the language
        // accepts. A switch that only recognised its own spelling would sit there unchecked
        // beside a box that plainly excludes drivers.
        Assert.True(QueryParserTests.Valid("!TYPE:Driver").Excludes("type", "driver"));
        Assert.True(QueryParserTests.Valid("!type:driver").Excludes("TYPE", "DRIVER"));
    }

    [Fact]
    public void The_question_is_about_the_value_as_written_not_what_it_stands_for()
    {
        // type:driver compiles to two symbols, and neither of them is the word that was
        // written. Asking about the symbols would answer yes to a question nobody asked, and
        // would make a switch labelled "drivers" respond to an exclusion of one driver kind.
        Assert.True(QueryParserTests.Valid("!type:kernelDriver").Excludes("type", "kernelDriver"));
        Assert.False(QueryParserTests.Valid("!type:kernelDriver").Excludes("type", "driver"));
    }

    [Fact]
    public void An_exclusion_among_several_values_still_counts()
    {
        Assert.True(QueryParserTests.Valid("!type:driver,ownProcess").Excludes("type", "driver"));
    }

    [Fact]
    public void An_unknown_field_is_simply_not_excluded()
    {
        // Asked with a field name nobody knows, the honest answer is no rather than an
        // exception: this is called from a switch reading whatever is in the box.
        Assert.False(QueryParserTests.Valid("!type:driver").Excludes("nosuchfield", "driver"));
    }

    private static Query Expression(string text)
    {
        // Wrapped here rather than at every call, so that the tests above read as the language
        // rather than as its punctuation. Slashes are what a person types to mean a pattern.
        var parsed = QueryParser.Parse("/" + text + "/");

        Assert.True(parsed.IsValid, $"Expected '{text}' to parse as an expression.");

        return parsed.Query!;
    }
}
