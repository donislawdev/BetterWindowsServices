using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The window's half of the machine overview: which control has the middle of the window, and the
/// one fact about a profile that decides whether the screen opens at all - `G`.
///
/// <b>A partial rather than a class of its own, and the boundary is where the state stops.</b>
/// Which numbers there are is <see cref="ViewModels.Overview"/>, whether the screen is showing is
/// <see cref="MainViewModel.ShowingOverview"/>, and whether this profile has seen it is
/// <see cref="KeptColumns.OverviewSeen"/> - three classes that know nothing about each other. What
/// is here is the introduction of the parties, which needs a grid, a bar, a model and a file at
/// once, and is therefore exactly what a window is for. The same argument
/// <see cref="Arrange"/> makes one file over.
///
/// <b>Its own file because an analyser asked</b> - the constructor went past 120 lines with the
/// wiring in it, which is the second time that has happened and the second time it pointed at a
/// real subject. The first was the columns, on 2026-08-19.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Wires the overview to the window and decides whether it opens on it.
    ///
    /// <b>SHOWN WHEN THIS PROFILE HAS NEVER PUT IT AWAY, which is a fact about a file.</b> `G` says
    /// "przy starcie bez zapisanej konfiguracji" and the obvious reading of that - no preferences
    /// file at all - is a signal eleven probes in tools/ already own for a different purpose:
    /// <c>tools/gui-probe/preferences.ps1</c> moves that file aside on every run so a probe measures
    /// the theme rather than somebody's arrangement. Overloading its absence with "and has never
    /// seen this tool" would open every one of them on this screen instead of the list they exist to
    /// measure. Schema 4 separates the two facts - owner's decision, 2026-08-25, and
    /// <see cref="ColumnLayouts.OverviewSeen"/> carries the argument in full.
    ///
    /// <b>The order of the last two lines is load-bearing.</b> Setting the state raises nothing when
    /// the profile HAS seen it - the value is already false - so a window that relied on the change
    /// alone would open with the overview's collapsed markup and a grid nobody had told to be
    /// visible. Arranging once unconditionally is what makes both paths end in a drawn window.
    /// </summary>
    private void IntroduceTheOverview()
    {
        _model.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(MainViewModel.ShowingOverview))
            {
                ArrangeTheMiddle();
            }
        };

        // WRITTEN THE MOMENT IT IS PUT AWAY rather than when the window closes, which is the case
        // this exists for: a first run ended by a machine going down would otherwise show the screen
        // again to somebody who had already read it. KeptColumns carries the rest of that argument.
        Overview.Finished += (_, _) => _kept?.TheOverviewWasSeen(_model.Says);

        // AND THE WAY BACK TO IT, which `G` does not ask for and which the screen needs - the
        // argument is beside the button, in ActionBar.xaml. It asks and the window answers, the
        // same arrangement every other button on that bar uses.
        Actions.OverviewRequest += (_, _) => _model.ShowingOverview = true;

        _model.ShowingOverview = _kept is { OverviewSeen: false };

        ArrangeTheMiddle();
    }

    /// <summary>
    /// Puts either the list or the machine overview in the middle of the window, never both.
    ///
    /// <b>Three controls rather than two, and the empty state is the one that is easy to forget.</b>
    /// It sits OVER the grid rather than instead of it, so collapsing the grid alone would leave a
    /// sentence about a list nobody can see floating across the overview - and on a first run the
    /// sentence it would be saying is "reading the service control manager".
    ///
    /// <b>Arranged by the window rather than by any of the three</b>, exactly as the details and
    /// plan panels are, because none of them has any business knowing the others exist. The window
    /// is what owns the cell they compete for.
    /// </summary>
    private void ArrangeTheMiddle()
    {
        var overview = _model.ShowingOverview;

        Overview.Visibility = overview ? Visibility.Visible : Visibility.Collapsed;
        Entries.Visibility = overview ? Visibility.Collapsed : Visibility.Visible;

        // THE EMPTY STATE IS PUT BACK BY CLEARING RATHER THAN BY SETTING, AND THE DIFFERENCE IS A
        // FAULT THIS METHOD SHIPPED FOR ONE BUILD. That control decides its own visibility from a
        // style trigger - it is on screen only while there is a sentence to say - and a local value
        // beats a style setter in WPF's precedence. So writing Visible here did not "put it back",
        // it took the decision away, and the panel then sat over a list full of rows saying nothing.
        //
        // Clearing hands the property back to the style, which is the only thing that knows whether
        // there is anything to say. Setting Collapsed is still a local value and that is correct:
        // while the overview has the middle of the window, nothing about a list is worth saying.
        if (overview)
        {
            ListEmptyState.Visibility = Visibility.Collapsed;
        }
        else
        {
            ListEmptyState.ClearValue(VisibilityProperty);
        }
    }
}
