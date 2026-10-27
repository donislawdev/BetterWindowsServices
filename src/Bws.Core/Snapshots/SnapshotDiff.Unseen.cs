namespace Bws.Core.Snapshots;

/// <summary>
/// What a comparison could not look at: fields one side refused, fields nobody asked about, and since
/// 2026-10-14 fields refused to both sides - the three readings <see cref="SnapshotDiff"/> tells apart
/// before it compares anything.
///
/// <b>Its own file since that day</b>, when the third kind arrived and the comparison stood over the
/// share of its file ceiling the size guard counts as near. One subject, the unread, so one place.
/// </summary>
public sealed partial record SnapshotDiff
{
    /// <summary>
    /// The entries the earlier side holds and the later one does not, each put where it belongs - and the
    /// ones the manager still holds handed back, in the order they came.
    ///
    /// <b>Asked of the manager when there is one to ask</b> (a comparison with this machine, since
    /// 2026-10-27 - logic report S03): what it holds is unlisted, and what it does not is removed, whatever
    /// the two sides' elevation. <b>Without one</b> - two files - an entry missing from a side read without
    /// elevation is "cannot tell", as before: nobody can ask a file's machine any more.
    /// </summary>
    /// <param name="mayBeUnseen">Whether the later side was read with fewer rights than the earlier one.</param>
    private static List<EntryPresence> Missing(
        List<EntryDocument> missing,
        Func<IReadOnlyList<string>, IReadOnlyList<string>>? held,
        bool mayBeUnseen,
        List<EntryPresence> removed,
        List<EntryPresence> uncertain)
    {
        var stillHeld = held is null || missing.Count == 0
            ? []
            : held([.. missing.Select(entry => entry.ServiceName)]).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unlisted = new List<EntryPresence>();

        foreach (var entry in missing)
        {
            var into = stillHeld.Contains(entry.ServiceName) ? unlisted
                : held is null && mayBeUnseen ? uncertain
                : removed;

            into.Add(new EntryPresence(entry.ServiceName, entry.DisplayName));
        }

        return unlisted;
    }

    /// <summary>Fields this machine refused. A fact about permissions on this entry.</summary>
    private static IEnumerable<string> Refused(EntryDocument entry) =>
        (entry.Unreadable?.Keys ?? Enumerable.Empty<string>()).Select(EntryDocument.Camel);

    /// <summary>Fields nobody asked about on that run. A fact about the run, not the entry.</summary>
    private static IEnumerable<string> NotAsked(EntryDocument entry) =>
        (entry.NotRead ?? []).Select(EntryDocument.Camel);

    /// <summary>
    /// What a comparison could not look at and says once rather than against each entry - fields
    /// neither snapshot asked about, and fields Windows refused to both. One value handed down the
    /// walk instead of two collections, so the per-entry comparison keeps the four parameters it had.
    /// </summary>
    private sealed class Unseen
    {
        internal HashSet<string> NeitherRead { get; } = new(StringComparer.Ordinal);

        /// <summary>Field to entries, sorted by field so the document comes out the same every time.</summary>
        internal SortedDictionary<string, List<string>> RefusedByBoth { get; } = new(StringComparer.Ordinal);

        internal void Refused(string field, string serviceName)
        {
            if (!RefusedByBoth.TryGetValue(field, out var entries))
            {
                entries = [];
                RefusedByBoth.Add(field, entries);
            }

            entries.Add(serviceName);
        }
    }
}
