using System.Globalization;
using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the window shows in place of the list when two sides are compared - `D2`, in the window since
/// 2026-10-14 (`docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md`, W1 for a snapshot against this machine,
/// W2 for two snapshots).
///
/// <b>Sentences and rows, worked out once when the comparison arrives, and nothing that can change
/// after</b> - except which rows are on screen, which is the person's view of the comparison rather
/// than the comparison (<see cref="Filter"/>). A comparison is a statement about two moments, and both
/// have passed by the time it is on screen - so unlike the list, nothing here is kept in step with the
/// machine. Comparing again is asking again.
///
/// <b>The same engine and the same order as the command line</b>, so a person and a script reading
/// the same pair never disagree about what was found: what makes the comparison less than exact
/// first (<see cref="Caveats"/>), then what was added, removed and changed. What differs is only
/// how it is laid out - a table a person can sort rather than an indented report.
///
/// <b>Every value from either side goes through <see cref="Printable"/></b>, because a side is a file
/// somebody else may have written - the decision C-4 made for the terminal, applied where the same
/// file is now read. Since W2 both sides can be such a file, and both are escaped.
/// </summary>
public sealed class DiffSheet
{
    /// <summary>The heading over the comparison - what was set side by side, in words (<see cref="DiffSides"/>).</summary>
    public required string Title { get; init; }

    /// <summary>The sides as the head of the view describes them - one for a snapshot against this machine, two for two snapshots.</summary>
    public required IReadOnlyList<DiffSide> Sides { get; init; }

    /// <summary>How many entries were added, removed and changed - the command line's summary line.</summary>
    public required string Counts { get; init; }

    /// <summary>Everything that makes this comparison less than exact, one sentence each, first on screen.</summary>
    public required IReadOnlyList<string> Caveats { get; init; }

    /// <summary>Every row of the table - <see cref="DiffLines"/> says what one row is.</summary>
    public required IReadOnlyList<DiffLine> Lines { get; init; }

    /// <summary>Which of the rows are on screen - all of them until somebody chooses otherwise.</summary>
    public required DiffFilter Filter { get; init; }

    /// <summary>The heading over the earlier side's values.</summary>
    public required string BeforeHeading { get; init; }

    /// <summary>The heading over the later side's values.</summary>
    public required string AfterHeading { get; init; }

    /// <summary>
    /// The sentence saying nothing differs, or empty when there is something to show - a table with
    /// no rows is a rectangle, and `docs/11` 3.8 asks for a sentence instead.
    /// </summary>
    public required string Same { get; init; }

    /// <summary>The sheet for a snapshot somebody opened, against this machine read now.</summary>
    internal static DiffSheet Of(SnapshotMetadata earlier, SnapshotDiff diff)
    {
        ArgumentNullException.ThrowIfNull(earlier);
        ArgumentNullException.ThrowIfNull(diff);

        const DiffSides between = DiffSides.WithThisMachine;
        var lines = DiffLines.Of(diff, between);
        var machine = Printable.Of(earlier.Machine);
        var by = Printable.Of(earlier.TakenBy);
        var about = earlier.Elevated
            ? Texts.Of("gui.diff.about.elevated", machine, When(earlier.TakenAt), by)
            : Texts.Of("gui.diff.about.unelevated", machine, When(earlier.TakenAt), by);

        return new DiffSheet
        {
            Title = Texts.Of("gui.diff.title"),
            Sides = [new DiffSide(about, Noted(earlier))],
            Counts = Counted(diff),
            Caveats = DiffCaveats.Of(diff, between),
            Lines = lines,
            Filter = new DiffFilter(lines),
            BeforeHeading = Texts.Of("gui.diff.column.before"),
            AfterHeading = Texts.Of("gui.diff.column.after"),
            Same = diff.Reported ? string.Empty : Texts.Of("gui.diff.same")
        };
    }

    /// <summary>
    /// The sheet for two snapshots somebody opened, the earlier one already on the left - the caller
    /// orders them by when they were taken (W2-a), and the file names say which file is which.
    /// </summary>
    internal static DiffSheet Of(Opened earlier, Opened later, SnapshotDiff diff)
    {
        ArgumentNullException.ThrowIfNull(earlier);
        ArgumentNullException.ThrowIfNull(later);
        ArgumentNullException.ThrowIfNull(diff);

        const DiffSides between = DiffSides.TwoSnapshots;
        var lines = DiffLines.Of(diff, between);

        return new DiffSheet
        {
            Title = Texts.Of("gui.diff.files.title"),
            Sides =
            [
                new DiffSide(Texts.Of("gui.diff.side.earlier", Printable.Of(earlier.File), Described(earlier.Metadata)), Noted(earlier.Metadata)),
                new DiffSide(Texts.Of("gui.diff.side.later", Printable.Of(later.File), Described(later.Metadata)), Noted(later.Metadata))
            ],
            Counts = Counted(diff),
            Caveats = DiffCaveats.Of(diff, between),
            Lines = lines,
            Filter = new DiffFilter(lines),
            BeforeHeading = Texts.Of("gui.diff.column.earlier"),
            AfterHeading = Texts.Of("gui.diff.column.later"),
            Same = diff.Reported ? string.Empty : Texts.Of("gui.diff.files.same")
        };
    }

    private static string Counted(SnapshotDiff diff) =>
        Texts.Of("gui.diff.counts", diff.Added.Count, diff.Removed.Count, diff.Changed.Count);

    /// <summary>One snapshot of a pair, the way the second half of its side's sentence says it - rights and all.</summary>
    private static string Described(SnapshotMetadata metadata)
    {
        var machine = Printable.Of(metadata.Machine);
        var by = Printable.Of(metadata.TakenBy);

        return metadata.Elevated
            ? Texts.Of("gui.diff.snapshot.elevated", machine, When(metadata.TakenAt), by)
            : Texts.Of("gui.diff.snapshot.unelevated", machine, When(metadata.TakenAt), by);
    }

    /// <summary>The note a snapshot carries, as a sentence, or empty when it carries none.</summary>
    private static string Noted(SnapshotMetadata metadata) =>
        string.IsNullOrEmpty(metadata.Note) ? string.Empty : Texts.Of("gui.diff.note", Printable.Of(metadata.Note));

    /// <summary>
    /// When the snapshot was taken, in this machine's time and this person's way of writing a date.
    ///
    /// <b>Local and in the person's own format, unlike the file name</b>, which has to sort and to read
    /// the same on every machine (SnapshotOnDisk.Target). This is read by one person on one screen.
    /// </summary>
    private static string When(DateTimeOffset takenAt) =>
        takenAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    /// <summary>A snapshot somebody opened, with the name of the file it came from.</summary>
    /// <param name="File">The file's name, not its path - the person chose the folder a moment ago.</param>
    internal sealed record Opened(string File, SnapshotMetadata Metadata);
}
