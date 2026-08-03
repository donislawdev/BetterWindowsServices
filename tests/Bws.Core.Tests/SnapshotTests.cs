using System.Text.Json;
using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Freezing a machine into a file.
///
/// Almost everything here is about determinism rather than about content, and that is not
/// fussiness: `ADR-6` promises a file that diffs cleanly in git, and every way of breaking
/// that promise is invisible in a single file. Key order, entry order, trailing newline,
/// escaping - none of them changes what the snapshot says, and every one of them makes two
/// snapshots of an unchanged machine differ on hundreds of lines.
/// </summary>
public sealed class SnapshotTests
{
    [Fact]
    public void Two_snapshots_of_the_same_entries_are_the_same_text()
    {
        // The promise, as one assertion. Anything non-deterministic in the writing - a
        // dictionary enumerated in hash order, a set, a timestamp read twice - shows up here
        // and nowhere else.
        var first = Render(Specimens.All);
        var second = Render(Specimens.All);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Keys_are_written_in_order_all_the_way_down()
    {
        var entry = FirstEntryObject(Render(Specimens.All));
        var keys = entry.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(keys.OrderBy(key => key, StringComparer.Ordinal), keys);

        // And inside a nested object too, which is where an ordering done at only the top
        // level would look correct and not be. Over the inspected catalogue, because the
        // ordinary one has nothing nested to look at - every signature in it is unread.
        var signature = EntryNamed(Render(Specimens.Inspected), "Spooler").GetProperty("signature");
        var nested = signature.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(nested.OrderBy(key => key, StringComparer.Ordinal), nested);
    }

    [Fact]
    public void Entries_are_written_in_name_order_regardless_of_what_order_they_arrived_in()
    {
        // The manager's order is not in any contract and has been seen to move. Without a
        // sort here, two snapshots of an unchanged machine could differ on every line.
        var forwards = Render(Specimens.All);
        var backwards = Render([.. Specimens.All.Reverse()]);

        Assert.Equal(forwards, backwards);
    }

    [Fact]
    public void Two_names_differing_only_in_case_keep_a_settled_order()
    {
        // Names are compared without case, so sorting by that alone leaves two entries that
        // differ only in case free to swap places between runs - and a swap reads as a
        // change. The catalogue carries Twin and TWIN for exactly this.
        var names = Names(Render(Specimens.All));
        var twins = names.Where(name => name.Equals("twin", StringComparison.OrdinalIgnoreCase)).ToArray();

        Assert.Equal(2, twins.Length);
        Assert.Equal(Names(Render([.. Specimens.All.Reverse()])), names);
    }

    [Fact]
    public void The_measurement_is_not_in_the_file()
    {
        // Memory is different a second later, so in a document whose purpose is being
        // compared against another one it is noise on every line that has it. D1 lists what
        // a snapshot holds and does not name it.
        var measured = MemoryPass.Fill(Specimens.All, new FakeProcessMemoryReader());
        var text = Render(measured);

        Assert.DoesNotContain("\"memory\"", text, StringComparison.Ordinal);

        // And not as an admission either. Left in "notRead" it would appear on every entry
        // of every snapshot, confessing to a gap in a document that never claimed the field.
        Assert.DoesNotContain("\"memory\"", Render(Specimens.All), StringComparison.Ordinal);
    }

    [Fact]
    public void A_refusal_is_written_as_a_refusal_and_not_as_an_absence()
    {
        // The reason snapshots have four states at all. An entry whose configuration was
        // refused must not read like an entry that has none, or the next comparison against
        // an elevated snapshot reports changes that never happened.
        var entry = EntryNamed(Render(Specimens.All), "Locked");

        Assert.Equal(JsonValueKind.Null, entry.GetProperty("startType").ValueKind);
        Assert.True(entry.TryGetProperty("unreadable", out var unreadable));
        Assert.True(unreadable.TryGetProperty("startType", out var reason));
        Assert.Equal(Entries.AccessDenied, reason.GetProperty("errorCode").GetInt32());
    }

    [Fact]
    public void A_service_that_vanished_is_told_apart_from_one_that_was_refused()
    {
        // Both arrive as the same read state, and only the number distinguishes them. A
        // service can be enumerated and be gone by the time its configuration is asked for -
        // the manager answers 1060 there and 5 for a genuine refusal.
        //
        // The state cannot tell them apart and deliberately does not try: adding a fifth
        // would make every consumer, the schema and the comparison learn a case that the
        // number already describes. What this holds is that the number is not flattened away
        // on the road to the file, because that is the only thing keeping the two apart.
        const int ServiceDoesNotExist = 1060;

        var vanished = Entries.Any with
        {
            ServiceName = "Vanished",
            StartType = Reading<StartType>.Denied(ServiceDoesNotExist, "The specified service does not exist.")
        };

        var entry = EntryNamed(Render([vanished]), "Vanished");

        Assert.Equal(
            ServiceDoesNotExist,
            entry.GetProperty("unreadable").GetProperty("startType").GetProperty("errorCode").GetInt32());

        Assert.NotEqual(
            Entries.AccessDenied,
            entry.GetProperty("unreadable").GetProperty("startType").GetProperty("errorCode").GetInt32());
    }

    [Fact]
    public void The_file_says_when_it_was_taken_and_whether_the_session_could_see_everything()
    {
        var metadata = Metadata(Render(Specimens.All));

        Assert.Equal(Snapshot.CurrentSchemaVersion, metadata.GetProperty("schemaVersion").GetInt32());
        Assert.NotEmpty(metadata.GetProperty("machine").GetString()!);
        Assert.NotEmpty(metadata.GetProperty("takenBy").GetString()!);
        Assert.NotEmpty(metadata.GetProperty("tool").GetString()!);

        // The field the whole metadata block exists for. Measured: an unelevated session is
        // handed 807 entries where an elevated one sees 810, so without this a diff of two
        // files taken differently reports three deletions nobody performed.
        Assert.True(metadata.TryGetProperty("elevated", out _));
    }

    [Fact]
    public void The_time_is_written_to_the_second()
    {
        // Not decoration. The rounding was written once in a way that read correctly and did
        // nothing - it took the ticks from one reading of the clock and the remainder from
        // another - and the only thing that showed it was comparing two files.
        var takenAt = Metadata(Render(Specimens.All)).GetProperty("takenAt").GetDateTimeOffset();

        Assert.Equal(0, takenAt.Ticks % TimeSpan.TicksPerSecond);
    }

    [Fact]
    public void A_note_is_kept_and_an_empty_one_is_not_invented()
    {
        Assert.Equal("before the deployment", Note("before the deployment"));

        // Whitespace is somebody typing --note "" and is not a note. Written as nothing
        // rather than as an empty string, so a reader does not have to decide which of the
        // two a blank means.
        Assert.Null(Note("   "));
        Assert.Null(Note(null));
    }

    [Fact]
    public void A_snapshot_reads_back_as_what_was_written()
    {
        // Everything in the catalogue except one half of the deliberate twin pair, and the
        // exception is the point rather than a convenience.
        //
        // Twin and TWIN are in the catalogue to pin the sort tie-break in the test above, which
        // is a fact about WRITING. Since 2026-08-03 the reader refuses a file holding both,
        // because Windows cannot produce two services whose names differ only in case and the
        // comparison engine matches its two sides without case - owner's decision. So the whole
        // catalogue is a legal thing to write and not a legal thing to read back, and saying that
        // here is better than a round trip quietly running over a smaller set than it claims.
        var machineLike = Specimens.All
            .Where(entry => !entry.ServiceName.Equals("TWIN", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(Specimens.All.Count - 1, machineLike.Length);

        var text = Render(machineLike);

        var reread = SnapshotJson.TryRead(text, out var read, out var failure);

        Assert.True(reread, failure);
        Assert.Equal(machineLike.Length, read!.Entries.Count);

        // And writing it again produces the same text. A round trip that loses a field would
        // otherwise be found by whoever compared an old snapshot with a new one, months from
        // now, as a change in a machine that did not change.
        Assert.Equal(text, SnapshotJson.Render(read));
    }

    [Fact]
    public void A_schema_this_build_does_not_know_is_refused_rather_than_guessed_at()
    {
        var text = Render(Specimens.All)
            .Replace($"\"schemaVersion\": {Snapshot.CurrentSchemaVersion}", "\"schemaVersion\": 99", StringComparison.Ordinal);

        Assert.False(SnapshotJson.TryRead(text, out var read, out var failure));
        Assert.Null(read);
        Assert.Contains("99", failure!, StringComparison.Ordinal);
    }

    [Fact]
    public void Something_that_is_not_a_snapshot_is_reported_and_not_thrown()
    {
        // Pointing at the wrong file is an ordinary thing for a person to do, and an
        // exception would make it look like the tool falling over.
        Assert.False(SnapshotJson.TryRead("{ this is not json", out _, out var failure));
        Assert.NotNull(failure);
    }

    [Fact]
    public void An_entry_that_is_empty_is_refused_rather_than_thrown_over()
    {
        // MEASURED 2026-08-03 on the real tool before this check existed: a file shaped exactly
        // like this ended `bws snapshot diff` with "Object reference not set to an instance of
        // an object." and code 1 - the code for the tool falling over, on a file that is simply
        // broken, with a message naming neither of the two files being compared.
        //
        // Structurally valid JSON, which is why the property test beside this never reached it:
        // that one damages a real snapshot by cutting, deleting, flipping and inserting
        // characters, and every one of those makes the document unreadable rather than wrong.
        var text = Render(Specimens.All)
            .Replace("\"entries\": [", "\"entries\": [\n    null,", StringComparison.Ordinal);

        Assert.False(SnapshotJson.TryRead(text, out var read, out var failure));
        Assert.Null(read);

        // Names the position, because there is no name to give - which is the whole of what is
        // wrong with it.
        Assert.Contains("1", failure!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_holding_one_service_twice_is_refused_rather_than_thrown_over()
    {
        // The comparison matches the two sides by service name, so a file naming one service
        // twice is one this build cannot compare. Before this check it did not say so: it ended
        // with "An item with the same key has already been added. Key: ..." and code 1.
        //
        // Written by handing the same entry over twice rather than by editing text, so what is
        // being read back is a document this project's own writer produced.
        var text = SnapshotJson.Render(
            Snapshot.Of([Specimens.All[0], Specimens.All[0]], note: null, new FakeClock()));

        Assert.False(SnapshotJson.TryRead(text, out var read, out var failure));
        Assert.Null(read);
        Assert.Contains(Specimens.All[0].ServiceName, failure!, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_names_differing_only_in_case_are_one_service_to_this_reader()
    {
        // Windows compares service names without case and so does the comparison engine, so
        // accepting this file would produce a document whose two halves cannot be matched. The
        // check has to fold case for the same reason the matching does.
        var second = Specimens.All[0] with { ServiceName = Specimens.All[0].ServiceName.ToUpperInvariant() };

        var text = SnapshotJson.Render(
            Snapshot.Of([Specimens.All[0], second], note: null, new FakeClock()));

        Assert.False(SnapshotJson.TryRead(text, out _, out var failure));
        Assert.NotNull(failure);
    }

    [Fact]
    public void The_file_ends_with_a_newline()
    {
        // Without one, the last line shows as changed the first time anything is appended,
        // and every tool that reads text files expects it.
        Assert.EndsWith("\n", Render(Specimens.All), StringComparison.Ordinal);
    }

    private static string Render(IReadOnlyList<ScmEntry> entries) =>
        SnapshotJson.Render(Snapshot.Of(entries, note: null, new FakeClock()));

    private static string? Note(string? note) =>
        Metadata(SnapshotJson.Render(Snapshot.Of(Specimens.All, note, new FakeClock())))
            .GetProperty("note") is { ValueKind: JsonValueKind.Null } ? null
            : Metadata(SnapshotJson.Render(Snapshot.Of(Specimens.All, note, new FakeClock())))
                .GetProperty("note").GetString();

    private static JsonElement Metadata(string text) =>
        JsonDocument.Parse(text).RootElement.GetProperty("metadata");

    private static JsonElement FirstEntryObject(string text) =>
        JsonDocument.Parse(text).RootElement.GetProperty("entries")[0];

    private static JsonElement EntryNamed(string text, string name) =>
        JsonDocument.Parse(text).RootElement.GetProperty("entries")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("serviceName").GetString() == name);

    private static string[] Names(string text) =>
    [
        .. JsonDocument.Parse(text).RootElement.GetProperty("entries")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("serviceName").GetString()!)
    ];
}
