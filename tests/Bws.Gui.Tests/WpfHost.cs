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
/// <b>The order below is App.xaml's order and that is load bearing.</b> Theme.xaml refers to WPF
/// UI's keys with StaticResource, which is resolved WHILE THE FILE IS READ - so read on its own
/// it fails outright on DefaultDataGridStyle. The merge in App.xaml works because all three are
/// parsed as one unit with the application's own resources as the fallback for the lookup. That
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

        return On(() => new MainWindow());
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

        // The product's real file, read off disk rather than copied. A copy would answer a
        // question about the copy - the same rule tools/wpfui-probe is built on.
        var path = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes", "Theme.xaml");

        using (var stream = File.OpenRead(path))
        {
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
