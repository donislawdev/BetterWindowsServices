using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The comparison view's cells for the component catalogue - GUI rule 4: every view in every state it
/// has, from the real engine rather than written by hand.
/// </summary>
public static partial class Catalogue
{
    /// <summary>
    /// Three comparisons of the catalogue's own specimens, made by <see cref="SnapshotDiff"/> exactly as
    /// the window makes one.
    ///
    /// <b>Data</b> is the ordinary case - a startup type and a state that moved, one entry gone.
    /// <b>Empty</b> is a snapshot against itself, which is a sentence rather than a table.
    /// <b>Extreme</b> is everything a comparison can admit at once: another machine, a side read
    /// without administrator rights, a field refused to both sides, the longest name and the longest
    /// description the catalogue holds - and since W2 it is TWO SNAPSHOTS, with the table narrowed
    /// by "Show", because two files are where another machine comes from and the narrowed state is
    /// the one whose line says how much is hidden.
    ///
    /// <b>Snapshots built from fixed metadata rather than read from the session</b>, because the
    /// catalogue reads nothing from the machine it runs on - the README says so of `--catalogue`.
    /// </summary>
    internal static (DiffSheet Data, DiffSheet Empty, DiffSheet Extreme) Comparisons()
    {
        var specimens = Specimens();
        var earlier = Taken(specimens, "CATALOGUE", elevated: true);

        var moved = specimens
            .Where(entry => entry.ServiceName != "Ghost")
            .Select(entry => entry.ServiceName == "Spooler"
                ? entry with { StartType = Reading<StartType>.Present(StartType.Manual), Status = EntryStatus.Stopped }
                : entry)
            .ToList();

        var refused = specimens
            .Select(entry => entry with
            {
                Description = entry.ServiceName == LongestSpecimen().ServiceName
                    ? Reading<string>.Present(LongDescription + " - and a sentence added since.")
                    : Reading<string>.Denied(15100, "The resource loader failed to find MUI file.")
            })
            .ToList();

        return (
            Sheet(earlier, Taken(moved, "CATALOGUE", elevated: true)),
            Sheet(earlier, earlier),
            Narrowed(Files(Taken(refused, "ANOTHER-MACHINE", elevated: false), Taken(refused.Select(Changed).ToList(), "CATALOGUE", elevated: true))));
    }

    /// <summary>
    /// The form before a snapshot (W2-d) in the states it has: a note typed, none, the machine being
    /// read for it with the save button off, and a window without administrator rights holding the
    /// longest text the catalogue has.
    /// </summary>
    private static Built[] SnapshotFormViews() =>
    [
        View(() => new SnapshotFormView(),
            data: Typed(elevated: true, "Before the October patch"),
            empty: Typed(elevated: true, string.Empty),
            loading: WhileRead(Typed(elevated: true, "Before the October patch")),
            extreme: Typed(elevated: false, LongDescription))
    ];

    private static SnapshotForm Typed(bool elevated, string note) => new(elevated) { Note = note };

    private static SnapshotForm WhileRead(SnapshotForm form)
    {
        form.Reading(busy: true);

        return form;
    }

    /// <summary>The last choice under "Show", so the cell shows a table narrowed and the line saying by how much.</summary>
    private static DiffSheet Narrowed(DiffSheet sheet)
    {
        sheet.Filter.Choose(sheet.Filter.Choices[^1]);

        return sheet;
    }

    /// <summary>
    /// The comparison view and the table inside it, each over the three comparisons above.
    ///
    /// <b>Over a comparison rather than over a model</b> - a comparison is worked out once and never
    /// read again, so neither has a loading or a wrong state: a file that is not a snapshot never
    /// becomes one, it is a sentence at the foot of the window. Out of <see cref="Views"/> for the
    /// reason <see cref="PlanViews"/> is: that method stands near the share of its ceiling the shape
    /// guard counts.
    /// </summary>
    private static Built[] DiffViews((DiffSheet Data, DiffSheet Empty, DiffSheet Extreme) compared) =>
    [
        View(() => new DiffView(), data: compared.Data, empty: compared.Empty, extreme: compared.Extreme),

        // No empty cell: when nothing differs there is no table to narrow, and the row is not there at
        // all - a cell would be a rectangle CatalogueViewGuards rightly refuses.
        View(() => new DiffFilterRow(), data: compared.Data, extreme: compared.Extreme),
        View(() => new DiffTable(), data: compared.Data, empty: compared.Empty, extreme: compared.Extreme)
    ];

    /// <summary>The longest specimen with its description moved, so the extreme cell has a long value in a cell.</summary>
    private static ScmEntry Changed(ScmEntry entry) =>
        entry.ServiceName == LongestSpecimen().ServiceName
            ? entry with { Description = Reading<string>.Present(LongDescription) }
            : entry;

    private static Snapshot Taken(IReadOnlyList<ScmEntry> entries, string machine, bool elevated) =>
        new(
            new SnapshotMetadata
            {
                SchemaVersion = Snapshot.CurrentSchemaVersion,
                Machine = machine,
                OperatingSystem = "Microsoft Windows NT 10.0.26200.0",

                // Both, as every snapshot this build writes carries them - without them the data cell
                // would show a caveat about an older file that no comparison in the window meets.
                OperatingSystemVersion = "10.0.26200.6899",
                NamesLanguage = "en-US",
                TakenAt = new DateTimeOffset(2026, 10, 14, 9, 30, 0, TimeSpan.Zero),
                TakenBy = machine + "\\administrator",
                Elevated = elevated,
                Note = elevated ? null : "Before the October patch, taken by somebody else.",
                Tool = "catalogue"
            },
            [.. entries.Select(EntryDocument.From)]);

    private static DiffSheet Sheet(Snapshot earlier, Snapshot later) =>
        SnapshotDiff.TryBetween(earlier, later, out var diff, out var failure)
            ? DiffSheet.Of(earlier.Metadata, diff)
            : throw new InvalidOperationException("The catalogue's own snapshots would not compare: " + failure);

    /// <summary>Two of the catalogue's snapshots as two files, the second name long enough to wrap.</summary>
    private static DiffSheet Files(Snapshot earlier, Snapshot later) =>
        SnapshotDiff.TryBetween(earlier, later, out var diff, out var failure)
            ? DiffSheet.Of(
                new DiffSheet.Opened("another-machine.json", earlier.Metadata),
                new DiffSheet.Opened("catalogue, kept beside the audit of the October patch for as long as the audit is kept.json", later.Metadata),
                diff)
            : throw new InvalidOperationException("The catalogue's own snapshots would not compare: " + failure);
}
