using System.Diagnostics;
using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// One walk over a listing has one budget for patterns the linear engine would not take - logic report R10,
/// owner's decision of 2026-10-26 "a budget for the whole query". Before it each entry had fifty milliseconds
/// of its own: 37.5 s over 804 entries from the terminal, and the window still for as long.
///
/// <b>A ruler handed in, not the clock</b>, for every test but the last: what is held is which matches are
/// tried and which are not, and a stopwatch would also be measuring the machine (QueryPatterns says why).
/// </summary>
public sealed class QueryBudgetTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void Once_the_walk_has_spent_its_budget_a_costly_pattern_is_not_tried_and_says_it_ran_out()
    {
        // Each match the ruler sees costs a hundred milliseconds, and the walk may spend two hundred.
        var ruler = new Ruler();
        var pass = new QueryPass(QueryParserTests.Valid("name:/Spool(?=er)/"), new QueryBudget(ruler.Read, 2 * Step));

        var matches = Enumerable.Range(0, 5).Select(_ => pass.Match(Entries.Named("Spooler", "Spooler"))).ToList();

        Assert.Equal([true, true, false, false, false], matches.Select(match => match.Matched));
        Assert.Equal([false, false, true, true, true], matches.Select(match => match.TooCostly));

        // Never checked means never asked: two matches, two readings of the ruler each.
        Assert.Equal(4, ruler.Readings);
    }

    [Fact]
    public void The_linear_engine_is_never_charged_however_little_is_left()
    {
        // It cannot run away, so a walk spending its time there is a large listing, not a costly pattern.
        var ruler = new Ruler();
        var pass = new QueryPass(QueryParserTests.Valid("name:/Spool/"), new QueryBudget(ruler.Read, TimeSpan.Zero));

        var match = pass.Match(Entries.Named("Spooler", "Spooler"));

        Assert.True(match.Matched);
        Assert.False(match.TooCostly);
        Assert.Equal(0, ruler.Readings);
    }

    [Fact]
    public void Every_walk_begins_with_the_whole_budget()
    {
        // The window walks its rows again at every keystroke - a spent budget is not carried into the next.
        var query = QueryParserTests.Valid("name:/Spool(?=er)/");

        query.Pass().Match(Entries.Named("Spooler", "Spooler"));

        Assert.True(query.Pass().Match(Entries.Named("Spooler", "Spooler")).Matched);
    }

    [Fact]
    public void A_listing_filtered_at_once_shares_one_budget_rather_than_one_per_entry()
    {
        // The pattern runs away on every entry: a backreference sends it to the backtracking engine, and
        // there each match takes its whole fifty milliseconds. Two hundred entries cost ten seconds with a
        // budget each, and about a quarter of a second sharing one. Three seconds between them is a margin
        // of more than ten either way - this does not measure speed, it tells the two arrangements apart.
        var runOfAs = new string('a', 32);
        var entries = Enumerable.Range(0, 200).Select(_ => Entries.Named(runOfAs, runOfAs)).ToList();
        var query = QueryParserTests.Valid(@"name:/(a+)+\1b/");

        var watch = Stopwatch.StartNew();
        var result = query.Filter(entries);

        watch.Stop();

        Assert.Equal(200, result.TooCostly);
        Assert.Empty(result.Entries);
        Assert.True(
            watch.Elapsed < TimeSpan.FromSeconds(3),
            "The walk took " + watch.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms.");
    }

    /// <summary>A ruler that moves a step every time it is read, and counts how often it was.</summary>
    private sealed class Ruler
    {
        private TimeSpan _now;

        internal int Readings { get; private set; }

        internal TimeSpan Read()
        {
            Readings++;
            _now += Step;

            return _now;
        }
    }
}
