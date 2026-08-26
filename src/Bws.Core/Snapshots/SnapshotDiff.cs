using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace Bws.Core.Snapshots;

/// <summary>Which question a changed field answers.</summary>
public enum DifferenceGroup
{
    /// <summary>How the machine is set up. This is the drift an audit is looking for.</summary>
    Configuration,

    /// <summary>
    /// What was running at the moment each snapshot was taken.
    ///
    /// Kept apart rather than mixed in, because two snapshots a day apart differ here on
    /// dozens of entries and none of it is drift. Mixed together, the six configuration
    /// changes that matter arrive underneath sixty that do not, and a report nobody finishes
    /// reading is worth as little as one nobody produces. Owner's decision, 2026-08-01.
    /// </summary>
    RunningState
}

/// <summary>One field that says something different in the two snapshots.</summary>
/// <param name="Before">Null means the field was absent, which is itself a difference.</param>
public sealed record FieldDifference(string Field, DifferenceGroup Group, string? Before, string? After);

/// <summary>An entry that is in one snapshot and not the other.</summary>
public sealed record EntryPresence(string ServiceName, string DisplayName);

/// <param name="Incomparable">
/// Fields that could not be compared because at least one side never read them. Named rather
/// than dropped: silence about a field nobody could read is exactly what rule 8 forbids, and
/// "we did not compare this" is a different sentence from "this did not change".
/// </param>
public sealed record ChangedEntry(
    string ServiceName,
    string DisplayName,
    IReadOnlyList<FieldDifference> Differences,
    IReadOnlyList<string> Incomparable);

/// <summary>
/// What makes this comparison less than exact, as facts rather than sentences.
///
/// Facts because the core does not write anything a person reads - `ADR-3` keeps it from
/// knowing an interface exists, and rule 13 keeps user-facing wording out of code entirely.
/// Whoever displays this turns the flags into words in their own language.
/// </summary>
public sealed record ComparisonCaveats(
    bool ElevationDiffers,
    bool MachineDiffers,
    bool OperatingSystemDiffers,
    bool ToolVersionDiffers);

/// <summary>
/// What changed between two snapshots.
///
/// The engine behind all three comparisons `D2` asks for. Two of them - snapshot against
/// snapshot, and one machine against another - are the same operation on two files. The
/// third, against the live machine, is this plus taking a snapshot in memory first.
///
/// <b>It walks the document rather than a list of fields kept by hand.</b> That is a decision
/// with a decade behind it: a hand-kept list drifts away from the schema the first time
/// somebody adds a field, and it drifts silently - the new field simply never shows up as
/// changed, and nobody finds out until a comparison that should have caught something did
/// not. Walking the tree means a field added to the snapshot is compared the day it is added.
///
/// <b>Refusals are never compared, only named.</b> A field one side could not read is null on
/// that side, so comparing it would report a change from a value to nothing - the exact false
/// difference `ADR-14` exists to prevent. It also sidesteps a trap found in a real file: the
/// sentence inside a refusal comes from Windows in the machine's own language, so two
/// machines refusing the same field for the same reason carry different words for it.
/// </summary>
/// <param name="Changed">Entries with at least one field that says something different.</param>
/// <param name="NotFullyCompared">
/// Entries where nothing differed but something could not be looked at.
///
/// Their own list rather than part of <see cref="Changed"/>, and that came out of running
/// this against a real pair rather than from reading it. An elevated snapshot compared with
/// a restricted one put five entries under "changed" that had nothing changed about them -
/// only a security descriptor one side was refused. The summary then read "5 changed, 0
/// differences", and <see cref="Any"/> was true, so --exit-code would have failed a pipeline
/// over a comparison that found nothing. That is the same false alarm the whole handling of
/// missing entries exists to prevent, arriving through the exit code instead.
/// </param>
/// <param name="NeitherRead">
/// Fields that at least one entry could not be compared on, because neither snapshot read them
/// there.
///
/// <b>At least one, not every one</b>, and the sentence used to say the second - which was a
/// claim about scale that the code does not make. It is gathered per entry and reported once, so
/// a field appearing here may have been unread on a single entry out of eight hundred. In the
/// ordinary case the two are the same thing, because the usual reason is that this build does
/// not read the field at all - but "usually" is not "always", and a caveat that overstates how
/// much was missed is a caveat nobody can act on.
///
/// Said once for the whole comparison rather than against every entry. Repeated per row it
/// would be the same admission eight hundred times, the shape this project already met when a
/// listing threatened to answer "I do not know" about every entry it had.
/// </param>
public sealed record SnapshotDiff(
    IReadOnlyList<EntryPresence> Added,
    IReadOnlyList<EntryPresence> Removed,
    IReadOnlyList<ChangedEntry> Changed,
    IReadOnlyList<ChangedEntry> NotFullyCompared,
    IReadOnlyList<string> NeitherRead,
    IReadOnlyList<EntryPresence> Uncertain,
    ComparisonCaveats Caveats)
{
    /// <summary>Identity, not a value - it is how the two sides are matched at all.</summary>
    private const string Identity = "serviceName";

    /// <summary>Names of what could not be read, rather than fields in their own right.</summary>
    private static readonly string[] Bookkeeping = ["unreadable", "notRead"];

    /// <summary>
    /// Never a difference.
    ///
    /// A process identifier is a fact about one boot. Everything running gets a new one after
    /// a restart, so comparing it would mark most of the machine as changed for saying
    /// nothing. It is in the file because a snapshot records what was true, and out of the
    /// comparison for the same reason memory is out of the file altogether: it carries no
    /// information from one snapshot to the next. Owner's decision, 2026-08-01.
    /// </summary>
    private const string Unstable = "processId";

    /// <summary>The one field that describes the moment rather than the setup.</summary>
    private const string State = "status";

    /// <summary>
    /// Whether anything actually differs.
    ///
    /// Deliberately not counting what could not be compared, and not counting the entries
    /// only one side could see. Both of those are admissions about the comparison rather
    /// than findings about the machine, and this is the answer --exit-code gives a pipeline.
    /// </summary>
    public bool Any => Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0;

    /// <summary>
    /// Compares two snapshots, or says why one of them is not something to compare.
    ///
    /// <b>Returns rather than throws, and that shape arrived on 2026-08-04 as a breaking change -
    /// owner's decision, taken knowing it was one.</b> Until then this was <c>Between</c>, and it
    /// fell over from inside a dictionary
    /// on a snapshot holding one service name twice - with a message naming neither side. It was
    /// safe in practice only because every document reaching it had come through
    /// <see cref="SnapshotJson.TryRead"/>, which refuses that file. That is a precondition
    /// recorded in a comment and enforced by nothing, and the second caller - a window showing a
    /// comparison - would have been free to reintroduce the crash.
    ///
    /// So the question moved to <see cref="Snapshot"/>, where it is a fact about the document
    /// rather than about JSON, and this asks it of whatever it is handed. Two callers, one rule,
    /// one place. The check costs a set insertion per entry against a comparison that already
    /// serialises every entry twice, which is why paying it on every run is not worth avoiding.
    ///
    /// A null argument is still thrown over, deliberately. A broken document is an ordinary
    /// thing to run into and gets a sentence; passing nothing at all is a mistake in the code
    /// calling this, and turning that into a return value would hide it.
    /// </summary>
    /// <param name="diff">
    /// Carries its own guarantee rather than leaving every caller to assert it. Without the
    /// annotation each one silences the compiler at the point of use, which reads the same and
    /// switches off the check that would notice the day this stops being true.
    /// </param>
    public static bool TryBetween(
        Snapshot before, Snapshot after, [NotNullWhen(true)] out SnapshotDiff? diff, out string? failure)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        diff = null;

        // The words the product already uses for the two sides - "two files, the earlier one
        // first". Not "before" and "after", which name the parameters rather than what a person
        // typed, and not the file names, which this layer has no business knowing.
        if (Unusable(before, "earlier", out failure) || Unusable(after, "later", out failure))
        {
            return false;
        }

        diff = Between(before, after);

        return true;
    }

    /// <summary>
    /// Whether a side is a snapshot at all.
    ///
    /// Both questions in order, and the order is load-bearing rather than tidy: the second walks
    /// the entries, so it may only be asked once the first has said there are entries to walk.
    /// </summary>
    private static bool Unusable(Snapshot snapshot, string side, out string? failure)
    {
        if (snapshot.MissingParts(out var fault) || snapshot.BrokenEntries(out fault))
        {
            failure = $"The {side} snapshot is broken. {fault}";

            return true;
        }

        failure = null;

        return false;
    }

    private static SnapshotDiff Between(Snapshot before, Snapshot after)
    {
        // Case-insensitively, because that is how Windows treats a service name, so two
        // spellings are the same service rather than two. What used to stand here was a note
        // saying nothing checked for two entries matching this way - the caller above now does.
        var left = before.Entries.ToDictionary(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase);
        var right = after.Entries.ToDictionary(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase);

        var elevationDiffers = before.Metadata.Elevated != after.Metadata.Elevated;

        var added = new List<EntryPresence>();
        var removed = new List<EntryPresence>();
        var uncertain = new List<EntryPresence>();
        var changed = new List<ChangedEntry>();
        var partial = new List<ChangedEntry>();
        var neitherRead = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in Ordered(after.Entries))
        {
            if (left.TryGetValue(entry.ServiceName, out var was))
            {
                var difference = Compare(was, entry, neitherRead);

                if (difference is not null)
                {
                    (difference.Differences.Count > 0 ? changed : partial).Add(difference);
                }
            }
            else if (Invisible(elevationDiffers, blind: before.Metadata.Elevated))
            {
                // Present now, absent from a snapshot taken without elevation. The manager
                // hands over fewer entries that way, so "it appeared" and "the other side
                // could not see it" look identical and only one of them is a fact.
                uncertain.Add(Presence(entry));
            }
            else
            {
                added.Add(Presence(entry));
            }
        }

        foreach (var entry in Ordered(before.Entries).Where(entry => !right.ContainsKey(entry.ServiceName)))
        {
            if (Invisible(elevationDiffers, blind: after.Metadata.Elevated))
            {
                uncertain.Add(Presence(entry));
            }
            else
            {
                removed.Add(Presence(entry));
            }
        }

        return new SnapshotDiff(
            added,
            removed,
            changed,
            partial,
            [.. neitherRead.OrderBy(field => field, StringComparer.Ordinal)],
            [.. uncertain.OrderBy(entry => entry.ServiceName, StringComparer.Ordinal)],
            new ComparisonCaveats(
                elevationDiffers,
                !string.Equals(before.Metadata.Machine, after.Metadata.Machine, StringComparison.OrdinalIgnoreCase),
                !string.Equals(before.Metadata.OperatingSystem, after.Metadata.OperatingSystem, StringComparison.Ordinal),
                !string.Equals(before.Metadata.Tool, after.Metadata.Tool, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Whether a row missing from one side might be missing only from view.
    ///
    /// Only in one direction, and the direction is the whole point. An unelevated snapshot is
    /// a subset of an elevated one, so a row the unelevated side lacks may simply be
    /// invisible - but a row the *elevated* side lacks is genuinely gone, because anything
    /// the restricted view could see the full one could see too.
    /// </summary>
    private static bool Invisible(bool elevationDiffers, bool blind) => elevationDiffers && !blind;

    private static ChangedEntry? Compare(EntryDocument before, EntryDocument after, HashSet<string> neitherRead)
    {
        var was = SnapshotJson.Document(before);
        var now = SnapshotJson.Document(after);

        // Two ways a field can be missing, and the snapshot format already tells them apart.
        // Using that distinction here is the whole of this decision.
        //
        // Refused is a fact about ONE ENTRY: the field exists, somebody asked, and this
        // machine said no. Five entries on a real machine refuse their security descriptor
        // without elevation while the other eight hundred hand it over, so an admission about
        // "the snapshot" would be false about most of it. Named beside the entry.
        //
        // Never asked is a fact about THE RUN, when it holds on both sides: almost always a
        // field this build does not read at all. Repeating it per entry would put the same
        // sentence on every row - the "810 shrugs" this project already walked into once with
        // triggers. Said once, for the whole comparison.
        var refused = Refused(before).Union(Refused(after), StringComparer.Ordinal);
        var neither = NotAsked(before).Intersect(NotAsked(after), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        var skip = NotAsked(before)
            .Union(NotAsked(after), StringComparer.Ordinal)
            .Except(neither, StringComparer.Ordinal)
            .Union(refused, StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        neitherRead.UnionWith(neither);

        var differences = new List<FieldDifference>();
        var incomparable = new List<string>();

        foreach (var field in was.Select(property => property.Key)
                     .Union(now.Select(property => property.Key), StringComparer.Ordinal)
                     .Where(Comparable)
                     .OrderBy(field => field, StringComparer.Ordinal))
        {
            if (skip.Contains(field))
            {
                incomparable.Add(field);
                continue;
            }

            var left = Text(was[field]);
            var right = Text(now[field]);

            if (!string.Equals(Compared(field, was[field]), Compared(field, now[field]), StringComparison.Ordinal))
            {
                differences.Add(new FieldDifference(
                    field,
                    field == State ? DifferenceGroup.RunningState : DifferenceGroup.Configuration,
                    left,
                    right));
            }
        }

        return differences.Count == 0 && incomparable.Count == 0
            ? null
            : new ChangedEntry(after.ServiceName, after.DisplayName, differences, incomparable);
    }

    /// <summary>Fields this machine refused. A fact about permissions on this entry.</summary>
    private static IEnumerable<string> Refused(EntryDocument entry) =>
        (entry.Unreadable?.Keys ?? Enumerable.Empty<string>()).Select(Camel);

    /// <summary>Fields nobody asked about on that run. A fact about the run, not the entry.</summary>
    private static IEnumerable<string> NotAsked(EntryDocument entry) =>
        (entry.NotRead ?? []).Select(Camel);

    /// <summary>
    /// The field names inside "unreadable" and "notRead" are written the same way as the
    /// fields they talk about. They come from the property names, so they arrive capitalised
    /// and the keys beside them do not.
    /// </summary>
    private static string Camel(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static bool Comparable(string field) =>
        field != Identity && field != Unstable && !Bookkeeping.Contains(field, StringComparer.Ordinal);

    /// <summary>
    /// Fields whose value is a list where neither the order nor the spelling carries meaning.
    ///
    /// <b>Three fields, and each one is on this list for a written reason rather than because it
    /// happens to be an array.</b>
    ///
    /// <c>requiredPrivileges</c> - the manager hands these back exactly as each service declared
    /// them, and the spelling varies between services on ONE machine: Schedule declares
    /// SeSystemTimePrivilege and Sense declares SeSystemtimePrivilege, and they are the same
    /// privilege. <see cref="ScmEntry.RequiredPrivileges"/> has said since it was written that
    /// comparison is case-insensitive everywhere and that a diff comparing them as plain text
    /// would report a change between two machines that had none. It did exactly that until
    /// 2026-08-26.
    ///
    /// <c>dependsOn</c> - service names, which Windows itself compares without case (`ADR-14`),
    /// in whatever order the manager answered. That order is promised nowhere. This file's own
    /// neighbour sorts ENTRIES for precisely that reason, having seen the order move.
    ///
    /// <c>triggers</c> - same argument about order. The two words inside each one are written by
    /// this build rather than by the manager, so their spelling is ours and stable.
    /// </summary>
    private static readonly string[] Unordered = ["dependsOn", "requiredPrivileges", "triggers"];

    /// <summary>
    /// A value in the form the comparison uses, which is NOT always the form a person is shown.
    ///
    /// <b>The two were one representation until 2026-08-26, and splitting them was deliberate
    /// rather than a convenience.</b> The old sentence here said that keeping one form meant what
    /// the comparison decided and what somebody is shown could never disagree - a real property,
    /// and it was paid for with a false difference on every list whose order or spelling moved
    /// without anything changing. A tool whose whole subject is telling real drift from noise may
    /// not report drift that is not there.
    ///
    /// <b>So the split goes one way only.</b> This form exists to decide whether two values differ
    /// and is never shown to anybody. What a person reads is still <see cref="Text"/> of the value
    /// as it was written - original spelling, original order - so a difference that IS reported
    /// still shows both sides exactly as the two snapshots hold them.
    ///
    /// Everything not named in <see cref="Unordered"/> is compared exactly as before. That is the
    /// half of this that is easy to get wrong: comparing every field without case would quietly
    /// stop <c>binaryHash</c> and <c>sddl</c> from reporting changes that are real.
    /// </summary>
    private static string? Compared(string field, JsonNode? node) =>
        node is JsonArray items && Unordered.Contains(field, StringComparer.Ordinal)
            ? string.Join(
                '\u001f',
                items
                    .Select(item => (Text(item) ?? string.Empty).ToLowerInvariant())
                    .OrderBy(text => text, StringComparer.Ordinal))
            : Text(node);

    /// <summary>
    /// A value as text, for showing, and for comparing everything <see cref="Compared"/> does not
    /// take a view on.
    ///
    /// Strings come out as themselves rather than quoted, because a quoted path in a table reads
    /// like a mistake.
    /// </summary>
    private static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => node.ToJsonString()
    };

    private static EntryPresence Presence(EntryDocument entry) =>
        new(entry.ServiceName, entry.DisplayName);

    private static IEnumerable<EntryDocument> Ordered(IReadOnlyList<EntryDocument> entries) =>
        entries.OrderBy(entry => entry.ServiceName, StringComparer.Ordinal);
}
