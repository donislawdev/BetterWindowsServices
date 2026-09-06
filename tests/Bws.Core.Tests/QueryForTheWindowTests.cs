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

    /// <summary>
    /// Every value this language OFFERS is a value it ACCEPTS.
    ///
    /// <b>The guard the surface needed before anything was allowed to draw a menu from it.</b>
    /// <see cref="QueryFields.ValuesOf"/> was opened on 2026-09-05 so that the window could stop
    /// spelling these out for itself - the filter chips carry fourteen of them as literals, which
    /// is a second copy of a frozen contract with nothing comparing the two. A list offered to a
    /// person is a promise that typing what it says will work, and this is the only place that
    /// promise can be checked.
    ///
    /// <b>Every field rather than the ones somebody remembered.</b> A value spelled wrongly in the
    /// table would be offered in a menu, typed by whoever clicked it, and refused by the parser -
    /// which reads as the window writing a broken query rather than as one wrong word in a list.
    /// </summary>
    [Fact]
    public void Every_value_this_language_offers_is_one_it_accepts()
    {
        var offered = 0;
        var refused = new List<string>();

        foreach (var field in QueryFields.Names)
        {
            foreach (var value in QueryFields.ValuesOf(field))
            {
                offered++;

                if (!QueryParser.Parse($"{field}:{value}").IsValid)
                {
                    refused.Add($"  {field}:{value}");
                }
            }
        }

        // Without this the loop above passes over an empty set and proves nothing - the same
        // failure the surface guards in the architecture tests are built to avoid.
        Assert.True(offered > 0, "No field offered a single value, so the check below saw nothing.");

        Assert.True(
            refused.Count == 0,
            "These values are offered to a person and refused by the parser, so a control drawn "
            + "from this list writes a query the window then rejects:"
            + Environment.NewLine + string.Join(Environment.NewLine, refused));
    }

    /// <summary>
    /// A field that takes text, a number or a size offers no values, and an unknown name is an
    /// ordinary answer rather than an exception.
    ///
    /// <b>The other direction, and it is what makes a caller able to ASK.</b> A menu built over
    /// this list has to be able to tell "this column can be filtered to a set of words" from "this
    /// one takes anything you type" - and it tells them apart by the list coming back empty. A
    /// surface that answered with something for every field would need a second question.
    /// </summary>
    [Fact]
    public void A_field_that_is_not_a_set_of_words_offers_nothing_and_an_unknown_name_is_not_a_fault()
    {
        Assert.NotEmpty(QueryFields.ValuesOf("status"));
        Assert.Contains("running", QueryFields.ValuesOf("status"), StringComparer.Ordinal);

        // Text, a number and a size in turn - three kinds, none of them a set of words.
        Assert.Empty(QueryFields.ValuesOf("name"));
        Assert.Empty(QueryFields.ValuesOf("pid"));
        Assert.Empty(QueryFields.ValuesOf("memory"));

        // Not knowing a name is what a window asking about an arbitrary column looks like.
        Assert.Empty(QueryFields.ValuesOf("nosuchfield"));

        // No name at all is a caller fault, and saying so here beats a null reference three
        // frames inside the normaliser.
        Assert.Throws<ArgumentNullException>(() => QueryFields.ValuesOf(null!));
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
