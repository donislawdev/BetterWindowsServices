// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What a REAL WINDOW leaves in the layout file, which is a different subject from how a file
/// refuses.
///
/// <b>Split from <see cref="PreferencesFileGuards"/> on 2026-09-03, and the size ratchet is what
/// asked - as it has five times before, by naming a topic rather than a line count.</b> That
/// class is about a real path and the four ways a real file refuses: it is not there, something
/// else is holding it open, it cannot be moved out of the way, and there is nowhere to put it.
/// Every test in it works on a PreferencesFile alone. These two open a MainWindow, drive it and
/// read the file back, and both are about the same thing: whether the ORDER somebody left the
/// list in survives what the window does next.
///
/// <b>The seam is worth naming because the two subjects fail differently.</b> A file refusing is
/// an ordinary accident of the machine - a roaming profile, a backup holding a handle. An order
/// lost by a window is nobody's accident: it is this program quietly rearranging somebody's
/// list, which is the shape both of these were written after finding.
///
/// <b>Every test here writes into a temporary directory of its own and deletes it</b>, for the
/// reason the other class gives: reading the real profile would make the answers depend on which
/// columns somebody had turned on last night.
/// </summary>
public sealed class KeptOrderGuards : IDisposable
{
    private readonly List<string> _made = [];

    /// <summary>
    /// A window opened on somebody's layout and closed again leaves that layout alone.
    ///
    /// <b>Backlog 255, and it is the one shape none of the guards around it held.</b> Everything
    /// else here proves that a CHANGE survives - a column turned off is still off the next time,
    /// a width somebody dragged comes back. Nothing proved that NO change survives, and on
    /// 2026-08-31 a probe that only looked at the window came away having turned four columns on
    /// in the layout of the person logged in. <c>tools/perf-probe/instrument-cost.ps1</c> says in
    /// its own header that it "changes nothing on the machine" - opening the window writes this
    /// file when it closes, so that sentence is true only while this test is green.
    ///
    /// <b>The layout differs from the defaults in BOTH directions on purpose</b> - two columns that
    /// are normally on turned off, two that are normally off turned on. A round trip that quietly
    /// fell back to what this build opens with would still pass against only one of those.
    ///
    /// <b>It compares the TEXT rather than the record</b>, because the file is what the next window
    /// reads and what a person opens. A layout that is equal as an object and different as a file
    /// is still a file that changed under somebody.
    /// </summary>
    [Fact]
    public void A_window_opened_on_a_layout_and_closed_leaves_that_layout_alone()
    {
        // FORCED, because every StaticResource in MainWindow.xaml is resolved as the file is read -
        // the same arrangement KeptColumnGuards records.
        _ = WpfHost.Resources;

        var file = Fresh();

        var arranged = ColumnLayouts.Default with
        {
            Services = new ColumnLayout([.. Arranged()], new KeptSort("status", Descending: true)),

            // Otherwise the window opens on the machine overview and the grid holds nothing, which
            // would make this pass without ever having a list to harvest - `G`, schema 4.
            OverviewSeen = true
        };

        Assert.Null(file.Write(arranged));

        var before = File.ReadAllText(file.Where);

        var window = WpfHost.On(() => new MainWindow(file));

        WpfHost.On(window.Close);

        Assert.Equal(before, File.ReadAllText(file.Where));
    }

    /// <summary>
    /// The way back takes somebody's own order off the FILE, and not only off the list on screen.
    ///
    /// <b>The exception that <see cref="KeptColumns.Harvested"/> is built around, and without this
    /// nothing would notice it going.</b> Every other write puts back the order the file holds
    /// whenever the grid carries none, because a grid carrying none nearly always means the window
    /// has not applied it yet. Here it means the opposite: somebody asked for the list this build
    /// opens with, so the one line that clears the kept order is the difference between a way back
    /// and an order that comes straight back on the next start.
    ///
    /// <b>WHAT IT ASSERTS CHANGED ON 2026-09-02 AND THE CLAIM DID NOT.</b> Until that day the list
    /// this build opens with was UNSORTED, so a way back that worked left no order at all in the
    /// file. The owner then asked for the order services.msc opens with, and the default became
    /// displayName ascending - so a way back now leaves THAT rather than nothing, and asserting a
    /// null here would be asserting that the way back does not go all the way back.
    ///
    /// <b>AND ON 2026-09-03 IT BECAME AN EQUALITY, BECAUSE THE THING THAT MADE IT FLAKY WAS A
    /// FAULT RATHER THAN A TEST.</b> Asking for displayName here used to pass alone and fail in a
    /// full run, and the reason was measured rather than guessed: twenty ways back on a window
    /// built and never shown left the grid carrying no order SEVENTEEN times, because the rows
    /// binding resolves at DataBind priority and a window nobody has shown usually has not got
    /// there yet. The file then held whatever <c>Defaults</c> had left, which was nothing - so the
    /// next start opened unsorted. <c>Defaults</c> now writes the default order instead of
    /// clearing it, both branches agree, and the same probe wrote displayName twenty times out of
    /// twenty. Backlog 295 and 315.
    /// </summary>
    [Fact]
    public void The_way_back_takes_the_kept_order_off_the_file_as_well()
    {
        _ = WpfHost.Resources;

        var file = Fresh();

        Assert.Null(file.Write(ColumnLayouts.Default with
        {
            Services = new ColumnLayout([.. Arranged()], new KeptSort("status", Descending: true)),
            OverviewSeen = true
        }));

        var window = WpfHost.On(() => new MainWindow(file));

        // THE WINDOW A PERSON IS LOOKING AT, rather than one built a microsecond ago, and this is
        // the line that stopped this test being flaky. The rows binding resolves at DataBind
        // priority, so until it has, ListColumns.Reapply has no view to hand a comparer to and the
        // grid comes out of the way back carrying no order at all - measured at seventeen times in
        // twenty. Nobody can click a menu item that early; Settled says so.
        WpfHost.Settled();

        WpfHost.On(window.RestoreColumns);
        WpfHost.On(window.Close);

        // BOTH HALVES, because either one alone passes for the wrong reason. That somebody's own
        // order is gone would be satisfied by a file holding nothing, which is the state this used
        // to reach and which opens the list unsorted on the next start. That the default is there
        // would be satisfied by a way back that never ran at all, since the default is what an
        // untouched profile holds anyway - the specimen written above is what rules that out.
        var back = file.Read().Layouts?.Services.Sort;

        Assert.NotNull(back);

        Assert.Equal(ColumnLayout.DefaultFor(EntryScope.Services).Sort, back);
    }

    /// <summary>
    /// A layout somebody arranged: not the defaults, in a different order, and one width dragged.
    ///
    /// Built from the catalogue rather than from a list written out here, so that a column joining
    /// the build cannot leave this specimen naming one that is gone.
    /// </summary>
    private static IEnumerable<KeptColumn> Arranged()
    {
        var chosen = new[] { "description", "serviceName", "displayName", "status", "memory" };

        var order = chosen.Concat(
            Columns.All.Select(column => column.Id).Where(id => !chosen.Contains(id)));

        return order.Select(id => new KeptColumn(
            id,
            Shown: chosen.Contains(id),
            Width: id == "serviceName" ? "333" : null));
    }

    public void Dispose()
    {
        foreach (var directory in _made.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private PreferencesFile Fresh() => new(Somewhere());

    private string Somewhere()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "bws-kept-order-tests",
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);
        _made.Add(directory);

        return directory;
    }
}
