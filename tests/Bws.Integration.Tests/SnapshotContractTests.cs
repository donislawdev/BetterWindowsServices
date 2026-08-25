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

        // Two since 2026-08-25, when perUserRole joined the entry document. This number is
        // pinned here on purpose: it is the only thing that turns "your file is from an older
        // build" into a sentence its owner can act on, and a change to it that nobody argued
        // for is a change that silently stops older snapshots from being readable.
        Assert.Equal(2, metadata.GetProperty("schemaVersion").GetInt32());
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

    [Fact]
    public void An_existing_file_is_not_overwritten_without_being_told()
    {
        // The one place this tool writes a file, and until 2026-08-02 it replaced whatever was
        // at the path without a word and ended with code 0. Found while reading a security
        // review from elsewhere, which asks of any tool that it never overwrite somebody
        // else's file - and confirmed by doing exactly that to the file below.
        var target = Path.Combine(_directory, "already-here.json");
        const string mine = "this is not a snapshot and it is not yours";

        File.WriteAllText(target, mine);

        var refused = CommandLineTool.Run("snapshot", "create", target);

        Assert.Equal(2, refused.ExitCode);

        // The whole point. A refusal that still replaced the file would be worse than no
        // refusal at all, because the exit code would say the file survived.
        Assert.Equal(mine, File.ReadAllText(target));

        // And the sentence names the path, because "there is already a file" without saying
        // where is a message somebody has to go and work out for themselves.
        Assert.Contains(target, refused.StandardError, StringComparison.Ordinal);

        // The other half, without which this passes on a build that refuses always. A switch
        // that turns nothing on is the same silence from the opposite side.
        var forced = CommandLineTool.Run("snapshot", "create", target, "--force");

        Assert.Equal(0, forced.ExitCode);
        Assert.NotEqual(mine, File.ReadAllText(target));

        // AND WHAT WAS THERE IS STILL THERE, under another name. `ADR-18` asks for quarantine
        // rather than deletion on the grounds that a corrupt snapshot is sometimes the only
        // remaining trace of what was there - and until 2026-08-03 AtomicFile.Quarantine existed,
        // was tested, and had no caller in the product at all, which made the promise look kept
        // while nothing kept it.
        var kept = Directory
            .GetFiles(_directory)
            .Where(file => !string.Equals(file, target, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Single(kept);
        Assert.Equal(mine, File.ReadAllText(kept[0]));

        // Named on the error channel, because a file moved somewhere nobody was told about is
        // barely better than one that was deleted.
        Assert.Contains(Path.GetFileName(kept[0]), forced.StandardError, StringComparison.Ordinal);
    }

    // NOT marked as running anywhere, although what they are about - how bytes are decoded - has
    // nothing to do with this machine. Both take a real snapshot to get a real file, which reads
    // the manager and verifies every signature on the machine, and that is the reason the rest of
    // this class stays off a build agent. Smuggling a slow machine-dependent test into the job by
    // naming it after the part that is portable would be the same silence as any other.
    [Theory]
    [InlineData("utf8-bom")]
    [InlineData("utf16")]
    public void A_snapshot_saved_with_a_byte_order_mark_still_reads(string encoding)
    {
        // The half that must keep working, and it is here first because it decides the shape of
        // the refusal below: a mark is honoured, so a file something else saved as UTF-16 is
        // still a snapshot. Only bytes that are none of those come back as a refusal.
        var original = Path.Combine(_directory, "original.json");

        Assert.Equal(0, CommandLineTool.Run("snapshot", "create", original).ExitCode);

        var copy = Path.Combine(_directory, encoding + ".json");
        var text = File.ReadAllText(original);

        File.WriteAllText(copy, text, encoding == "utf16"
            ? System.Text.Encoding.Unicode
            : new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var compared = CommandLineTool.Run("snapshot", "diff", original, copy);

        Assert.Equal(0, compared.ExitCode);

        // NOTHING DIFFERS, WHICH IS THE CLAIM - and it used to be asserted as the literal words
        // "No differences", which stopped being what this prints on 2026-08-12. The description
        // arrived, and eight entries of 809 on this machine carry one the manager will not resolve
        // into words - so a file compared against a byte-identical copy of itself correctly reports
        // that eight entries hold a field nobody could put a value to. Both summaries say "nothing
        // differs" and that is the half this test is about: the subject here is the ENCODING, and a
        // mark honoured means the copy reads back as the same machine.
        Assert.Contains("nothing differs", compared.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Changed", compared.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void A_snapshot_that_is_not_utf8_is_refused_rather_than_guessed_at()
    {
        // MEASURED before this held: the same snapshot re-encoded to Windows-1250 and compared
        // against itself reported CHANGED (297) - two hundred and ninety seven entries said to
        // have changed, and not one of them had. File.ReadAllText honours a byte order mark and
        // otherwise assumes UTF-8, and where the bytes are not UTF-8 it substitutes a replacement
        // character and carries on without a word.
        //
        // It takes one person opening a snapshot in an editor set to the machine's code page and
        // saving it. With --exit-code that is a pipeline failing over drift that does not exist,
        // which in a tool built to tell real drift from noise is the worst answer available.
        // The note carries a character that exists in both UTF-8 and a single-byte code page, so
        // the file below really is the same text in another encoding rather than a corrupted one.
        // Latin-1 rather than Windows-1250, which is what was measured: .NET does not carry the
        // legacy code pages without an extra package, and taking a dependency to reproduce a
        // failure that any single-byte encoding produces would be paying for nothing. What
        // matters is a byte above 0x7F standing alone, which is not valid UTF-8 in any of them.
        var original = Path.Combine(_directory, "utf8.json");

        Assert.Equal(0, CommandLineTool.Run("snapshot", "create", original, "--note", "café").ExitCode);

        var recoded = Path.Combine(_directory, "single-byte.json");

        File.WriteAllBytes(recoded, System.Text.Encoding.Latin1.GetBytes(File.ReadAllText(original)));

        // The two files say the same thing, and one of them cannot be read as UTF-8.
        Assert.NotEqual(File.ReadAllBytes(original).Length, File.ReadAllBytes(recoded).Length);

        var compared = CommandLineTool.Run("snapshot", "diff", original, recoded);

        Assert.Equal(2, compared.ExitCode);
        Assert.Contains(recoded, compared.StandardError, StringComparison.Ordinal);
        Assert.Equal(string.Empty, compared.StandardOutput.Trim());

        // AND IT SAYS THE ENCODING IS THE REASON, WHICH IS THE HALF THIS GUARD WAS MISSING UNTIL
        // 2026-08-12. The three assertions above are satisfied by ANY refusal, and on 2026-08-12
        // the mutation entry that flips throwOnInvalidBytes came back MISSED for exactly that: with
        // the encoding check off, the file is refused anyway, because the description field arrived
        // and a single-byte re-encoding of it now produces JSON the parser rejects outright -
        // "'n' is invalid after a value" at entries[461]. Two different refusals, three assertions
        // that cannot tell them apart, and a guard that would have gone on passing after somebody
        // removed the thing it is named for.
        //
        // The sentence is read from the product's own language file rather than written here, which
        // is the same rule tools/window-journey follows: a copy of a sentence drifts from the
        // sentence, and then the guard is about the copy.
        Assert.Contains(
            Sentences.Of("cli.diff.notUtf8", "").Trim(),
            compared.StandardError,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_readable_snapshot_is_replaced_rather_than_kept()
    {
        // The other side of the line above, and it decides what quarantine means. Somebody who
        // asks for --force over a snapshot they took an hour ago means replace it - keeping a
        // copy of every one of those would fill their directory with files they never asked for.
        // Only what cannot be read back is worth keeping, because only that cannot be produced
        // again.
        var target = Path.Combine(_directory, "replaceable.json");

        Assert.Equal(0, CommandLineTool.Run("snapshot", "create", target).ExitCode);
        Assert.Equal(0, CommandLineTool.Run("snapshot", "create", target, "--force").ExitCode);

        Assert.Single(Directory.GetFiles(_directory));
    }

    // THERE IS NO TEST HERE FOR THE NAME THE TOOL WORKS OUT ITSELF, and the absence is written
    // down rather than left to be noticed.
    //
    // That path had no overwrite guard at all until 2026-08-03, on the reasoning that a name
    // carrying a timestamp to the second "collides with nothing" - true of one person running
    // the command twice, false of two runs started together by a script, of a file restored from
    // a backup, and of a fast machine. It has one now.
    //
    // A test was written for it and REMOVED THE SAME HOUR, because the mutation runner said it
    // proved nothing: it passed a file name explicitly, so the early check refused first and the
    // gate it claimed to guard was never reached. Reaching that gate needs the tool to choose a
    // name that already exists, which needs two runs inside one second - and the command line has
    // no way to hand a clock in. A test whose name claims more than it checks is worse than no
    // test, because the next session reads the name.

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
