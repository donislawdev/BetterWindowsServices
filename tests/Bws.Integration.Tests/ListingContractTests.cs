using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// Runs the real command line tool against the real service control manager and checks
/// the things the owner can check by hand: does it agree with the system, does it keep
/// its output channels apart, does it end with the right code.
///
/// Read-only. Nothing here changes any service, so it is safe on the machine we work on.
/// Anything that writes belongs on a throwaway virtual machine.
/// </summary>
public sealed class ListingContractTests
{
    [Fact]
    public void The_entry_count_agrees_with_the_service_control_manager()
    {
        var ours = CommandLineTool.Listing().Length;
        var theirs = CommandLineTool.ServiceControlCount("query", "type=", "all", "state=", "all");

        // sc.exe is the authority here. A difference is a bug on our side unless it is
        // deliberate and explained, and for the total there is nothing to explain away.
        Assert.Equal(theirs, ours);
    }

    [Fact]
    public void The_driver_count_agrees_with_the_service_control_manager()
    {
        var ours = CommandLineTool.Listing().Count(entry =>
            CommandLineTool.Text(entry, "entryType") is "KernelDriver" or "FileSystemDriver");

        Assert.Equal(CommandLineTool.ServiceControlCount("query", "type=", "driver", "state=", "all"), ours);
    }

    [Fact]
    public void We_show_more_services_than_sc_because_we_include_per_user_ones()
    {
        var ours = CommandLineTool.Listing().Count(entry =>
            CommandLineTool.Text(entry, "entryType") is not ("KernelDriver" or "FileSystemDriver"));

        var theirs = CommandLineTool.ServiceControlCount("query", "type=", "service", "state=", "all");

        // This is the one intended difference. sc.exe asks for plain Win32 services only,
        // so it leaves out per-user services. We show them, collapsed later in the GUI.
        // Asserting "greater" rather than an exact gap keeps this from breaking every time
        // somebody logs in or out.
        Assert.True(
            ours > theirs,
            $"Expected more entries than sc.exe reports for services. Ours: {ours}, sc.exe: {theirs}.");
    }

    [Fact]
    public void Data_goes_to_the_output_channel_and_nothing_else_does()
    {
        var run = CommandLineTool.Run("--json");

        Assert.Equal(0, run.ExitCode);

        // Valid JSON with nothing else mixed in. The moment a warning slips into this
        // channel, every pipe reading our output breaks.
        var parsed = JsonDocument.Parse(run.StandardOutput);
        Assert.True(parsed.RootElement.GetArrayLength() > 0);
    }

    [Fact]
    public void A_failed_run_writes_nothing_to_the_data_channel()
    {
        var run = CommandLineTool.Run("--no-such-option");

        Assert.Equal(2, run.ExitCode);
        Assert.Empty(run.StandardOutput.Trim());
        Assert.Contains("Unknown option", run.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void An_option_that_needs_a_value_and_has_none_is_a_mistake_rather_than_an_empty_query()
    {
        // Reading it as "no query" would quietly list every entry on the machine, which is
        // the opposite of what somebody who typed --query wanted.
        var run = CommandLineTool.Run("--query");

        Assert.Equal(2, run.ExitCode);
        Assert.Empty(run.StandardOutput.Trim());
    }

    [Theory]
    [InlineData(0, "--json")]
    [InlineData(0, "--query", "status:running", "--json")]
    [InlineData(0, "--query", "name:nothing-is-called-this", "--json")]
    [InlineData(2, "--no-such-option")]
    [InlineData(2, "--query")]
    [InlineData(2, "--query", "stat:running")]
    [InlineData(2, "--query", "status:runing")]
    [InlineData(2, "--query", "pid:abc")]
    [InlineData(2, "--query", "name:/[unclosed/")]
    [InlineData(2, "stray-word")]
    public void Every_way_this_can_end_keeps_the_channels_apart(int expected, params string[] arguments)
    {
        // One case per way the tool can finish, because a stray write in the branch nobody
        // ran is the one that reaches somebody's pipe. The exit codes are a public contract
        // of their own: monitoring and CI branch on them, so they are pinned here rather
        // than left to whatever the code happens to return.
        var run = CommandLineTool.Run(arguments);

        Assert.Equal(expected, run.ExitCode);

        if (expected == 0)
        {
            // Valid JSON and nothing else mixed in, even when the query matched nothing.
            _ = JsonDocument.Parse(run.StandardOutput);
        }
        else
        {
            Assert.Empty(run.StandardOutput);
            Assert.NotEmpty(run.StandardError.Trim());
        }
    }

    [Fact]
    public void A_partial_answer_is_not_a_failure()
    {
        // Reporting an ordinary lack of permissions as a failing run would make scripts stop
        // on something normal. The warning goes to the error channel and the code stays zero.
        var run = CommandLineTool.Run("--query", "account:?", "--json");

        Assert.Equal(0, run.ExitCode);
        _ = JsonDocument.Parse(run.StandardOutput);
    }

    [Fact]
    public void Every_entry_carries_a_name_a_display_name_and_a_type()
    {
        foreach (var entry in CommandLineTool.Listing())
        {
            Assert.False(string.IsNullOrWhiteSpace(CommandLineTool.Text(entry, "serviceName")));
            Assert.False(string.IsNullOrWhiteSpace(CommandLineTool.Text(entry, "displayName")));
            Assert.NotEqual("Unknown", CommandLineTool.Text(entry, "entryType"));
        }
    }
}
