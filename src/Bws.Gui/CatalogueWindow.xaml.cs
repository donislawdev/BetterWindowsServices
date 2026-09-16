using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The window that shows the component catalogue.
///
/// <b>It holds no logic beyond asking.</b> What the components ARE is worked out in
/// <see cref="Catalogue"/>, which takes a resource dictionary and can be asked the same question
/// from a test with no window - which is how <c>CatalogueGuards</c> checks that nothing declared
/// in a theme file is missing from this sheet.
///
/// <b>The sentences below are literals and that is a decision.</b> Rule 13 keeps text a person
/// reads out of the source, and it is right about the product: every word the window says is a
/// translation key. This screen is not the product - it is reached only by a launch argument and
/// read only by whoever is changing the components. Putting its lines in the language files would
/// be asking a translator for sentences no user will ever be shown.
///
/// <b>Two arrivals, since 2026-09-16.</b> The styles and the templates are on the sheet before it
/// is shown - they are built from dictionaries, synchronously. The views arrive after it is shown,
/// because each is drawn over a model that has LOADED a machine, and the model loads the way the
/// product loads: off the thread, back on the thread that asked. A constructor cannot await that,
/// and a constructor that blocked on it would deadlock the thread the answer is coming back to.
/// So the window shows, asks, and adds the views when they come - which on a machine of eight
/// specimens is a few dozen milliseconds later.
/// </summary>
public partial class CatalogueWindow : Window, INotifyPropertyChanged
{
    private readonly ResourceDictionary _resources;
    private readonly long _builtIn;
    private Catalogue.Prepared? _prepared;
    private string _counted;

    /// <summary>
    /// The one WPF calls, when App points StartupUri here.
    ///
    /// <b>It exists because StartupUri makes the window itself</b> - see App.xaml.cs, where the
    /// alternative was measured and throws. Everything it does is hand the running application's
    /// own resources to the constructor below, so there is one place that knows what a catalogue
    /// is built from.
    /// </summary>
    public CatalogueWindow()
        : this(Application.Current.Resources)
    {
    }

    /// <summary>
    /// Builds the sheet out of the dictionaries the application has actually merged.
    /// </summary>
    /// <param name="resources">
    /// Handed in rather than reached for through Application.Current, so that a window built in a
    /// test uses the dictionaries that test merged.
    /// </param>
    public CatalogueWindow(ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        _resources = resources;

        // TIMED, AND THE NUMBER IS PRINTED ON THE SHEET - since 2026-09-16, the day this screen
        // started growing (docs/PROJEKT-KATALOG-20260916.md, section 7). Building the sheet had
        // never been measured, and a screen that renders every sample to a bitmap on the way in,
        // then a group of views over models, is a screen whose build time can grow past a person's
        // patience without anybody noticing - unless the number is on the screen every time it
        // opens. A guard holds a ceiling over the same number.
        var clock = System.Diagnostics.Stopwatch.StartNew();

        Groups = new ObservableCollection<Catalogue.Group>(Catalogue.Read(resources));

        clock.Stop();

        _builtIn = clock.ElapsedMilliseconds;
        _counted = Counting(viewsIn: null);

        DataContext = this;

        InitializeComponent();

        Loaded += async (_, _) => await AddTheViewsAsync().ConfigureAwait(true);

        // The loading samples hold a read each, waiting on a gate. The gate opens with the window,
        // so nothing is left waiting after the sheet is gone.
        Closed += (_, _) => _prepared?.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>What this screen is.</summary>
    public string Heading => "Component catalogue";

    /// <summary>How much of what is declared is on this sheet, and how long it took to build.</summary>
    public string Counted
    {
        get => _counted;

        private set
        {
            _counted = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Counted)));
        }
    }

    /// <summary>
    /// The two states this screen cannot arrange for itself, and how to see them anyway.
    ///
    /// Saying it here rather than leaving the columns to imply it: a sheet with no hover column
    /// looks like a sheet that forgot hover, and hover on a screen full of real controls is one
    /// mouse movement away.
    /// </summary>
    public string Note =>
        "Hover and keyboard focus are not columns here, because neither can be arranged without a "
        + "person. Move the pointer over a sample to see hover, and press Tab to walk the focus "
        + "ring through every control on this sheet. A dash under 'disabled' means the component "
        + "is not a control and has no disabled state to draw. A dash under 'wrong' means its "
        + "style has no answer to Validation.HasError, so there is no wrong state to draw. A dash "
        + "under 'chosen' means nothing in its style or template answers to being checked or "
        + "selected. The last group is the views, each over a model in one of its four states.";

    /// <summary>The components, grouped by the theme file that declares them - and, last, the views.</summary>
    public ObservableCollection<Catalogue.Group> Groups { get; }

    /// <summary>
    /// The views, over models that have loaded - after the window is shown, for the reason in
    /// the header. Timed on its own, and the number is added to the line at the top.
    /// </summary>
    private async Task AddTheViewsAsync()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();

        _prepared = await Catalogue.PrepareViewsAsync().ConfigureAwait(true);

        Groups.Add(Catalogue.Views(_prepared, _resources));

        clock.Stop();

        Counted = Counting(viewsIn: clock.ElapsedMilliseconds);
    }

    private string Counting(long? viewsIn)
    {
        var counts = Catalogue.Count(Groups);

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} components in {1} groups. {2} drawn here, {3} explained instead. Built in {4} ms{5}.",
            counts.Known,
            Groups.Count,
            counts.Shown,
            counts.Explained,
            _builtIn,
            viewsIn is null ? ", views still loading" : ", the views in " + viewsIn.Value.ToString(CultureInfo.InvariantCulture) + " ms more");
    }
}
