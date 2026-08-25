// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// One single threaded apartment, one Application, and the dictionaries the window really merges.
///
/// <b>Shared because it has to be, not because it is tidier.</b> An Application is a singleton
/// with thread affinity: built on a thread that then ends, it stays in
/// <see cref="Application.Current"/> and every later use of its resources throws about the wrong
/// thread. So a second test class cannot have its own, and the alternative to sharing is a
/// second test that will not run.
///
/// <b>The order below is App.xaml's order and that is load bearing.</b> Our theme files refer to
/// WPF UI's keys with StaticResource, which is resolved WHILE THE FILE IS READ - so read on its
/// own each one fails outright on DefaultDataGridStyle. The merge in App.xaml works because they
/// are parsed in order with the application's own resources as the fallback for the lookup. That
/// is a fact about the shipped window as much as about these tests.
///
/// It costs a live thread and whatever it holds. Named here because this assembly measures the
/// managed heap over a thousand ticks and Parallelism.cs records what happened the last time
/// something allocated beside that measurement - the difference is that this allocates once, on
/// the first call, rather than while anything is being measured.
/// </summary>
internal static class WpfHost
{
    /// <summary>Runs something on the interface thread and brings back what it returned.</summary>
    internal static T On<T>(Func<T> work) => Thread.Value.Invoke(work);

    /// <summary>Runs something on the interface thread.</summary>
    internal static void On(Action work) => Thread.Value.Invoke(work);

    /// <summary>
    /// Waits until bindings and triggers have caught up, before anything asks what the window shows.
    ///
    /// <b>Because a property somebody sets and a property WPF then works out are not the same
    /// moment.</b> A trigger hanging off a binding is re-evaluated by the dispatcher at a lower
    /// priority than the code that changed the source - so reading the result immediately gets the
    /// value from before, and only when the machine is busy enough to make the gap visible.
    ///
    /// <b>Found the hard way, twice, on 2026-08-13:</b> a guard over the details panel passed on
    /// its own, passed with its whole project, and failed in a full solution run - which is the
    /// worst shape a test can have, because it looks like a regression somewhere else.
    ///
    /// ContextIdle rather than Background: it is below every priority WPF uses for binding and
    /// layout, so an empty callback at that level cannot run until they have.
    /// </summary>
    internal static void Settled() =>
        Thread.Value.Invoke(() => { }, DispatcherPriority.ContextIdle);

    /// <summary>Everything the window merges, in the order it merges it.</summary>
    internal static ResourceDictionary Resources => Merged.Value;

    /// <summary>
    /// A window, with the resources it needs already in place.
    ///
    /// <b>The forcing line is the whole point of this method existing.</b> Every StaticResource in
    /// MainWindow.xaml is resolved as the file is read, against the application's resources - so a
    /// window built before those are merged throws, and a window built after one another test
    /// happened to merge them does not. The first arrangement of these tests passed for exactly
    /// that reason and failed the moment the coverage run put the classes in a different order,
    /// which is a test that was green because of its neighbours.
    /// </summary>
    internal static MainWindow Window()
    {
        _ = Resources;

        return On(() => new MainWindow(Nowhere()));
    }

    /// <summary>
    /// A window looking at a machine the caller chose.
    ///
    /// <b>Handed to the window rather than assigned afterwards, and that distinction cost an
    /// afternoon on 2026-08-18.</b> This window keeps its model in a field as well as in its
    /// DataContext, so a test that replaced the DataContext moved every binding and left every
    /// handler talking to the model the window had built for itself. A plan opened on one and a panel
    /// watching the other looks exactly like markup that was never wired up, and the build says
    /// nothing.
    /// </summary>
    /// <param name="seenTheOverview">
    /// Whether the profile this window opens against has already put the machine overview away.
    /// False gives a genuine first run, which is what the tests about that screen need.
    /// </param>
    internal static MainWindow Window(MainViewModel model, bool seenTheOverview = true)
    {
        _ = Resources;

        return On(() => new MainWindow(Nowhere(seenTheOverview), model));
    }

    /// <summary>
    /// Somewhere to keep a layout that is not the profile of whoever is running the tests.
    ///
    /// <b>Without this every test that builds a window reads the layout of the person at the
    /// keyboard and then writes over it.</b> Both halves are faults: a test asserting six columns
    /// would fail because somebody had turned one off last night, and a test run would silently
    /// rearrange their window. The same seam is what keeps the width probes measuring the theme
    /// rather than a layout they saved themselves.
    ///
    /// <b>ONE DIRECTORY PER CALL SINCE 2026-08-25, AND THE DECISION IT REVERSES IS WORTH KEEPING
    /// BECAUSE IT WAS RIGHT UNTIL THE DAY IT WAS NOT.</b> This used to hand every caller the same
    /// directory and empty it - "so a run never inherits the file the previous run left and the
    /// temporary folder does not grow one directory per run". That works while the fixture only ever
    /// DELETES. The moment it also had to WRITE one, deleting and writing stopped being one step:
    /// these tests run in parallel, so a window built while another call was between the two saw no
    /// file at all - and a window with no file opens on the machine overview with its grid collapsed.
    ///
    /// <b>It cost three clipboard tests and they blamed the machine.</b> They failed with "the
    /// clipboard belonged to another process", because a window showing the overview has no rows to
    /// copy, so nothing reached the clipboard and whatever was there before was read back instead.
    /// Backlog 206 predicts a race in exactly those three tests, which is what made it easy to file
    /// this under a known fault rather than find it - it reproduced on every run and that is what
    /// gave it away.
    ///
    /// The parent is still emptied, once per process, so the old reason survives the change.
    ///
    /// <b>A PROFILE THAT HAS USED THIS TOOL BEFORE, since 2026-08-25, and an empty directory is no
    /// longer that.</b> `G` opens the window on the machine overview instead of the list until
    /// somebody has put it away once, so a test that wanted a window with rows in it was suddenly
    /// getting a window with numbers in it - and every assertion about the grid, the columns and the
    /// empty state went with it. The fixture now says what it always meant: not the person's layout,
    /// and not a first run either.
    ///
    /// <b>The file names NO columns, which is a shape this product already has a test for.</b> An
    /// empty <c>columns</c> array is read, accepted, says nothing worth reporting, and leaves every
    /// scope on its defaults - so the six columns these tests assert about are still the theme's
    /// rather than this fixture's. Writing a real layout here would make every column test a test of
    /// this method.
    /// </summary>
    /// <param name="seenTheOverview">
    /// Whether this profile has already put the machine overview away. False gives a genuine first
    /// run, which is what the tests about that screen need and what no other test wants.
    /// </param>
    internal static PreferencesFile Nowhere(bool seenTheOverview = true)
    {
        var directory = Path.Combine(
            Parent.Value,
            Interlocked.Increment(ref profiles).ToString(System.Globalization.CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);

        if (seenTheOverview)
        {
            File.WriteAllText(
                Path.Combine(directory, PreferencesFile.Name),
                $$"""{ "columns": [], "overviewSeen": true, "schemaVersion": {{ColumnLayout.CurrentSchemaVersion}} }""");
        }

        return new PreferencesFile(directory);
    }

    /// <summary>Which profile this is, so two parallel tests never share one.</summary>
    private static int profiles;

    /// <summary>
    /// The folder every test profile lives under, emptied once per process.
    ///
    /// <b>Lazy rather than in a static constructor, so the emptying happens on the first call rather
    /// than whenever the runtime decides to initialise this type</b> - and once, however many
    /// threads arrive at the same moment, which is what Lazy is for.
    /// </summary>
    private static readonly Lazy<string> Parent = new(() =>
    {
        var parent = Path.Combine(Path.GetTempPath(), "bws-gui-tests");

        if (Directory.Exists(parent))
        {
            Directory.Delete(parent, recursive: true);
        }

        Directory.CreateDirectory(parent);

        return parent;
    });

    /// <summary>The colour the theme declares under a name, so no expected value is written twice.</summary>
    internal static Color Declared(string name) =>
        On(() => ((SolidColorBrush)Resources[name]).Color);

    private static readonly Lazy<Dispatcher> Thread = new(() =>
    {
        Dispatcher? dispatcher = null;

        using var ready = new ManualResetEventSlim();

        var thread = new System.Threading.Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;

            ready.Set();

            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "gui tests"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        ready.Wait();

        return dispatcher!;
    });

    private static readonly Lazy<ResourceDictionary> Merged = new(() => On(() =>
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;

        var application = Application.Current
            ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        application.Resources.MergedDictionaries.Add(
            new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });

        application.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

        // The product's real files, read off disk rather than copied. A copy would answer a
        // question about the copy - the same rule tools/wpfui-probe is built on.
        //
        // SIX FILES SINCE 2026-08-17 AND THE LOOP IS ORDERED, NOT A CONVENIENCE. Every styles
        // file names keys declared in a value file, StaticResource resolves them as the file is
        // read, and each dictionary is added to the application before the next one is parsed - so
        // the later ones find the earlier the same way all of them find WPF UI's. Backlog 156 for
        // the first split, 163 for the second, 166 for the third and 189 for the values splitting
        // into three, and this is App.xaml's order in every case.
        //
        // SPELLED OUT RATHER THAN GLOBBED, unlike the guards that read these files looking for a
        // declaration. Those want every file and do not care in which order they see them. This
        // one is building a real application, so the order IS the behaviour - a directory listing
        // happens to be alphabetical, which would put Cells.xaml first and leave every name in it
        // resolving to nothing.
        var themes = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes");

        foreach (var name in new[]
                 {
                     "Values.xaml", "Colours.xaml", "Columns.xaml",
                     "Text.xaml", "Controls.xaml", "List.xaml", "Marks.xaml", "Cells.xaml"
                 })
        {
            using var stream = File.OpenRead(Path.Combine(themes, name));

            application.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(stream));
        }

        // And the language, the same way App.xaml.cs puts it there. Without it every heading and
        // every menu item in the markup resolves to nothing - which is not an error and would
        // make a window built here quietly unlike the one that ships.
        PutTheLanguageIn(application.Resources);

        return application.Resources;
    }));

    /// <summary>
    /// Reaches the product's own text loader, which is internal on purpose.
    ///
    /// Reflection rather than a copy of the file, because a copy would drift and this is checking
    /// the shipped window rather than an arrangement invented here.
    /// </summary>
    private static void PutTheLanguageIn(ResourceDictionary resources) =>
        typeof(MainWindow).Assembly
            .GetType("Bws.Gui.Texts", throwOnError: true)!
            .GetMethod("PutInto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [resources]);
}
