// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.Globalization;
using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The half of a kept layout that only a grid can answer - `S6d3`.
///
/// <b>Two of the three things a layout remembers exist nowhere but on the grid.</b> Which columns
/// are on is held by the picker and can be checked without a window. The order somebody dragged a
/// heading into and the width they dragged an edge to are WPF's, held on the column object, and
/// nothing tells a view model when either moves - so reading them back correctly is a thing that
/// has to be checked here or not at all.
///
/// <b>What these tests do NOT claim, said plainly because a harness in this project has already
/// been wrong about it once:</b> nothing here says anything about a pixel on a screen. A grid laid
/// out in this assembly reported column widths the real window did not have - `docs/10` section 8
/// - so a width claim belongs to <c>tools/gui-probe/columns.ps1</c> against a real window. What is
/// held here is the instruction the grid is given and the text that is written down, which is the
/// half that travels between machines and lands in somebody's file.
/// </summary>
public sealed class KeptColumnGuards : IDisposable
{
    private readonly List<string> _made = [];

    /// <summary>
    /// A width somebody kept is what the column starts at, and the floor moves with it.
    ///
    /// <b>The floor is the half that would have been missed.</b> A width on its own is a request -
    /// when the columns want more room than the window has, DataGrid takes it back down to twenty
    /// pixels, which is what left seven headings reading as a single full stop on 2026-08-12. A
    /// kept width with the theme's floor under it would be a column that comes back where it was
    /// put and collapses the moment somebody scrolls sideways.
    /// </summary>
    [Fact]
    public void A_width_that_was_kept_is_where_the_column_starts_and_it_cannot_be_squeezed_below_it()
    {
        var grid = Built(new ColumnLayout([new KeptColumn("status", Shown: true, Width: "444")]));

        var status = WpfHost.On(() => grid.Columns.First(column => column.SortMemberPath == "status"));

        Assert.Equal(444d, WpfHost.On(() => status.Width.Value));
        Assert.Equal(444d, WpfHost.On(() => status.MinWidth));
    }

    /// <summary>
    /// The kept order is the order the grid puts its columns in.
    ///
    /// <b>Asserted through <c>ColumnFromDisplayIndex</c> rather than through the collection</b>,
    /// which is the same distinction the frozen column was measured on: the collection is the order
    /// the columns were added in and never changes, and the display order is what a person sees.
    /// </summary>
    [Fact]
    public void The_order_that_was_kept_is_the_order_the_grid_shows()
    {
        var wanted = Columns.All.Reverse().Select(column => column.Id).ToList();

        var grid = Built(new ColumnLayout(
            [.. wanted.Select(id => new KeptColumn(id, Shown: true, Width: null))]));

        var shown = WpfHost.On(() => Enumerable
            .Range(0, grid.Columns.Count)
            .Select(position => grid.ColumnFromDisplayIndex(position).SortMemberPath)
            .ToList());

        Assert.Equal(wanted, shown);
    }

    /// <summary>
    /// What comes off the grid is the display order, what is on, and only the widths that moved.
    ///
    /// <b>A column nobody has touched writes no width at all, and that is `ADR-23` surviving a
    /// file.</b> Writing every width down would freeze the theme into everybody's profile on the
    /// first close: change a starting width afterwards and it would reach nobody who had ever
    /// opened the window.
    /// </summary>
    [Fact]
    public void Only_a_width_that_moved_is_written_down_and_the_order_is_what_is_on_screen()
    {
        var grid = Built(ColumnLayout.DefaultFor(EntryScope.Services));

        WpfHost.On(() =>
        {
            var status = grid.Columns.First(column => column.SortMemberPath == "status");

            status.Width = new DataGridLength(321);
            status.DisplayIndex = 0;
        });

        var harvested = WpfHost.On(() => ListColumns.Harvest(grid));

        Assert.Equal("status", harvested.Columns[0].Id);
        Assert.Equal("321", harvested.Columns[0].Width);

        // Everything else is where the theme left it, so the file says nothing about it.
        Assert.All(harvested.Columns.Skip(1), column => Assert.Null(column.Width));

        Assert.Equal(
            Columns.All.Where(column => column.ShownAtFirst).Select(column => column.Id).Order(StringComparer.Ordinal),
            harvested.Columns.Where(column => column.Shown).Select(column => column.Id).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A width is written the same way whatever language the machine is set to.
    ///
    /// <b>`ADR-14` one layer down, and this is the fault it prevents.</b> A layout is a file
    /// somebody copies between servers - section H of the specification promises exactly that - and
    /// a share written as <c>2,5*</c> on a Polish machine is a width that stops being a width when
    /// it crosses a border. The machine's own habits belong to what a person is shown, never to
    /// what goes in a file.
    /// </summary>
    [Fact]
    public void A_width_is_written_in_the_same_form_on_a_machine_that_writes_decimals_with_a_comma()
    {
        var grid = Built(ColumnLayout.DefaultFor(EntryScope.Services));

        var harvested = WpfHost.On(() =>
        {
            var was = System.Threading.Thread.CurrentThread.CurrentCulture;

            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("pl-PL");

            try
            {
                grid.Columns.First(column => column.SortMemberPath == "status").Width =
                    new DataGridLength(2.5, DataGridLengthUnitType.Star);

                return ListColumns.Harvest(grid);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = was;
            }
        });

        Assert.Equal("2.5*", harvested.Columns.First(column => column.Id == "status").Width);
    }

    /// <summary>
    /// Turn a column off, close the window, open it again - it is still off.
    ///
    /// <b>This is the promise `docs/04` writes for this slice, end to end, through the real
    /// window.</b> Everything else in these two classes checks one link of it; this one checks that
    /// the links are joined, which is the failure this product has met most often - a mechanism
    /// that works in every part and is wired to nothing.
    ///
    /// <b>The columns are reached through the menu the window hands out</b>, rather than through a
    /// field: that is the same object a person clicks, so nothing here can pass while the picker in
    /// the window is wired to something else.
    /// </summary>
    [Fact]
    public void A_column_turned_off_is_still_off_the_next_time_the_window_opens()
    {
        // FORCED, because every StaticResource in MainWindow.xaml is resolved as the file is read.
        // Without it this passes or throws depending on which test ran first - the arrangement
        // WpfHost and ColumnPickerGuards both record biting once each.
        _ = WpfHost.Resources;

        var file = new PreferencesFile(Somewhere());

        var first = WpfHost.On(() => new MainWindow(file));

        WpfHost.On(() => Choice(first, "displayName").IsShown = false);
        WpfHost.On(first.Close);

        Assert.True(File.Exists(file.Where), "Nothing was written, so there is nothing to open again.");

        var second = WpfHost.On(() => new MainWindow(file));

        Assert.False(WpfHost.On(() => Choice(second, "displayName").IsShown));

        Assert.Equal(
            Visibility.Collapsed,
            WpfHost.On(() => second.Entries.Columns
                .First(column => column.SortMemberPath == "displayName").Visibility));

        // AND THE OTHER HALF OF THE PROMISE: delete the file and the usual columns come back. A
        // layout somebody cannot get out of is worse than one that is not kept at all.
        File.Delete(file.Where);

        var third = WpfHost.On(() => new MainWindow(file));

        Assert.True(WpfHost.On(() => Choice(third, "displayName").IsShown));

        WpfHost.On(second.Close);
        WpfHost.On(third.Close);
    }

    /// <summary>
    /// The way back puts the usual columns on, and writes that down like any other change.
    ///
    /// <b>A layout is kept, so a layout needs a way out.</b> Turning on the description and the five
    /// signature columns to look at something once leaves them there on every start after it, and
    /// the only road back was turning twenty-six columns off one at a time.
    ///
    /// <b>The second half is the one that would be missed:</b> a restore that lasts until the window
    /// closes is not a way back at all, it is a view somebody has to repeat every morning. So the
    /// window is opened again on the same file.
    ///
    /// <b>What this does NOT hold, said rather than left to be found:</b> that clicking the item in
    /// the menu reaches this. The item is asserted to be in the list the menu is handed, and the
    /// click was driven through UI Automation on a live window on 2026-08-25 - six columns, on with
    /// Description, six again - but nothing in this suite presses it.
    /// </summary>
    [Fact]
    public void The_way_back_puts_the_usual_columns_on_and_keeps_them_that_way()
    {
        _ = WpfHost.Resources;

        var file = new PreferencesFile(Somewhere());

        var first = WpfHost.On(() => new MainWindow(file));

        // The item a person clicks is in the same list the picker is handed, rather than somewhere
        // this test knows about and the window does not.
        Assert.Contains(
            WpfHost.On(() => first.ColumnsButton.ContextMenu!.ItemsSource.OfType<object>().ToList()),
            entry => entry is ColumnReset);

        // Both directions have something to undo: one column on that is normally off, and one off
        // that is normally on. The first was description until 2026-09-02, when it became one of the
        // usual five and serviceName took its place by moving the other way.
        WpfHost.On(() => Choice(first, "serviceName").IsShown = true);
        WpfHost.On(() => Choice(first, "displayName").IsShown = false);

        WpfHost.On(first.RestoreColumns);

        Assert.False(WpfHost.On(() => Choice(first, "serviceName").IsShown));
        Assert.True(WpfHost.On(() => Choice(first, "displayName").IsShown));

        Assert.Equal(
            Visibility.Visible,
            WpfHost.On(() => first.Entries.Columns
                .First(column => column.SortMemberPath == "displayName").Visibility));

        WpfHost.On(first.Close);

        var second = WpfHost.On(() => new MainWindow(file));

        Assert.False(WpfHost.On(() => Choice(second, "serviceName").IsShown));
        Assert.True(WpfHost.On(() => Choice(second, "displayName").IsShown));

        WpfHost.On(second.Close);
    }

    /// <summary>
    /// A layout the window could not honour is said out loud, in the line a person can see.
    ///
    /// Rule 8 where it is easiest to break without anything looking wrong: a window that came up
    /// almost the way somebody left it, with no way to tell that from misremembering.
    /// </summary>
    [Fact]
    public void A_layout_that_could_not_be_honoured_is_said_in_the_window()
    {
        _ = WpfHost.Resources;

        var file = new PreferencesFile(Somewhere());

        File.WriteAllText(file.Where, "half a layout {");

        var window = WpfHost.On(() => new MainWindow(file));

        Assert.False(
            string.IsNullOrWhiteSpace(
                WpfHost.On(() => ((MainViewModel)window.DataContext).Says.Problem)),
            "The window opened with a layout it could not read and said nothing about it.");

        WpfHost.On(window.Close);
    }

    private static ColumnChoice Choice(MainWindow window, string id) =>
        window.ColumnsButton.ContextMenu!.ItemsSource
            .OfType<ColumnChoice>()
            .First(choice => choice.Column.Id == id);

    private static DataGrid Built(ColumnLayout layout)
    {
        _ = WpfHost.Resources;

        var bar = new ColumnBar();
        var plan = ColumnPlan.Of(layout, EntryScope.Services);

        bar.Follow(plan);

        return WpfHost.On(() =>
        {
            var built = new DataGrid();

            ListColumns.Fill(built, bar, plan);

            return built;
        });
    }

    /// <summary>
    /// The order somebody put the list in is still there the next time they open the window.
    ///
    /// <b>The third thing a layout remembers, arriving 2026-08-25 - and the one that could not be
    /// applied where the other two are.</b> Width and order are properties of columns, which exist
    /// the moment the grid is built. An order is a comparer on the VIEW over the rows, and at the
    /// moment the columns are built there are no rows and no view - so the sort is applied once the
    /// first reading has arrived, which is what SortAsKept is.
    ///
    /// <b>The click goes through the handler the grid really subscribes</b>, rather than through
    /// the properties it sets. A test that set SortDirection itself would pass over a window where
    /// nothing is wired to the headings at all.
    /// </summary>
    [Fact]
    public async Task The_order_somebody_put_the_list_in_comes_back_the_next_time_it_opens()
    {
        _ = WpfHost.Resources;

        var file = new PreferencesFile(Somewhere());

        var first = await Sorted(file, descending: false);

        Assert.Equal(
            ListSortDirection.Ascending,
            WpfHost.On(() => Heading(first).SortDirection));

        // Closing is the write, so the file only exists after this line.
        WpfHost.On(first.Close);

        Assert.Contains(
            "\"sort\"",
            await File.ReadAllTextAsync(file.Where),
            StringComparison.Ordinal);

        var second = await Opened(file);

        WpfHost.On(second.SortAsKept);
        WpfHost.Settled();

        Assert.Equal(
            ListSortDirection.Ascending,
            WpfHost.On(() => Heading(second).SortDirection));

        WpfHost.On(second.Close);
    }

    /// <summary>
    /// A window over two entries, with the name column clicked once.
    ///
    /// The rows are handed over before the click, because an order is a comparer on the view over
    /// them and a grid with no items has no view to hand one to.
    /// </summary>
    private static async Task<MainWindow> Sorted(PreferencesFile file, bool descending)
    {
        var window = await Opened(file);

        var heading = WpfHost.On(() => Heading(window));


        WpfHost.On(() => ListSorting.WhenAHeadingIsClicked(
            window.Entries, new DataGridSortingEventArgs(heading)));

        if (descending)
        {
            WpfHost.On(() => ListSorting.WhenAHeadingIsClicked(
                window.Entries, new DataGridSortingEventArgs(heading)));
        }

        WpfHost.Settled();

        return window;
    }

    /// <summary>A window over a small machine, with its rows already on the grid.</summary>
    private static async Task<MainWindow> Opened(PreferencesFile file)
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Bravo"), Rows.Entry("Alpha")), new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.On(() => new MainWindow(file, model));

        WpfHost.On(() => window.Entries.ItemsSource = model.Rows);
        WpfHost.Settled();

        return window;
    }

    /// <summary>
    /// A heading these tests can click - one that is ON before anybody chooses. It was serviceName
    /// until 2026-09-02, when that column went off by default and the sibling guard below started
    /// correctly refusing an order by a hidden column to two tests meaning to click a visible one.
    /// </summary>
    private static DataGridColumn Heading(MainWindow window) =>
        window.Entries.Columns.First(column =>
            string.Equals(column.SortMemberPath, "displayName", StringComparison.Ordinal));


    /// <summary>
    /// A kept order naming a column that is turned OFF is not applied, and one naming a column this
    /// build does not have is ignored.
    ///
    /// <b>Both were claims in a comment until this test.</b> The first is a decision: a list ordered
    /// by something nobody can see is a list whose order cannot be read, so the order stays in the
    /// file and comes back with the column. The second is the same rule the columns array already
    /// follows for a name that has gone away.
    /// </summary>
    [Fact]
    public void An_order_by_a_column_that_is_off_or_gone_is_not_applied()
    {
        _ = WpfHost.Resources;

        var grid = Built(ColumnLayout.DefaultFor(EntryScope.Services));

        WpfHost.On(() => grid.ItemsSource = new List<EntryRow> { EntryRow.Of(Rows.Entry("Spooler")) });
        WpfHost.Settled();

        // A column this build has never heard of.
        WpfHost.On(() => ListSorting.By(grid, new KeptSort("noSuchColumn", Descending: false)));

        Assert.All(
            WpfHost.On(() => grid.Columns.ToList()),
            column => Assert.Null(WpfHost.On(() => column.SortDirection)));

        // And one that exists and is turned off - serviceName is not among the usual five. It was
        // description here and serviceName below until 2026-09-02, when the two swapped defaults.
        WpfHost.On(() => ListSorting.By(grid, new KeptSort("serviceName", Descending: true)));

        Assert.All(
            WpfHost.On(() => grid.Columns.ToList()),
            column => Assert.Null(WpfHost.On(() => column.SortDirection)));

        // The even claim: one that is on IS applied, so the two above are refusals rather than a
        // method that does nothing at all.
        WpfHost.On(() => ListSorting.By(grid, new KeptSort("displayName", Descending: true)));

        Assert.Equal(
            ListSortDirection.Descending,
            WpfHost.On(() => grid.Columns
                .First(column => column.SortMemberPath == "displayName").SortDirection));
    }

    private string Somewhere()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "bws-kept-tests",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);
        _made.Add(directory);

        return directory;
    }

    public void Dispose()
    {
        foreach (var directory in _made.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
