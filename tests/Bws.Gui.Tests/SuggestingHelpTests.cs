using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The list under the search box as the help it became on 2026-11-17 - the searches made lately over
/// the questions, and the guide behind the ? - checked without a window. The live window is
/// tools/gui-probe/query-help.ps1. docs/PROJEKT-POLE-ZAPYTANIA-20261117.md has the design.
/// </summary>
public sealed class SuggestingHelpTests
{
    private static Suggesting With(params string[] recent)
    {
        var suggesting = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock()).Suggesting;

        suggesting.RecentQueries = () => recent;

        return suggesting;
    }

    /// <summary>
    /// The searches first, then the questions - and the questions say where they start, because
    /// another group stands over them now. Two are left out: one that is already a question, and one
    /// that no longer reads.
    /// </summary>
    [Fact]
    public void An_empty_box_offers_the_searches_made_lately_over_the_questions()
    {
        var suggesting = With("status:running name:sql*", "stat:running", "mismatch:stopped", "start:auto");

        suggesting.Arrived(string.Empty);

        var offered = suggesting.Offered;
        var recent = offered.TakeWhile(row => row.IsRecent).Select(row => row.Word).ToList();

        Assert.Equal(["status:running name:sql*", "start:auto"], recent);
        Assert.Equal(Texts.Of("gui.suggest.caption.recent"), suggesting.Caption);
        Assert.Equal(Texts.Of("gui.suggest.caption.questions"), offered[recent.Count].Heading);
        Assert.All(offered.Skip(recent.Count + 1), row => Assert.Null(row.Heading));
        Assert.All(offered.Take(recent.Count), row => Assert.Equal(0..0, row.Replaces));
    }

    [Fact]
    public void With_no_searches_the_questions_open_the_list_as_they_always_did()
    {
        var suggesting = With();

        suggesting.Arrived(string.Empty);

        Assert.Equal(Texts.Of("gui.suggest.caption.questions"), suggesting.Caption);
        Assert.All(suggesting.Offered, row => Assert.False(row.IsRecent));
        Assert.All(suggesting.Offered, row => Assert.Null(row.Heading));
    }

    /// <summary>The keys under the list name Delete only while a search made lately is the chosen row.</summary>
    [Fact]
    public void The_keys_name_Delete_only_on_a_search_made_lately()
    {
        var suggesting = With("start:auto");

        suggesting.Arrived(string.Empty);
        Assert.Equal(Texts.Of("gui.suggest.keys.recent"), suggesting.Keys);

        suggesting.Next();
        Assert.Equal(Texts.Of("gui.suggest.keys"), suggesting.Keys);
    }

    /// <summary>
    /// After a search is forgotten the row that took its place is chosen, so Delete pressed again
    /// goes on down the list.
    /// </summary>
    [Fact]
    public void Forgetting_a_search_chooses_the_row_that_took_its_place()
    {
        string[] kept = ["a:1", "start:auto", "start:disabled"];
        var suggesting = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock()).Suggesting;

        suggesting.RecentQueries = () => kept;

        // a:1 does not read, so the two that do are offered.
        suggesting.Arrived(string.Empty);
        Assert.Equal("start:auto", suggesting.Chosen!.Word);

        kept = ["a:1", "start:disabled"];
        suggesting.Forgot(string.Empty);

        Assert.Equal("start:disabled", suggesting.Chosen!.Word);
        Assert.True(suggesting.Chosen.IsRecent);
    }

    /// <summary>
    /// The guide offers the ways to write a value as rows, each written at the END of what the box
    /// holds - after a space when the query does not end in one - and says it is the guide.
    /// </summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("status:running", " ")]
    [InlineData("status:running ", "")]
    public void The_guide_offers_the_ways_to_write_a_value_at_the_end_of_the_query(string text, string gap)
    {
        var suggesting = With("start:auto");

        suggesting.Guide(text);

        Assert.True(suggesting.Guiding);
        Assert.Equal(Texts.Of("gui.suggest.caption.guide"), suggesting.Caption);
        Assert.Equal(Texts.Of("gui.suggest.keys.guide"), suggesting.Keys);
        Assert.Equal(QueryGuide.Lines.Select(line => line.Written), suggesting.Offered.Select(row => row.Word));
        Assert.All(suggesting.Offered, row => Assert.Equal(text.Length..text.Length, row.Replaces));
        Assert.All(suggesting.Offered, row => Assert.Equal(gap + row.Word, row.Written));
        Assert.All(suggesting.Offered, row => Assert.False(row.IsRecent));
        Assert.False(suggesting.TabWrites);
    }

    /// <summary>The guide is the list, so whatever closes the list closes it - and typing turns it back into completions.</summary>
    [Fact]
    public void Closing_or_typing_ends_the_guide()
    {
        var suggesting = With();

        suggesting.Guide(string.Empty);
        Assert.True(suggesting.Close());
        Assert.False(suggesting.Guiding);

        suggesting.Guide(string.Empty);
        suggesting.Follow("sta", 3, 0);
        Assert.False(suggesting.Guiding);
        Assert.Equal(Texts.Of("gui.suggest.caption.words"), suggesting.Caption);
    }

    /// <summary>Every way to write a value in the guide reads - a guide teaching a spelling the parser refuses is worse than none.</summary>
    [Fact]
    public void Every_example_in_the_guide_reads()
    {
        Assert.All(QueryGuide.Lines, line => Assert.True(QueryParser.Parse(line.Written).IsValid, line.Written));
        Assert.All(QueryGuide.Lines, line => Assert.False(string.IsNullOrWhiteSpace(line.Meaning), line.Written));
    }

    /// <summary>The guide names every field of the language and no other, each with its colon and the sentence the list gives it.</summary>
    [Fact]
    public void The_guide_names_every_field_of_the_language()
    {
        Assert.Equal(QueryFields.Names, QueryGuide.Fields.Select(field => field.Word));
        Assert.All(QueryGuide.Fields, field => Assert.Equal(field.Word + ":", field.Written));
        Assert.All(QueryGuide.Fields, field => Assert.False(string.IsNullOrWhiteSpace(field.Meaning), field.Word));
    }
}
