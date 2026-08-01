using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// Reading query text: what is accepted, what is refused, and what is merely unfinished.
///
/// The line between the last two is the one worth guarding. Validation runs on every
/// keystroke, so half-typed text passes through here constantly, and treating it as an
/// error would leave the search box red most of the time somebody is typing.
/// </summary>
public sealed class QueryParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_query_selects_everything(string? text)
    {
        var parsed = QueryParser.Parse(text);

        Assert.True(parsed.IsValid);
        Assert.True(parsed.Query!.IsEmpty);
    }

    [Fact]
    public void An_unknown_field_is_refused_and_the_real_ones_are_offered()
    {
        var problem = OneProblem("stat:running");

        Assert.Equal(QueryProblemKind.UnknownField, problem.Kind);
        Assert.Equal("stat", problem.Text);

        // The list is the point. "Unknown field" alone sends somebody guessing, and the
        // fix is one glance away if the alternatives come with the complaint.
        Assert.Equal(QueryFields.Names, problem.Alternatives);
    }

    [Fact]
    public void A_misspelled_value_is_refused_with_the_nearest_real_one()
    {
        var problem = OneProblem("status:runing");

        Assert.Equal(QueryProblemKind.UnknownValue, problem.Kind);
        Assert.Equal("status", problem.Field);
        Assert.Equal("running", problem.Nearest);
        Assert.Contains("stopped", problem.Alternatives);
    }

    [Fact]
    public void A_value_nothing_resembles_is_refused_without_a_guess()
    {
        // A suggestion that is not actually close is worse than none: it sends somebody
        // off to try a value they never meant.
        var problem = OneProblem("status:zzzzzzzzzz");

        Assert.Equal(QueryProblemKind.UnknownValue, problem.Kind);
        Assert.Null(problem.Nearest);
    }

    [Theory]
    [InlineData("pid:abc")]
    [InlineData("pid:>x")]
    [InlineData("pid:1-")]
    [InlineData("pid:-5")]
    public void A_number_that_is_not_one_is_refused(string text)
    {
        Assert.Equal(QueryProblemKind.BadNumber, OneProblem(text).Kind);
    }

    [Fact]
    public void A_broken_expression_is_refused_and_says_what_the_engine_disliked()
    {
        var problem = OneProblem("name:/[unclosed/");

        Assert.Equal(QueryProblemKind.BadPattern, problem.Kind);
        Assert.Equal("[unclosed", problem.Text);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    [Fact]
    public void A_quote_that_never_closes_is_refused()
    {
        var problem = OneProblem("""name:"never closed""");

        Assert.Equal(QueryProblemKind.UnclosedQuote, problem.Kind);

        // Pointing at where it opened is what makes this fixable in a long query.
        Assert.StartsWith("\"never closed", problem.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_query_with_a_problem_produces_nothing_to_filter_with()
    {
        // Never a filter that silently does less than asked. The caller has to deal with
        // the problem, and cannot accidentally run a half-understood query.
        var parsed = QueryParser.Parse("status:runing name:spooler");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Query);
    }

    [Theory]
    [InlineData("status:")]
    [InlineData("status: name:spooler")]
    [InlineData("sta")]
    public void A_member_still_being_typed_is_not_an_error(string text)
    {
        // Typing "status:running" passes through "sta", "status" and "status:" on the way.
        // None of those is a mistake, so none of them may light up red.
        Assert.True(QueryParser.Parse(text).IsValid);
    }

    [Fact]
    public void A_field_with_nothing_after_the_colon_is_ignored_rather_than_matching_nothing()
    {
        var query = Valid("status:");

        Assert.True(query.IsEmpty);
        Assert.True(query.Match(Entries.Any).Matched);
    }

    [Fact]
    public void Field_names_ignore_case()
    {
        Assert.True(Valid("Status:Running").Match(Entries.Any).Matched);
    }

    internal static Query Valid(string text)
    {
        var parsed = QueryParser.Parse(text);

        Assert.True(parsed.IsValid, $"Expected '{text}' to parse. Problems: {parsed.Problems.Count}.");

        return parsed.Query!;
    }

    private static QueryProblem OneProblem(string text)
    {
        var parsed = QueryParser.Parse(text);

        Assert.False(parsed.IsValid, $"Expected '{text}' to be refused.");

        return Assert.Single(parsed.Problems);
    }
}
