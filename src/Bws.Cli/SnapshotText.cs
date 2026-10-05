using System.Text.Json;
using Bws.Core.Snapshots;

namespace Bws.Cli;

/// <summary>
/// What the tool says after writing a snapshot.
///
/// The document this command produces is the file, so what lands on the data channel is a
/// receipt: where it went and what is in it. Short on purpose - somebody who wants the
/// contents opens the file, and repeating any of it here would be a second copy of a thing
/// that is meant to have exactly one.
/// </summary>
internal static class SnapshotText
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // The path a snapshot went to is the one string here that can hold letters outside ASCII,
        // and the encoder that stood here by default already escaped them - along with every
        // apostrophe. Through AsciiJson since 2026-10-05 like every other document, so the receipt
        // and the listing spell the same path the same way.
        Encoder = AsciiJson.Encoder
    };

    /// <summary>
    /// The receipt, for a person or for a script.
    ///
    /// The machine readable form carries the absolute path rather than what was typed,
    /// because the next thing a script does is open the file, and a relative path is only
    /// meaningful next to a working directory it has no way to know.
    /// </summary>
    internal static string Render(Snapshot snapshot, string path, bool asJson)
    {
        var full = System.IO.Path.GetFullPath(path);

        if (asJson)
        {
            return AsciiJson.Serialize(
                new SnapshotReceipt(
                    full,
                    snapshot.Entries.Count,
                    snapshot.Metadata.TakenAt,
                    snapshot.Metadata.Machine,
                    snapshot.Metadata.Elevated,
                    snapshot.Metadata.SchemaVersion),
                Options);
        }

        var written = snapshot.Entries.Count == 1
            ? Texts.Of("cli.snapshot.written.one", full, snapshot.Entries.Count)
            : Texts.Of("cli.snapshot.written.many", full, snapshot.Entries.Count);

        // Said on the receipt and not only in the file, because it is the one thing about a
        // snapshot that changes what a later comparison means. Measured: without elevation
        // the manager hands over 807 entries where an elevated session sees 810, so two
        // files taken differently differ on three entries nobody touched.
        return snapshot.Metadata.Elevated
            ? written
            : written + Environment.NewLine + Texts.Of("cli.snapshot.notElevated");
    }

    private sealed record SnapshotReceipt(
        string Path,
        int Entries,
        DateTimeOffset TakenAt,
        string Machine,
        bool Elevated,
        int SchemaVersion);
}
