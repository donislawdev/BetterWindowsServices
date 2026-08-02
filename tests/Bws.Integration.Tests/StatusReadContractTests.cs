using System.Diagnostics;
using Bws.Core;

namespace Bws.Integration.Tests;

/// <summary>
/// The cheap reading, against the real manager.
///
/// It exists so a window can ask "what is running" once a second without paying for a full
/// reading, so the two things worth checking are that it says the same thing as the expensive
/// one and that it really is cheaper.
///
/// <b>What comparing it against the full reading cannot prove:</b> both start from the same
/// enumeration, so a mistake inside that agrees with itself. The independent authority is
/// <c>sc.exe</c>, which is why one of these asks it.
/// </summary>
public sealed class StatusReadContractTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void The_cheap_reading_covers_every_entry_the_full_one_does()
    {
        var catalog = new WindowsScmCatalog();

        var statuses = catalog.ReadStatuses();
        var everything = catalog.ReadAll();

        var cheap = statuses.Select(status => status.ServiceName).ToHashSet(StringComparer.Ordinal);
        var full = everything.Select(entry => entry.ServiceName).ToHashSet(StringComparer.Ordinal);

        // A live machine registers and removes entries, so the two readings are seconds apart
        // and a small difference is the machine rather than us.
        var disagreed = cheap.Except(full, StringComparer.Ordinal)
            .Union(full.Except(cheap, StringComparer.Ordinal), StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            disagreed.Length <= 5,
            $"The two readings disagreed about {disagreed.Length} names: {string.Join(", ", disagreed.Take(20))}");

        Assert.NotEmpty(cheap);
    }

    [Fact]
    public void Drivers_are_in_it_and_that_is_the_reason_it_is_a_reading_rather_than_a_subscription()
    {
        // Windows offers NotifyServiceStatusChange instead of asking, and its own documentation
        // says it cannot report on driver services. Measured on this machine at the time of
        // writing: drivers are more than half the entries, so a window built on notifications
        // alone would leave most of the list frozen. This check is what makes that argument a
        // fact about the machine under the test rather than a sentence in a document.
        var catalog = new WindowsScmCatalog();

        var drivers = catalog.ReadAll()
            .Where(entry => entry.EntryType is EntryType.KernelDriver or EntryType.FileSystemDriver)
            .Select(entry => entry.ServiceName)
            .ToHashSet(StringComparer.Ordinal);

        var cheap = catalog.ReadStatuses().Select(status => status.ServiceName).ToHashSet(StringComparer.Ordinal);

        Assert.True(drivers.Count > 0, "No drivers found, so this check proved nothing.");

        var missing = drivers.Except(cheap, StringComparer.Ordinal).ToArray();

        Assert.True(
            missing.Length <= 5,
            $"The cheap reading is missing {missing.Length} drivers: {string.Join(", ", missing.Take(20))}");

        output.WriteLine($"{drivers.Count} drivers of {cheap.Count} entries");
    }

    [Fact]
    public void What_it_calls_running_is_running_according_to_sc()
    {
        // The authority from outside our own code. Comparing the cheap reading against the
        // full one only proves the plumbing, because both walk the same enumeration.
        var running = new WindowsScmCatalog().ReadStatuses()
            .Where(status => status.Status == EntryStatus.Running)
            .Take(15)
            .ToArray();

        Assert.NotEmpty(running);

        foreach (var status in running)
        {
            var reported = CommandLineTool.ServiceControl("query", status.ServiceName).StandardOutput;

            // A service can stop between the two calls, so a name sc.exe no longer calls
            // running is only a failure if sc.exe also does not know it at all.
            if (!reported.Contains("SERVICE_NAME", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(
                reported.Contains("RUNNING", StringComparison.Ordinal)
                || reported.Contains("STOP_PENDING", StringComparison.Ordinal),
                $"We call {status.ServiceName} running and sc.exe says otherwise:{Environment.NewLine}{reported}");
        }
    }

    [Fact]
    public void A_process_identifier_is_absent_rather_than_zero_for_anything_not_running()
    {
        var statuses = new WindowsScmCatalog().ReadStatuses();

        var stopped = statuses.Where(status => status.Status == EntryStatus.Stopped).ToArray();

        Assert.NotEmpty(stopped);

        // Zero is a value and "not running" is not. Reporting process zero would be a number
        // somebody could paste into Task Manager.
        Assert.All(stopped, status => Assert.Equal(ReadOutcome.Absent, status.ProcessId.Outcome));

        var runningWithProcess = statuses.Count(status =>
            status.Status == EntryStatus.Running && status.ProcessId.Outcome == ReadOutcome.Present);

        Assert.True(runningWithProcess > 0, "Nothing running carried a process, so this proved nothing.");
    }

    [Fact]
    public void The_cheap_reading_is_the_reason_a_window_can_ask_often()
    {
        // Interleaved rather than run in blocks, because the two variants would otherwise get
        // different minutes of the machine's life - the rule docs/04 spells out.
        var catalog = new WindowsScmCatalog();

        // Cold pair, discarded.
        catalog.ReadStatuses();
        catalog.ReadAll();

        var cheap = new List<double>();
        var full = new List<double>();
        var entries = 0;

        for (var run = 0; run < 5; run++)
        {
            var before = Stopwatch.GetTimestamp();
            var statuses = catalog.ReadStatuses();
            cheap.Add(Stopwatch.GetElapsedTime(before).TotalMilliseconds);

            before = Stopwatch.GetTimestamp();
            entries = catalog.ReadAll().Count;
            full.Add(Stopwatch.GetElapsedTime(before).TotalMilliseconds);

            Assert.Equal(entries, statuses.Count);
        }

        output.WriteLine($"CHEAP {cheap.Min():F0}-{cheap.Max():F0} ms over {entries} entries");
        output.WriteLine($"FULL  {full.Min():F0}-{full.Max():F0} ms over {entries} entries");

        // The claim is that leaving out a handle per entry is worth something, and the spread
        // has to be narrower than the difference or there is no difference to speak of.
        Assert.True(
            cheap.Max() < full.Min(),
            $"The cheap reading measured {cheap.Min():F0}-{cheap.Max():F0} ms against " +
            $"{full.Min():F0}-{full.Max():F0} ms for the full one, so the two overlap and " +
            "there is nothing being saved.");
    }
}
