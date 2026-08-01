using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// The query language against the real machine.
///
/// The unit tests decide what the language means. These decide whether what it means
/// agrees with the system, which is the check that catches the mistakes reading the code
/// would not: sc.exe already knows the answers, so any difference is ours unless it is
/// deliberate and explained.
///
/// Read-only, so it is safe on the machine we work on.
/// </summary>
public sealed class QueryContractTests
{
    [Fact]
    public void No_query_and_an_empty_query_both_select_everything()
    {
        // Has to be checked rather than assumed. The other reading, an empty search box
        // showing an empty list, would be absurd and is exactly what a naive filter does.
        var everything = CommandLineTool.Listing().Length;
        var empty = CommandLineTool.Listing("--query", "").Length;

        // The machine is live, so entries come and go between two runs seconds apart.
        Assert.InRange(empty, everything - 5, everything + 5);
    }

    [Fact]
    public void A_query_narrows_the_listing_and_every_entry_left_satisfies_it()
    {
        var running = CommandLineTool.Listing("--query", "status:running");
        var everything = CommandLineTool.Listing();

        Assert.True(running.Length > 0);
        Assert.True(running.Length < everything.Length);

        foreach (var entry in running)
        {
            Assert.Equal("Running", CommandLineTool.Text(entry, "status"));
        }
    }

    [Fact]
    public void The_acceptance_scenario_returns_automatic_entries_that_are_not_running()
    {
        // Scenario one from the specification, and the pair the slice is judged on: the
        // entries services.msc shows as Automatic with an empty status column.
        var found = CommandLineTool.Listing("--query", "start:auto !status:running !type:driver");

        foreach (var entry in found)
        {
            Assert.Equal("Automatic", CommandLineTool.Text(entry, "startType"));
            Assert.NotEqual("Running", CommandLineTool.Text(entry, "status"));
            Assert.DoesNotContain("Driver", CommandLineTool.Text(entry, "entryType"), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Everything_we_call_delayed_is_delayed_according_to_sc()
    {
        // The independent check on the flag we started reading for this slice. sc.exe marks
        // it in the start type line, and it is the authority.
        var delayed = CommandLineTool.Listing("--query", "start:delayed");

        Assert.True(delayed.Length > 0, "No delayed entries found, so this check proved nothing.");

        foreach (var entry in delayed)
        {
            var name = CommandLineTool.Text(entry, "serviceName");
            var configuration = CommandLineTool.ServiceControl("qc", name).StandardOutput;

            Assert.Contains("(DELAYED)", configuration, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Nothing_we_call_automatic_without_delay_is_delayed_according_to_sc()
    {
        // The other direction, and the one that would catch a flag read as true for
        // everything. Without it the test above would pass on a tool that says every
        // automatic entry is delayed.
        var plain = CommandLineTool.Listing("--query", "start:auto !start:delayed");

        Assert.True(plain.Length > 0, "No plain automatic entries found, so this check proved nothing.");

        foreach (var entry in plain.Take(25))
        {
            var name = CommandLineTool.Text(entry, "serviceName");
            var configuration = CommandLineTool.ServiceControl("qc", name).StandardOutput;

            Assert.DoesNotContain("(DELAYED)", configuration, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Automatic_covers_delayed_and_the_two_parts_add_up()
    {
        var automatic = CommandLineTool.Listing("--query", "start:auto").Length;
        var delayed = CommandLineTool.Listing("--query", "start:delayed").Length;
        var plain = CommandLineTool.Listing("--query", "start:auto !start:delayed").Length;

        // Live machine, so a small drift between runs is ordinary. A wrong split would be
        // off by far more than this.
        Assert.InRange(delayed + plain, automatic - 5, automatic + 5);
    }

    [Fact]
    public void The_delayed_flag_reaches_the_machine_readable_output()
    {
        var delayed = CommandLineTool.Listing("--query", "start:delayed");

        foreach (var entry in delayed)
        {
            Assert.True(entry.GetProperty("delayedAuto").GetBoolean());

            // The start type keeps the value it always had. Anything already reading this
            // output has to keep working, which is why the delay is a field of its own.
            Assert.Equal("Automatic", CommandLineTool.Text(entry, "startType"));
        }
    }

    [Fact]
    public void The_delayed_flag_is_null_where_the_idea_does_not_apply()
    {
        foreach (var entry in CommandLineTool.Listing("--query", "start:manual").Take(25))
        {
            Assert.Equal(JsonValueKind.Null, entry.GetProperty("delayedAuto").ValueKind);
        }
    }

    [Theory]
    [InlineData("status:runing", "running")]
    [InlineData("stat:running", "name")]
    [InlineData("pid:abc", "1234")]
    public void A_query_with_a_mistake_says_what_is_wrong_instead_of_answering(string query, string expectedHint)
    {
        var run = CommandLineTool.Run("--query", query, "--json");

        // Nothing on the data channel. Answering a typo with the unfiltered listing would
        // hand every entry on the machine to whatever comes next in the pipeline.
        Assert.Equal(2, run.ExitCode);
        Assert.Empty(run.StandardOutput.Trim());

        // And the complaint has to carry the way out, not just the fact of a mistake.
        Assert.Contains(expectedHint, run.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Asking_what_could_not_be_read_is_an_ordinary_query_and_succeeds()
    {
        // Deliberately not claiming to prove that a partial result reports itself. On this
        // machine the configuration query is never refused, so there is nothing partial to
        // observe, and a test named after behaviour it cannot see would be worse than none.
        // What it does check is that the reserved question mark runs and ends cleanly.
        var run = CommandLineTool.Run("--query", "account:?", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.NotEmpty(run.StandardOutput.Trim());
    }

    [Fact]
    public void A_bare_word_searches_more_than_the_service_name_alone()
    {
        var byName = CommandLineTool.Listing("--query", "name:=Spooler");
        var free = CommandLineTool.Listing("--query", "spooler");

        Assert.Single(byName);
        Assert.True(
            free.Length >= byName.Length,
            $"A bare word found {free.Length} entries, fewer than the {byName.Length} found by name alone.");

        Assert.Contains(free, entry => CommandLineTool.Text(entry, "serviceName") == "Spooler");
    }
}
