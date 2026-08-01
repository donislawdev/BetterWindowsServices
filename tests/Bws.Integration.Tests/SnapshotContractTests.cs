using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// Freezing this machine into a file, checked against what the same tool reports live.
///
/// There is no system tool to compare a snapshot against - nothing else on Windows writes
/// one - so the authority here is our own listing, which is itself checked against sc.exe
/// everywhere else in this suite. What these tests hold to account is the part a listing
/// cannot show: that the file is complete, that it is deterministic, and that two of them
/// taken minutes apart differ only where the machine did.
///
/// Read-only as far as the machine goes. Files are written into a directory of the test's
/// own making and removed afterwards.
/// </summary>
public sealed class SnapshotContractTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("bws-snapshot-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void A_snapshot_holds_every_entry_the_listing_reports()
    {
        var snapshot = Take("all.json");
        var listed = CommandLineTool.Listing().Length;

        // Not equality against a number written here. The machine is alive - per-user
        // services come and go - so the two are taken moments apart and compared against
        // each other, which is the same shape the S1 tests use for the same reason.
        var held = snapshot.GetProperty("entries").GetArrayLength();

        Assert.InRange(held, listed - 5, listed + 5);
        Assert.True(held > 500, $"A snapshot of {held} entries is not a snapshot of this machine.");
    }

    [Fact]
    public void Every_entry_carries_its_signature_and_its_hash()
    {
        // The promise that makes two snapshots comparable. A listing leaves these unread
        // because verifying them costs seconds, and a snapshot cannot: one without them
        // compared against one with them would report the whole machine as changed.
        var entries = Take("signed.json").GetProperty("entries").EnumerateArray().ToArray();

        Assert.All(entries, entry =>
        {
            var unread = entry.TryGetProperty("notRead", out var notRead)
                ? notRead.EnumerateArray().Select(name => name.GetString()).ToArray()
                : [];

            Assert.DoesNotContain("signature", unread);
            Assert.DoesNotContain("binaryHash", unread);
        });

        // And most of them actually have one, rather than every line saying the file was
        // missing - which would satisfy the assertion above while meaning nothing.
        var hashed = entries.Count(entry => entry.GetProperty("binaryHash").ValueKind != JsonValueKind.Null);

        Assert.True(hashed > 500, $"Only {hashed} entries came back with a hash.");
    }

    [Fact]
    public void The_measurement_is_not_in_the_file()
    {
        // Memory is different a second later, so a document meant to be compared with
        // another one has no business holding it. D1 lists what a snapshot keeps and does
        // not name it.
        var text = File.ReadAllText(TakeToPath("nomemory.json"));

        Assert.DoesNotContain("\"memory\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_snapshots_of_this_machine_differ_only_where_the_machine_did()
    {
        // The whole of ADR-6 in one test: a file that diffs cleanly. Anything
        // non-deterministic in the writing shows up here as hundreds of changed lines on a
        // machine where nothing happened.
        //
        // Not zero differences, because this is a live machine and services legitimately
        // start and stop between two runs seconds apart - measured on this one, entry counts
        // move by one or two within minutes. What is checked is the shape: a handful of
        // lines, not a rewrite.
        var first = File.ReadAllLines(TakeToPath("first.json"));
        var second = File.ReadAllLines(TakeToPath("second.json"));

        var changed = first.Except(second, StringComparer.Ordinal).Count()
            + second.Except(first, StringComparer.Ordinal).Count();

        Assert.True(
            changed < 40,
            $"{changed} lines differ between two snapshots taken seconds apart. A handful is " +
            "the machine moving. This many means something in the writing is not deterministic.");
    }

    [Fact]
    public void The_file_says_what_it_needs_to_be_compared_later()
    {
        var metadata = Take("meta.json", "--note", "before the deployment").GetProperty("metadata");

        Assert.Equal(1, metadata.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("before the deployment", metadata.GetProperty("note").GetString());
        Assert.Equal(Environment.MachineName, metadata.GetProperty("machine").GetString());
        Assert.NotEmpty(metadata.GetProperty("takenBy").GetString()!);
        Assert.NotEmpty(metadata.GetProperty("operatingSystem").GetString()!);
        Assert.NotEmpty(metadata.GetProperty("tool").GetString()!);

        // The one that decides what a later comparison means. Measured: an unelevated
        // session is handed 807 entries where an elevated one sees 810.
        Assert.True(metadata.TryGetProperty("elevated", out _));
    }

    [Fact]
    public void Display_names_are_written_as_themselves()
    {
        // On a machine whose language has characters outside ASCII, the default JSON encoder
        // turns every display name into a row of escapes. Valid, and unreadable in exactly
        // the diff this format exists to be readable in.
        //
        // Asked as a property of the values rather than as a pattern in the text, and the
        // first attempt shows why: it searched for the escape prefix and found it inside
        // genuine Windows paths, because an AMD driver store folder is really called
        // "u0202642.inf_amd64_..." and sits behind an escaped backslash.
        var path = TakeToPath("names.json");
        var text = File.ReadAllText(path);

        var translated = JsonDocument.Parse(text).RootElement.GetProperty("entries")
            .EnumerateArray()
            .Select(entry => CommandLineTool.Text(entry, "displayName"))
            .Where(name => name.Any(character => character > 127))
            .ToArray();

        if (translated.Length == 0)
        {
            // An English install has nothing here to check, and saying so beats an assertion
            // that quietly passes on a machine it was never able to test.
            return;
        }

        Assert.All(translated.Take(10), name => Assert.Contains(name, text, StringComparison.Ordinal));
    }

    [Fact]
    public void A_directory_that_does_not_exist_is_refused_and_nothing_is_created()
    {
        var target = Path.Combine(_directory, "nowhere", "snapshot.json");
        var run = CommandLineTool.Run("snapshot", "create", target);

        // The usage code, because it is about what somebody typed. And nothing on the data
        // channel: a failed run must not leave a stray line in whatever it was piped into.
        Assert.Equal(2, run.ExitCode);
        Assert.Empty(run.StandardOutput);
        Assert.False(Directory.Exists(Path.Combine(_directory, "nowhere")));
    }

    [Fact]
    public void Nothing_temporary_is_left_beside_the_snapshot()
    {
        TakeToPath("tidy.json");

        Assert.Single(Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void A_verb_under_snapshot_that_does_not_exist_yet_says_so_as_a_pair()
    {
        // E1 promises diff and restore under the same noun and neither is built. The answer
        // has to name what was typed rather than one word of it: "there is no such thing as
        // snapshot" would be untrue, and naming only "diff" sends somebody looking in the
        // wrong place.
        var run = CommandLineTool.Run("snapshot", "diff", "yesterday.json");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("snapshot diff", run.StandardError, StringComparison.Ordinal);
    }

    private JsonElement Take(string name, params string[] arguments) =>
        JsonDocument.Parse(File.ReadAllText(TakeToPath(name, arguments))).RootElement;

    private string TakeToPath(string name, params string[] arguments)
    {
        var target = Path.Combine(_directory, name);
        var run = CommandLineTool.Run(["snapshot", "create", target, .. arguments]);

        Assert.Equal(0, run.ExitCode);
        Assert.True(File.Exists(target), $"Nothing was written to {target}. {run.StandardError}");

        return target;
    }
}
