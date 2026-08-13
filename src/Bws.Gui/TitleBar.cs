using System.Windows;
using System.Windows.Media;

namespace Bws.Gui;

/// <summary>
/// The bar above the window, which is the one surface the theme cannot style.
///
/// <b>Its own file since 2026-08-13, and the size ratchet is what asked.</b> MainWindow crossed
/// five hundred lines when the kept layout arrived, and the dial that counts long files may only
/// go down - so the question was which subject in that file is not about this window at all. This
/// one: everything else there is about what the window DOES - a timer, a key press, a menu, a
/// selection - and this is about what WINDOWS draws around it, through an interop call that the
/// theme, the markup and `ADR-23` cannot reach any other way.
///
/// <b>Why it has to be asked for at all.</b> Merging the control library's dark dictionaries
/// styles everything INSIDE the window and says nothing about the frame, which belongs to the
/// window manager. Measured on screen 2026-08-10: the bar came out #4C4A48 against content at
/// #202020 two rows below it, on a machine whose system theme is dark. Backlog 148.
///
/// <b>Tried first and rejected with a measurement:</b> `ApplicationThemeManager.Apply(window)`
/// from the control library, which changed the bar by nothing at all - #4C4A48 before and after.
/// The library's only other lever is <c>WindowBackdrop</c>, and that is the door Mica and Acrylic
/// come through, which this product refuses because a translucent background turns ClearType off
/// across all eight hundred rows.
/// </summary>
internal static class TitleBar
{
    /// <summary>
    /// Tells the window manager that this window's bar is a dark one, and which dark.
    ///
    /// <b>Only once the window has a HANDLE, which is the whole of why the first attempt at this
    /// did nothing.</b> The attribute is set against a handle, and a WPF window has none until its
    /// source is initialised - so the same call one step earlier is a call about a window that does
    /// not exist yet, and it fails in the quietest way there is: by succeeding.
    ///
    /// <b>Every failure here is deliberately ignored.</b> A pale title bar is a blemish, and taking
    /// the window down over one would be a far worse answer than the blemish.
    /// </summary>
    internal static unsafe void Darken(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        var frame = new Windows.Win32.Foundation.HWND(handle);
        var dark = 1;

        _ = Windows.Win32.PInvoke.DwmSetWindowAttribute(
            frame,
            Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
            &dark,
            sizeof(int));

        // AND THE COLOUR ITSELF, BECAUSE THE DARK MODE FLAG DOES NOT DELIVER WHAT THE OLD COMMENT
        // SAID IT DID. Measured 2026-08-12 on the release build, both states: the caption is
        // #4C4A48 over content at #202020, active and inactive alike. That note claimed the flag
        // closed backlog 148 - it darkens the caption from the light default and stops well short
        // of the window's own colour, so the window still reads as two programs stacked.
        //
        // DWMWA_CAPTION_COLOR is Windows 11 only, and a failure is ignored for the reason above.
        //
        // THE COLOUR COMES FROM THE WINDOW'S OWN BACKGROUND RATHER THAN FROM A NUMBER HERE, which
        // is `ADR-23` reaching the one surface it could not otherwise reach: the frame cannot be
        // styled, but it can be handed the brush the theme already chose. A literal here would be a
        // second copy of the background, and the two would drift the first time anybody changed
        // the theme.
        if (window.Background is SolidColorBrush brush)
        {
            // COLORREF is 0x00BBGGRR, which is the opposite order from every other colour in this
            // product - and getting it backwards produces a plausible wrong colour rather than an
            // error, so it is written out rather than packed in one expression.
            var colour = (uint)(brush.Color.R | (brush.Color.G << 8) | (brush.Color.B << 16));

            _ = Windows.Win32.PInvoke.DwmSetWindowAttribute(
                frame,
                Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR,
                &colour,
                sizeof(uint));
        }
    }
}
