using System.Text.Json;
using Bws.Core.History;

namespace Bws.Cli.Tests;

/// <summary>
/// <c>bws history</c> as the person and the script see it: the sentences that say what the log can
/// speak for, the table, the document, and the two answers that are not a history at all - a name
/// nothing answers to, and words the verb has no room for.
///
/// <b>The sentences are asserted in their own words</b>, not through the keys the product uses -
/// a test reading its expectation from the same key as the code would pass with any wording at all.
/// </summary>
public sealed class HistoryOutputTests
{
    private const string Person = "S-1-5-21-1111111111-2222222222-3333333333-1001";
    private static readonly DateTimeOffset Cleared = new(2026, 10, 11, 14, 58, 5, TimeSpan.Zero);

    [Fact]
    public void A_cleared_log_says_when_and_who()
    {
        var lines = HistoryText.Admissions(History(Log(cleared: true)), [], serviceName: null);

        Assert.Contains("reaches back to", lines[0], StringComparison.Ordinal);
        Assert.Contains("HOST\\USER cleared it", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void No_log_is_promised_to_have_lost_nothing_and_one_that_loses_says_which_end()
    {
        // H02, 2026-10-27: nothing about a log proves it was written to without a gap, so the first
        // sentence says how far back it reaches and stops there.
        var whole = HistoryText.Admissions(History(Log()), [], serviceName: null);
        var overwritten = HistoryText.Admissions(History(Log(overwritten: true)), [], serviceName: null);
        var full = HistoryText.Admissions(History(Log(full: true) with { Mode = LogMode.Retain }), [], serviceName: null);

        Assert.Equal(
            "The System log reaches back to "
            + Cleared.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + ".",
            whole[0]);
        Assert.DoesNotContain(whole, line => line.Contains("lost nothing", StringComparison.Ordinal));

        Assert.Contains(overwritten, line => line.Contains("anything older is gone", StringComparison.Ordinal));

        // The other end missing: a full log that keeps what it has is not writing the newest.
        Assert.Contains(full, line => line.Contains("latest changes are missing", StringComparison.Ordinal));
    }

    [Fact]
    public void An_archived_log_says_older_events_may_be_in_files_bws_does_not_read()
    {
        // H02: said whatever its size, because the archives are there from the first time it filled.
        var archived = HistoryText.Admissions(History(Log() with { Mode = LogMode.AutoBackup }), [], serviceName: null);
        var circular = HistoryText.Admissions(History(Log()), [], serviceName: null);

        Assert.Contains(archived, line => line.Contains("archive files", StringComparison.Ordinal));
        Assert.DoesNotContain(circular, line => line.Contains("archive", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_log_is_called_empty_rather_than_given_a_date()
    {
        var log = Log() with { ReachesBack = null };

        Assert.Equal("The System log is empty.", HistoryText.Admissions(History(log), [], serviceName: null)[0]);
    }

    [Fact]
    public void The_whole_machine_says_why_installations_are_in_quotes()
    {
        var history = History(Log(), Installed());
        var lines = HistoryText.Admissions(history, history.Changes, serviceName: null);

        Assert.Contains(lines, line => line.Contains("display name in quotes", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("not listed for one name", StringComparison.Ordinal));
    }

    [Fact]
    public void One_entry_says_why_installations_are_not_there_and_counts_what_names_nothing()
    {
        var history = History(Log(), Installed(), Changed(null), Changed("Spooler"));
        var lines = HistoryText.Admissions(history, history.Of("Spooler"), "Spooler");

        Assert.Contains(lines, line => line.Contains("not listed for one name", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("1 start type change does not name its entry", StringComparison.Ordinal));
    }

    [Fact]
    public void No_change_is_said_rather_than_printed_as_an_empty_table()
    {
        var lines = HistoryText.Admissions(History(Log()), [], "Spooler");

        Assert.Contains("No start type change to Spooler is recorded in it.", lines);
        Assert.Equal(string.Empty, HistoryText.Render([]));
    }

    [Fact]
    public void A_row_says_when_who_which_entry_and_what_changed()
    {
        var table = HistoryText.Render([Changed("Spooler")]).Split(Environment.NewLine);

        Assert.Equal(2, table.Length);
        Assert.StartsWith("WHEN", table[0], StringComparison.Ordinal);
        Assert.Contains("HOST\\USER", table[1], StringComparison.Ordinal);
        Assert.Contains("Spooler", table[1], StringComparison.Ordinal);
        Assert.Contains("start type auto start -> demand start", table[1], StringComparison.Ordinal);
    }

    [Fact]
    public void An_installation_shows_its_display_name_in_quotes_and_the_launch_command_last()
    {
        var row = HistoryText.Render([Installed()]).Split(Environment.NewLine)[1];

        Assert.Contains("\"Hostile - switched off\"", row, StringComparison.Ordinal);
        Assert.EndsWith("installed (user mode service, disabled, LocalSystem) C:\\Windows\\system32\\svchost.exe -k netsvcs", row, StringComparison.Ordinal);
    }

    [Fact]
    public void An_account_this_machine_could_not_name_is_shown_as_its_sid()
    {
        var change = Changed("Spooler") with { By = new Actor(Person, null) };

        Assert.Contains(Person, HistoryText.Render([change]), StringComparison.Ordinal);
    }

    [Fact]
    public void A_line_break_in_a_display_name_does_not_make_a_second_row()
    {
        // Written by whoever installed the service - the table escapes it before laying out.
        var change = Installed() with { DisplayName = "two" + '\n' + "lines" };

        Assert.Equal(2, HistoryText.Render([change]).Split(Environment.NewLine).Length);
    }

    [Fact]
    public void The_document_holds_every_field_in_every_change_with_the_log_first()
    {
        var history = History(Log(cleared: true), Changed("Spooler"), Installed());
        var root = JsonDocument.Parse(HistoryJson.Render(history.Log, history.Changes)).RootElement;

        Assert.Equal(["log", "changes"], root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["name", "reachesBack", "overwritten", "full", "cleared", "mode"],
            root.GetProperty("log").EnumerateObject().Select(property => property.Name));

        string[] fields = ["at", "kind", "serviceName", "displayName", "by", "binaryPath", "account", "windowsWords"];
        string[] words = ["startTypeBefore", "startTypeAfter", "entryType", "startType"];

        foreach (var change in root.GetProperty("changes").EnumerateArray())
        {
            Assert.Equal(fields, change.EnumerateObject().Select(property => property.Name));
            Assert.Equal(words, change.GetProperty("windowsWords").EnumerateObject().Select(property => property.Name));
        }

        var installed = root.GetProperty("changes")[1];

        // Written out as null rather than left out - J1-b, there is no service name to give.
        Assert.Equal(JsonValueKind.Null, installed.GetProperty("serviceName").ValueKind);
        Assert.Equal("Hostile - switched off", installed.GetProperty("displayName").GetString());
        Assert.Equal(Person, root.GetProperty("log").GetProperty("cleared").GetProperty("by").GetProperty("sid").GetString());
    }

    [Fact]
    public void The_words_windows_wrote_are_never_written_where_a_value_of_this_tool_would_be()
    {
        // J1-d: "auto start" beside the listing's startType would be compared by a script and be wrong
        // without a sound. The words live in windowsWords and nowhere else.
        var root = JsonDocument.Parse(HistoryJson.Render(Log(), [Changed("Spooler")])).RootElement;
        var change = root.GetProperty("changes")[0];

        Assert.False(change.TryGetProperty("startType", out _));
        Assert.Equal("auto start", change.GetProperty("windowsWords").GetProperty("startTypeBefore").GetString());
    }

    [Fact]
    public void A_name_nothing_answers_to_is_a_mistake_and_the_manager_is_asked_only_then()
    {
        var asked = 0;
        IEnumerable<string> Names()
        {
            asked++;
            return ["Spooler"];
        }

        var reader = new Fixed(History(Log(), Changed("Spooler")));

        Assert.Equal(ExitCode.Usage, HistoryCommand.Run(CommandLine.Read(["history", "Spoolr"]), reader, Names).Exit);
        Assert.Equal(1, asked);

        // An entry with changes, and the whole machine, never open the manager.
        Assert.Equal(ExitCode.Ok, HistoryCommand.Run(CommandLine.Read(["history", "spooler"]), reader, Names).Exit);
        Assert.Equal(ExitCode.Ok, HistoryCommand.Run(CommandLine.Read(["history"]), reader, Names).Exit);
        Assert.Equal(1, asked);
    }

    [Fact]
    public void An_entry_nobody_ever_changed_is_an_answer_rather_than_a_mistake()
    {
        var (data, exit) = HistoryCommand.Run(
            CommandLine.Read(["history", "BITS"]), new Fixed(History(Log(), Changed("Spooler"))), () => ["BITS"]);

        Assert.Equal(ExitCode.Ok, exit);
        Assert.Equal(string.Empty, data);
    }

    [Fact]
    public void Two_names_and_a_switch_of_another_verb_are_refused_before_anything_is_read()
    {
        Assert.Equal(ExitCode.Usage, Refusals.Answer(CommandLine.Read(["history", "Spooler", "BITS"])));
        Assert.Equal(ExitCode.Usage, Refusals.Answer(CommandLine.Read(["history", "--query", "x"])));
        Assert.Null(Refusals.Answer(CommandLine.Read(["history"])));
        Assert.Null(Refusals.Answer(CommandLine.Read(["history", "Spooler", "--json", "--timing"])));
    }

    /// <summary>A circular log - another mode is <c>Log() with { Mode = ... }</c>.</summary>
    private static LogCoverage Log(bool cleared = false, bool overwritten = false, bool full = false) => new()
    {
        Name = "System",
        ReachesBack = Cleared,
        Overwritten = overwritten,
        Full = full,
        Cleared = cleared ? new Clearing(Cleared, new Actor(Person, "HOST\\USER")) : null,
        Mode = LogMode.Circular
    };

    private static ChangeHistory History(LogCoverage log, params ServiceChange[] changes) => new(log, changes);

    private static ServiceChange Changed(string? name) => new()
    {
        At = Cleared.AddDays(1),
        Kind = ChangeKind.StartTypeChanged,
        ServiceName = name,
        DisplayName = "Print Spooler",
        By = new Actor(Person, "HOST\\USER"),
        Words = new WindowsWords("auto start", "demand start", null, null)
    };

    private static ServiceChange Installed() => new()
    {
        At = Cleared.AddDays(2),
        Kind = ChangeKind.Installed,
        DisplayName = "Hostile - switched off",
        By = new Actor(Person, "HOST\\USER"),
        BinaryPath = @"C:\Windows\system32\svchost.exe -k netsvcs",
        Account = "LocalSystem",
        Words = new WindowsWords(null, null, "user mode service", "disabled")
    };

    private sealed class Fixed(ChangeHistory history) : IChangeHistoryReader
    {
        public ChangeHistory Read() => history;
    }
}
