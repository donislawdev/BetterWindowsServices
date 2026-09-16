namespace Bws.Gui.ViewModels;

// WHAT THE GRID IS HANDED, AWAY FROM WHAT THE FILE HOLDS - split out 2026-08-25, when the size
// ratchet asked ColumnLayout.cs for a seam and `G` needed one more field in the schema.
//
// The seam is a subject rather than a line count, and it is the same one the file itself draws
// three times: everything above this used to be about a FILE - what is in it, how it is written,
// what came back from reading it. This is about a GRID - what to show given a file that may be
// missing, stale or contradictory, reconciled with the columns this build actually has. Nothing
// here parses or renders anything.
/// <summary>
/// A layout file reconciled with the columns this build actually has.
///
/// <b>Four degenerate files, all of them ordinary, and each answered here rather than at the
/// grid.</b> `docs/04` names them at `S6d`: a layout holding a column that has since been taken
/// away, a layout written before a column was added, a layout with every column hidden, and a
/// layout from a schema this build does not read. The last one never reaches this class - it is
/// refused while reading, because guessing at its fields is the fault it exists to prevent.
///
/// <b>What comes out is always complete and always usable</b>: every column this build has,
/// exactly once, in a display order, with at least one of them shown. The grid is then handed
/// something it cannot be broken by, and everything that had to be left out is named.
/// </summary>
internal sealed record ColumnPlan(ColumnLayout Layout, IReadOnlyList<string> Ignored, bool NoneWasShown)
{
    /// <summary>
    /// Works out what to show, from a layout that may be missing, stale or contradictory.
    ///
    /// <b>A column the file never mentioned joins at the end with the state it would have had if
    /// there were no file.</b> That is the case of a build that added a column since the layout
    /// was written, and the alternative - dropping it - is a feature that silently does not exist
    /// for everybody who used the previous version.
    ///
    /// <b>A file with every column hidden is refused rather than obeyed.</b> Eight hundred rows of
    /// nothing above a count line still saying eight hundred is the empty rectangle complaint 9 of
    /// `docs/11` is about, and <see cref="ColumnChoice.MayHide"/> already refuses to let anybody
    /// reach it through the window - so a file that asks for it was hand edited, and the honest
    /// answer is the usual six and a sentence saying so.
    /// </summary>
    internal static ColumnPlan Of(ColumnLayout? layout, EntryScope scope)
    {
        if (layout is null)
        {
            return new ColumnPlan(ColumnLayout.DefaultFor(scope), [], NoneWasShown: false);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<KeptColumn>();
        var ignored = new List<string>();

        foreach (var column in layout.Columns)
        {
            // A name this build does not have, or the same column twice. Both are files somebody
            // edited or files older than a rename, and both leave a column out of the order -
            // which is a thing the person is told about rather than a thing they notice later.
            if (Columns.Of(column.Id) is null || !seen.Add(column.Id))
            {
                ignored.Add(column.Id);

                continue;
            }

            kept.Add(column);
        }

        // The scope decides what "the state it would have had" means, because a column empty in one
        // list is not empty in the other - Column.OffAtFirstIn.
        foreach (var column in Columns.All.Where(column => seen.Add(column.Id)))
        {
            kept.Add(new KeptColumn(column.Id, column.ShownAtFirstIn(scope), Width: null));
        }

        var noneWasShown = !kept.Exists(column => column.Shown);

        if (noneWasShown)
        {
            kept = [.. kept.Select(column =>
                column with { Shown = Columns.Of(column.Id)!.ShownAtFirstIn(scope) })];
        }

        // THE ORDER TRAVELS WITH THE COLUMNS, and leaving it behind here is a fault that looks
        // exactly like a feature nobody built: the file holds the sort, the window reads the file,
        // and the list comes up unsorted with nothing anywhere saying why. Found 2026-08-25 by the
        // guard that opens a window twice.
        //
        // AN ORDER THE FILE NEVER MENTIONED GETS THE ONE IT WOULD HAVE HAD IF THERE WERE NO FILE,
        // since 2026-09-15 - the same rule the loop above applies to a column the file never
        // mentioned. Until then a null here went to the grid as "take every order off", so a
        // layout written before 2026-09-02 - when a fresh profile started opening sorted - kept
        // opening the list in the manager's order for as long as it existed, which is alphabetical
        // by a column that is off by default and looks like no order at all. The owner's own
        // profile was one of those. Seen on a screenshot, not in any test: every test window read
        // a fixture that named no order either.
        //
        // Only when the file says nothing. An order it does name still wins, whether or not that
        // column is on - ListSorting.By refuses to apply one by a hidden column and the file keeps
        // it for the day the column comes back, and that is unchanged.
        return new ColumnPlan(
            new ColumnLayout(kept, layout.Sort ?? ColumnLayout.DefaultFor(scope).Sort),
            ignored,
            noneWasShown);
    }
}
