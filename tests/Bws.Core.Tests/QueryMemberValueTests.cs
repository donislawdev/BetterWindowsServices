using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// Turning a chip off takes its value out of a list, and leaves the rest of the list alone.
///
/// <b>Measured on the core before the repair</b> (stability report Q-5):
/// <c>QueryMembers.Without("status:running,stopped", "status", "stopped")</c> gave an empty line,
/// so the chip for Running went dark with the one that was clicked. The same two values written
/// as two members gave <c>status:running</c>, which is the answer both spellings now give.
/// </summary>
public sealed class QueryMemberValueTests
{
    [Theory]
    [InlineData("status:running,stopped", "stopped", "status:running")]
    [InlineData("status:running,stopped", "running", "status:stopped")]
    [InlineData("status:running,stopped,paused", "stopped", "status:running,paused")]
    [InlineData("status:running,stopped,paused", "paused", "status:running,stopped")]
    [InlineData("status:stopped,stopped,running", "stopped", "status:running")]
    [InlineData("status:running,stopped,stopped", "stopped", "status:running")]
    [InlineData("STATUS:Running,Stopped", "stopped", "STATUS:Running")]
    [InlineData("status:running,\"stopped\"", "stopped", "status:running")]
    [InlineData("status:\"running\",stopped", "stopped", "status:\"running\"")]
    [InlineData("name:x status:running,stopped name:y", "stopped", "name:x status:running name:y")]
    public void Only_the_value_asked_about_leaves_the_list(string text, string value, string expected)
    {
        Assert.Equal(expected, QueryMembers.Without(text, "status", value, negated: false));
    }

    [Fact]
    public void The_list_keeps_its_place_and_the_line_keeps_its_spacing()
    {
        // Nothing outside the cut moves - the same promise the whole-member cut makes.
        Assert.Equal(
            "name:x   status:running\tname:y",
            QueryMembers.Without("name:x   status:running,stopped\tname:y", "status", "stopped", negated: false));
    }

    [Fact]
    public void An_exclusion_loses_one_value_the_same_way()
    {
        Assert.Equal(
            "!status:running",
            QueryMembers.Without("!status:running,stopped", "status", "stopped", negated: true));

        // And the other side is not touched: a chip for "stopped" is not a chip for "not stopped".
        Assert.Equal(
            "!status:running,stopped",
            QueryMembers.Without("!status:running,stopped", "status", "stopped", negated: false));
    }

    [Theory]
    [InlineData("status:stopped,stopped", "")]
    [InlineData("status:stopped, name:x", "name:x")]
    [InlineData("name:x status:stopped,", "name:x")]
    public void A_list_with_nothing_else_in_it_goes_whole(string text, string expected)
    {
        // When the value asked about is all the member says, the member goes, gap and all - so
        // a stray comma never leaves a member that says nothing behind.
        Assert.Equal(expected, QueryMembers.Without(text, "status", "stopped", negated: false));
    }

    [Fact]
    public void The_result_reads_the_way_the_chips_say()
    {
        // The point of the cut, asked of the thing the window asks.
        var left = QueryMembers.Without("status:running,stopped", "status", "stopped", negated: false);

        Assert.True(QueryMembers.Carries(left, "status", "running", negated: false));
        Assert.False(QueryMembers.Carries(left, "status", "stopped", negated: false));
    }

    [Fact]
    public void A_comma_inside_a_pattern_is_not_a_place_to_cut()
    {
        // The cut reads values through the same rule the parser does. A second copy of the rule
        // here would cut this pattern at its comma the first day the two disagreed.
        Assert.Equal(
            "name:/^w{2,}/",
            QueryMembers.Without("name:/^w{2,}/,spooler", "name", "spooler", negated: false));
    }
}
