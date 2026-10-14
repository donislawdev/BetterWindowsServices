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
