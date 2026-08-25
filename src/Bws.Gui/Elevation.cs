using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Bws.Gui;

/// <summary>
/// Starting this program again, with the rights this session does not have.
///
/// <b>THE ONLY FILE IN THIS PRODUCT ALLOWED TO NAME A PROCESS, AND THAT IS A DECISION OF THE
/// OWNER'S RATHER THAN AN ARRANGEMENT OF MINE.</b> <c>LayeringGuards</c> has forbidden
/// <c>System.Diagnostics.Process</c> in every shipped assembly since 2026-08-02, with an argument
/// that is still right: a tool running with administrator rights on somebody else's production
/// machine, whose whole subject is which programs that machine launches, is the last place a quiet
/// process start belongs. The guard also says what to do when a slice genuinely needs one - have
/// the conversation and write the reason down.
///
/// <b>The conversation happened on 2026-08-25 and the exception is deliberately narrow.</b> The
/// assembly guard still refuses <c>Activator</c> and <c>AppDomain</c> here, and a second guard -
/// <c>LayeringGuards.Only_one_file_in_the_window_may_start_a_process</c> - reads the sources and
/// reddens if the name appears anywhere but this file. So the exception has a name, a place and a
/// test, rather than being a door left open.
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
    internal static string? Restart()
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
                Verb = "runas"
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
}
