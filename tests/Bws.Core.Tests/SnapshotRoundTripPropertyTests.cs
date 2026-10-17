using System.Reflection;
using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// A whole generated machine through the two passes, into a snapshot file on disk and back, compared
/// with itself - rule 8 and the drift promise checked together on every field at once.
///
/// <b>Three answers, each of which used to be checked only on the specimen catalogue somebody wrote out.</b>
/// The file reads back (the reader never refuses what the writer wrote). The comparison of the two finds
/// nothing (a field lost or changed by the trip would show as drift on a machine that did not move). And
/// nothing unread is lost: every field refused or never asked is still refused or never asked after the
/// file, entry by entry - a refusal that came back as an absence would be a snapshot claiming a
/// completeness it never had, the one silence rule 8 exists to forbid. Written again, the file is the
/// same text, so a later version of the tool reading an old file sees what was written.
///
/// <b>Through the real file doors</b> - AtomicFile.Write and SnapshotOnDisk.TryLoad, the two the
/// command line and the window use - into a folder of its own that is removed afterwards.
/// </summary>
public sealed class SnapshotRoundTripPropertyTests
{
    [Fact]
    [Trait("search", "weekly")]
    public void A_machine_written_to_a_snapshot_and_read_back_is_the_same_machine_with_nothing_unread_lost()
    {
        var folder = Directory.CreateTempSubdirectory("bws-round-trip-");

        try
        {
            WholeEntries.Any.Searched(
                listed => Wrong(listed, folder.FullName) is null,
                new(500) { Print = listed => $"{Wrong(listed, folder.FullName)} - {string.Join(", ", listed)}" });
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    private static string? Wrong(IReadOnlyList<Listed> listed, string folder)
    {
        var filled = Passed(listed);
        var taken = Machines.Taken(filled);
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".json");

        try
        {
            AtomicFile.Write(path, SnapshotJson.Render(taken));

            return SnapshotOnDisk.TryLoad(path, out var read, out var refusal)
                ? Unpassed(listed, filled) ?? Lost(filled, read) ?? Moved(taken, read) ?? Rewritten(taken, read)
                : $"the reader refused the file the writer wrote: {refusal.Fault} {refusal.Detail}";
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// What the manager said about who depends on an entry arrives in the entry as what it was - a
    /// refusal as a refusal. Asked of the double's own answer rather than of anything the pass wrote.
    /// </summary>
    private static string? Unpassed(IReadOnlyList<Listed> listed, IReadOnlyList<ScmEntry> filled) =>
        listed.Zip(filled)
            .FirstOrDefault(pair => pair.First.Told.Dependents.Outcome != pair.Second.RequiredBy.Outcome) is ({ } told, { } entry)
            ? $"{entry.ServiceName}: the manager answered {told.Told.Dependents.Outcome}, the entry holds {entry.RequiredBy.Outcome}"
            : null;

    /// <summary>The entries as the two passes fill them, from what the doubles were told.</summary>
    private static IReadOnlyList<ScmEntry> Passed(IReadOnlyList<Listed> listed)
    {
        var catalog = new FakeScmCatalog(listed.Select(one => one.Entry));
        var signatures = new Dictionary<string, Reading<BinarySignature>>(StringComparer.OrdinalIgnoreCase);
        var versions = new Dictionary<string, Reading<string>>(StringComparer.OrdinalIgnoreCase);
        var hashes = new Dictionary<string, Reading<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (entry, told) in listed)
        {
            Taught(catalog, entry.ServiceName, told.Dependents);

            if (entry.BinaryFile is { IsPresent: true, Value: { } file })
            {
                signatures[file] = told.Signature;
                versions[file] = told.Version;
                hashes[file] = told.Hash;
            }
        }

        return SnapshotPasses.Fill(
            [.. listed.Select(one => one.Entry)], catalog, new FakeBinaryInspector(signatures, versions, hashes), out _, out _);
    }

    private static void Taught(FakeScmCatalog catalog, string name, Reading<IReadOnlyList<string>> dependents)
    {
        if (dependents.Outcome == ReadOutcome.Denied)
        {
            catalog.RefuseDependentsFor.Add(name);
        }
        else if (dependents.IsPresent)
        {
            catalog.DependedOnBy(name, [.. dependents.Value!]);
        }
    }

    /// <summary>
    /// Every entry is still there, and every field refused or never asked is still that after the file.
    ///
    /// <b>The expected fields come from the entry's own readings, never from the document</b> - both
    /// snapshots here are built by the same EntryDocument.From, so comparing one document with the other
    /// would let that code drop a refusal on both sides at once and agree with itself.
    /// </summary>
    private static string? Lost(IReadOnlyList<ScmEntry> filled, Snapshot read)
    {
        if (filled.Count != read.Entries.Count)
        {
            return $"{filled.Count} entries written, {read.Entries.Count} read back";
        }

        var back = read.Entries.ToDictionary(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase);

        return filled
            .Select(entry => back.TryGetValue(entry.ServiceName, out var document)
                ? Expected(entry) == Unread(document) ? null : $"{entry.ServiceName}: unread [{Expected(entry)}] came back as [{Unread(document)}]"
                : $"{entry.ServiceName} is missing from the file")
            .FirstOrDefault(wrong => wrong is not null);
    }

    /// <summary>
    /// Every reading an entry carries, as the file names it - except the two the file holds in no state
    /// at all, on purpose. Memory is a measurement, different a second later, and left in "notRead" it
    /// would confess on every entry to a gap in a document that never claimed the field (SnapshotJson,
    /// SnapshotTests.The_measurement_is_not_in_the_file - the first run of this property tripped over it,
    /// which is how the exclusion was found rather than assumed). AcceptsStop says where an entry is this
    /// second rather than how it is set up (its own comment in ScmEntry).
    /// </summary>
    private static readonly PropertyInfo[] Readings =
    [
        .. typeof(ScmEntry).GetProperties()
            .Where(property => property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(Reading<>)

                // Read rather than worked out: RunsWhileDisabled and its kind are answers computed from
                // two readings, and the file holds the readings, not the sums.
                && property.SetMethod is not null
                && property.Name is not (nameof(ScmEntry.Memory) or nameof(ScmEntry.AcceptsStop)))
    ];

    private static string Expected(ScmEntry entry) =>
        $"refused {Fields(entry, ReadOutcome.Denied)}; not asked {Fields(entry, ReadOutcome.NotRead)}";

    private static string Fields(ScmEntry entry, ReadOutcome outcome) =>
        string.Join(" ", Readings
            .Where(property => Outcome(property.GetValue(entry)!) == outcome)
            .Select(property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..])
            .Order(StringComparer.Ordinal));

    private static ReadOutcome Outcome(object reading) =>
        (ReadOutcome)reading.GetType().GetProperty(nameof(Reading<int>.Outcome))!.GetValue(reading)!;

    private static string Unread(EntryDocument entry) =>
        $"refused {string.Join(" ", (entry.Unreadable?.Keys ?? Enumerable.Empty<string>()).Order(StringComparer.Ordinal))}; "
        + $"not asked {string.Join(" ", (entry.NotRead ?? []).Order(StringComparer.Ordinal))}";

    /// <summary>The machine compared with itself across the file finds nothing at all.</summary>
    private static string? Moved(Snapshot taken, Snapshot read)
    {
        if (!SnapshotDiff.TryBetween(taken, read, out var diff, out var failure))
        {
            return $"the comparison refused: {failure}";
        }

        return diff.Drifted || diff.Added.Count + diff.Removed.Count + diff.Changed.Count > 0
            ? $"drift across the file: {diff.Added.Count} added, {diff.Removed.Count} removed, "
                + $"{string.Join("; ", diff.Changed.Select(changed => $"{changed.ServiceName} {string.Join(" ", changed.Differences.Select(field => field.Field).Concat(changed.Incomparable))}"))}"
            : null;
    }

    /// <summary>Written again, the file is the same text.</summary>
    private static string? Rewritten(Snapshot taken, Snapshot read) =>
        SnapshotJson.Render(taken) == SnapshotJson.Render(read) ? null : "written again, the file is different text";
}
