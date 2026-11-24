using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;

namespace Bws.Integration.Tests;

/// <summary>
/// The recovery the listing reads - every half the Recovery tab of services.msc shows - held against
/// sc.exe qfailure and qfailureflag, entry by entry.
///
/// <b>Beside RecoveryContractTests, not instead of it.</b> That class holds the reader the plan that ends a
/// process uses against RpcSs. The listing reads three more things on top of that reader - the program and
/// the message, bounded inside the same block, and the flag, asked with a call of its own - and nothing else
/// compares any of them with an answer from outside the product.
///
/// <b>Chosen from two sources, judged by a third.</b> An entry is compared when our reading or the registry
/// says it carries a program, a message or the flag, plus six more with any recovery at all. Chosen from our
/// answer alone, a reader that lost the flag would choose no entry with the flag and compare nothing about it.
/// Chosen from the registry alone, a per-user instance would be missed: it carries its template's recovery
/// with no value under its own key (measured 2026-11-24, WpnUserService_12dc17 answers TRUE for the flag).
/// sc.exe judges every value - the registry only chooses.
///
/// <b>Every Windows looked at has entries of both rare kinds</b> (2026-11-24): the Windows 11 machine this was
/// written on, 10 with the flag and 2 with a program or a message, and the throwaway Windows Server 2025, 8
/// and 3 - MSiSCSI carries all three on both. So a run that compared none of them is a reading asleep, not a
/// quiet machine, and it fails rather than skips: an entry is chosen from the registry as well as from our
/// reading, so a host where both find nothing is one this class was never meant to run on.
///
/// Read-only.
/// </summary>
public sealed class RecoveryListingContractTests
{
    private const string NotCompared = "(not compared)";

    private const string NotAscii = "(not ASCII)";

    [Fact]
    public void Recovery_in_the_listing_reads_as_sc_exe_prints_it()
    {
        var listed = CommandLineTool.Listing()
            .ToDictionary(entry => CommandLineTool.Text(entry, "serviceName"), StringComparer.OrdinalIgnoreCase);

        var compared = Chosen(listed).Select(name => Ours(listed[name])).Select(ours => (Ours: ours, Theirs: Theirs(ours))).ToArray();

        foreach (var (ours, theirs) in compared)
        {
            Assert.Equal(theirs, ours);
        }

        // The three halves nothing else compares, each seen at least once - the summary says why that holds.
        // A text sc.exe printed in another code page was not compared, so it does not count as seen.
        Assert.Contains(compared, pair => pair.Theirs.Flag == "TRUE");
        Assert.Contains(compared, pair => Compared(pair.Theirs.Command));
        Assert.Contains(compared, pair => Compared(pair.Theirs.Message));
    }

    private static bool Compared(string text) => text.Length > 0 && text != NotAscii;

    [Fact]
    public void An_entry_we_say_has_no_recovery_has_none_according_to_sc()
    {
        // The other direction, and the one that would hide a reading that answered "nothing set" for everybody.
        var sampled = 0;

        foreach (var entry in CommandLineTool.Listing("--query", "recovery:none !type:driver").Take(6))
        {
            var ours = Ours(entry);

            Assert.Equal(Theirs(ours), ours);
            sampled++;
        }

        Assert.Equal(6, sampled);
    }

    private sealed record Told(string Name, string Reset, string Actions, string Command, string Message, string Flag);

    private static string[] Chosen(Dictionary<string, JsonElement> listed)
    {
        var withRecovery = listed.Where(pair => pair.Value.GetProperty("recovery").ValueKind == JsonValueKind.Object).ToArray();

        return
        [
            .. withRecovery.Where(pair => HasARareHalf(pair.Value.GetProperty("recovery"))).Select(pair => pair.Key)
                .Concat(FromTheRegistry().Where(listed.ContainsKey))
                .Concat(withRecovery.Take(6).Select(pair => pair.Key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static bool HasARareHalf(JsonElement recovery) =>
        recovery.GetProperty("onNonCrashFailures").GetBoolean()
        || recovery.GetProperty("command").ValueKind == JsonValueKind.String
        || recovery.GetProperty("rebootMessage").ValueKind == JsonValueKind.String;

    /// <summary>
    /// The keys whose values say a program, a message or the flag is set. A key the manager does not know -
    /// four leftover clr_optimization keys on the machine this was written on - is dropped by the caller,
    /// which keeps only names the listing has.
    /// </summary>
    private static IEnumerable<string> FromTheRegistry()
    {
        using var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");

        Assert.NotNull(services);

        foreach (var name in services.GetSubKeyNames())
        {
            using var key = services.OpenSubKey(name);

            if (key is not null
                && (key.GetValue("FailureCommand") is string { Length: > 0 }
                    || key.GetValue("RebootMessage") is string { Length: > 0 }
                    || key.GetValue("FailureActionsOnNonCrashFailures") is int and not 0))
            {
                yield return name;
            }
        }
    }

    /// <summary>
    /// What we say, in sc.exe's words. An entry with nothing set leaves the period and the flag uncompared,
    /// because the product does not read them then - without an item they decide nothing.
    /// </summary>
    private static Told Ours(JsonElement entry)
    {
        var name = CommandLineTool.Text(entry, "serviceName");
        var recovery = entry.GetProperty("recovery");

        if (recovery.ValueKind != JsonValueKind.Object)
        {
            return new Told(name, NotCompared, string.Empty, string.Empty, string.Empty, NotCompared);
        }

        var reset = recovery.GetProperty("resetPeriodSeconds").GetInt64();

        return new Told(
            name,
            reset == uint.MaxValue ? "INFINITE" : reset.ToString(CultureInfo.InvariantCulture),
            string.Join(", ", recovery.GetProperty("actions").EnumerateArray().Select(Spoken).Where(item => item.Length > 0)),
            Printable(recovery.GetProperty("command").GetString()),
            Printable(recovery.GetProperty("rebootMessage").GetString()),
            recovery.GetProperty("onNonCrashFailures").GetBoolean() ? "TRUE" : "FALSE");
    }

    /// <summary>
    /// One item as sc.exe prints it, or nothing for the two kinds it prints no line for - an item that does
    /// nothing, and a kind Microsoft does not document (Schedule's first item).
    /// </summary>
    private static string Spoken(JsonElement item)
    {
        var kind = item.GetProperty("action").GetString() switch
        {
            "RestartService" => "RESTART",
            "RunProgram" => "RUN PROCESS",
            "RestartComputer" => "REBOOT",
            _ => string.Empty
        };

        return kind.Length == 0
            ? string.Empty
            : kind + " " + item.GetProperty("delayMilliseconds").GetInt64().ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A text outside ASCII is set aside on both sides: sc.exe writes through the console code page, so a
    /// difference there would be about the pipe, not about the reading.
    /// </summary>
    private static string Printable(string? text) =>
        text is null ? string.Empty : text.All(char.IsAscii) ? text : NotAscii;

    /// <summary>
    /// What sc.exe says, with what our side set aside set aside here too.
    ///
    /// <b>By position, not by label</b>, as everywhere in these tests: the three lines after the name are the
    /// period, the message and the command, in that order, and a label is what a translated sc.exe would
    /// change. An action is a line carrying "-- Delay", read by RecoveryContractTests' own helpers.
    /// </summary>
    private static Told Theirs(Told ours)
    {
        var lines = Lines("qfailure", ours.Name, "5000");
        var head = Array.FindIndex(lines, line => line.StartsWith("SERVICE_NAME:", StringComparison.Ordinal));

        Assert.True(head >= 0 && lines.Length > head + 3, $"sc.exe qfailure {ours.Name} printed nothing to compare with.");

        return new Told(
            ours.Name,
            ours.Reset == NotCompared ? NotCompared : Value(lines[head + 1]),
            string.Join(", ", lines
                .Where(line => line.Contains("-- Delay", StringComparison.Ordinal))
                .Select(line => RecoveryContractTests.Kind(line) + " " + RecoveryContractTests.Delay(line))),
            ours.Command == NotAscii ? NotAscii : Value(lines[head + 3]),
            ours.Message == NotAscii ? NotAscii : Value(lines[head + 2]),
            ours.Flag == NotCompared ? NotCompared : Flag(ours.Name));
    }

    /// <summary>
    /// The last word of the line after the name - TRUE or FALSE, which sc.exe does not translate.
    /// </summary>
    private static string Flag(string name)
    {
        var lines = Lines("qfailureflag", name);
        var head = Array.FindIndex(lines, line => line.StartsWith("SERVICE_NAME:", StringComparison.Ordinal));

        Assert.True(head >= 0 && lines.Length > head + 1, $"sc.exe qfailureflag {name} printed nothing to compare with.");

        return lines[head + 1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
    }

    private static string[] Lines(params string[] arguments) =>
        CommandLineTool.ServiceControl(arguments).StandardOutput.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');

    /// <summary>
    /// Everything after the first colon and the one space sc.exe puts after it. An empty value is printed as
    /// the colon and that space, which this reads as empty - the product keeps an empty text as none.
    /// </summary>
    private static string Value(string line)
    {
        var value = line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..];

        return value.StartsWith(' ') ? value[1..] : value;
    }
}
