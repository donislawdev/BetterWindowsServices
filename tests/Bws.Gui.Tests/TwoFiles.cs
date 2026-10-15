using System.IO;
using Bws.Core;
using Bws.Core.Snapshots;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Snapshots made for a test and written where the window would open them - W2's comparison of two
/// files, its filter and its export all start from a pair of these.
///
/// <b>Metadata a snapshot of this build carries in full</b>, the operating system's version and the
/// language of the names included, so a comparison of two of them says only what the test changed -
/// without those two every pair would carry the caveat about an older file.
/// </summary>
internal static class TwoFiles
{
    /// <summary>A snapshot of the entries given, taken the way the side says.</summary>
    internal static Snapshot Of(Side side, params ScmEntry[] entries) =>
        new(
            new SnapshotMetadata
            {
                SchemaVersion = Snapshot.CurrentSchemaVersion,
                Machine = side.Machine,
                OperatingSystem = "Microsoft Windows NT 10.0.26200.0",
                OperatingSystemVersion = "10.0.26200.6899",
                NamesLanguage = "en-US",
                TakenAt = DateTimeOffset.UnixEpoch.AddHours(side.Hour),
                TakenBy = side.Machine + "\\somebody",
                Elevated = side.Elevated,
                Note = side.Note,
                Tool = "0.4.0"
            },
            [.. entries.Select(EntryDocument.From)]);

    /// <summary>The snapshot written as a file in the folder, the way the command line writes one.</summary>
    internal static string Written(string folder, string name, Snapshot snapshot)
    {
        var path = Path.Combine(folder, name);

        File.WriteAllText(path, SnapshotJson.Render(snapshot));

        return path;
    }

    /// <summary>The sheet the window shows for two snapshots, named earlier.json and later.json.</summary>
    internal static DiffSheet Sheet(Snapshot earlier, Snapshot later)
    {
        Assert.True(SnapshotDiff.TryBetween(earlier, later, out var diff, out var failure), failure);

        return DiffSheet.Of(new DiffSheet.Opened("earlier.json", earlier.Metadata), new DiffSheet.Opened("later.json", later.Metadata), diff);
    }
}

/// <summary>How a test snapshot was taken - a class with defaults rather than a record, because a
/// record of four is a constructor of four and the shape guard's ceiling for a test signature is five.</summary>
internal sealed class Side
{
    public string Machine { get; init; } = "TESTBOX";

    public int Hour { get; init; }

    public bool Elevated { get; init; } = true;

    public string? Note { get; init; }
}
