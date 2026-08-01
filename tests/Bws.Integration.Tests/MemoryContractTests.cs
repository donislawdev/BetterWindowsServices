using System.Globalization;
using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// What the processes behind services are using, checked against what PowerShell says
/// about the same processes.
///
/// sc.exe has nothing to say here, so the authority is Get-Process - and it is a good one
/// for this, because the two numbers we report are the two it reports under names of its
/// own: WorkingSet64 and PrivateMemorySize64.
///
/// These numbers move while the test runs, which nothing else in this suite has to deal
/// with. Comparisons are therefore made with a tolerance, and the tolerance is the point of
/// several of these tests rather than a weakness in them: a reading of the wrong field is
/// wrong by a factor, never by a few percent.
///
/// Read-only. Asking a process how much memory it holds changes nothing in it.
/// </summary>
public sealed class MemoryContractTests
{
    [Fact]
    public void Our_working_set_is_the_one_PowerShell_reports_for_the_same_process()
    {
        var sampled = 0;

        foreach (var entry in Running().Take(6))
        {
            var processId = entry.GetProperty("processId").GetInt32();
            var ours = entry.GetProperty("memory").GetProperty("workingSet").GetInt64();
            var theirs = long.Parse(
                CommandLineTool.PowerShell($"(Get-Process -Id {processId}).WorkingSet64"),
                CultureInfo.InvariantCulture);

            // Within a fifth. A working set genuinely moves between two reads seconds apart,
            // and this is not measuring how much - it is catching a reading that reports the
            // peak, the commit or the pool instead, every one of which is out by far more.
            Assert.InRange(ours, theirs * 0.8, theirs * 1.2);
            sampled++;
        }

        Assert.Equal(6, sampled);
    }

    [Fact]
    public void The_commit_figure_is_the_other_one_PowerShell_reports_and_not_a_copy_of_the_first()
    {
        var sampled = 0;

        foreach (var entry in Running().Take(6))
        {
            var processId = entry.GetProperty("processId").GetInt32();
            var memory = entry.GetProperty("memory");
            var ours = memory.GetProperty("commit").GetInt64();
            var theirs = long.Parse(
                CommandLineTool.PowerShell($"(Get-Process -Id {processId}).PrivateMemorySize64"),
                CultureInfo.InvariantCulture);

            Assert.InRange(ours, theirs * 0.8, theirs * 1.2);

            // And it is genuinely the other number. Reading the same field into both is the
            // shape of mistake that leaves every value plausible - measured on this machine,
            // the commit runs well under the working set for a service process.
            Assert.NotEqual(memory.GetProperty("workingSet").GetInt64(), ours);
            sampled++;
        }

        Assert.Equal(6, sampled);
    }

    [Fact]
    public void An_entry_that_is_not_running_reports_no_memory_rather_than_zero()
    {
        var stopped = CommandLineTool.Listing("--memory", "--query", "status:stopped").Take(20).ToArray();

        Assert.NotEmpty(stopped);

        foreach (var entry in stopped)
        {
            // Null, and not named in "unreadable" either: there is genuinely no process, so
            // this is a fact about the entry. Zero would be a measurement nobody took.
            Assert.Equal(JsonValueKind.Null, entry.GetProperty("memory").ValueKind);

            Assert.False(
                entry.TryGetProperty("unreadable", out var unreadable)
                    && unreadable.TryGetProperty("memory", out _),
                $"{CommandLineTool.Text(entry, "serviceName")} reported its lack of a process as a refusal.");
        }
    }

    [Fact]
    public void Entries_sharing_a_process_all_say_how_many_are_sharing_it()
    {
        var listing = CommandLineTool.Listing("--memory");

        var byProcess = listing
            .Where(entry => entry.GetProperty("memory").ValueKind != JsonValueKind.Null)
            .GroupBy(entry => entry.GetProperty("processId").GetInt32())
            .ToArray();

        Assert.NotEmpty(byProcess);

        foreach (var group in byProcess)
        {
            foreach (var entry in group)
            {
                // The count each entry reports has to be the number of entries that actually
                // have that process. Getting this wrong leaves every megabyte figure correct
                // and only the sentence around it false, which is the whole reason the count
                // travels inside the answer.
                Assert.Equal(group.Count(), entry.GetProperty("memory").GetProperty("sharedBy").GetInt32());
            }
        }

        // And at least one process really is shared on this machine, so the assertion above
        // is not passing over a list where every group has one member.
        Assert.Contains(byProcess, group => group.Count() > 1);
    }

    [Fact]
    public void Asking_for_one_of_a_shared_pair_still_reports_how_many_share_it()
    {
        // The other quiet mistake this family invites: counting the sharing after the filter
        // has run. Every megabyte figure stays right and only the count changes, so a
        // service in a shared process would report having it to itself - and every other
        // test here works on an unfiltered listing, where the two orderings agree.
        var shared = CommandLineTool.Listing("--memory")
            .Where(entry => entry.GetProperty("memory").ValueKind != JsonValueKind.Null)
            .GroupBy(entry => entry.GetProperty("processId").GetInt32())
            .FirstOrDefault(group => group.Count() > 1);

        Assert.NotNull(shared);

        var name = CommandLineTool.Text(shared.First(), "serviceName");
        var alone = CommandLineTool.Listing("--memory", "--query", $"name:={name}").Single();

        Assert.Equal(shared.Count(), alone.GetProperty("memory").GetProperty("sharedBy").GetInt32());
    }

    [Fact]
    public void A_query_about_memory_reads_it_without_the_switch_and_without_the_signatures()
    {
        // Two promises in one run. The query turns the reading on by itself, or it would
        // answer from an unread field with what reads exactly like "there are none". And it
        // does not drag the signatures along: verifying those measured 4620-7656 ms against
        // under a millisecond for this, so a run that did both would take seconds.
        var run = CommandLineTool.Run("list", "--query", "memory:>1MB", "--json", "--timing");

        Assert.Equal(0, run.ExitCode);
        Assert.DoesNotContain("signature", run.StandardError, StringComparison.OrdinalIgnoreCase);

        var matched = JsonDocument.Parse(run.StandardOutput).RootElement.EnumerateArray().ToArray();

        Assert.NotEmpty(matched);
        Assert.All(matched, entry =>
            Assert.NotEqual(JsonValueKind.Null, entry.GetProperty("memory").ValueKind));
    }

    [Fact]
    public void A_size_without_a_unit_is_refused_and_says_what_would_have_worked()
    {
        var run = CommandLineTool.Run("list", "--query", "memory:>500");

        // Exit code 2, the usage code, rather than an empty listing with code 0. An empty
        // listing is an answer, and here there is none.
        Assert.Equal(2, run.ExitCode);
        Assert.Empty(run.StandardOutput);
        Assert.Contains("500MB", run.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void The_column_appears_only_when_there_is_something_in_it()
    {
        var without = CommandLineTool.Run("list", "--query", "name:=Spooler");
        var with = CommandLineTool.Run("list", "--memory", "--query", "name:=Spooler");

        Assert.Equal(0, without.ExitCode);
        Assert.Equal(0, with.ExitCode);

        Assert.DoesNotContain("MEMORY", without.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("MEMORY", with.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_a_running_entry_uses_is_out_of_reach_of_an_administrator()
    {
        // The one assertion that catches the mistake this family invites: asking for more
        // access than the reading needs. GetProcessMemoryInfo is documented as wanting
        // PROCESS_QUERY_INFORMATION with PROCESS_VM_READ, and it accepts
        // PROCESS_QUERY_LIMITED_INFORMATION instead - which is what this tool asks for.
        // Measured on 2026-08-01 over the 110 processes behind services: the limited right
        // was refused by none and the wider pair by seven, all of them protected. Reaching
        // for the wider one costs those seven their answer and looks exactly like our bug.
        //
        // Every other test here samples entries that answered, so every one of them stays
        // green with the wrong mask in place - verified by putting it in. This one asks the
        // opposite question: did anything fail to answer.
        var refused = CommandLineTool.Listing("--memory", "--query", "status:running")
            .Where(entry => entry.TryGetProperty("unreadable", out var unreadable)
                && unreadable.TryGetProperty("memory", out _))
            .Select(entry => CommandLineTool.Text(entry, "serviceName"))
            .ToArray();

        if (!Elevated())
        {
            // Without elevation most service processes belong to accounts this session
            // cannot open at all, so a refusal here is the system behaving normally. The
            // strong claim is only available elevated, and saying so beats an assertion
            // that fires on a correct build.
            Assert.All(refused, name => Assert.NotEmpty(name));
            return;
        }

        Assert.True(
            refused.Length == 0,
            $"An elevated session could not read the memory of: {string.Join(", ", refused)}. " +
            "Protected processes give up this much to PROCESS_QUERY_LIMITED_INFORMATION, so " +
            "a refusal here means the reader is asking for a wider access right than it needs.");
    }

    /// <summary>
    /// Whether this session has administrator rights, asked through the built-in role and
    /// therefore through the well-known identifier.
    ///
    /// Never by the name of the group. That comparison reads "Administrators", and on a
    /// machine where the group is called something else - Administratorzy on the one this
    /// was written on - it answers no to an elevated session, which is exactly how a whole
    /// set of measurements in this project came to be recorded under the wrong heading.
    /// </summary>
    private static bool Elevated() =>
        CommandLineTool.PowerShell(
                "[Security.Principal.WindowsPrincipal]::new(" +
                "[Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(" +
                "[Security.Principal.WindowsBuiltInRole]::Administrator)")
            .Equals("True", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<JsonElement> Running() =>
        CommandLineTool.Listing("--memory", "--query", "status:running !type:driver")
            .Where(entry => entry.GetProperty("memory").ValueKind != JsonValueKind.Null);
}
