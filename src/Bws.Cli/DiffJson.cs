using System.Text.Json;
using System.Text.Json.Serialization;
using Bws.Core.Snapshots;

namespace Bws.Cli;

/// <summary>
/// What changed, written for a script.
///
/// <b>A public contract.</b> People will build pipeline steps on these names, so a rename
/// costs a version rather than a tidy-up - the same rule the snapshot schema lives under.
///
/// The shape mirrors what a person is shown rather than being a second design: added,
/// removed, changed, uncertain, and the caveats first. A script and a person disagreeing
/// about what a comparison found is the failure this file exists to avoid.
/// </summary>
internal static class DiffJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // KEYS camelCase, VALUES the way the enumeration spells them - rule 5 of docs/03, which the
        // snapshot has followed since 2026-08-26 and this document did not until 2026-09-30
        // (stability report D-6 and round 2 point 8, owner's decision, a breaking change in 0.x). Until then the naming
        // policy was handed to the converter as well, so "group" said "configuration" while every
        // value in the snapshot beside it said "OwnProcess" and "Running". A script reading both
        // had to know which document was in which convention.
        Converters = { new JsonStringEnumConverter() },

        // ESCAPES SINCE 2026-10-05 (stability report C-1). What stood here said a pipeline would
        // otherwise be "comparing escapes", and that was never true: every JSON parser hands back
        // the character an escape names, so a step comparing display names compares the names
        // either way. What escapes do change is the bytes, and those are the half a console code
        // page could break - AsciiJson carries the measurement.
        Encoder = AsciiJson.Encoder
    };

    internal static string Render(SnapshotDiff diff) =>
        AsciiJson.Serialize(
            new DiffDocument(
                diff.Drifted,
                new CaveatsDocument(
                    diff.Caveats.ElevationDiffers,
                    diff.Caveats.MachineDiffers,
                    diff.Caveats.OperatingSystemDiffers,
                    diff.Caveats.ToolVersionDiffers,
                    diff.Caveats.LanguageDiffers,
                    diff.Caveats.AccountDiffers,
                    diff.Caveats.NotKnown,
                    diff.Caveats.FileVersionSourceDiffers),
                [.. diff.Added.Select(Presence)],
                [.. diff.Removed.Select(Presence)],
                [.. diff.Uncertain.Select(Presence)],
                [.. diff.Changed.Select(Changed)],
                [.. diff.NotFullyCompared.Select(Changed)],
                diff.NeitherRead,
                new LeftOutDocument(diff.LeftOut.Earlier, diff.LeftOut.Later)),
            Options);

    private static PresenceDocument Presence(EntryPresence entry) =>
        new(entry.ServiceName, entry.DisplayName);

    private static ChangedDocument Changed(ChangedEntry entry) =>
        new(
            entry.ServiceName,
            entry.DisplayName,
            [.. entry.Differences.Select(difference => new FieldDocument(
                difference.Field, difference.Group, difference.Before, difference.After))],
            entry.Incomparable);

    /// <param name="Differs">
    /// One boolean so a step does not have to add four lists up to find out whether anything
    /// was found. It answers the same question --exit-code answers, for whoever is reading
    /// the document rather than the code - and since 2026-09-30 that question is about
    /// CONFIGURATION: an entry that differs only in running state is still under
    /// <paramref name="Changed"/> and no longer makes this true (stability report D-2).
    /// </param>
    /// <param name="NotFullyCompared">
    /// Nothing differed, and something could not be looked at. Apart from
    /// <paramref name="Changed"/> on purpose: a step that treated these as drift would fail
    /// over one snapshot having been taken without elevation.
    /// </param>
    /// <param name="InstancesLeftOut">
    /// Per-user session copies neither side was compared on, counted per side. Added 2026-09-30
    /// (D-1) - an addition, so nothing a script already reads changes.
    /// </param>
    private sealed record DiffDocument(
        bool Differs,
        CaveatsDocument Caveats,
        IReadOnlyList<PresenceDocument> Added,
        IReadOnlyList<PresenceDocument> Removed,
        IReadOnlyList<PresenceDocument> Uncertain,
        IReadOnlyList<ChangedDocument> Changed,
        IReadOnlyList<ChangedDocument> NotFullyCompared,
        IReadOnlyList<string> NeitherRead,
        LeftOutDocument InstancesLeftOut);

    /// <param name="NotKnown">
    /// Metadata one of the two files does not carry, as the field names the file uses - a
    /// version four snapshot names both fields version five added. A flag beside a name here is
    /// false because nobody could tell, not because the two agree. The three fields after the
    /// first four were added 2026-09-30 (D-5), so nothing a script already reads changed type.
    /// </param>
    /// <param name="FileVersionSourceDiffers">
    /// Added 2026-10-06 (package SB, schema six) and LAST rather than beside the other flags, so the
    /// document only grew at its end. True when one side is schema six and the other older: file
    /// versions were then not compared on any entry.
    /// </param>
    private sealed record CaveatsDocument(
        bool ElevationDiffers,
        bool MachineDiffers,
        bool OperatingSystemDiffers,
        bool ToolVersionDiffers,
        bool LanguageDiffers,
        bool AccountDiffers,
        IReadOnlyList<string> NotKnown,
        bool FileVersionSourceDiffers);

    private sealed record LeftOutDocument(int Earlier, int Later);

    private sealed record PresenceDocument(string ServiceName, string DisplayName);

    private sealed record ChangedDocument(
        string ServiceName,
        string DisplayName,
        IReadOnlyList<FieldDocument> Differences,
        IReadOnlyList<string> Incomparable);

    /// <param name="Before">Null means the field was not there, which is itself the change.</param>
    private sealed record FieldDocument(
        string Field, DifferenceGroup Group, string? Before, string? After);
}
