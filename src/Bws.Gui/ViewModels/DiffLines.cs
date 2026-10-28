using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One row of the comparison table: an entry, one thing about it, and what that thing was on the
/// earlier side and is on the later one.
/// </summary>
/// <param name="ServiceName">The identity the two sides were matched by - `ADR-14`.</param>
/// <param name="What">
/// The field, in the words the list's column heading uses for it - or, for an entry that came or
/// went, a sentence saying so.
/// </param>
public sealed record DiffLine(string ServiceName, string DisplayName, string What, string Before, string After)
{
    /// <summary>
    /// What kind of row this is - the question the filter over the table asks (W2-b). Kept beside the
    /// words rather than read back out of them, because the words are a translation and this is not.
    /// </summary>
    internal DiffLineKind Kind { get; init; }

    /// <summary>
    /// The snapshot's own name for the field a row is about, or null for a row about a whole entry -
    /// what the filter groups the changed rows by.
    /// </summary>
    internal string? Field { get; init; }
}

/// <summary>The five kinds of row the comparison table holds, in the order <see cref="DiffLines.Of"/> lays them out.</summary>
internal enum DiffLineKind
{
    Added,
    Removed,
    Uncertain,
    Changed,
    NotCompared
}

/// <summary>
/// The table under a comparison: one row per field that differs, one per entry that came, went or
/// one side could not see, and one per field that could not be compared on an entry.
///
/// <b>A row per FIELD rather than per entry, owner's decision of 2026-10-14 (W-b).</b> An entry with
/// three changed fields is three rows, each sortable and readable on its own - the shape a table
/// is good at - rather than one row holding a paragraph. The command line's report groups the same
/// facts under the entry, and both come from the same <see cref="SnapshotDiff"/> in the same order.
///
/// <b>The field is named by the list's own heading where the list has a column for it.</b> The
/// window's column ids ARE the snapshot's field names - <c>startType</c> is both - so "Startup type"
/// here is the word somebody reads above the list. A field with no column (a privilege list, the
/// security descriptor) keeps the name the file gives it.
/// </summary>
internal static class DiffLines
{
    internal static IReadOnlyList<DiffLine> Of(SnapshotDiff diff, DiffSides sides) =>
    [
        .. diff.Added.Select(entry => Presence(
            entry, DiffLineKind.Added, Added(sides), Texts.Of("gui.diff.row.notThere"), Texts.Of("gui.diff.row.there"))),
        .. diff.Removed.Select(entry => Presence(
            entry, DiffLineKind.Removed, Removed(sides), Texts.Of("gui.diff.row.there"), Texts.Of("gui.diff.row.notThere"))),

        // Its own rows rather than among the removals or the additions, for the reason the command
        // line gives its own heading: "these are gone" and "one side could not see them" are
        // different sentences, and only one of them is a fact.
        .. diff.Uncertain.Select(entry => Presence(
            entry, DiffLineKind.Uncertain, Texts.Of("gui.diff.row.uncertain"), Texts.Of("gui.diff.row.cannotTell"), Texts.Of("gui.diff.row.cannotTell"))),

        // Seen on one side only too, and filtered with those - but the manager says it still holds these,
        // so the row says so rather than "cannot tell" (logic report S03, 2026-10-27).
        .. diff.Unlisted.Select(entry => Presence(
            entry, DiffLineKind.Uncertain, Texts.Of("gui.diff.row.unlisted"), Texts.Of("gui.diff.row.there"), Texts.Of("gui.diff.row.notListed"))),
        .. diff.Changed.Concat(diff.NotFullyCompared).SelectMany(Fields)
    ];

    /// <summary>
    /// The heading the list puts over this field, or the field's own name when the list has no column
    /// for it.
    ///
    /// <b>The own name escaped like any value from a file.</b> Most names here are this build's own -
    /// the comparison walks its own fields - but a field NEITHER side read is a name both files wrote,
    /// and once both sides are files (the command line's two-file comparison, and the window's since
    /// W2) a pair of files decides it. The command line escapes every value it formats, and the
    /// window's texts do not, so the window escapes it here.
    /// </summary>
    internal static string Label(string field) =>
        Columns.Of(field) is { } column ? Texts.Of(column.LabelKey) : Printable.Of(field);

    /// <summary>
    /// What an entry that came is called, by what it came against - "since the snapshot" is true of a
    /// machine read now and false of a second file, which may be another machine altogether.
    /// </summary>
    private static string Added(DiffSides sides) =>
        sides == DiffSides.TwoSnapshots ? Texts.Of("gui.diff.files.row.added") : Texts.Of("gui.diff.row.added");

    /// <summary>The same for an entry that went.</summary>
    private static string Removed(DiffSides sides) =>
        sides == DiffSides.TwoSnapshots ? Texts.Of("gui.diff.files.row.removed") : Texts.Of("gui.diff.row.removed");

    private static DiffLine Presence(EntryPresence entry, DiffLineKind kind, string what, string before, string after) =>
        new(Printable.Of(entry.ServiceName), Printable.Of(entry.DisplayName), what, before, after) { Kind = kind };

    private static IEnumerable<DiffLine> Fields(ChangedEntry entry)
    {
        var name = Printable.Of(entry.ServiceName);
        var shown = Printable.Of(entry.DisplayName);

        foreach (var row in entry.Differences.SelectMany(Rows))
        {
            yield return new DiffLine(name, shown, What(row.Difference), row.Before, row.After)
            {
                Kind = DiffLineKind.Changed,
                Field = row.Difference.Field
            };
        }

        // Named, never counted as a change - the rule 8 argument the command line makes at the same
        // place. A field one side holds no value for says nothing about whether it moved.
        foreach (var field in entry.Incomparable)
        {
            yield return new DiffLine(
                name, shown, Label(field), Texts.Of("gui.diff.row.notCompared"), Texts.Of("gui.diff.row.notCompared"))
            {
                Kind = DiffLineKind.NotCompared,
                Field = field
            };
        }
    }

    /// <summary>
    /// The field, marked when it is about what was running rather than how the machine is set up -
    /// the distinction `--exit-code` draws since 2026-09-30, and the one a person most needs to see.
    /// </summary>
    private static string What(FieldDifference difference) =>
        difference.Group == DifferenceGroup.RunningState
            ? Texts.Of("gui.diff.row.state", Label(difference.Field))
            : Label(difference.Field);

    /// <summary>
    /// The before and the after of each row one difference makes: one row, or for the triggers a row per
    /// trigger only one side holds, the other side the word for nothing (backlog 625, owner's decision "a
    /// line per trigger", 2026-10-27) - the command line's lines as rows, so the filter, the sort and the
    /// export take them without a mechanism of their own. Until then a trigger row held the two whole lists
    /// as JSON, about three thousand characters for an entry losing one of thirteen.
    ///
    /// <b>The two whole lists are still the row when there is no trigger to name</b> - nothing against an
    /// empty list, which a file can hold. A difference with no row under it would be silence. And when a
    /// side holds a trigger that is not one to say, which the core answers with no rows at all (since the
    /// review of PR #98).
    /// </summary>
    private static IEnumerable<ChangedRow> Rows(FieldDifference difference)
    {
        if (difference.Triggers is not { } change || change.Removed.Count + change.Added.Count == 0)
        {
            return [new ChangedRow(difference, Value(difference.Before), Value(difference.After))];
        }

        return change.Removed.Select(trigger => new ChangedRow(difference, TriggerLines.Line(trigger), Value(null)))
            .Concat(change.Added.Select(trigger => new ChangedRow(difference, Value(null), TriggerLines.Line(trigger))));
    }

    /// <summary>One row a difference makes, before it is a <see cref="DiffLine"/>.</summary>
    private readonly record struct ChangedRow(FieldDifference Difference, string Before, string After);

    /// <summary>A missing value in a word rather than a blank cell, which reads as the window running out of room.</summary>
    private static string Value(string? value) =>
        value is null ? Texts.Of("gui.diff.row.nothing") : Printable.Of(value);
}
