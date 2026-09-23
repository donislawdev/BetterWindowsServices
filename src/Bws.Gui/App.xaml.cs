using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The application, and the one place the chosen language reaches the markup.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // FIRST, BECAUSE THE TWO LINES UNDER IT ARE THEMSELVES THINGS THAT CAN THROW. Mishaps
        // carries the whole argument, including what it deliberately does not cover.
        Mishaps.Arm(this);

        Texts.PutInto(Resources);

        // One class handler for every cell tooltip in the list, registered once. CellTips carries
        // the argument, including why no markup names it and why it therefore has to be told to.
        CellTips.Arm();

        HandedOver = HandOver.Read(e.Args);

        ShowTheCatalogueInstead(e);
    }

    /// <summary>
    /// What the window this one was restarted from handed over, if anything - read here because the
    /// arguments are, and taken by the main window after its first reading. UX-GUI-004 (c).
    /// </summary>
    internal (HandOver? Carried, bool Refused) HandedOver { get; private set; }

    /// <summary>
    /// The argument that opens the catalogue. Since 2026-09-23 there is a second one,
    /// <see cref="HandOver.Argument"/>, and it is not an interface either - it is how a window
    /// without rights hands its screen to its own replacement.
    /// </summary>
    private const string CatalogueAsked = "--catalogue";

    /// <summary>
    /// Opens the component catalogue instead of the window, when asked.
    ///
    /// <b>Rule 4 of the owner's GUI rules asks for a hidden view showing every component in every
    /// state, and offers a launch argument as the way in.</b> This is that argument. It was the only
    /// one until 2026-09-23 - the command line tool is a separate executable and is where arguments
    /// a person types belong - and the second, the hand-over, is written by this program for itself.
    ///
    /// <b>StartupUri is POINTED SOMEWHERE ELSE rather than cleared, and the first version of this
    /// cleared it.</b> WPF creates the window named there after OnStartup returns, so the catalogue
    /// cannot simply be opened here - that would be two windows. Setting the property to null is
    /// the obvious answer and it is not available: measured 2026-09-10, the setter throws
    /// ArgumentNullException out of Application.set_StartupUri and the process dies before a window
    /// exists. Naming the catalogue instead lets WPF make the one window, the way it always does.
    ///
    /// <b>Matched exactly, not by prefix.</b> An argument this program does not know is ignored in
    /// silence, which is the right answer for a surface with one hidden switch on it - anything
    /// else would be inventing a command line for a window.
    /// </summary>
    private void ShowTheCatalogueInstead(StartupEventArgs e)
    {
        if (!e.Args.Any(argument => string.Equals(argument, CatalogueAsked, StringComparison.Ordinal)))
        {
            return;
        }

        StartupUri = new Uri("CatalogueWindow.xaml", UriKind.Relative);
    }
}
