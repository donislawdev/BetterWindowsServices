using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// A pattern between slashes: what reaches the engine, where it ends, and what happens when it
/// does not.
///
/// <b>Every case here was measured on the owner's machine before it was repaired</b>, 800 entries,
/// all with a code of success and not a word on the error stream (stability report, Q-1, Q-2 and
/// Q-6): <c>name:/a\?/</c> answered with all 800, <c>path:/C:\\Windows/</c> with none, against 307
/// for <c>path:/Windows/</c>, <c>name:/^w{2,}/</c> with none against 1 for <c>name:/^ww/</c>,
/// <c>name://</c> with all 800, and <c>name:/abc</c> searched for the text with the slash in it.
/// </summary>
public sealed class QueryPatternTests
{
    [Theory]
    [InlineData("name:/a\\?/", "data?", "data")]
    [InlineData("name:/a\\*/", "a*b", "aab")]
    [InlineData("path:/C:\\\\WINDOWS/", "Spooler", null)]
    [InlineData("name:/a\\/b/", "a/b", "ab")]
    public void An_escape_inside_a_pattern_reaches_the_pattern(string query, string matches, string? doesNot)
    {
        // The scanner takes a backslash away before a value is read, and until 2026-10-05 a
        // pattern got what was left: a\? became a?, which matches every name there is, and
        // C:\\Windows became C:\Windows, where \W is a character class.
        Assert.True(Matches(query, Named(matches)), $"{query} should match {matches}");

        if (doesNot is not null)
        {
            Assert.False(Matches(query, Named(doesNot)), $"{query} should not match {doesNot}");
        }
    }

    [Fact]
    public void A_quote_inside_a_pattern_still_only_keeps_a_space_away_from_the_scanner()
    {
        // The other half of the repair, and the reason it is a mask of escapes rather than a mask
        // of everything literal: quotes have never been part of a pattern.
        Assert.True(Matches("display:/\"Print Spooler\"/", Entries.Any));
        Assert.True(Matches("display:/^\"Print Spooler\"$/", Entries.Any));
        Assert.False(Matches("display:/\"Print Spooler\"x/", Entries.Any));
    }

    [Fact]
    public void A_comma_inside_a_pattern_belongs_to_it()
    {
        // Measured: 0 where name:/^ww/ finds 1. The comma cut the pattern into two pieces, neither
        // of them a pattern, and both were searched for as text.
        Assert.True(Matches("name:/^w{2,}/", Named("WwanSvc")));
        Assert.False(Matches("name:/^w{2,}/", Named("Wsearch")));
        Assert.True(Matches("name:/,b/", Named("a,b")));
    }

    [Fact]
    public void Two_patterns_with_a_comma_between_them_are_still_two()
    {
        // What closes a pattern is a bare slash right before the comma, so the spelling that has
        // always meant "either" still does.
        Assert.True(Matches("name:/^a/,/^b/", Named("alpha")));
        Assert.True(Matches("name:/^a/,/^b/", Named("beta")));
        Assert.False(Matches("name:/^a/,/^b/", Named("gamma")));
    }

    [Theory]
    [InlineData("name:/abc")]
    [InlineData("/abc")]
    [InlineData("name:/")]
    [InlineData("/")]
    [InlineData("name:/a\\/")]
    [InlineData("name:/a/b")]
    [InlineData("name:/^sql spooler")]
    public void A_pattern_that_never_closes_is_a_mistake_once_nobody_is_typing(string query)
    {
        // Until 2026-10-05 it fell through to text, so a forgotten slash came back as an empty
        // list and a code of success - which reads as "there are none".
        var problem = Assert.Single(QueryParser.Parse(query).Problems);

        Assert.Equal(QueryProblemKind.UnclosedPattern, problem.Kind);
    }

    [Theory]
    [InlineData("name:/ab")]
    [InlineData("spooler /ab")]
    [InlineData("display:/\"Print Spooler\"")]
    [InlineData("name:/a,b")]
    public void A_pattern_still_being_typed_is_passed_over(string query)
    {
        // Every pattern is unclosed for the keystrokes between its two slashes. The rest of the
        // text still answers, and the quote does not count as the end of the value: inside a
        // pattern it only keeps a space away from the scanner.
        var parsed = QueryParser.Parse(query, QueryInput.BeingTyped);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));
        Assert.True(parsed.Query!.Match(Entries.Any).Matched);
    }

    [Theory]
    [InlineData("name:/ab spooler")]
    [InlineData("name:/ab ")]
    public void A_pattern_somebody_moved_on_from_is_a_mistake_even_while_typing(string query)
    {
        var problem = Assert.Single(QueryParser.Parse(query, QueryInput.BeingTyped).Problems);

        Assert.Equal(QueryProblemKind.UnclosedPattern, problem.Kind);
    }

    [Theory]
    [InlineData("//", QueryInput.Finished)]
    [InlineData("name://", QueryInput.Finished)]
    [InlineData("//", QueryInput.BeingTyped)]
    [InlineData("name://", QueryInput.BeingTyped)]
    public void An_empty_pattern_says_nothing_even_while_typed(string query, QueryInput input)
    {
        // Measured: name:// answered with all 800 entries. A pair of slashes is closed the way a
        // pair of quotes is, so it is a mistake in the window too - the same line name:"" sits on.
        var problem = Assert.Single(QueryParser.Parse(query, input).Problems);

        Assert.Equal(QueryProblemKind.EmptyTerm, problem.Kind);
    }

    private static ScmEntry Named(string name) => Entries.Named(name, name);

    private static bool Matches(string query, ScmEntry entry)
    {
        var parsed = QueryParser.Parse(query);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));

        return parsed.Query!.Match(entry).Matched;
    }
}
