using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Everything that makes a comparison less than exact, one sentence each - shown ABOVE the table.
///
/// <b>First, not last, for the command line's reason:</b> somebody who reads the differences before
/// learning that one side was read without administrator rights has already believed something.
/// The same facts the command line says, in the window's own words and its own language file.
/// </summary>
internal static class DiffCaveats
{
    internal static IReadOnlyList<string> Of(SnapshotDiff diff)
    {
        var caveats = diff.Caveats;

        (bool Says, Func<string> Line)[] lines =
        [
            (caveats.ElevationDiffers, () => Texts.Of("gui.diff.caveat.elevation")),
            (caveats.MachineDiffers, () => Texts.Of("gui.diff.caveat.machine")),
            (caveats.OperatingSystemDiffers, () => Texts.Of("gui.diff.caveat.operatingSystem")),
            (caveats.ToolVersionDiffers, () => Texts.Of("gui.diff.caveat.tool")),
            (caveats.LanguageDiffers, () => Texts.Of("gui.diff.caveat.language")),
            (caveats.AccountDiffers, () => Texts.Of("gui.diff.caveat.account")),
            (caveats.FileVersionSourceDiffers, () => Texts.Of("gui.diff.caveat.fileVersion")),
            (caveats.NotKnown.Count > 0, () => Texts.Of("gui.diff.caveat.notKnown", string.Join(", ", caveats.NotKnown))),
            (diff.NeitherRead.Count > 0, () => Texts.Of("gui.diff.caveat.neitherRead", string.Join(", ", diff.NeitherRead.Select(DiffLines.Label)))),
            (diff.LeftOut.Any, () => Texts.Of("gui.diff.caveat.instancesLeftOut", diff.LeftOut.Earlier, diff.LeftOut.Later)),
            (diff.NotFullyCompared.Count > 0, () => NotCompared(diff.NotFullyCompared.Count))
        ];

        return [.. lines.Where(line => line.Says).Select(line => line.Line()), .. diff.RefusedByBoth.Select(Refused)];
    }

    /// <summary>
    /// How many entries carry a row saying "not compared" - said above the table as well as in it, so
    /// somebody who reads only the top does not come away with "nothing changed".
    /// </summary>
    private static string NotCompared(int entries) =>
        entries == 1
            ? Texts.Of("gui.diff.caveat.notCompared.one", entries)
            : Texts.Of("gui.diff.caveat.notCompared.many", entries);

    /// <summary>
    /// A field Windows refused to both sides, once, with the entries it refused it on - backlog 559,
    /// the same line the command line prints since 2026-10-14.
    /// </summary>
    private static string Refused(RefusedField field)
    {
        var entries = string.Join(", ", field.Entries.Select(Printable.Of));

        return field.Entries.Count == 1
            ? Texts.Of("gui.diff.caveat.refusedByBoth.one", DiffLines.Label(field.Field), field.Entries.Count, entries)
            : Texts.Of("gui.diff.caveat.refusedByBoth.many", DiffLines.Label(field.Field), field.Entries.Count, entries);
    }
}
