using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// Turns the seventeen columns of `A8` into columns a DataGrid can show.
///
/// <b>Beside the window rather than in the view models, and that boundary is proved by a
/// build.</b> `Bws.Integration.Tests` references Bws.Gui deliberately WITHOUT UseWPF, so the fact
/// that no view model knows what WPF is gets checked by that project compiling at all - and a
/// DataGridColumn is as WPF as anything gets. What each column IS lives in
/// <see cref="Columns"/>, where it can be checked without a window.
///
/// <b>Nothing here binds ON a column, and that is a trap this window has already fallen into.</b>
/// Columns are not in the visual tree and inherit no data context, which is why a column header
/// could never bind to the view model and takes its text from a resource instead. So visibility is
/// SET from a subscription rather than bound, and the width and the header are read once out of
/// the theme and the language file.
/// </summary>
internal static class ListColumns
{
    /// <summary>
    /// Builds every column once and keeps them all, showing the ones that are on.
    ///
    /// <b>Built once and hidden rather than added and removed, which is a decision about what a
    /// person expects.</b> A column somebody widened, then turned off, then turned on again comes
    /// back the width they left it and in the place they dragged it to - because it is the same
    /// object, still carrying its Width and its DisplayIndex. Rebuilding it would silently throw
    /// both away, and the person would have no way to tell that from a bug.
    ///
    /// <b>The plan decides the width and the order, and the theme decides where a column starts</b>
    /// - which is `ADR-23` with the sentence added to it on 2026-08-11. A column nobody has ever
    /// dragged carries no width in the plan and takes the theme's, so changing the theme still
    /// reaches everybody who has not moved that particular edge.
    ///
    /// What comes back is the identifiers whose saved width could not be read, which is the one
    /// thing this can find out and the caller cannot: the plan carries text, and only here is
    /// there something that knows what a width means.
    /// </summary>
    internal static IReadOnlyList<string> Fill(DataGrid grid, ColumnBar bar, ColumnPlan plan)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(bar);
        ArgumentNullException.ThrowIfNull(plan);

        var refused = new List<string>();
        var built = new Dictionary<string, DataGridColumn>(StringComparer.Ordinal);

        foreach (var choice in bar.Choices)
        {
            var kept = plan.Layout.Columns.FirstOrDefault(column => column.Id == choice.Column.Id);
            var column = Build(choice.Column, grid, kept?.Width, refused);

            Show(column, choice.IsShown);

            choice.PropertyChanged += (_, changed) =>
            {
                if (changed.PropertyName == nameof(ColumnChoice.IsShown))
                {
                    Show(column, choice.IsShown);
                    Freeze(grid);
                }
            };

            grid.Columns.Add(column);
            built[choice.Column.Id] = column;
        }

        // THE ORDER, AFTER EVERY COLUMN EXISTS. Setting a display index moves whichever column is
        // already there, so a permutation only comes out right if it is applied to a complete
        // collection - and applied in increasing order of the position being claimed, which is what
        // walking the plan does.
        for (var position = 0; position < plan.Layout.Columns.Count; position++)
        {
            if (built.TryGetValue(plan.Layout.Columns[position].Id, out var column))
            {
                column.DisplayIndex = position;
            }
        }

        grid.Sorting += ListSorting.WhenAHeadingIsClicked;

        // BOTH THINGS THAT CAN MOVE THE LEFT EDGE, and dragging is the one that is easy to
        // forget: turning a column off is handled above, and reordering by dragging a heading
        // changes which column is leftmost without changing what is on.
        grid.ColumnDisplayIndexChanged += (_, _) => Freeze(grid);

        Freeze(grid);

        return refused;
    }

    /// <summary>
    /// Puts another layout onto columns that already exist.
    ///
    /// <b>Built 2026-08-19 for the scope switch, and it is NOT Fill run a second time.</b> Fill
    /// creates every column and subscribes three handlers - one per choice, one for sorting, one
    /// for reordering. Calling it again would give the grid a second set of columns and every
    /// handler twice, so a single click would be answered two or three times. What changes when a
    /// scope changes is only which columns are on, how wide and in what order, and all three are
    /// properties of columns that are already there.
    ///
    /// <b>Visibility is deliberately NOT here.</b> It arrives through <c>ColumnBar.Follow</c>,
    /// which sets the choices, which the handler Fill subscribed already turns into a Visibility -
    /// so there is exactly one road from "this column is on" to a column being on, whether the
    /// change came from a menu or from moving to another list.
    /// </summary>
    internal static IReadOnlyList<string> Reapply(DataGrid grid, ColumnPlan plan)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(plan);

        var refused = new List<string>();
        var built = new Dictionary<string, DataGridColumn>(StringComparer.Ordinal);

        foreach (var column in grid.Columns)
        {
            if (Columns.Of(column.SortMemberPath ?? string.Empty) is { } known)
            {
                built[known.Id] = column;
            }
        }

        foreach (var kept in plan.Layout.Columns)
        {
            if (built.TryGetValue(kept.Id, out var column) && Columns.Of(kept.Id) is { } known)
            {
                column.Width = Wide(grid, known, kept.Width, refused);
            }
        }

        // In increasing order of the position being claimed, for the reason Fill gives at length:
        // setting a display index MOVES whichever column is already there, so a permutation only
        // comes out right when it is walked forwards over a complete collection.
        for (var position = 0; position < plan.Layout.Columns.Count; position++)
        {
            if (built.TryGetValue(plan.Layout.Columns[position].Id, out var column))
            {
                column.DisplayIndex = position;
            }
        }

        Freeze(grid);

        // THE ORDER TRAVELS WITH THE LAYOUT, since 2026-08-25. Both callers of this - moving to
        // another scope and putting the usual columns back - are moments where the list somebody
        // is looking at is replaced, and an order left over from the one they left would be a
        // heading marked as sorting a list it no longer sorts.
        ListSorting.By(grid, plan.Layout.Sort);

        return refused;
    }

    /// <summary>
    /// What the grid looks like right now, in the form a file can hold.
    ///
    /// <b>Read off the grid rather than out of the view models, because the grid is where two of
    /// the three answers live.</b> Which columns are on is in both places, but the order somebody
    /// dragged a heading into and the width they dragged an edge to exist nowhere else - WPF holds
    /// them on the column object, and nothing tells a view model when they move.
    ///
    /// <b>A width is written only once somebody has moved it</b>, and the comparison is on the
    /// unit and the value alone. A <c>DataGridLength</c> also carries a desired and a displayed
    /// size that a layout pass fills in, so comparing whole values would report every column as
    /// moved the moment the window had been drawn once.
    ///
    /// <b>What a drag does to the Width property is NOT VERIFIED by us.</b> WPF may record a
    /// resize as a pixel length or as a changed share - this writes down faithfully whatever is
    /// there and restores the same thing, so both answers come back the way they were left. The
    /// case that would defeat it is a resize recorded nowhere in Width at all, which would mean a
    /// width that is not kept rather than a wrong one in the file.
    /// </summary>
    internal static ColumnLayout Harvest(DataGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var kept = new List<KeptColumn>();

        for (var position = 0; position < grid.Columns.Count; position++)
        {
            var column = grid.ColumnFromDisplayIndex(position);

            if (Columns.Of(column.SortMemberPath ?? string.Empty) is not { } known)
            {
                continue;
            }

            kept.Add(new KeptColumn(
                known.Id,
                column.Visibility == Visibility.Visible,
                Moved(grid, known, column.Width)));
        }

        return new ColumnLayout(kept, ListSorting.Of(grid));
    }

    private static string? Moved(FrameworkElement grid, Column known, DataGridLength width)
    {
        var start = (DataGridLength)grid.FindResource(known.WidthKey);

        return width.UnitType == start.UnitType && width.Value.Equals(start.Value)
            ? null
            : Widths.ConvertTo(null, System.Globalization.CultureInfo.InvariantCulture, width, typeof(string))
                as string;
    }

    /// <summary>
    /// The one place a width turns into text and back, and it never asks the machine's culture.
    ///
    /// A layout file is copied between machines - that is what section H of the specification
    /// promises - so a share written as <c>2,5*</c> on one and read on another would be a width
    /// that stops being a width when it crosses a border. The same reasoning as `ADR-14`, one
    /// layer down: what goes in a file is the neutral form, and the machine's own habits belong to
    /// what a person is shown.
    /// </summary>
    private static readonly DataGridLengthConverter Widths = new();

    /// <summary>
    /// Keeps the leftmost column that is ON SCREEN the frozen one.
    ///
    /// <b>A count worked out rather than the number one, and the difference is measured rather
    /// than argued.</b> WPF freezes the first N columns of the DISPLAY ORDER, and a collapsed
    /// column keeps its place in that order - measured on 2026-08-12 with a built window: with the
    /// count set to one and the name column turned off, the name column stayed frozen while
    /// collapsed and "Display name", the leftmost column actually on screen, did not. So a literal
    /// one in the markup would freeze a column nobody can see, which is a frozen region that
    /// scrolls away - the failure looks exactly like the feature never having been built.
    ///
    /// <b>Why freeze anything at all:</b> seventeen columns need about 1900 pixels and the window
    /// opens with room for a thousand, so somebody reading a security descriptor is looking at a
    /// row whose name is off the left of the screen. A list that cannot say which service a row
    /// belongs to is the same uselessness as the collapsed columns this scrolling replaced, just
    /// arrived at from the other side.
    ///
    /// <b>Nothing frozen when nothing is shown</b>, rather than a count of one against an empty
    /// list. <see cref="ColumnChoice.MayHide"/> stops a person reaching that state by refusing to
    /// turn the last column off, so this is the second lock on a door rather than the first - but a
    /// count that assumes a visible column would be a crash in a state this class cannot rule out
    /// on its own.
    ///
    /// <b>IT ASKS THE GRID WHICH COLUMN SITS AT EACH POSITION, AND THE FIRST VERSION READ
    /// DisplayIndex INSTEAD - which was wrong in a way that a green build and a working window both
    /// hid.</b> Measured on 2026-08-12, on the real window straight after its constructor: every
    /// column reported <c>DisplayIndex</c> of MINUS ONE, so a count worked out from the smallest one
    /// came to zero and froze nothing at all. WPF fills that property in lazily - the display order
    /// exists, but nothing had asked for it yet, and reading the property does not ask.
    /// <c>ColumnFromDisplayIndex</c> is the question rather than a workaround for it: it returns the
    /// grid's own display order, it answers correctly before the window has ever been shown, and
    /// asking is what materialises the map the property reads from.
    /// </summary>
    private static void Freeze(DataGrid grid)
    {
        for (var position = 0; position < grid.Columns.Count; position++)
        {
            if (grid.ColumnFromDisplayIndex(position).Visibility == Visibility.Visible)
            {
                grid.FrozenColumnCount = position + 1;

                return;
            }
        }

        grid.FrozenColumnCount = 0;
    }

    private static void Show(DataGridColumn column, bool shown) =>
        column.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

    private static DataGridColumn Build(
        Column column, FrameworkElement grid, string? kept, List<string> refused)
    {
        var built = column.Face switch
        {
            ColumnFace.Status => Templated(grid, "StatusCell"),
            ColumnFace.StartType => Templated(grid, "StartTypeCell"),
            ColumnFace.Mismatch => Templated(grid, "AgainstStartTypeCell"),
            ColumnFace.Rollup => Templated(grid, "NameCell"),
            ColumnFace.Number => Written(grid, column, "CellNumber", "ColumnHeadingNumber"),

            // The only cell in this list that grows, and only while its row is the chosen one.
            // CellProse carries the whole argument and the measurement behind the line count.
            ColumnFace.Prose => Written(grid, column, "CellProse", headerStyle: null),
            ColumnFace.Fixed => Written(grid, column, "CellFixed", headerStyle: null),
            _ => Written(grid, column, "CellText", headerStyle: null)
        };

        built.Header = Texts.Of(column.LabelKey);
        built.Width = Wide(grid, column, kept, refused);

        // A starred column gives way and needs a floor under it.
        //
        // A FIXED ONE NEEDS ONE TOO, AND THE SENTENCE THAT USED TO STAND HERE IS WHY SEVEN COLUMNS
        // WERE UNREADABLE. It said a floor under a fixed column would be a second number claiming to
        // decide the same width - which is true of a DIFFERENT number and false of this one. The
        // width alone is a request: when the columns want more room than there is, DataGrid takes it
        // back from whatever it can, down to its own MinColumnWidth of twenty. Photographed with all
        // seventeen columns on, before this line existed: Status, Start, PID, Entry type, Against its
        // start type, Error control and Service SID type all at twenty pixels, every heading a single
        // full stop over a column of sliced dots, and nothing on screen saying so.
        //
        // The same number as a floor says the width is not negotiable, which is what "sized to the
        // widest thing it can hold" meant all along. What gives way instead is the starred columns,
        // down to their floor, and then the row gets wider than the window and gains a scrollbar -
        // which is the owner's decision of 2026-08-12.
        //
        // THE FLOOR FOLLOWS THE WIDTH THAT WAS ACTUALLY USED, which matters now that a width can
        // come from a file: a column somebody dragged to 300 pixels needs 300 as its floor, or the
        // grid takes it back to twenty the moment the row is wider than the window. Asked as "is
        // this an absolute number" rather than "is this a share", because a hand written Auto is
        // now reachable and has no pixel value to use as its own floor.
        built.MinWidth = built.Width.IsAbsolute
            ? built.Width.Value
            : (double)grid.FindResource("ColumnFloor");

        // NOT USED BY THE GRID, WHICH IS WHY IT CAN CARRY THIS. Sorting is handled below rather
        // than left to the grid, so this path is never resolved against a row - it is how the
        // handler finds out which column was clicked, by identity rather than by header text or by
        // position.
        built.SortMemberPath = column.Id;

        return built;
    }

    /// <summary>
    /// How wide a column starts: what a file kept for it, or what the theme says.
    ///
    /// <b>A width that cannot be read falls back to the theme and is named to the caller</b>,
    /// rather than taking the window down or quietly resizing a column somebody had set. This is
    /// the one field in the layout a person can plausibly hand edit into nonsense - the
    /// identifiers are checked against the catalogue and the flags are true or false - so it is
    /// also the one that needs an answer for nonsense.
    /// </summary>
    private static DataGridLength Wide(
        FrameworkElement grid, Column column, string? kept, List<string> refused)
    {
        if (kept is not null)
        {
            if (Parsed(kept) is { } saved)
            {
                return saved;
            }

            refused.Add(column.Id);
        }

        return (DataGridLength)grid.FindResource(column.WidthKey);
    }

    /// <summary>
    /// The widest a kept width may be before it stops being a width.
    ///
    /// <b>Ten thousand device independent pixels, which is about three times the widest screen
    /// anybody puts a window on and nothing like a number a person types.</b> The point is not to
    /// judge somebody's taste in columns - it is that everything above this is the same thing:
    /// a file edited by hand or by something that got the units wrong.
    /// </summary>
    private const double Widest = 10_000;

    /// <summary>
    /// Whether this is a width at all, as opposed to a number the converter was willing to read.
    ///
    /// <b>BACKLOG 308(a). The converter refuses text that is not a number and accepts every number
    /// there is</b>, so <c>1e300</c> came through as a perfectly good width - and the line above
    /// makes the width its own floor, so the grid was then asked for a column that cannot be drawn
    /// and the whole list went with it. Nothing on screen would have said why.
    ///
    /// <b>The share columns are held to the same number, and that is deliberate rather than lazy.</b>
    /// A star weight of 1e300 is not a width but it reaches the same layout pass with the same
    /// result, and a rule that only guarded the absolute ones would be a rule about how the file
    /// happens to be spelled.
    /// </summary>
    private static bool AWidth(DataGridLength width) =>
        !double.IsNaN(width.Value)
        && !double.IsInfinity(width.Value)
        && width.Value >= 0
        && width.Value <= Widest;

    private static DataGridLength? Parsed(string text)
    {
        // Three exceptions rather than one broad catch, because they are the three the converter
        // documents itself as throwing and a broad one here would need an argument in
        // BroadCatchGuards that this does not have: nothing about a width is unknowable.
        try
        {
            return Widths.ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, text)
                is DataGridLength width && AWidth(width)
                ? width
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// A column drawn by a named template, without the presenter the framework would wrap it in.
    ///
    /// <see cref="UnwrappedColumn"/> carries the whole argument and the numbers. The short of it:
    /// DataGridTemplateColumn builds a ContentPresenter of its own, and our cell template already
    /// has one, so every cell of every mark column carried two.
    /// </summary>
    private static DataGridColumn Templated(FrameworkElement grid, string template) =>
        new UnwrappedColumn { CellTemplate = (DataTemplate)grid.FindResource(template) };

    private static DataGridColumn Written(
        FrameworkElement grid, Column column, string cellStyle, string? headerStyle)
    {
        var built = new DataGridTextColumn
        {
            // Through the indexer, like every other cell. The identifier is the language-neutral
            // half of the column and the heading is the translated half, and only one of them may
            // ever be used to ask a row for something.
            Binding = new Binding($"[{column.Id}]") { Mode = BindingMode.OneWay },
            ElementStyle = (Style)grid.FindResource(cellStyle)
        };

        if (headerStyle is not null)
        {
            built.HeaderStyle = (Style)grid.FindResource(headerStyle);
        }

        return built;
    }

}
