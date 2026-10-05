using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Bws.Gui;

/// <summary>
/// A menu opened by a left click, under the button that asks for it.
///
/// <b>A context menu opened by a left click, which is unusual and is the point.</b> The menu is the
/// surface - already themed by the control library, already used by this window to copy a name -
/// and the button is the discoverability, because anything reachable only by right clicking is a
/// feature you have to already know about. `docs/11` section 6 grades "recognition rather than
/// recall" as one of this window's two weakest heuristics.
///
/// Placed under the button rather than at the pointer, so it reads as belonging to the button
/// rather than as a menu about whatever was clicked.
///
/// <b>Its own file since 2026-08-13, and the size ratchet is what asked.</b> The window carried this
/// reasoning twice, once for the columns and once for the example queries, in two methods that
/// differed only in which button they named - so the seam was already there and the ratchet only
/// made somebody look. The two callers are one line each now, which is also the honest shape: what
/// the window knows is WHICH button, and everything else about opening a menu is the same both
/// times.
/// </summary>
internal static class ButtonMenu
{
    /// <summary>
    /// Opens the button's own menu beneath it, and says whether there was one.
    ///
    /// <b>The answer is worth having rather than being a courtesy.</b> A button that opens nothing
    /// looks exactly like a feature that is not there - and the menus are handed their contents in
    /// code, so "there is no menu" is a state this window can really reach.
    /// </summary>
    internal static bool OpenUnder(Button button)
    {
        if (button?.ContextMenu is not { } menu || Tucked(button))
        {
            return false;
        }

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;

        return true;
    }

    /// <summary>
    /// Whether the button, or anything it stands inside, is put away - G-9 of the external stability
    /// report of 2026-09-29. The overview collapses the row holding Help and the row holding the
    /// columns button, and F1 there opened the help menu under a button that was not on screen, at
    /// whatever position a collapsed element has. A shortcut whose button is away now does nothing
    /// and leaves the key to whatever else wants it.
    ///
    /// <b>Asked of the Visibility each element SETS rather than of IsVisible</b>, which is false for
    /// every element of a window that has not been shown yet - a window built and never shown is
    /// what every test of these menus holds. <b>Up the logical tree first</b>, because a part of the
    /// window in its own file has no visual parent until its template is applied, and the logical
    /// one is there from the moment the markup is read. <b>The window itself is not asked</b>: it
    /// is Collapsed until it is shown, which the first run of these tests found - and a shortcut
    /// cannot reach a window nobody has shown anyway.
    /// </summary>
    private static bool Tucked(DependencyObject element)
    {
        for (var at = element; at is not null and not Window; at = LogicalTreeHelper.GetParent(at) ?? (at is Visual ? VisualTreeHelper.GetParent(at) : null))
        {
            if (at is UIElement { Visibility: not Visibility.Visible })
            {
                return true;
            }
        }

        return false;
    }
}
