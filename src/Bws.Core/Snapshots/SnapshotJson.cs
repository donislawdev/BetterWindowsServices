using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Bws.Core.Snapshots;

/// <summary>
/// Turns a snapshot into the text that goes in the file, and back.
///
/// `ADR-6` in code: JSON, keys in a deterministic order, one field per line. Every one of
/// those three is about <c>git diff</c> rather than about JSON. Unordered keys make two
/// files of an unchanged machine differ on nothing; a single line makes every change look
/// like the whole file changed.
///
/// The ordering is done on the tree rather than by declaring properties in the right order,
/// which is the obvious alternative and the fragile one: it would hold until somebody moved
/// a property while tidying, and nothing would say so - the file would simply diff dirtily
/// against every snapshot taken before.
/// </summary>
public static class SnapshotJson
{
    /// <summary>
    /// Never written into a snapshot, whatever the entry happens to be carrying.
    ///
    /// Memory is a measurement rather than a description of how the machine is set up. It is
    /// different a second later, so in a document whose entire purpose is being compared
    /// with another one it would be noise on every line that had it. `D1` lists what a
    /// snapshot holds and does not name it.
    /// </summary>
    private const string Measurement = "memory";

    private static readonly JsonSerializerOptions Shape = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static readonly JsonSerializerOptions Layout = new()
    {
        WriteIndented = true,

        // Characters as themselves, not as escape sequences.
        //
        // The default encoder escapes anything outside ASCII, which on this machine turns
        // every Polish display name into a row of ł and the timestamp's own plus sign
        // into +. That is valid JSON and unreadable prose, and `ADR-6` asks for a file
        // a person can read in a diff - which is the entire reason the format is JSON and
        // not something binary.
        //
        // "Unsafe" in the name means unsafe to drop into HTML without escaping. This goes
        // into a file, and the tool has no HTML anywhere near it.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Render(Snapshot snapshot)
    {
        var tree = JsonSerializer.SerializeToNode(snapshot, Shape)!.AsObject();

        foreach (var entry in tree["entries"]!.AsArray())
        {
            Drop(entry!.AsObject(), Measurement);
        }

        // A newline at the end, because a file without one makes the last line show up as
        // changed the first time anybody appends to it, and because every tool that reads
        // text files expects it.
        return Sorted(tree).ToJsonString(Layout) + "\n";
    }

    /// <summary>
    /// Reads a snapshot back, or says what is wrong with the file.
    ///
    /// Returns rather than throws, because "this file is not a snapshot" is an ordinary
    /// thing for a person to run into - they pointed at the wrong file - and an exception
    /// would turn it into something that looks like the tool falling over.
    /// </summary>
    /// <summary>
    /// What a parser failure says to a person, which is not what it says to a developer.
    ///
    /// <b>RELEASE-010 of the pre-release audit, 2026-09-09.</b> The reader's own message went
    /// straight to the operator, so pointing at an empty file answered with
    /// <c>"Expected the input to start with a valid JSON token, when isFinalBlock is true. Path: $
    /// | LineNumber: 0 | BytePositionInLine: 0."</c> - two clauses of vocabulary belonging to the
    /// inside of a serializer and one debugging tail, after one sentence that was genuinely useful.
    ///
    /// <b>What is kept is chosen rather than trimmed to taste.</b> The first sentence says WHAT is
    /// wrong and is the half worth reading. The position says WHERE, and in a snapshot of a
    /// thousand entries that is the difference between a fix and a search - so it is kept, in this
    /// tool's own words and counted from one, because a person looking at a file in an editor
    /// counts that way and the reader does not.
    ///
    /// <b>Cut on their own separator rather than on a phrase.</b> <c>" Path: "</c> is where the
    /// serializer stops describing the problem and starts describing its own position in the
    /// document, and cutting there needs no knowledge of which clauses a given version writes.
    /// Matching on <c>isFinalBlock</c> by name would have been a guess about somebody else's
    /// wording, and it would rot silently at the next runtime.
    ///
    /// <b>What this costs, said rather than left to be found:</b> a failure whose second sentence
    /// carries something the first does not would lose it here. Every shape met so far puts the
    /// what in the first sentence and a restatement in the second, and the position - which is the
    /// part that could not be recovered - is kept explicitly for that reason.
    /// </summary>
    /// <remarks>
    /// Internal rather than private for one reason, and it is the same one QueryPatterns gives:
    /// two of its three branches cannot be reached through TryRead. A serialiser failure always
    /// carries a position and always writes its own tail, so a message without either - which is
    /// what a future runtime, or a wrapped exception, would hand over - has no way in from outside.
    /// A branch nothing can reach is a branch nothing checks.
    /// </remarks>
    internal static string Said(JsonException problem)
    {
        var whole = problem.Message;

        var tail = whole.IndexOf(" Path: ", StringComparison.Ordinal);
        var said = tail < 0 ? whole : whole[..tail];

        var second = said.IndexOf(". ", StringComparison.Ordinal);

        if (second >= 0)
        {
            said = said[..(second + 1)];
        }

        if (problem.LineNumber is not { } line || problem.BytePositionInLine is not { } column)
        {
            return said;
        }

        // Counted from one, and converted with the invariant culture rather than inside the hole:
        // these are digits in a sentence about a file, and a machine whose culture writes numerals
        // differently would otherwise print a position no editor agrees with.
        var where = (line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var at = (column + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        return said + " Line " + where + ", position " + at + ".";
    }

    public static bool TryRead(string content, out Snapshot? snapshot, out string? failure)
    {
        snapshot = null;
        failure = null;

        try
        {
            snapshot = JsonSerializer.Deserialize<Snapshot>(content, Shape);
        }
        catch (JsonException problem)
        {
            failure = Said(problem);
            return false;
        }

        if (snapshot is null)
        {
            failure = "The file holds no snapshot.";
            return false;
        }

        // Both halves, because neither is guaranteed by anything the compiler enforces and a
        // file on disk answers to nobody.
        //
        // Found by a property test on the day one was written, not by reading this: a snapshot
        // damaged five characters in the right place made `bws snapshot diff` end with a stack
        // trace instead of the sentence this method exists to produce. A snapshot is a file
        // kept for months and carried between machines, so "somebody edited it" and "the disk
        // filled up while it was being written" are ordinary rather than exotic.
        if (snapshot.MissingParts(out failure))
        {
            snapshot = null;

            return false;
        }

        if (snapshot.Metadata.SchemaVersion != Snapshot.CurrentSchemaVersion)
        {
            // Said out loud rather than attempted. A file from a schema this build does not
            // know may be missing fields, or may mean something different by one it has - and
            // reading it anyway would produce a comparison that looks ordinary and is not.
            //
            // AHEAD OF THE CONTENT CHECK BELOW, and the order carries an argument: a rule about
            // what the entries may hold is a rule of THIS schema, so applying it to a document
            // written against another one would report a fault that may not be one there.
            failure =
                $"The snapshot uses schema version {snapshot.Metadata.SchemaVersion} and this " +
                $"build reads version {Snapshot.CurrentSchemaVersion}.";

            snapshot = null;
            return false;
        }

        // Everything above is about the document. This is about what is inside it, and it was
        // missing until 2026-08-03 - so this method kept its promise and the caller broke it two
        // lines later.
        //
        // MEASURED, not reasoned: a file with `"entries": [ {...}, null ]` ended
        // `bws snapshot diff` with "Object reference not set to an instance of an object." and
        // code 1, and a file holding one service name twice ended it with "An item with the same
        // key has already been added." Neither message names either of the two files being
        // compared, and code 1 says the tool fell over when what happened is that a file is
        // broken.
        //
        // Neither shape is exotic for a document kept for months, edited by hand, merged by
        // somebody's tooling or truncated by a full disk. The property test beside this damages
        // a real snapshot by cutting, deleting, flipping and inserting characters, which is the
        // right instrument and reaches neither of these: both are structurally valid JSON.
        //
        // The question itself belongs to Snapshot rather than here, since 2026-08-04. It was
        // written here because this was the first caller that needed it, and being written here
        // meant the comparison engine relied on it without being able to ask it.
        if (snapshot.BrokenEntries(out failure))
        {
            snapshot = null;

            return false;
        }

        return true;
    }

    /// <summary>
    /// One entry exactly as it appears in the file, as a tree.
    ///
    /// Here rather than in the comparison that needs it, because the alternative is a second
    /// place that decides what a snapshot entry is made of. The shape and the dropped
    /// measurement are the same object both callers use, so a field that starts or stops
    /// being written is written and compared the same way without anybody remembering to
    /// change two things.
    /// </summary>
    internal static JsonObject Document(EntryDocument entry)
    {
        var tree = JsonSerializer.SerializeToNode(entry, Shape)!.AsObject();

        Drop(tree, Measurement);

        return tree;
    }

    private static void Drop(JsonObject entry, string property)
    {
        entry.Remove(property);

        // And out of the list of things nobody read, where it would otherwise show up on
        // every entry of every snapshot - an admission about a field the document does not
        // claim to hold in the first place.
        if (entry["notRead"] is JsonArray notRead)
        {
            var mentions = notRead.Where(name => name?.GetValue<string>() == property).ToArray();

            foreach (var mention in mentions)
            {
                notRead.Remove(mention);
            }

            if (notRead.Count == 0)
            {
                entry.Remove("notRead");
            }
        }
    }

    /// <summary>
    /// The same tree with every object's keys in order, all the way down.
    ///
    /// Ordinal ordering, not the machine's idea of alphabetical. A file written on a machine
    /// whose language sorts differently would otherwise diff against one written here on
    /// nothing but the order of its keys - the same class of false difference `ADR-14` exists
    /// to prevent, one layer further down.
    /// </summary>
    private static JsonNode Sorted(JsonNode node)
    {
        if (node is JsonObject unordered)
        {
            var ordered = new JsonObject();

            foreach (var property in unordered.OrderBy(property => property.Key, StringComparer.Ordinal))
            {
                // Detached from the old parent first: a node belongs to one tree, and moving
                // it without saying so is an exception rather than a copy.
                var value = property.Value;
                unordered[property.Key] = null;

                ordered[property.Key] = value is null ? null : Sorted(value);
            }

            return ordered;
        }

        if (node is JsonArray array)
        {
            var ordered = new JsonArray();

            // Order preserved. The array holds entries, and their order is already decided
            // deliberately when the snapshot is built - sorting them again here would put
            // that decision in two places.
            for (var index = 0; index < array.Count; index++)
            {
                var value = array[index];
                array[index] = null;

                ordered.Add(value is null ? null : Sorted(value));
            }

            return ordered;
        }

        return node.DeepClone();
    }
}
