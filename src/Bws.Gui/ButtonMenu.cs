using System.Windows.Controls;

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
        if (button?.ContextMenu is not { } menu)
        {
            return false;
        }

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;

        return true;
    }
}
