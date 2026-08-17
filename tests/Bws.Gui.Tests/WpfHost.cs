// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

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
    /// Somewhere to keep a layout that is not the profile of whoever is running the tests.
    ///
    /// <b>Without this every test that builds a window reads the layout of the person at the
    /// keyboard and then writes over it.</b> Both halves are faults: a test asserting six columns
    /// would fail because somebody had turned one off last night, and a test run would silently
    /// rearrange their window. The same seam is what keeps the width probes measuring the theme
    /// rather than a layout they saved themselves.
    ///
    /// Emptied rather than made unique, so a run never inherits the file the previous run left and
    /// the temporary folder does not grow one directory per run.
    /// </summary>
    internal static PreferencesFile Nowhere()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bws-gui-tests");

        Directory.CreateDirectory(directory);
        File.Delete(Path.Combine(directory, PreferencesFile.Name));

        return new PreferencesFile(directory);
    }

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
        // FOUR FILES SINCE 2026-08-12 AND THE LOOP IS ORDERED, NOT A CONVENIENCE. Every styles
        // file names keys declared in Values.xaml, StaticResource resolves them as the file is
        // read, and each dictionary is added to the application before the next one is parsed - so
        // the later ones find the first the same way all of them find WPF UI's. Backlog 156 for
        // the first split, 163 for the second and 166 for the third, and this is App.xaml's order
        // in every case.
        var themes = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes");

        foreach (var name in new[] { "Values.xaml", "Controls.xaml", "List.xaml", "Cells.xaml" })
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
