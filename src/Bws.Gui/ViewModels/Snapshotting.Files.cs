using System.Globalization;
using System.IO;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The half of snapshots in the window that never reads the machine: what two files say when
/// compared with each other, and a comparison written to a file - W2 of
/// `docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md`, since 2026-10-15.
///
/// <b>A partial rather than more of Snapshotting.cs</b>, because a file that grows to hold every
/// errand of one type is the shape the size ratchet exists to stop. <b>What goes off the window's
/// thread stays in Snapshotting.cs</b> - CompareFilesAsync sends FilesCompared there - because that
/// file is where ConcurrencyGuards holds the argument for it, and a second file arguing the same thing
/// would be a list grown for nothing.
/// </summary>
public sealed partial class Snapshotting
{
    /// <summary>
    /// Writes the comparison on screen to a file - its sentences, then the rows the table shows in the
    /// order it shows them - and says how many went where, or why none did (W2-c). Hands back the
    /// sentence about a failure, for a guard to read, the way the list's export does.
    ///
    /// <b>The text is made here and written off the window's thread</b>, since a review of 2026-10-15:
    /// the rows and the sentences are the window's and cost nothing to join, and the write goes to a
    /// folder somebody chose, which may be a share on the network - the argument the snapshot's own
    /// write was moved off the thread for.
    /// </summary>
    /// <param name="rows">The rows as the table has them, sorted the way somebody sorted them.</param>
    internal async Task<string?> ExportAsync(string path, IReadOnlyList<DiffLine> rows)
    {
        if (Shown is not { } sheet)
        {
            return null;
        }

        var text = DiffExporting.AsCsv(sheet, rows, Exporting.Separator(CultureInfo.CurrentCulture));
        string? trouble;

        try
        {
            await WrittenOffTheWindow(path, Exporting.Mark + text).ConfigureAwait(true);

            trouble = null;
        }
        catch (IOException failure)
        {
            trouble = Texts.Of("gui.diff.export.failed", failure.Message);
        }
        catch (UnauthorizedAccessException failure)
        {
            trouble = Texts.Of("gui.diff.export.failed", failure.Message);
        }

        if (trouble is null)
        {
            _says().Did(DiffExporting.Done(rows.Count, Path.GetFileName(path)));
        }
        else
        {
            _says().CouldNotDo(trouble);
        }

        return trouble;
    }

    /// <summary>
    /// Both files read and compared off the window's thread - a snapshot may be up to the size ceiling,
    /// and a share on the network takes its time. The first file that is not a snapshot is the answer,
    /// in the sentence the window says about any file it cannot read.
    /// </summary>
    private static (DiffSheet.Opened? Earlier, DiffSheet.Opened? Later, SnapshotDiff? Diff, string? Trouble) FilesCompared(string first, string second)
    {
        if (!SnapshotOnDisk.TryLoad(first, out var one, out var refusal))
        {
            return (null, null, null, SnapshotWords.Of(refusal));
        }

        if (!SnapshotOnDisk.TryLoad(second, out var two, out refusal))
        {
            return (null, null, null, SnapshotWords.Of(refusal));
        }

        (string Path, Snapshot Snapshot) earlier = (first, one), later = (second, two);

        if (two.Metadata.TakenAt < one.Metadata.TakenAt)
        {
            (earlier, later) = (later, earlier);
        }

        return SnapshotDiff.TryBetween(earlier.Snapshot, later.Snapshot, out var diff, out var failure)
            ? (Opened(earlier), Opened(later), diff, null)
            : (null, null, null, Texts.Of("gui.snapshot.cannotCompare", failure ?? string.Empty));
    }

    /// <summary>One side of the pair as the sheet names it: the file's name and what the snapshot says about itself.</summary>
    private static DiffSheet.Opened Opened((string Path, Snapshot Snapshot) side) =>
        new(Path.GetFileName(side.Path), side.Snapshot.Metadata);
}
