using System.Text.Json.Nodes;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The searches made lately - what is kept, how many, and how the layout file holding them is read
/// back when somebody else wrote it. The owner's first choice of 2026-11-16 (RecentQueries).
///
/// <b>The file is somebody's own data</b> (CLAUDE.md, untouchable rule 24 and 25): a hand-edited key
/// must cost at most the list of searches, never the column layout beside it and never the window.
/// </summary>
public sealed class RecentQueriesTests
{
    [Fact]
    public void A_search_goes_first_and_trimmed_and_the_same_search_moves_rather_than_standing_twice()
    {
        IReadOnlyList<string> kept = [];

        kept = RecentQueries.With(kept, "status:running");
        kept = RecentQueries.With(kept, "  name:sql  ");
        kept = RecentQueries.With(kept, "status:running");

        Assert.Equal(["status:running", "name:sql"], kept);
    }

    [Fact]
    public void Five_are_kept_and_the_oldest_goes()
    {
        IReadOnlyList<string> kept = [];

        foreach (var query in new[] { "a", "b", "c", "d", "e", "f" })
        {
            kept = RecentQueries.With(kept, query);
        }

        Assert.Equal(["f", "e", "d", "c", "b"], kept);
        Assert.Equal(RecentQueries.Kept, kept.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_typed_is_not_a_search(string query) =>
        Assert.Equal(["kept"], RecentQueries.With(["kept"], query));

    [Fact]
    public void A_query_longer_than_the_most_kept_leaves_the_list_as_it_was()
    {
        Assert.Equal(["kept"], RecentQueries.With(["kept"], new string('x', RecentQueries.LengthMost + 1)));
        Assert.Equal(RecentQueries.LengthMost, RecentQueries.With([], new string('x', RecentQueries.LengthMost))[0].Length);
    }

    [Fact]
    public void Case_is_part_of_what_was_typed() =>
        Assert.Equal(["status:Running", "status:running"], RecentQueries.With(["status:running"], "status:Running"));

    /// <summary>
    /// What the box holds is kept only when the model read that very text, it reads, and it says
    /// something - never a query with a mistake, never a text typed inside the 400 ms the model waits.
    /// </summary>
    [Theory]
    [InlineData("status:running", "status:running", true)]
    [InlineData("spooler", "spooler", true)]
    [InlineData("stat:running", "stat:running", false)]
    [InlineData("status:", "status:", false)]
    [InlineData("   ", "   ", false)]
    [InlineData("status:running name:sql", "status:running", false)]
    [InlineData("name:spooler start:dis", "name:spooler start:dis", false)]
    [InlineData("mismatch:stopped", "mismatch:stopped", false)]
    [InlineData(" file:missing ", " file:missing ", false)]
    public void Only_a_search_that_reads_and_that_the_model_has_read_is_worth_keeping(string inTheBox, string read, bool worth) =>
        Assert.Equal(worth, RecentQueries.Worth(inTheBox, Bws.Core.Querying.QueryAsTyped.Of(read)));

    [Fact]
    public void Forgetting_takes_that_search_and_no_other() =>
        Assert.Equal(["a", "c"], RecentQueries.Without(["a", "b", "c"], "b"));

    /// <summary>
    /// What a hand-edited file can hold, read back without a fault: not an array, numbers and
    /// objects among the queries, blanks, repeats, a query past the length kept, and more than five.
    /// </summary>
    [Theory]
    [InlineData("\"status:running\"", new string[0])]
    [InlineData("{ \"a\": 1 }", new string[0])]
    [InlineData("[ 1, true, null, { \"q\": \"x\" }, \"status:running\" ]", new[] { "status:running" })]
    [InlineData("[ \"  \", \"a\", \"a\", \" b \" ]", new[] { "a", "b" })]
    [InlineData("[ \"a\", \"b\", \"c\", \"d\", \"e\", \"f\", \"g\" ]", new[] { "a", "b", "c", "d", "e" })]
    public void A_list_somebody_edited_is_read_for_what_it_holds_and_nothing_else(string json, string[] expected) =>
        Assert.Equal(expected, RecentQueries.From(JsonNode.Parse(json)));

    [Fact]
    public void A_query_past_the_length_kept_is_passed_over_rather_than_cut()
    {
        var json = new JsonArray(new string('x', RecentQueries.LengthMost + 1), "a");

        Assert.Equal(["a"], RecentQueries.From(json));
    }

    [Fact]
    public void An_absent_key_is_no_searches() => Assert.Empty(RecentQueries.From(null));

    /// <summary>Written and read back through the whole file, in schema 6, under its own key.</summary>
    [Fact]
    public void The_searches_go_into_the_layout_file_and_come_back_out()
    {
        var written = (ColumnLayouts.Default with { RecentQueries = ["status:running", "name:\"Print Spooler\""] }).Render();

        Assert.Contains("\"recentQueries\"", written, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\": 6", written, StringComparison.Ordinal);

        var read = ColumnLayouts.Read(written);

        Assert.Equal(["status:running", "name:\"Print Spooler\""], read.Layouts!.RecentQueries);
        Assert.Null(read.CarriedForwardFrom);
    }

    [Fact]
    public void No_searches_write_no_key() =>
        Assert.DoesNotContain("recentQueries", ColumnLayouts.Default.Render(), StringComparison.Ordinal);

    /// <summary>
    /// A version 5 file is a version 6 file that says nothing about searches - read, carried forward,
    /// and its columns kept.
    /// </summary>
    [Fact]
    public void A_file_from_before_the_searches_is_carried_forward_with_none()
    {
        var read = ColumnLayouts.Read("""{ "columns": [], "overviewSeen": true, "schemaVersion": 5 }""");

        Assert.NotNull(read.Layouts);
        Assert.Empty(read.Layouts!.RecentQueries);
        Assert.True(read.Layouts.OverviewSeen);
        Assert.Equal(5, read.CarriedForwardFrom);
    }

    /// <summary>A key holding nonsense costs the searches and nothing else - the layout beside it is read.</summary>
    [Fact]
    public void A_broken_list_of_searches_does_not_cost_the_layout()
    {
        var read = ColumnLayouts.Read("""{ "columns": [], "overviewSeen": true, "recentQueries": 42, "schemaVersion": 6 }""");

        Assert.NotNull(read.Layouts);
        Assert.Null(read.Unreadable);
        Assert.True(read.Layouts!.OverviewSeen);
        Assert.Empty(read.Layouts.RecentQueries);
    }
}
