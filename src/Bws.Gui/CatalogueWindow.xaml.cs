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
/// <b>The three sentences below are literals and that is a decision.</b> Rule 13 keeps text a
/// person reads out of the source, and it is right about the product: every word the window says
/// is a translation key. This screen is not the product - it is reached only by a launch argument
/// and read only by whoever is changing the components. Putting its three lines in the language
/// files would be asking a translator for sentences no user will ever be shown.
/// </summary>
public partial class CatalogueWindow : Window
{
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

        Groups = Catalogue.Read(resources);

        var counts = Catalogue.Count(Groups);

        Counted = string.Format(
            CultureInfo.InvariantCulture,
            "{0} components in {1} theme files. {2} drawn here, {3} explained instead.",
            counts.Known,
            Groups.Count,
            counts.Shown,
            counts.Explained);

        DataContext = this;

        InitializeComponent();
    }

    /// <summary>What this screen is.</summary>
    public string Heading => "Component catalogue";

    /// <summary>How much of what is declared is on this sheet.</summary>
    public string Counted { get; }

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
        + "style has no answer to Validation.HasError, so there is no wrong state to draw.";

    /// <summary>The components, grouped by the theme file that declares them.</summary>
    public IReadOnlyList<Catalogue.Group> Groups { get; }
}
