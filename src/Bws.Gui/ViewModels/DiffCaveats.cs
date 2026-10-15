using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Everything that makes a comparison less than exact, one sentence each - shown ABOVE the table.
///
/// <b>First, not last, for the command line's reason:</b> somebody who reads the differences before
/// learning that one side was read without administrator rights has already believed something.
/// The same facts the command line says, in the window's own words and its own language file.
///
/// <b>Seven of them name the sides, and those come in two wordings since W2</b> - "the snapshot and
/// this window" against a machine read now, "the two snapshots" against a second file, which is the
/// wording the command line has always used for two files. The other four speak of "either side" and
/// are true of both.
/// </summary>
internal static class DiffCaveats
{
    internal static IReadOnlyList<string> Of(SnapshotDiff diff, DiffSides sides)
    {
        var caveats = diff.Caveats;
        var files = sides == DiffSides.TwoSnapshots;

        (bool Says, Func<string> Line)[] lines =
        [
            (caveats.ElevationDiffers, () => files ? Texts.Of("gui.diff.files.caveat.elevation") : Texts.Of("gui.diff.caveat.elevation")),
            (caveats.MachineDiffers, () => files ? Texts.Of("gui.diff.files.caveat.machine") : Texts.Of("gui.diff.caveat.machine")),
            (caveats.OperatingSystemDiffers, () => files ? Texts.Of("gui.diff.files.caveat.operatingSystem") : Texts.Of("gui.diff.caveat.operatingSystem")),
            (caveats.ToolVersionDiffers, () => files ? Texts.Of("gui.diff.files.caveat.tool") : Texts.Of("gui.diff.caveat.tool")),
            (caveats.LanguageDiffers, () => Texts.Of("gui.diff.caveat.language")),
            (caveats.AccountDiffers, () => files ? Texts.Of("gui.diff.files.caveat.account") : Texts.Of("gui.diff.caveat.account")),
            (caveats.FileVersionSourceDiffers, () => files ? Texts.Of("gui.diff.files.caveat.fileVersion") : Texts.Of("gui.diff.caveat.fileVersion")),
            (caveats.NotKnown.Count > 0, () => Texts.Of("gui.diff.caveat.notKnown", string.Join(", ", caveats.NotKnown))),
            (diff.NeitherRead.Count > 0, () => Texts.Of("gui.diff.caveat.neitherRead", string.Join(", ", diff.NeitherRead.Select(DiffLines.Label)))),
            (diff.LeftOut.Any, () => LeftOut(diff.LeftOut, files)),
            (diff.NotFullyCompared.Count > 0, () => NotCompared(diff.NotFullyCompared.Count))
        ];

        return [.. lines.Where(line => line.Says).Select(line => line.Line()), .. diff.RefusedByBoth.Select(Refused)];
    }

    /// <summary>Per-user copies left out, counted on each side by the name the side goes by.</summary>
    private static string LeftOut(InstancesLeftOut leftOut, bool files) =>
        files
            ? Texts.Of("gui.diff.files.caveat.instancesLeftOut", leftOut.Earlier, leftOut.Later)
            : Texts.Of("gui.diff.caveat.instancesLeftOut", leftOut.Earlier, leftOut.Later);

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
