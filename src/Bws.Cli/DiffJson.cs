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
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },

        // Characters as themselves. Display names on this machine are translated, and a
        // pipeline comparing them against anything would otherwise be comparing escapes.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static string Render(SnapshotDiff diff) =>
        JsonSerializer.Serialize(
            new DiffDocument(
                diff.Any,
                new CaveatsDocument(
                    diff.Caveats.ElevationDiffers,
                    diff.Caveats.MachineDiffers,
                    diff.Caveats.OperatingSystemDiffers,
                    diff.Caveats.ToolVersionDiffers),
                [.. diff.Added.Select(Presence)],
                [.. diff.Removed.Select(Presence)],
                [.. diff.Uncertain.Select(Presence)],
                [.. diff.Changed.Select(Changed)],
                [.. diff.NotFullyCompared.Select(Changed)],
                diff.NeitherRead),
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
    /// the document rather than the code.
    /// </param>
    /// <param name="NotFullyCompared">
    /// Nothing differed, and something could not be looked at. Apart from
    /// <paramref name="Changed"/> on purpose: a step that treated these as drift would fail
    /// over one snapshot having been taken without elevation.
    /// </param>
    private sealed record DiffDocument(
        bool Differs,
        CaveatsDocument Caveats,
        IReadOnlyList<PresenceDocument> Added,
        IReadOnlyList<PresenceDocument> Removed,
        IReadOnlyList<PresenceDocument> Uncertain,
        IReadOnlyList<ChangedDocument> Changed,
        IReadOnlyList<ChangedDocument> NotFullyCompared,
        IReadOnlyList<string> NeitherRead);

    private sealed record CaveatsDocument(
        bool ElevationDiffers,
        bool MachineDiffers,
        bool OperatingSystemDiffers,
        bool ToolVersionDiffers);

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
