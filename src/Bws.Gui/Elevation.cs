using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Bws.Core;
using Bws.Gui.ViewModels;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Bws.Gui;

/// <summary>
/// Starting this program again, with the rights this session does not have.
///
/// <b>THE FIRST FILE IN THIS PRODUCT ALLOWED TO NAME A PROCESS, AND THAT IS A DECISION OF THE
/// OWNER'S RATHER THAN AN ARRANGEMENT OF MINE.</b> It was the only one until 2026-09-23, when the
/// Donate button made <see cref="ExternalLinks"/> the second, by the same kind of decision.
/// <c>LayeringGuards</c> has forbidden
/// <c>System.Diagnostics.Process</c> in every shipped assembly since 2026-08-02, with an argument
/// that is still right: a tool running with administrator rights on somebody else's production
/// machine, whose whole subject is which programs that machine launches, is the last place a quiet
/// process start belongs. The guard also says what to do when a slice genuinely needs one - have
/// the conversation and write the reason down.
///
/// <b>The conversation happened on 2026-08-25 and the exception is deliberately narrow.</b> The
/// assembly guard still refuses <c>Activator</c> and <c>AppDomain</c> here, and a second guard -
/// <c>LayeringGuards.Only_the_named_files_in_the_window_may_start_a_process</c> - reads the sources
/// and reddens if the name appears in any file it does not list. So the exception has a name, a
/// place and a test, rather than being a door left open.
///
/// <b>What it does NOT carry over, said rather than left to be found:</b> the query somebody had
/// typed, the columns they had turned on, or which list they were looking at. The new session reads
/// the same kept layout from disk, so the columns and the order come back on their own - the query
/// does not, and a person who had typed one starts it again.
///
/// <b>OUT OF THE COVERAGE MEASUREMENT, AND IT IS THE ONLY FILE IN THIS PRODUCT THAT IS - the owner's
/// decision, 2026-08-25, with lowering the floor beside it as the alternative.</b> Every line below
/// either starts a process or handles the failure of starting one. A test that executed it would put
/// a UAC prompt on somebody's screen and a second copy of this program on their desktop, so the
/// lines are unreachable by construction rather than by neglect - and an untestable file dragging
/// the floor down would lower it for the whole window, where the rest IS testable.
///
/// <b>The attribute rather than a lower floor, because it says WHICH code cannot be run.</b> A floor
/// two tenths lower says nothing about where the hole is, and the next slice would inherit the room
/// without meeting this argument.
///
/// <b>SINCE 2026-10-06 IT ALSO STARTS THE PROGRAM AGAIN FOR SECURITY REPORT S-1, AND SAYS SO WHEN IT
/// WILL NOT START AT ALL.</b> An elevated window whose native libraries were unpacked where others can
/// change them starts once more with them somewhere only administrators can (<see cref="Again"/>), and
/// one that cannot be made safe says why in a box (<see cref="Refuse"/>). Both are "start this program
/// again, or say why not", which is what this file was already allowed to do - so they live here rather
/// than in a third file named in LayeringGuards, which the owner's word of that day allowed but did not
/// ask for. The argument for leaving the file out of coverage holds for both: one starts a process, the
/// other puts a box on the screen that waits for a person.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class Elevation
{
    /// <summary>
    /// What Windows reports when a person answers no to the prompt. It is not a failure of this
    /// program and must not read like one.
    /// </summary>
    private const int Cancelled = 1223;

    /// <summary>
    /// Starts this program again as administrator, or says why it did not.
    ///
    /// <b>Null means it started</b>, which is the one case where the caller has something to do -
    /// the session that asked has no reason to stay open once its replacement is on the way.
    ///
    /// <b>Everything else comes back as a sentence rather than an exception</b>, rule 8: answering
    /// no to the prompt is an ordinary thing a person does, and a window that closed anyway, or
    /// said nothing at all, would leave them with the same session and no idea why.
    /// </summary>
    /// <param name="handOver">
    /// What this window was showing, already encoded - UX-GUI-004 (c). Letters, digits, '-' and '_'
    /// only, so it needs no quoting on a command line. HandOver says why it is read as hostile on the
    /// other side.
    /// </param>
    internal static string? Restart(string handOver)
    {
        // A process with no path on disk is not a state this program reaches - it is here because
        // the answer is nullable and a silent return of "it worked" would be the worst reading.
        if (Environment.ProcessPath is not { } program)
        {
            return Texts.Of("gui.elevate.noPath");
        }

        try
        {
            // UseShellExecute is what makes the verb mean anything: runas is a shell verb, and
            // without the shell it is a string nobody reads.
            using var started = Process.Start(new ProcessStartInfo(program)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = HandOver.Argument + " " + handOver
            });

            return started is null ? Texts.Of("gui.elevate.refused") : null;
        }
        catch (Win32Exception refused) when (refused.NativeErrorCode == Cancelled)
        {
            return Texts.Of("gui.elevate.refused");
        }
        catch (Win32Exception failed)
        {
            return Texts.Of("gui.elevate.failed", failed.Message);
        }
    }

    /// <summary>What the process ends with when it refused to start. Nothing reads it but a script.</summary>
    private const int Refused = 1;

    /// <summary>
    /// Starts this program once more, with its native libraries unpacked into the administrators'
    /// folder - security report S-1, <see cref="Unpacking"/>. Null means it started, and the caller
    /// then ends: the owner's decision of 2026-10-06 was that this process does not wait.
    ///
    /// <b>Without the shell, and that is the difference from <see cref="Restart"/>.</b> The rights are
    /// already the right ones, so the child takes this token as it is, the same arguments word for
    /// word, and the host's variable set in its environment alone - this process's own is not touched.
    /// </summary>
    internal static FolderCheck? Again(IReadOnlyList<string> arguments, string home)
    {
        if (Environment.ProcessPath is not { } program)
        {
            return FolderCheck.Failed(home, UnpackingFault.NotStarted);
        }

        try
        {
            var start = new ProcessStartInfo(program) { UseShellExecute = false };

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            start.Environment[Unpacking.BaseVariable] = home;

            using var started = Process.Start(start);

            return started is null ? FolderCheck.Failed(home, UnpackingFault.NotStarted) : null;
        }
        catch (Win32Exception failed)
        {
            return FolderCheck.Failed(home, UnpackingFault.NotStarted, failed.Message);
        }
    }

    /// <summary>
    /// Says why the program did not start, in a box of user32's rather than WPF's, and answers the code
    /// the process ends with.
    ///
    /// <b>Not WPF, and that is the point of the box rather than a preference.</b> It is shown when the
    /// folder WPF's native half came from could not be trusted, and constructing a WPF application is
    /// already what loads the first library from there. The only other box in this program
    /// (<see cref="Mishaps"/>) is WPF's, because by the time it is needed the window has been allowed
    /// to start.
    /// </summary>
    internal static int Refuse(string sentence)
    {
        _ = PInvoke.MessageBox(
            HWND.Null,
            sentence,
            Texts.Of("gui.window.title"),
            MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_ICONERROR);

        return Refused;
    }
}
