using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// Two one-sided bounds on the same number narrow, and everything else about a repeated field
/// stays an alternative.
///
/// <b>Measured on the owner's machine before the repair</b> (stability report Q-3):
/// <c>pid:&gt;1000</c> 121 entries, <c>pid:&lt;2000</c> 11, <c>pid:1000-2000</c> 11 - and
/// <c>pid:&gt;1000 pid:&lt;2000</c> 121, because the two members were folded into "either". After it,
/// 11. Owner's decisions of 2026-09-30 and, for the mixed case, 2026-10-05.
/// </summary>
public sealed class BoundQueryTests
{
    private static readonly ScmEntry Low = WithProcess("Low", 500);
    private static readonly ScmEntry Middle = WithProcess("Middle", 1500);
    private static readonly ScmEntry High = WithProcess("High", 2500);

    private static readonly IReadOnlyList<ScmEntry> Three = [Low, Middle, High];

    [Theory]
    [InlineData("pid:>1000 pid:<2000")]
    [InlineData("pid:<2000 pid:>1000")]
    [InlineData("pid:>=1500 pid:<=1500")]
    public void Two_bounds_written_as_two_members_narrow(string query)
    {
        Assert.Equal(["Middle"], Selected(query));
    }

    [Fact]
    public void Exact_values_written_as_two_members_are_still_either()
    {
        // The reason the fold exists at all: two exact values required at once contradict each
        // other, and two chips ticked at once must not write a query that matches nothing.
        Assert.Equal(["Low", "High"], Selected("pid:500 pid:2500"));
        Assert.Equal(["Low", "Middle"], Selected("pid:400-600 pid:1400-1600"));
    }

    [Fact]
    public void A_comma_is_still_the_word_for_or()
    {
        // The one question two bounds can ask that a range cannot: both ends outside it.
        Assert.Equal(["Low", "High"], Selected("pid:<1000,>2000"));
    }

    [Fact]
    public void A_bound_beside_an_exact_value_is_a_member_like_any_other()
    {
        // The mixed case, decided on 2026-10-05: the bound narrows whatever it stands beside, so
        // an exact value outside it leaves nothing - and inside it, leaves itself.
        Assert.Empty(Selected("pid:500 pid:>1000"));
        Assert.Equal(["Middle"], Selected("pid:1500,2500 pid:<2000"));
    }

    [Fact]
    public void Bounds_on_a_size_narrow_the_same_way()
    {
        var small = WithMemory("Small", 50);
        var medium = WithMemory("Medium", 500);
        var large = WithMemory("Large", 5000);

        Assert.Equal(["Medium"], Selected("memory:>100MB memory:<1GB", [small, medium, large]));
    }

    private static ScmEntry WithProcess(string name, int processId) =>
        Entries.Named(name, name) with { ProcessId = Reading<int>.Present(processId) };

    private static ScmEntry WithMemory(string name, long megabytes) =>
        Entries.Named(name, name) with
        {
            Memory = Reading<ProcessMemory>.Present(new ProcessMemory(megabytes * 1024 * 1024, 0, 1))
        };

    private static List<string> Selected(string query) => Selected(query, Three);

    private static List<string> Selected(string query, IReadOnlyList<ScmEntry> entries)
    {
        var parsed = QueryParser.Parse(query);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));

        return [.. parsed.Query!.Filter(entries).Entries.Select(entry => entry.ServiceName)];
    }
}
