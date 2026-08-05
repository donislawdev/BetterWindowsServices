using System.Diagnostics;
using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Integration.Tests;

/// <summary>
/// One query, two interfaces, the same entries.
///
/// <c>docs/07</c> calls this a contract rather than a coincidence and asks for exactly this
/// test. It is the check the whole slice is judged on, and it is worth being clear about what
/// it can and cannot prove.
///
/// <b>What it cannot prove:</b> that the query language is right. Both sides call the same
/// code in <c>Bws.Core.Querying</c>, so a bug in the engine agrees with itself perfectly -
/// this is the lesson from S5a1, where a guard comparing two runs of the same code turned out
/// to see nothing but non-determinism. The engine is held to <c>sc.exe</c> elsewhere.
///
/// <b>What it does prove:</b> that the wiring around it matches. A window that filters rows
/// instead of entries, forgets a switch, applies one twice, hands over the text it rewrote
/// rather than the text it was given, or reads a different set of entries to begin with -
/// every one of those shows up here and nowhere else.
///
/// Read only, so it is safe on the machine we work on.
/// </summary>
public sealed class QueryParityContractTests(Xunit.Abstractions.ITestOutputHelper output)
{
    /// <summary>
    /// Queries about how a machine is set up, which is the half that does not move while the
    /// test runs. These are compared exactly.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("start:auto")]
    [InlineData("type:driver")]
    [InlineData("!type:driver")]
    [InlineData("account:LocalSystem")]
    [InlineData("name:/^w.*/")]
    [InlineData("svc")]
    [InlineData("start:auto,manual !account:LocalSystem")]
    public async Task The_same_text_picks_the_same_entries_in_the_window_and_in_the_terminal(string query)
    {
        var window = await Window(query);
        var terminal = Terminal(query);

        // Only the entries both readings saw. A service registered or removed in the seconds
        // between the two runs is a fact about a live machine, not evidence about filtering,
        // and letting it fail the test would make this the flakiest check in the suite.
        var both = window.Everything.Intersect(terminal.Everything, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(both);

        var fromWindow = window.Selected.Where(both.Contains).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var fromTerminal = terminal.Selected.Where(both.Contains).OrderBy(name => name, StringComparer.Ordinal).ToArray();

        Assert.Equal(fromTerminal, fromWindow);

        // Two empty lists are equal, and a comparison of two empty lists proves nothing. Every
        // query above has to select something on any Windows, so nothing here can pass by both
        // sides finding the same nothing.
        Assert.NotEmpty(fromTerminal);
    }

    /// <summary>
    /// The same, for queries about what is running - compared with room for the machine
    /// moving underneath.
    ///
    /// Measured at S5b2 on this machine: a minute after a snapshot, XblAuthManager and
    /// msiserver had started on their own with nobody touching them. A running state read
    /// twice, seconds apart, is genuinely two different facts, so an exact comparison here
    /// would be testing the machine's stillness rather than our wiring. A wiring mistake moves
    /// this by dozens or hundreds of entries, never by three.
    /// </summary>
    [Theory]
    [InlineData("status:running")]
    [InlineData("status:stopped")]
    [InlineData("start:auto !status:running !type:driver")]
    public async Task A_query_about_what_is_running_agrees_to_within_what_the_machine_moved(string query)
    {
        var window = await Window(query);
        var terminal = Terminal(query);

        var both = window.Everything.Intersect(terminal.Everything, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        var fromWindow = window.Selected.Where(both.Contains).ToHashSet(StringComparer.Ordinal);
        var fromTerminal = terminal.Selected.Where(both.Contains).ToHashSet(StringComparer.Ordinal);

        var disagreed = fromWindow.Except(fromTerminal, StringComparer.Ordinal)
            .Union(fromTerminal.Except(fromWindow, StringComparer.Ordinal), StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            disagreed.Length <= 5,
            $"The window and the terminal disagreed about {disagreed.Length} entries for '{query}': " +
            string.Join(", ", disagreed.Take(20)));

        // And it selected something, so this is not passing by both sides finding nothing.
        Assert.NotEmpty(fromWindow);
    }

    [Fact]
    public async Task The_drivers_switch_selects_what_the_written_member_selects()
    {
        // The switch is the member, so the two have to be the same thing rather than merely
        // agree - owner's decision, 2026-08-02.
        var model = await Load();
        var everything = Names(model);

        model.ShowDrivers = false;

        Assert.Equal("!type:driver", model.QueryText);

        var afterSwitch = Names(model);
        var terminal = Terminal("!type:driver");
        var both = everything.Intersect(terminal.Everything, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            terminal.Selected.Where(both.Contains).OrderBy(name => name, StringComparer.Ordinal),
            afterSwitch.Where(both.Contains).OrderBy(name => name, StringComparer.Ordinal));

        // And it really removed something, so the check is not passing on a machine with no
        // drivers on it.
        Assert.True(afterSwitch.Count < everything.Count);
    }

    [Fact]
    public async Task The_regex_switch_means_the_same_thing_as_writing_the_slashes()
    {
        // The switch is not a second syntax. With it on, a bare word is the expression - which
        // is what the language already writes between slashes, so the two spellings have to
        // pick the same entries.
        //
        // The expression is chosen so that the two readings genuinely differ. The first
        // version of this test asked about ^sql, and on a machine with no SQL Server both
        // sides answered with nothing - so it passed while checking nothing at all, the trap
        // ADR-10 names. Found by mutation: turning the switch off in the view model left this
        // test green.
        var model = await Load();

        model.BareWordsAreExpressions = true;
        model.QueryText = "^w";

        var window = Names(model);
        var everything = Names(model, everything: true);
        var terminal = Terminal("/^w/");
        var both = everything.Intersect(terminal.Everything, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        var fromWindow = window.Where(both.Contains).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var fromTerminal = terminal.Selected.Where(both.Contains).OrderBy(name => name, StringComparer.Ordinal).ToArray();

        Assert.Equal(fromTerminal, fromWindow);

        // It selected something, and not everything. Without both of these the comparison
        // above holds for a switch that does nothing.
        Assert.NotEmpty(fromWindow);
        Assert.True(fromWindow.Length < everything.Count);
    }

    [Fact]
    public async Task A_question_about_signatures_is_answered_differently_and_the_window_says_so()
    {
        // A known and deliberate difference, pinned here so nobody finds it in a year and
        // files it as a bug. The terminal answers this by verifying every binary, measured at
        // 1100-1245 ms over 810 entries and 544 files. A search box cannot pay that on every
        // keystroke, so the window says nobody looked instead of answering with an empty list
        // that reads exactly like "there are none".
        //
        // Reading it in the background is A10's work and belongs to S6c.
        //
        // THE QUESTION IS signed:yes AND IT USED TO BE signed:no. Changed 2026-08-04, after the
        // second machine this project has ever run on answered signed:no with nothing at all -
        // Windows Server 2025, clean install, every binary signed - and the last line here failed
        // saying the collection was empty. It was right to fail: it had stopped being able to
        // tell "the terminal answered" from "the terminal found none", which is the ADR-10 trap
        // this very test class was rewritten for once already, when a parity check asked about
        // ^sql on a machine with no SQL Server and passed by comparing two empty answers.
        //
        // signed:yes is the same kind of question and its answer cannot be empty on a Windows
        // machine: 660 of 661 entries on the server, and the one left out is the entry whose
        // binary could not be resolved. So the claim below survives being asked on somebody
        // else's install, which is the only thing that was wrong with it.
        var model = await Load();

        model.QueryText = "signed:yes";

        Assert.Empty(model.Rows);
        Assert.Equal(string.Empty, model.Says.Problem);
        Assert.NotEqual(string.Empty, model.Says.Notice);

        var terminal = Terminal("signed:yes");

        Assert.NotEmpty(terminal.Selected);
    }

    [Fact]
    public async Task Filtering_the_whole_listing_stays_inside_the_budget()
    {
        // Section 8.1 of the specification gives the query 50 ms over the whole listing, and
        // docs/07 calls it a design constraint rather than a performance note: it runs on every
        // keystroke, on the interface thread.
        //
        // This measures the view model, which is the part we wrote - reading the query,
        // judging every entry, and building the rows that are left. It does not measure the
        // list drawing itself, and no test here can: that needs a window on a screen and a
        // person looking at it.
        var model = await Load();

        model.QueryText = string.Empty;

        var everything = model.Rows.Count;

        // Cold run, thrown away. It pays for the first touch of everything underneath.
        model.QueryText = "status:running";

        var runs = new List<(string Query, double Milliseconds, int Rows)>();

        // Timed in ticks rather than milliseconds. The first attempt at this printed 0 ms for
        // most of these, which is not a fast measurement - it is a measurement below the
        // resolution of the instrument, and reporting it as zero would be the instrument
        // lying quietly.
        //
        // Two shapes, alternated rather than run in blocks, and one of them is the worst case:
        // a bare word is tested against every free-search field of every entry.
        foreach (var query in new[]
                 {
                     "status:running", "spool", "start:auto", "winmgmt",
                     "status:stopped", "svc", "start:auto !status:running", "s"
                 })
        {
            model.QueryText = string.Empty;

            var before = Stopwatch.GetTimestamp();
            model.QueryText = query;
            var elapsed = Stopwatch.GetElapsedTime(before);

            runs.Add((query, elapsed.TotalMilliseconds, model.Rows.Count));
        }

        // Printed rather than merely compared, because a number nobody saw is not a
        // measurement. Run with a detailed logger to read them.
        foreach (var (query, milliseconds, rows) in runs)
        {
            output.WriteLine($"{milliseconds,7:F2} ms  {rows,4} rows  {query}");
        }

        var slowest = runs.Max(run => run.Milliseconds);

        output.WriteLine(
            $"SPREAD {runs.Min(run => run.Milliseconds):F2}-{slowest:F2} ms over {everything} entries, budget 50 ms");

        // The row counts have to differ, or this measured a filter that never filtered.
        Assert.True(
            runs.Select(run => run.Rows).Distinct().Count() > 1,
            "Every query selected the same number of entries, so this timed something other than filtering.");

        Assert.True(
            slowest <= 50,
            $"Filtering took up to {slowest:F2} ms over {everything} entries, " +
            "against the 50 ms in section 8.1 of the specification.");
    }

    private static async Task<MainViewModel> Load()
    {
        var model = new MainViewModel(new WindowsScmCatalog(), new SystemClock());

        await model.LoadAsync();

        Assert.NotEmpty(model.Rows);

        return model;
    }

    private static HashSet<string> Names(MainViewModel model, bool everything = false)
    {
        if (!everything)
        {
            return model.Rows.Select(row => row.ServiceName).ToHashSet(StringComparer.Ordinal);
        }

        var query = model.QueryText;
        model.QueryText = string.Empty;
        var all = model.Rows.Select(row => row.ServiceName).ToHashSet(StringComparer.Ordinal);
        model.QueryText = query;

        return all;
    }

    private static async Task<(HashSet<string> Everything, HashSet<string> Selected)> Window(string query)
    {
        var model = await Load();
        var everything = Names(model);

        model.QueryText = query;

        return (everything, Names(model));
    }

    private static (HashSet<string> Everything, HashSet<string> Selected) Terminal(string query)
    {
        var everything = CommandLineTool.Listing()
            .Select(entry => CommandLineTool.Text(entry, "serviceName"))
            .ToHashSet(StringComparer.Ordinal);

        var selected = CommandLineTool.Listing("--query", query)
            .Select(entry => CommandLineTool.Text(entry, "serviceName"))
            .ToHashSet(StringComparer.Ordinal);

        return (everything, selected);
    }
}
