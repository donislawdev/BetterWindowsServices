using Bws.Core.Querying;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Asking about memory, and about sizes, which arrived with it.
///
/// The specification's own showcase query is <c>name:/^Sql.*/ memory:&gt;500MB</c>, so the
/// unit is not a flourish - it is the notation the language was promised in. Most of these
/// tests are about the unit rather than about memory, because that is where a mistake would
/// be silent: a size read wrongly still filters, it just filters something else.
/// </summary>
public sealed class MemoryQueryTests
{
    /// <summary>
    /// What the fake reports for Spooler: a megabyte per unit of its process id. Taken from
    /// the specimen rather than written out, because writing it out is how the first draft
    /// of these tests went wrong - the number was copied from the wrong specimen and two
    /// tests failed for a reason that had nothing to do with the code.
    /// </summary>
    private static readonly long SpoolerMegabytes = Specimens.Ordinary.ProcessId.Value;

    [Fact]
    public void A_size_is_compared_the_way_a_number_is()
    {
        var above = $"memory:>{SpoolerMegabytes - 1}MB";
        var below = $"memory:>{SpoolerMegabytes + 1}MB";

        Assert.Contains("Spooler", Match(above));
        Assert.DoesNotContain("Spooler", Match(below));
        Assert.Contains("Spooler", Match($"memory:{SpoolerMegabytes - 1}MB-{SpoolerMegabytes + 1}MB"));
        Assert.DoesNotContain("Spooler", Match("memory:1MB-100MB"));

        // The boundary in both directions. A comparison written with the wrong end of the
        // inequality passes every test that only ever asks about values far from it.
        Assert.DoesNotContain("Spooler", Match($"memory:>{SpoolerMegabytes}MB"));
        Assert.Contains("Spooler", Match($"memory:>={SpoolerMegabytes}MB"));
    }

    [Fact]
    public void A_size_without_a_unit_is_refused_rather_than_guessed_at()
    {
        // The whole reason sizes are their own kind. Read as bytes, memory:>500 matches every
        // running service while looking exactly like a filter that worked - and read as
        // megabytes it would be this code deciding what somebody meant.
        var parsed = QueryParser.Parse("memory:>500");

        Assert.False(parsed.IsValid);
        Assert.Equal(QueryProblemKind.BadSize, parsed.Problems[0].Kind);
        Assert.Equal("memory", parsed.Problems[0].Field);
    }

    [Fact]
    public void A_bad_size_is_not_reported_as_a_bad_number()
    {
        // Two different mistakes needing two different sentences. Somebody who wrote
        // memory:>500 wrote a perfectly good number, and being told it is not a number would
        // send them to count the digits.
        Assert.Equal(QueryProblemKind.BadSize, Problem("memory:>500"));
        Assert.Equal(QueryProblemKind.BadNumber, Problem("pid:>abc"));
    }

    [Fact]
    public void Units_are_powers_of_1024_because_that_is_what_Windows_means()
    {
        // Task Manager, the file properties dialog and Get-Process all divide by 1024. Using
        // the disk-drive meaning would put every number we print at odds with everything a
        // person could check it against, by about seven percent at this scale.
        var reader = new FakeProcessMemoryReader(new Dictionary<int, Reading<ProcessMemory>>
        {
            [Specimens.Ordinary.ProcessId.Value] =
                Reading<ProcessMemory>.Present(new ProcessMemory(1024L * 1024 * 1000, 0, 1))
        });

        var entries = MemoryPass.Fill([Specimens.Ordinary], reader);

        // Exactly 1000 MiB, so >= matches and > does not. Under the disk-drive meaning of
        // the word this value would be over 1048 MB and both would match.
        Assert.Contains("Spooler", MatchOver("memory:>=1000MB", entries));
        Assert.DoesNotContain("Spooler", MatchOver("memory:>1000MB", entries));
    }

    [Fact]
    public void Every_unit_the_message_offers_actually_works()
    {
        // The error message names B, KB, MB and GB. A message offering a spelling the parser
        // refuses is worse than no message, and nothing else would ever check it.
        foreach (var written in new[] { "1048576B", "1024KB", "1MB" })
        {
            var parsed = QueryParser.Parse($"memory:>={written}");

            Assert.True(parsed.IsValid, $"{written} was refused");
        }

        Assert.True(QueryParser.Parse("memory:<1GB").IsValid);
        Assert.True(QueryParser.Parse("memory:<1TB").IsValid);
    }

    [Fact]
    public void Case_does_not_decide_whether_a_unit_is_understood()
    {
        Assert.True(QueryParser.Parse("memory:>100mb").IsValid);
        Assert.True(QueryParser.Parse("memory:>100Mb").IsValid);
    }

    [Fact]
    public void A_range_the_wrong_way_round_is_a_mistake_rather_than_an_empty_answer()
    {
        // Same rule the number field follows. A range that ends before it starts matches
        // nothing ever, so letting it through would answer a transposition with a list that
        // reads as "there are none".
        Assert.False(QueryParser.Parse("memory:1GB-100MB").IsValid);
        Assert.True(QueryParser.Parse("memory:100MB-1GB").IsValid);
    }

    [Fact]
    public void An_entry_that_is_not_running_is_a_no_rather_than_an_error()
    {
        // A member that cannot be judged does not match and never breaks the query, exactly
        // as pid:>1000 behaves on a stopped service.
        var parsed = QueryParser.Parse("memory:>1MB");

        Assert.True(parsed.IsValid);
        Assert.DoesNotContain("AsusUpdateCheck", Match("memory:>1MB"));
    }

    [Fact]
    public void Having_no_process_and_nobody_having_looked_are_different_answers()
    {
        // Over the ordinary catalogue nobody has measured anything, so no entry answers any
        // of the three reserved words - the fourth state the query language still has no
        // word for, pinned here as it is for triggers.
        Assert.Empty(MatchOver("memory:none", Specimens.All));
        Assert.Empty(MatchOver("memory:any", Specimens.All));

        // Measured, a stopped entry genuinely has none and says so.
        var measured = MemoryPass.Fill(Specimens.All, new FakeProcessMemoryReader());

        Assert.Contains("AsusUpdateCheck", MatchOver("memory:none", measured));
        Assert.Contains("Spooler", MatchOver("memory:any", measured));
    }

    private static List<string> Match(string query) =>
        MatchOver(query, MemoryPass.Fill(Specimens.All, new FakeProcessMemoryReader()));

    private static List<string> MatchOver(string query, IReadOnlyList<ScmEntry> entries)
    {
        var parsed = QueryParser.Parse(query);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));

        return [.. parsed.Query!.Filter(entries).Entries.Select(entry => entry.ServiceName)];
    }

    private static QueryProblemKind Problem(string query) =>
        QueryParser.Parse(query).Problems[0].Kind;
}
