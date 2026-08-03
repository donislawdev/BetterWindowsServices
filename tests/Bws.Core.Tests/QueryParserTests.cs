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
    [InlineData("pid:2147483648")]
    public void A_number_that_is_not_one_is_refused(string text)
    {
        Assert.Equal(QueryProblemKind.BadNumber, OneProblem(text).Kind);
    }

    [Fact]
    public void A_range_that_ends_before_it_starts_is_refused_rather_than_matching_nothing()
    {
        // The transposition is easy to make and impossible to spot in the answer: the range
        // is satisfiable by no number at all, so the reply is an empty list, which reads as
        // "there are none" instead of "you wrote the ends the wrong way round".
        Assert.Equal(QueryProblemKind.BadNumber, OneProblem("pid:200-100").Kind);

        Assert.True(QueryParser.Parse("pid:100-200").IsValid);
        Assert.True(QueryParser.Parse("pid:100-100").IsValid);
    }

    [Fact]
    public void An_equals_sign_with_nothing_after_it_is_unfinished_rather_than_a_search_for_emptiness()
    {
        // Same reasoning as a colon with nothing after it. Taken literally it asks for
        // entries whose name is the empty string, which is an empty list dressed as an answer.
        var query = Valid("name:=");

        Assert.True(query.IsEmpty);
        Assert.True(query.Match(Entries.Any).Matched);
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

    /// <summary>
    /// A word made of nothing but exclamation marks is a mistake, not a filter that lets
    /// everything through.
    ///
    /// <b>The boundary between this and the test above is the whole point, and it is a real
    /// distinction rather than a line drawn somewhere.</b> <c>status:</c> is what a search box
    /// holds between the colon and the value, so calling it an error would flash red after every
    /// keystroke. <c>!</c> is not on the way to anything - it is finished, and it says nothing.
    ///
    /// Until 2026-08-03 both were dropped in silence and <c>bws list --query "!!!"</c> answered
    /// with all 810 entries and a code of success. A script with a typo in its query got the
    /// whole machine and a green light.
    ///
    /// <b>Written by example rather than left to the property test next door, and the mutation
    /// registry is why.</b> That property covers this and only reaches it when the generator
    /// happens to produce a string of nothing but exclamation marks, so removing the behaviour
    /// came back MISSED - the property was green on a build that had the defect back. A guard
    /// that catches sometimes is not a guard.
    /// </summary>
    [Theory]
    [InlineData("!")]
    [InlineData("!!")]
    [InlineData("!!!")]
    public void A_word_of_nothing_but_exclamation_marks_is_a_mistake(string text)
    {
        var parsed = QueryParser.Parse(text);

        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Problems, problem => problem.Kind == QueryProblemKind.EmptyTerm);
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
