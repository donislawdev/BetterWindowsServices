using System.Windows;
using System.Windows.Controls;
using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The views the window is built out of, each over a model put into a state on purpose - the
/// third kind of component on the sheet, since 2026-09-16 (docs/PROJEKT-KATALOG-20260916.md,
/// section 3.3).
///
/// <b>Two steps, and the seam between them is a thread.</b> A model is put into its state by
/// loading a machine, which the model does through <c>Task.Run</c> and finishes on whichever
/// thread called it - so the models are prepared with <c>await</c>, off any window, and only
/// then are the views built over them, which has to happen where WPF controls may be made. The
/// catalogue window does the first after it is shown and the second on its own thread when the
/// first is done. A test does the first on a pool thread and the second on the test host.
///
/// <b>The states are the REAL model's states.</b> "Loading" is a model whose machine has not
/// answered. "Wrong" is a model whose machine refused, reached through the same catch the
/// product reaches it through. "Empty" is a machine with nothing on it, or a query that matches
/// nothing. Nothing here tells a view to look loading.
///
/// <b>Each sample is one view over one model, and a model is never shared between two views that
/// would pull it two ways</b> - three models are loaded with data for the details view alone,
/// because a chosen row is a property of the model and the sheet shows three of them chosen.
/// Eight entries a model, so eleven models are a hundred rows, which is a rounding error against
/// the eight hundred one window holds.
/// </summary>
public static partial class Catalogue
{
    /// <summary>
    /// The models, prepared. Holds the gate the loading samples wait on, so whoever built the
    /// sheet can let those reads go when the sheet is done.
    /// </summary>
    public sealed class Prepared : IDisposable
    {
        internal Prepared(Stalling stalling, Task stillReading)
        {
            Stalling = stalling;
            StillReading = stillReading;
        }

        internal Stalling Stalling { get; }

        /// <summary>
        /// The loading sample's read, which finishes only after <see cref="Dispose"/> opens the
        /// gate. Kept so that somebody waits for it - a test does, after disposing - rather than
        /// dropped, which is the shape BackgroundWorkGuards refuses without exception.
        /// </summary>
        public Task StillReading { get; }

        internal MainViewModel Data { get; init; } = null!;

        internal MainViewModel Empty { get; init; } = null!;

        internal MainViewModel Loading { get; init; } = null!;

        internal MainViewModel Wrong { get; init; } = null!;

        internal MainViewModel Narrowed { get; init; } = null!;

        internal MainViewModel Lit { get; init; } = null!;

        internal MainViewModel ChoseOrdinary { get; init; } = null!;

        internal MainViewModel ChoseDriver { get; init; } = null!;

        internal MainViewModel ChoseLongest { get; init; } = null!;

        /// <summary>
        /// The panel open on an entry that has since left the listing - the one state of the
        /// panel that carries a notice, and the one the sheet could not show until 2026-09-16.
        /// </summary>
        internal MainViewModel ChoseGone { get; init; } = null!;

        internal MainViewModel WithPlan { get; init; } = null!;

        internal MainViewModel WithRefusedPlan { get; init; } = null!;

        internal MainViewModel WithBulkPlan { get; init; } = null!;

        /// <summary>Lets the loading samples' reads return. The window calls this when it closes.</summary>
        public void Dispose() => Stalling.Release();
    }

    /// <summary>
    /// Every model the views need, each loaded into its state. Awaitable anywhere - nothing here
    /// touches a control.
    /// </summary>
    public static async Task<Prepared> PrepareViewsAsync()
    {
        var stalling = new Stalling();
        var clock = new SystemClock();

        static MainViewModel Over(IScmCatalog machine, IClock clock) => new(machine, clock);

        var data = Over(new Frozen(Specimens()), clock);
        var empty = Over(new Frozen([]), clock);
        var wrong = Over(new Refusing(), clock);
        var narrowed = Over(new Frozen(Specimens()), clock);
        var lit = Over(new Frozen(Specimens()), clock);
        var choseOrdinary = Over(new Frozen(Specimens()), clock);
        var choseDriver = Over(new Frozen(Specimens()), clock);
        var choseLongest = Over(new Frozen(Specimens()), clock);
        var choseGone = Over(new Frozen(Specimens()), clock);
        var planned = Over(new Frozen(Specimens()), clock);
        var plannedInVain = Over(new Frozen(Specimens()), clock);
        var plannedInBulk = Over(new Frozen(Specimens()), clock);

        // THE LOADING ONE IS STARTED AND NOT AWAITED HERE. Its read waits on the gate until the
        // sheet is done, which is the whole point of it: the model is in the state between asking
        // and hearing back, for as long as anybody looks. The task is KEPT, not dropped -
        // BackgroundWorkGuards allows no exceptions to that and is right - and it is awaited by
        // whoever disposes the sheet, after the gate opens: StillReading, below.
        var loading = Over(new Stalled(stalling), clock);
        var stillReading = loading.LoadAsync();

        foreach (var model in new[] { data, empty, wrong, narrowed, lit, choseOrdinary, choseDriver, choseLongest, choseGone, planned, plannedInVain, plannedInBulk })
        {
            await model.LoadAsync().ConfigureAwait(false);
        }

        // Each one opened out to the whole machine, so a driver is on the list a view is over.
        foreach (var model in new[] { data, empty, narrowed, lit, choseOrdinary, choseDriver, choseLongest, choseGone, planned, plannedInVain, plannedInBulk })
        {
            model.Scope = EntryScope.Everything;
        }

        narrowed.QueryText = "name:nothing-on-this-machine-is-called-this";
        lit.QueryText = "status:running";
        lit.ShowingEveryInstance = true;

        // Chosen AND shown: picking a row does not open the panel in the product either -
        // `docs/11` opens with the sentence that the list is for searching - so the panel is asked
        // to show the way Enter or a double click asks it.
        foreach (var (model, name) in new[] { (choseOrdinary, "Spooler"), (choseDriver, "disk"), (choseLongest, LongestSpecimen().ServiceName), (choseGone, "Spooler") })
        {
            model.Chosen.Row = model.Rows.Single(row => row.ServiceName == name);
            model.Chosen.Show();
        }

        // The entry the panel follows is asked about against a listing that no longer holds it -
        // which is what the window asks once a second, through the same call, when a service is
        // deleted or an update removes it. Rule 8's own state, drawn where somebody can see it.
        choseGone.Chosen.StillIn([]);

        // A plan that can be carried out, one the builder refuses - a driver cannot be stopped
        // from here, PlanBuilder says so with a problem rather than a step - and one across the
        // whole machine.
        await ShowPlanAsync(planned, ActionKind.Stop, "Spooler").ConfigureAwait(false);
        await ShowPlanAsync(plannedInVain, ActionKind.Stop, "disk").ConfigureAwait(false);
        await ShowPlanAsync(plannedInBulk, ActionKind.Restart, [.. Specimens().Select(entry => entry.ServiceName)]).ConfigureAwait(false);

        return new Prepared(stalling, stillReading)
        {
            Data = data,
            Empty = empty,
            Loading = loading,
            Wrong = wrong,
            Narrowed = narrowed,
            Lit = lit,
            ChoseOrdinary = choseOrdinary,
            ChoseDriver = choseDriver,
            ChoseLongest = choseLongest,
            ChoseGone = choseGone,
            WithPlan = planned,
            WithRefusedPlan = plannedInVain,
            WithBulkPlan = plannedInBulk
        };
    }

    private static async Task ShowPlanAsync(MainViewModel model, ActionKind kind, params string[] names)
    {
        var plan = await model.PlanAsync(new BulkAction(kind, names)).ConfigureAwait(false);
        var shownAs = names.Length == 1 ? model.Rows.Single(row => row.ServiceName == names[0]).DisplayName : null;

        model.Planned.Show(plan, shownAs);
    }

    /// <summary>
    /// The views, built over the prepared models. On the thread controls may be made on.
    ///
    /// <b>Every UserControl of this window, by hand, and the guard holds the list to the
    /// assembly.</b> A view is not a style and cannot be found by walking a dictionary - the
    /// assembly's types are the declaration, and CatalogueGuards compares this list against them
    /// in both directions. A view added to the window and not here is a red build.
    ///
    /// The five cells of a view are its four states and the extreme one: with data, empty,
    /// wrong, loading, extreme. A view that has no such state - a scope switch cannot be
    /// "loading" - has a dash there, as a text block has under "disabled".
    /// </summary>
    public static Group Views(Prepared ready, ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(ready);
        ArgumentNullException.ThrowIfNull(resources);

        var limits = Limits.Of(resources);

        Built[] built =
        [
            View(() => new SearchRow(),
                data: ready.Data, empty: ready.Empty, wrong: ready.Wrong, loading: ready.Loading),

            View(() => new FilterRow(),
                data: ready.Lit, empty: ready.Empty),

            View(() => new ScopeBar(),
                data: ready.Data, empty: ready.Empty, loading: ready.Loading),

            // The empty state's own DataContext is the model's sentences, as MainWindow.xaml binds it.
            View(() => new EmptyState(),
                empty: ready.Narrowed.Says, wrong: ready.Wrong.Says),

            View(() => new OverviewView(),
                data: ready.Data, empty: ready.Empty, wrong: ready.Wrong, loading: ready.Loading),

            View(() => new DetailsView(),
                data: ready.ChoseOrdinary.Chosen, wrong: ready.ChoseGone.Chosen, extreme: ready.ChoseLongest.Chosen),

            // A driver has no account, no process and no trigger, so its details panel is a
            // different shape - the sheet shows it, keyed by what it is.
            View(() => new DetailsView(), key: nameof(DetailsView) + " over a driver",
                data: ready.ChoseDriver.Chosen),

            View(() => new PlanView(),
                data: ready.WithPlan.Planned, wrong: ready.WithRefusedPlan.Planned, extreme: ready.WithBulkPlan.Planned),

            View(() => new PlanFooter(),
                data: ready.WithPlan.Planned, wrong: ready.WithRefusedPlan.Planned, extreme: ready.WithBulkPlan.Planned),

            // The bar reads nothing - the window tells it how many entries are picked. THE THIRD
            // CELL IS THE BAR OVER SEVERAL, since 2026-09-16, and it borrows the "extreme" column
            // because the sheet has no column for it: it is the one state in which the bar is not
            // all on or all off - the four verbs that take any number are live and the two that end
            // a process are off, each saying so on itself. Not more text than fits; a different
            // shape, and the column that was free.
            Made(nameof(ActionBar),
                data: Bar(1), empty: Bar(0), extreme: Bar(2)),

            View(() => new StatusRow(),
                data: Saying(ready, notice: true), wrong: Saying(ready, notice: false)),

            // The list, built from the same styles and the same column builder the window uses,
            // over the same models - a second grid rather than the window's own, because the
            // window's is written into MainWindow.xaml and that file stands on the markup ceiling.
            Made("EntryList",
                data: ListOver(ready.Data, resources), empty: ListOver(ready.Empty, resources),
                wrong: ListOver(ready.Wrong, resources), loading: ListOver(ready.Loading, resources))
        ];

        // EVERY VIEW IS BUILT, THEN THE QUEUE IS DRAINED, THEN EVERY VIEW IS MEASURED - and the
        // middle step is docs/10 trap 27 in a new costume. A binding attaches at DataBind
        // priority, not when the DataContext is set, so a view measured straight after being
        // built has a DataTrigger on Showing that has not looked yet and an ItemsSource that has
        // not arrived: the details panel measured to nothing, the scope switch to a width and no
        // height, and the sheet said "nothing on its own" of three views that draw a great deal.
        // Found by measuring each one twice on 2026-09-16, before and after the pump. One pump
        // for all of them rather than one per view, because it drains the whole queue either way.
        Settle();

        return Group.OfViews(
        [
            .. built.Select(each => new Entry(
                each.Key,
                nameof(UserControl),
                Cell(each.Cells[0], limits),
                Cell(each.Cells[1], limits),
                Cell(each.Cells[2], limits),
                Cell(each.Cells[3], limits),
                Cell(each.Cells[4], limits)))
        ]);
    }

    /// <summary>One view in up to five states, built and not yet measured.</summary>
    private sealed record Built(string Key, FrameworkElement?[] Cells);

    /// <summary>
    /// A view in up to five states, each a fresh control from the factory over its own model.
    /// A factory rather than a generic new(), because a generic new() is Activator.CreateInstance
    /// underneath, and LayeringGuards forbids this assembly from naming Activator - the same
    /// constraint the styles' factory table met on 2026-09-10, met the same way.
    /// </summary>
    private static Built View<TView>(
        Func<TView> make, string? key = null, object? data = null, object? empty = null, object? wrong = null, object? loading = null, object? extreme = null)
        where TView : FrameworkElement =>
        Made(key ?? typeof(TView).Name,
            data: Over(make, data), empty: Over(make, empty), wrong: Over(make, wrong), loading: Over(make, loading), extreme: Over(make, extreme));

    private static Built Made(
        string key, FrameworkElement? data = null, FrameworkElement? empty = null, FrameworkElement? wrong = null, FrameworkElement? loading = null, FrameworkElement? extreme = null) =>
        new(key, [data, empty, wrong, loading, extreme]);

    /// <summary>
    /// Lets every binding queued by the views above attach, by running the dispatcher down to
    /// idle from inside this call. The same thing WpfHost.Settled does for the tests, for the same
    /// reason, on the same priority.
    /// </summary>
    private static void Settle() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            static () => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

    private static FrameworkElement? Over<TView>(Func<TView> make, object? context)
        where TView : FrameworkElement
    {
        if (context is null)
        {
            return null;
        }

        var view = make();
        view.DataContext = context;

        return view;
    }

    /// <summary>
    /// A view sized as a view and measured like any sample. Given the width of the narrowest
    /// window the product draws it in, because a view fills its window and a sheet measured to
    /// content would otherwise size itself to the view - and always tall, because a view is.
    /// </summary>
    private static Sample Cell(FrameworkElement? view, Limits limits)
    {
        if (view is null)
        {
            return Sample.None(NoSuchState);
        }

        view.Width = limits.ViewWidth;
        view.MaxHeight = limits.TallCeiling;

        var sized = Sized(view, limits);

        return sized.Element is null ? sized : sized with { Tall = true };
    }

    private static ActionBar Bar(int picked)
    {
        var bar = new ActionBar();

        bar.Picked(picked);

        return bar;
    }

    /// <summary>The status row over a model saying a notice, or over one saying a problem.</summary>
    private static MainViewModel Saying(Prepared ready, bool notice)
    {
        var model = notice ? ready.Data : ready.Empty;

        if (notice)
        {
            model.Says.AboutTheLayout("A sentence the window admits to, in the line under the list - the kept layout named a column this build does not have.");
        }
        else
        {
            model.Says.CouldNotDo("What the window could not do, in its own words: the machine said no, and here is why.");
        }

        return model;
    }

    /// <summary>
    /// The list over a model - a DataGrid wearing the window's styles, with the window's columns
    /// built by the window's builder, over the model's rows.
    /// </summary>
    private static FrameworkElement ListOver(MainViewModel model, ResourceDictionary resources)
    {
        var grid = new DataGrid
        {
            Style = (Style)resources["ListGrid"],
            RowStyle = (Style)resources["ListRow"],
            ItemsSource = model.Rows,
            DataContext = model,
            IsReadOnly = true,
            AutoGenerateColumns = false
        };

        ListColumns.Fill(grid, new ColumnBar(), ColumnPlan.Of(null, EntryScope.Everything));

        return grid;
    }
}
