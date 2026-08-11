using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// The two things the language gained for the window: a bare word read as an expression, and
/// a query able to say whether it carries a particular exclusion.
///
/// <b>BOTH USED TO EXIST BECAUSE OF A SWITCH BESIDE THE SEARCH BOX AND ONLY ONE STILL DOES.</b>
/// The regex switch went on 2026-08-11, backlog 152, and the reading it stood for turned out to
/// have been in the language all along - <c>/between marks/</c> is an expression and anything
/// else is text, for field values and for free search alike. So the first half is now about the
/// marks rather than about a control. The second half is still about a control: the drivers
/// checkbox asks a query whether it already excludes a value, which is what lets a filter you
/// clicked be a query you can read.
///
/// Both live here rather than in the window for the same reason: working out which part of the
/// text is a bare word, and which member a checkbox stands for, is the language's job. A second
/// copy of either in the interface would drift from this one without a sound.
///
/// <b>The names below were the last thing still saying "switch" about the half that no longer
/// has one</b>, and they were repaired on 2026-08-11 rather than left. Nothing guards prose, so a
/// test named after a control that was deleted reads as evidence that the control is still there.
/// </summary>
public sealed class QueryForTheWindowTests
{
    [Fact]
    public void A_bare_word_with_no_marks_around_it_is_text_to_be_contained()
    {
        var query = QueryParserTests.Valid("^spooler$");

        // The characters are part of what is being looked for, so nothing has them.
        Assert.False(query.Match(Entries.Any).Matched);
    }

    [Fact]
    public void A_bare_word_between_marks_is_an_expression()
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
    public void Marks_around_an_ordinary_word_do_not_stop_it_working()
    {
        // Most words are also expressions that mean themselves, which is what makes the marks
        // cheap to reach for - somebody who adds them to a plain word gets the same answer.
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
