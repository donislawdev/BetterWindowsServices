// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.Globalization;
using System.IO;
using System.Windows;
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
    /// A width that is not a width leaves the column where the theme puts it, and is named.
    ///
    /// The one field in the file a person can plausibly hand edit into nonsense - identifiers are
    /// checked against the catalogue and the flags are true or false. Rule 8: the column quietly
    /// being the wrong size is exactly the fault nobody would investigate.
    /// </summary>
    [Fact]
    public void A_width_that_cannot_be_read_falls_back_to_the_theme_and_is_named()
    {
        _ = WpfHost.Resources;

        var bar = new ColumnBar();
        var refused = new List<string>();

        var grid = WpfHost.On(() =>
        {
            var built = new DataGrid();

            refused.AddRange(ListColumns.Fill(
                built,
                bar,
                ColumnPlan.Of(new ColumnLayout([new KeptColumn("status", Shown: true, Width: "as wide as it likes")]))));

            return built;
        });

        Assert.Equal(["status"], refused);

        var theme = WpfHost.On(() => (DataGridLength)WpfHost.Resources["ColumnStatus"]);
        var status = WpfHost.On(() => grid.Columns.First(column => column.SortMemberPath == "status"));

        Assert.Equal(theme.Value, WpfHost.On(() => status.Width.Value));
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
        var grid = Built(ColumnLayout.Default);

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
        var grid = Built(ColumnLayout.Default);

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
        var plan = ColumnPlan.Of(layout);

        bar.Follow(plan);

        return WpfHost.On(() =>
        {
            var built = new DataGrid();

            ListColumns.Fill(built, bar, plan);

            return built;
        });
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
