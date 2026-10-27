using System.Diagnostics.CodeAnalysis;

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
public sealed record FieldDifference(string Field, DifferenceGroup Group, string? Before, string? After)
{
    /// <summary>
    /// For the triggers field, the triggers each side holds and the other does not - null for every
    /// other field (backlog 625, owner's decision "a line per trigger").
    ///
    /// <b>Beside <see cref="Before"/> and <see cref="After"/>, not instead of them</b>: those two are the
    /// whole lists as JSON, which is what <c>--json</c> writes and a script reads - a frozen contract.
    /// This is what a person reads instead of two lines of three thousand characters, and the machine
    /// document maps its fields one by one, so it does not reach it.
    /// </summary>
    public TriggerChange? Triggers { get; init; }
}

/// <summary>
/// The triggers on one side of a comparison only. <b>Counted as a multiset, not a set</b>: two identical
/// triggers on one side and one on the other leave one behind, where a set would say nothing changed.
/// <b>Not paired</b> - a trigger with one value changed is one removed and one added, because a trigger has
/// no identity of its own to pair them by.
/// </summary>
/// <param name="Removed">In the earlier snapshot only, in the order it holds them.</param>
/// <param name="Added">In the later snapshot only, in the order it holds them.</param>
public sealed record TriggerChange(IReadOnlyList<TriggerDocument> Removed, IReadOnlyList<TriggerDocument> Added);

/// <summary>An entry that is in one snapshot and not the other.</summary>
public sealed record EntryPresence(string ServiceName, string DisplayName);

/// <param name="Incomparable">
/// Fields that could not be compared because one side holds no value for them - refused there, or
/// never asked. Named rather than dropped: silence about a field nobody could read is exactly what
/// rule 8 forbids, and "we did not compare this" is a different sentence from "this did not
/// change". A field Windows refused to BOTH sides is not here since 2026-10-14 - it is in
/// <see cref="SnapshotDiff.RefusedByBoth"/>, said once for the comparison.
/// </param>
public sealed record ChangedEntry(
    string ServiceName,
    string DisplayName,
    IReadOnlyList<FieldDifference> Differences,
    IReadOnlyList<string> Incomparable);

/// <summary>
/// A field Windows refused to BOTH snapshots, and the entries it refused it on.
///
/// <b>Its own shape since 2026-10-14 (backlog 559, owner's decision).</b> Until then a field refused
/// on both sides was named against each entry like a field only one side could read, so a machine
/// compared with itself a minute later listed ten entries as "not fully compared" every time - their
/// descriptions point at a resource file that is not there, and Windows says so to both snapshots.
/// Nothing about those entries is uncertain in a way one of the two snapshots could have settled, so
/// it is said once for the whole comparison, the way a field neither snapshot asked about is.
/// </summary>
/// <param name="Entries">Service names, in the order the comparison walked them.</param>
public sealed record RefusedField(string Field, IReadOnlyList<string> Entries);

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
/// differences", and what is now <see cref="Drifted"/> was true, so --exit-code would have failed a pipeline
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
/// <param name="RefusedByBoth">
/// Fields Windows refused to both snapshots, each with the entries it refused it on. Never part
/// of <see cref="NotFullyCompared"/> and never counted by <see cref="Reported"/> - see
/// <see cref="RefusedField"/> for why a refusal on both sides is said once rather than per entry.
/// </param>
/// <param name="LeftOut">
/// The per-user session copies neither side was compared on - see <see cref="InstancesLeftOut"/>.
/// Matched by the role the type bits give an entry, never by the shape of its name.
/// </param>
/// <param name="Unlisted">
/// Entries the snapshot holds that this machine's listing did not, and that the manager, asked by name,
/// says it still holds - only a comparison with this machine fills it (<see cref="TryAgainstMachine"/>,
/// logic report S03, 2026-10-27). Not drift and not removed: the listing leaves out what this account
/// may not query. Their own key at the end of the machine-readable comparison, "unlisted" - owner's
/// decision of the same day (backlog 623), so a script tells them from "cannot tell" as a person does.
/// </param>
public sealed partial record SnapshotDiff(
    IReadOnlyList<EntryPresence> Added,
    IReadOnlyList<EntryPresence> Removed,
    IReadOnlyList<ChangedEntry> Changed,
    IReadOnlyList<ChangedEntry> NotFullyCompared,
    IReadOnlyList<string> NeitherRead,
    IReadOnlyList<RefusedField> RefusedByBoth,
    IReadOnlyList<EntryPresence> Uncertain,
    ComparisonCaveats Caveats,
    InstancesLeftOut LeftOut,
    IReadOnlyList<EntryPresence> Unlisted)
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
    /// Whether the machine drifted: an entry added or removed, or one set up differently.
    ///
    /// <b>CONFIGURATION ONLY SINCE 2026-09-30 - stability report D-2, owner's decision, a change of
    /// meaning under an unchanged exit code.</b> Until then this was <c>Any</c>, and an entry that
    /// differed only in what it was doing at the two moments counted - so a nightly
    /// <c>--exit-code</c> paged somebody over a service that had stopped by itself, which contradicts
    /// the decision of 2026-08-01 this file opens with: running state is not drift. It is still
    /// reported, under <see cref="Changed"/>, and it no longer decides anything.
    ///
    /// Deliberately not counting what could not be compared, and not counting the entries
    /// only one side could see. Both of those are admissions about the comparison rather
    /// than findings about the machine, and this is the answer --exit-code gives a pipeline.
    ///
    /// <b>Its readers had a second question hidden in them, found before the change rather than
    /// after it.</b> The text report asked this to decide whether to say "no differences", and
    /// with the new meaning that sentence would have hidden an entry that differed only in state.
    /// It asks <see cref="Reported"/> now.
    /// </summary>
    public bool Drifted =>
        Added.Count > 0
        || Removed.Count > 0
        || Changed.Any(entry => entry.Differences.Any(difference => difference.Group == DifferenceGroup.Configuration));

    /// <summary>
    /// Whether there is anything at all to put in front of a person - a difference of either kind,
    /// something one side could not see, or something one side never read.
    /// </summary>
    public bool Reported =>
        Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0 || Uncertain.Count > 0 || NotFullyCompared.Count > 0
        || Unlisted.Count > 0;

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
    /// thing to run into and gets a sentence - passing nothing at all is a mistake in the code
    /// calling this, and turning that into a return value would hide it.
    /// </summary>
    /// <param name="diff">
    /// Carries its own guarantee rather than leaving every caller to assert it. Without the
    /// annotation each one silences the compiler at the point of use, which reads the same and
    /// switches off the check that would notice the day this stops being true.
    /// </param>
    public static bool TryBetween(
        Snapshot before, Snapshot after, [NotNullWhen(true)] out SnapshotDiff? diff, out string? failure) =>
        TryBetween(before, after, held: null, out diff, out failure);

    /// <summary>
    /// Compares a snapshot with this machine read now, asking the manager by name about every entry the
    /// snapshot holds and the reading does not - since 2026-10-27, logic report S03.
    ///
    /// <b>Until then a missing entry was removed unless the two sides differed in elevation</b>, and the
    /// listing is not the manager's word on what exists: it leaves out, without an error, what this account
    /// may not query. An entry whose permissions changed between the two readings came back as removed,
    /// and <c>--exit-code</c> called it drift. Asked by name, the manager settles it both ways - an entry it
    /// holds is <see cref="Unlisted"/>, and one it does not is removed even across a difference in
    /// elevation, where it used to be "cannot tell".
    /// </summary>
    /// <param name="machine">The manager the later side was read from, asked after it was read.</param>
    /// <exception cref="System.ComponentModel.Win32Exception">The manager could not be opened to ask.</exception>
    public static bool TryAgainstMachine(
        Snapshot before, Snapshot now, IScmCatalog machine, [NotNullWhen(true)] out SnapshotDiff? diff, out string? failure)
    {
        ArgumentNullException.ThrowIfNull(machine);

        return TryBetween(before, now, machine.ReadHeld, out diff, out failure);
    }

    private static bool TryBetween(
        Snapshot before,
        Snapshot after,
        Func<IReadOnlyList<string>, IReadOnlyList<string>>? held,
        [NotNullWhen(true)] out SnapshotDiff? diff,
        out string? failure)
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

        diff = Between(before, after, held);

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

    private static SnapshotDiff Between(
        Snapshot before, Snapshot after, Func<IReadOnlyList<string>, IReadOnlyList<string>>? held)
    {
        var caveats = ComparisonCaveats.Between(before.Metadata, after.Metadata);
        var elevationDiffers = caveats.ElevationDiffers;

        // Not looked at on any entry, rather than named against each - the caveat says it once.
        var ignored = caveats.FieldsNotCompared();

        var earlier = Kept(before.Entries, out var leftOutEarlier);
        var later = Kept(after.Entries, out var leftOutLater);

        // Case-insensitively, because that is how Windows treats a service name, so two
        // spellings are the same service rather than two. What used to stand here was a note
        // saying nothing checked for two entries matching this way - the caller above now does.
        var left = earlier.ToDictionary(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase);
        var right = later.ToDictionary(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase);

        var added = new List<EntryPresence>();
        var removed = new List<EntryPresence>();
        var uncertain = new List<EntryPresence>();
        var changed = new List<ChangedEntry>();
        var partial = new List<ChangedEntry>();
        var unseen = new Unseen();

        foreach (var entry in Ordered(later))
        {
            if (left.TryGetValue(entry.ServiceName, out var was))
            {
                var difference = Compare(was, entry, unseen, ignored, caveats.TriggerDetailDiffers);

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

        var unlisted = Missing(
            [.. Ordered(earlier).Where(entry => !right.ContainsKey(entry.ServiceName))],
            held,
            Invisible(elevationDiffers, blind: after.Metadata.Elevated),
            removed,
            uncertain);

        return new SnapshotDiff(
            added,
            removed,
            changed,
            partial,
            [.. unseen.NeitherRead.OrderBy(field => field, StringComparer.Ordinal)],
            [.. unseen.RefusedByBoth.Select(pair => new RefusedField(pair.Key, pair.Value))],
            [.. uncertain.OrderBy(entry => entry.ServiceName, StringComparer.Ordinal)],
            caveats,
            new InstancesLeftOut(leftOutEarlier, leftOutLater),
            unlisted);
    }

    /// <summary>
    /// The entries a comparison looks at: everything but the per-user session copies, and how many
    /// of those there were.
    ///
    /// <b>By the role, never by the name</b> - stability report D-1, owner's decision of 2026-09-30.
    /// A copy's name carries a session suffix, and a rule reading that shape would take a real
    /// service whose name happens to end the same way for one. The role comes from the type bits the
    /// manager hands over with every entry (docs/03, "Rola per-uzytkownik"), and it is spelled the
    /// way the enumeration spells it, like every value in the file. The template the copies are made
    /// from is not a copy and is compared field by field as before.
    /// </summary>
    private static List<EntryDocument> Kept(IReadOnlyList<EntryDocument> entries, out int leftOut)
    {
        var kept = entries
            .Where(entry => !string.Equals(entry.PerUserRole, nameof(PerUserRole.Instance), StringComparison.Ordinal))
            .ToList();

        leftOut = entries.Count - kept.Count;

        return kept;
    }

    /// <summary>
    /// Whether a row missing from one side might be missing only from view.
    ///
    /// Only in one direction, and the direction is the whole point. An unelevated snapshot is
    /// a subset of an elevated one, so a row the unelevated side lacks may simply be
    /// invisible. A row the *elevated* side lacks was called genuinely gone here, because anything
    /// the restricted view could see the full one could see too.
    ///
    /// <b>That last sentence is not true, and since 2026-10-27 it says so</b> (logic report S03): the
    /// listing leaves out whatever this account may not query, elevated or not, and an entry whose
    /// permissions changed between two readings at the same elevation is missing from the second. Two
    /// files cannot settle it - nobody can ask a file's machine - so for them this rule stands, said.
    /// A comparison with this machine asks the manager instead (<see cref="TryAgainstMachine"/>).
    /// </summary>
    private static bool Invisible(bool elevationDiffers, bool blind) => elevationDiffers && !blind;

    /// <param name="ignored">
    /// Fields not looked at on this entry at all, because a caveat about the whole comparison already
    /// says why - never listed as incomparable here, which would repeat that sentence on every row.
    /// </param>
    /// <param name="kindsOnly">
    /// One side is older than whole triggers, so both are narrowed to what the older one holds
    /// (<see cref="ComparisonCaveats.TriggerDetailDiffers"/>), and a reported difference shows the two
    /// narrowed values rather than one whole and one not.
    /// </param>
    private static ChangedEntry? Compare(
        EntryDocument before, EntryDocument after, Unseen unseen, string[] ignored, bool kindsOnly)
    {
        var (was, now) = Documents(before, after, kindsOnly);

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
        //
        // Refused on BOTH sides is the third case and it is about the entry again, but not about the
        // comparison being unable to settle it: neither snapshot holds a value, so no second look
        // could have told a change from none. Said once for the whole comparison since 2026-10-14
        // (backlog 559) - named per entry, it put the same ten entries under "not fully compared"
        // every time this machine was compared with itself.
        var refused = Refused(before).Union(Refused(after), StringComparer.Ordinal);
        var refusedByBoth = Refused(before).Intersect(Refused(after), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var neither = NotAsked(before).Intersect(NotAsked(after), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        var skip = NotAsked(before)
            .Union(NotAsked(after), StringComparer.Ordinal)
            .Except(neither, StringComparer.Ordinal)
            .Union(refused, StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        unseen.NeitherRead.UnionWith(neither);

        var differences = new List<FieldDifference>();
        var incomparable = new List<string>();

        foreach (var field in was.Select(property => property.Key)
                     .Union(now.Select(property => property.Key), StringComparer.Ordinal)
                     .Where(field => Comparable(field) && !ignored.Contains(field, StringComparer.Ordinal))
                     .OrderBy(field => field, StringComparer.Ordinal))
        {
            // Refused to both is a part of skip, so it is asked first - and flat rather than nested,
            // because the shape guard counts a third level here as a method climbing towards its ceiling.
            if (refusedByBoth.Contains(field))
            {
                unseen.Refused(field, after.ServiceName);
                continue;
            }

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
                    right) { Triggers = Change(field, was[field], now[field]) });
            }
        }

        return differences.Count == 0 && incomparable.Count == 0
            ? null
            : new ChangedEntry(after.ServiceName, after.DisplayName, differences, incomparable);
    }

    private static bool Comparable(string field) =>
        field != Identity && field != Unstable && !Bookkeeping.Contains(field, StringComparer.Ordinal);

    private static EntryPresence Presence(EntryDocument entry) =>
        new(entry.ServiceName, entry.DisplayName);

    private static IEnumerable<EntryDocument> Ordered(IReadOnlyList<EntryDocument> entries) =>
        entries.OrderBy(entry => entry.ServiceName, StringComparer.Ordinal);
}
