using System.Globalization;
using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// <c>bws history</c> against the real System log, checked against what Get-WinEvent says about the
/// same log - the second opinion for this command, the way sc.exe is for the listing.
///
/// <b>Read only, and readable without administrator rights</b> - measured 2026-10-15 under a
/// restricted token, so this runs anywhere the suite does.
///
/// <b>The log is written while the test reads it</b>: Windows itself flips BITS between automatic and
/// manual about fifteen times a day. So ours is read FIRST and PowerShell's after, and the comparison
/// allows the second to have seen a few more - never fewer.
/// </summary>
[Trait("runs", "anywhere")]
public sealed class HistoryContractTests
{
    private const string Query =
        "@(Get-WinEvent -FilterHashtable @{LogName='System';ProviderName='Service Control Manager';Id=7040,7045} " +
        "-ErrorAction SilentlyContinue).Count";

    [Fact]
    public void Every_start_type_change_and_installation_the_log_holds_is_listed()
    {
        var ours = Document().GetProperty("changes").GetArrayLength();
        var theirs = int.Parse(CommandLineTool.PowerShell(Query), CultureInfo.InvariantCulture);

        Assert.InRange(theirs - ours, 0, 3);
    }

    [Fact]
    public void The_log_reaches_back_as_far_as_its_oldest_record()
    {
        var log = Document().GetProperty("log");
        var oldest = CommandLineTool.PowerShell(
            "(Get-WinEvent -LogName System -MaxEvents 1 -Oldest).TimeCreated.ToUniversalTime().ToString('o')");

        // Equal unless the log is overwriting - then the oldest record moves forward between the reads.
        var ours = log.GetProperty("reachesBack").GetDateTimeOffset();
        var theirs = DateTimeOffset.Parse(oldest, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        if (log.GetProperty("overwritten").GetBoolean())
        {
            Assert.True(ours <= theirs);
        }
        else
        {
            Assert.Equal(theirs, ours);
        }

        Assert.Equal("System", log.GetProperty("name").GetString());
    }

    [Fact]
    public void Changes_come_newest_first_and_a_start_type_change_always_names_its_entry()
    {
        var changes = Document().GetProperty("changes").EnumerateArray().ToArray();
        var times = changes.Select(change => change.GetProperty("at").GetDateTimeOffset()).ToArray();

        Assert.Equal(times.OrderByDescending(time => time), times);

        // On every Windows this suite runs on, 7040 carries param4. A null here is a shape worth
        // knowing about the day it appears.
        Assert.All(
            changes.Where(change => change.GetProperty("kind").GetString() == "StartTypeChanged"),
            change => Assert.False(string.IsNullOrEmpty(change.GetProperty("serviceName").GetString())));
    }

    private static JsonElement Document()
    {
        var run = CommandLineTool.Run("history", "--json");

        Assert.True(run.ExitCode == 0, $"bws history exited {run.ExitCode}:\n{run.StandardError}");

        return JsonDocument.Parse(run.StandardOutput).RootElement;
    }
}
