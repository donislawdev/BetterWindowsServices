using System.Globalization;
using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the window shows in place of the list when a snapshot is compared with this machine - `D2`,
/// in the window since 2026-10-14 (`docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md`, W1).
///
/// <b>Sentences and rows, worked out once when the comparison arrives, and nothing that can change
/// after.</b> A comparison is a statement about two moments, and both have passed by the time it is
/// on screen - so unlike the list, nothing here is kept in step with the machine. Comparing again is
/// asking again.
///
/// <b>The same engine and the same order as the command line</b>, so a person and a script reading
/// the same pair never disagree about what was found: what makes the comparison less than exact
/// first (<see cref="Caveats"/>), then what was added, removed and changed. What differs is only
/// how it is laid out - a table a person can sort rather than an indented report.
///
/// <b>Every value from either side goes through <see cref="Printable"/></b>, because one side is a
/// file somebody else may have written - the decision C-4 made for the terminal, applied where the
/// same file is now read.
/// </summary>
public sealed class DiffSheet
{
    /// <summary>Which snapshot, from which machine, when, by whom, and with what rights.</summary>
    public required string About { get; init; }

    /// <summary>The note the snapshot carries, as a sentence, or empty when it carries none.</summary>
    public required string Note { get; init; }

    /// <summary>How many entries were added, removed and changed - the command line's summary line.</summary>
    public required string Counts { get; init; }

    /// <summary>Everything that makes this comparison less than exact, one sentence each, first on screen.</summary>
    public required IReadOnlyList<string> Caveats { get; init; }

    /// <summary>The table - <see cref="DiffLines"/> says what one row is.</summary>
    public required IReadOnlyList<DiffLine> Lines { get; init; }

    /// <summary>
    /// The sentence saying nothing differs, or empty when there is something to show - a table with
    /// no rows is a rectangle, and `docs/11` 3.8 asks for a sentence instead.
    /// </summary>
    public required string Same { get; init; }

    /// <summary>The sheet for one comparison, the earlier side being the snapshot somebody opened.</summary>
    internal static DiffSheet Of(SnapshotMetadata earlier, SnapshotDiff diff)
    {
        ArgumentNullException.ThrowIfNull(earlier);
        ArgumentNullException.ThrowIfNull(diff);

        var machine = Printable.Of(earlier.Machine);
        var by = Printable.Of(earlier.TakenBy);

        return new DiffSheet
        {
            About = earlier.Elevated
                ? Texts.Of("gui.diff.about.elevated", machine, When(earlier.TakenAt), by)
                : Texts.Of("gui.diff.about.unelevated", machine, When(earlier.TakenAt), by),
            Note = string.IsNullOrEmpty(earlier.Note) ? string.Empty : Texts.Of("gui.diff.note", Printable.Of(earlier.Note)),
            Counts = Texts.Of("gui.diff.counts", diff.Added.Count, diff.Removed.Count, diff.Changed.Count),
            Caveats = DiffCaveats.Of(diff),
            Lines = DiffLines.Of(diff),
            Same = diff.Reported ? string.Empty : Texts.Of("gui.diff.same")
        };
    }

    /// <summary>
    /// When the snapshot was taken, in this machine's time and this person's way of writing a date.
    ///
    /// <b>Local and in the person's own format, unlike the file name</b>, which has to sort and to read
    /// the same on every machine (SnapshotOnDisk.Target). This is read by one person on one screen.
    /// </summary>
    private static string When(DateTimeOffset takenAt) =>
        takenAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
