using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Bws.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Bws.Gui;

/// <summary>
/// The addresses this window hands to a browser, and the two ways it hands them over. One until
/// 2026-09-24 - the support page - and three since the help menu (UX-GUI-014) offered the project's
/// website and its query language page.
///
/// <b>THE SECOND FILE IN THIS WINDOW ALLOWED TO START SOMETHING, and that is the owner's decision
/// of 2026-09-23 rather than an arrangement of mine.</b> <see cref="Elevation"/> was the only one
/// since 2026-08-25, and <c>LayeringGuards.Only_the_named_files_in_the_window_may_start_a_process</c>
/// now names both. ADR-19 allows exactly this and nothing wider: a page opened in the user's own
/// browser after the user pressed something. This program still opens no connection of its own -
/// the address goes to the shell, the shell starts the browser, and the browser is what connects.
///
/// <b>Every destination is a constant here and no caller supplies one.</b> The shell runs whatever
/// string it is handed, so a caller free to pass its own would turn a Donate button into a way of
/// starting anything. <c>ExternalLinksGuards</c> reads the sources to keep it that way.
///
/// <b>WHY TWO WAYS, AND IT IS THE ONE THING HERE THAT IS NOT IN THE SIBLING PROJECT THIS WAS COPIED
/// FROM.</b> This window usually runs as administrator, and a process started from it inherits its
/// token - so an ordinary shell start would open the browser with administrator rights whenever no
/// browser was already running to take the address over. A browser on the internet with those
/// rights is the last thing a tool for production servers should leave behind. So an elevated
/// window hands the address to the DESKTOP's shell instead, which runs as the person at the
/// desktop, and the page opens with their rights. A window without administrator rights has
/// nothing to hand down, and starts the browser directly.
///
/// <b>The desktop route is Microsoft's own, not an invention.</b> Raymond Chen, "How can I launch
/// an unelevated process from my elevated process and vice versa?" (The Old New Thing, 2013-11-18)
/// and "Manipulating the positions of desktop icons" (2013-03-18) for the first half of the
/// sequence, both on devblogs.microsoft.com. Read on 2026-09-23. Measured on this machine the same
/// day: the ShellWindows class is registered as a local server only, with RunAs set to Interactive
/// User - which is why it answers as the person at the desktop even when an administrator of a
/// different account raised this window.
///
/// <b>WHAT IT CANNOT DO, said rather than left to be found.</b> With User Account Control turned
/// off the desktop itself runs with administrator rights, and so does anything it starts - the
/// source above says so, and there is no unelevated token left on such a machine to hand anything
/// to. And where there is no desktop shell at all, on Server Core or with Explorer ended, the
/// elevated window says where the page is rather than starting a browser as administrator. It
/// never falls back to the direct start: that fallback is exactly the outcome this file exists to
/// avoid.
/// </summary>
internal static class ExternalLinks
{
    /// <summary>Where somebody who wants to support the project is sent.</summary>
    internal const string Support = "https://donislawdev.com/support/";

    /// <summary>
    /// IDispatch, asked for by its identifier because the generator does not emit that interface.
    /// The published sequence asks the folder view for exactly this and then for the dual
    /// interface behind it, so this does the same rather than guessing that a shortcut works.
    /// </summary>
    private static readonly Guid DispatchInterface = new("00020400-0000-0000-C000-000000000046");

    /// <summary>SW_SHOWNORMAL - the window the browser opens, shown the way it would be anyway.</summary>
    private const int ShowNormally = 1;

    /// <summary>
    /// The one hand-over of the support page, shared by every press so that a second press while
    /// the shell still has the first joins it. <see cref="ShellHandover"/> says why that matters.
    /// </summary>
    private static readonly ShellHandover Supporting = new(
        () => Open(Support, Session.IsElevated(), Start, HandToDesktop),
        ShellHandover.Patience,
        Texts.Of("gui.support.slow", Support));

    /// <summary>
    /// Hands the support page to a browser, or says why it did not.
    ///
    /// <b>Null means it was handed over</b>, the same shape as <see cref="Elevation.Restart"/>, and
    /// for the same reason: a failure here is a sentence a person reads, rule 8, and it carries the
    /// address so that the way out is typing it into a browser by hand.
    ///
    /// <b>Awaited, and never on the window's thread, since a review of 2026-09-23.</b> Both ways
    /// below call into another process, and the window's thread was the one waiting for it. This is
    /// the only way in from outside this file, so there is no longer a synchronous one to reach for.
    /// </summary>
    internal static Task<string?> OpenSupportAsync() => Supporting.Ask();

    /// <summary>The project's website, from the help menu - UX-GUI-014, 2026-09-24.</summary>
    internal const string ProjectPage = "https://betterwindowsservices.donislawdev.com/";

    /// <summary>
    /// The page describing the query language, from the help menu. The same address the README
    /// gives, so a person who found it there finds the same page here.
    /// </summary>
    internal const string QueryLanguagePage = "https://betterwindowsservices.donislawdev.com/query-language/";

    /// <summary>
    /// The two pages the help menu offers, each with a hand-over of its own for the reason
    /// <see cref="Supporting"/> has one: a second press while the shell still has the first joins
    /// it rather than starting another. THE SAME TWO WAYS AS THE SUPPORT PAGE AND NO THIRD - an
    /// elevated window hands the address to the desktop, and this adds no call into the shell.
    /// </summary>
    private static readonly ShellHandover Project = Page(ProjectPage);

    /// <inheritdoc cref="Project"/>
    private static readonly ShellHandover QueryLanguage = Page(QueryLanguagePage);

    /// <summary>Hands the project's website to a browser, or says why it did not.</summary>
    internal static Task<string?> OpenProjectPageAsync() => Project.Ask();

    /// <summary>Hands the query language page to a browser, or says why it did not.</summary>
    internal static Task<string?> OpenQueryLanguagePageAsync() => QueryLanguage.Ask();

    private static ShellHandover Page(string address) => new(
        () => Open(address, Session.IsElevated(), Start, HandToDesktop, "gui.link.failed"),
        ShellHandover.Patience,
        Texts.Of("gui.link.slow", address));

    /// <summary>
    /// Which of the two ways, and what a failure says.
    ///
    /// <b>A function of its inputs so that the rule can be tested without opening anything.</b> The
    /// rule is the whole safety property of this file: an elevated window goes to the desktop and
    /// ONLY to the desktop, whatever the desktop answers.
    /// </summary>
    internal static string? Open(
        string address,
        bool elevated,
        Func<string, string?> start,
        Func<string, string?> handToDesktop,
        string failedKey = "gui.support.failed")
    {
        var reason = elevated ? handToDesktop(address) : start(address);

        return reason is null ? null : Texts.Of(failedKey, reason, address);
    }

    /// <summary>
    /// Starts the browser directly, with this process's own rights.
    ///
    /// Internal rather than private so that a test can hand it a path the shell is certain to
    /// refuse - a file that does not exist - which is the only honest way to prove the failure
    /// path, and it opens no window: the runtime asks the shell for no error dialog.
    /// </summary>
    internal static string? Start(string address)
    {
        try
        {
            // UseShellExecute is what makes this open the user's browser instead of trying to run
            // the address as a program, and it is why every destination is a constant above.
            using var started = Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
            return null;
        }
        catch (Win32Exception failure)
        {
            return failure.Message;
        }
    }

    /// <summary>
    /// Asks the desktop's shell to open the address, so that it opens with the desktop's rights.
    ///
    /// <b>The three exceptions are what COM interop raises for the three ways this realistically
    /// fails</b> - the shell's process refusing or gone, an interface it does not offer, and a
    /// refusal of access. Anything else would mean the shell breaking its own published contract,
    /// and it reaches the window's own last line in Mishaps, which also says what happened.
    /// <see cref="Refused"/> says what the three read as.
    /// </summary>
    private static string? HandToDesktop(string address)
    {
        try
        {
            if (Desktop() is not { } shell)
            {
                return Texts.Of("gui.support.noDesktop");
            }

            var file = Marshal.StringToBSTR(address);

            try
            {
                // The order differs from ShellExecute's own, which the source above warns about:
                // file, parameters, directory, verb, show. An empty verb is the default one.
                shell.ShellExecute(new BSTR(file), string.Empty, string.Empty, string.Empty, ShowNormally);
            }
            finally
            {
                Marshal.FreeBSTR(file);
            }

            return null;
        }
        catch (COMException failure)
        {
            return Refused(failure);
        }
        catch (InvalidCastException failure)
        {
            return Refused(failure);
        }
        catch (UnauthorizedAccessException failure)
        {
            return Refused(failure);
        }
    }

    /// <summary>
    /// What a refusal by the desktop reads as: a sentence of this window's own, and the number
    /// Windows gave.
    ///
    /// <b>NEVER THE EXCEPTION'S OWN TEXT - a review of 2026-09-23.</b> For two of the likeliest
    /// refusals the runtime writes that text itself, in English and about itself: "Unexpected
    /// HRESULT has been returned from a call to a COM component." for a plain failure and "Specified
    /// cast is not valid." for an interface the desktop does not offer, measured on .NET 10.0.12.
    /// Both reached the status line. The number is the part that means the same thing in every
    /// language and can be looked up.
    ///
    /// <b>The direct start keeps the system's sentence, and that is not an oversight.</b> A
    /// Win32Exception always carries the text Windows itself gives for the code, in the language of
    /// the machine - "no application is associated" is the answer a person can act on - and the
    /// restart as administrator beside it in Elevation reports its failure the same way.
    /// </summary>
    internal static string Refused(Exception failure) =>
        Texts.Of("gui.support.desktopRefused", failure.HResult.ToString("X8", CultureInfo.InvariantCulture));

    /// <summary>
    /// The shell object of the desktop, or null when there is no desktop to ask.
    ///
    /// <b>Internal so that a test can reach the desktop without starting anything</b> - every call
    /// here asks and none of them acts, so a machine with a desktop can prove the route exists.
    /// </summary>
    internal static unsafe IShellDispatch2? Desktop()
    {
        // A local server and nothing else, which is what the registration says ShellWindows is.
        // Asking for it any other way could only ever find a copy that is not the desktop's.
        //
        // A failure throws what every other call below throws for the same number, so the caller
        // has one shape to turn into a sentence rather than a sentence of this method's own that
        // it would have to recognise.
        PInvoke.CoCreateInstance<IShellWindows>(
            typeof(ShellWindows).GUID, null, CLSCTX.CLSCTX_LOCAL_SERVER, out var windows).ThrowOnFailure();

        object location = (int)PInvoke.CSIDL_DESKTOP;
        object empty = null!;

        // No desktop window is an answer rather than a failure - Server Core has none.
        if (windows.FindWindowSW(in location, in empty, ShellWindowTypeConstants.SWC_DESKTOP, out _,
                ShellWindowFindWindowOptions.SWFO_NEEDDISPATCH) is not Windows.Win32.System.Com.IServiceProvider desktop)
        {
            return null;
        }

        desktop.QueryService<IShellBrowser>(PInvoke.SID_STopLevelBrowser, out var browser);
        browser.QueryActiveShellView(out var view);

        var dispatch = DispatchInterface;
        view.GetItemObject(_SVGIO.SVGIO_BACKGROUND, &dispatch, out var background);

        return ((IShellFolderViewDual)background).Application as IShellDispatch2;
    }
}
