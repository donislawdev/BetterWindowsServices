using System.Runtime.InteropServices;
using Bws.Gui;
using Xunit.Abstractions;

namespace Bws.Integration.Tests;

/// <summary>
/// The desktop's shell, asked the way the Donate button asks it, and stopped one step short.
///
/// <b>Why this is here and not beside the rest of that button's tests.</b> Whether a desktop
/// answers depends on the session the tests run in - a person's desktop has one, a service has
/// none, and the build server is neither of those in a way anybody has measured. So it is a
/// question for a real machine, and it carries no "runs anywhere" mark.
///
/// <b>Every call it makes asks and none of them acts.</b> It finds the desktop's shell object and
/// stops there, so nothing opens on the screen of whoever runs it. Whether the page then opens is
/// answered by pressing the button, never by a test.
///
/// <b>WHICH OF TWO TRUTHS IT HOLDS IS DECIDED BY THE WINDOW MANAGER, NOT BY THE CODE UNDER TEST - a
/// review of 2026-09-23.</b> Until then it failed on any session without a desktop, a remote
/// shell or Server Core, where the button saying where the page is was the intended answer. Skipping
/// there is not open to it - xUnit 2 has no skip decided while a test runs, and a test that returns
/// green when it cannot look is the silent pass this project refuses. Its assertion library does
/// carry a SkipException, and thrown under this project's runner it was reported as a FAILURE,
/// measured the same day with a probe that was then deleted. Nor may it skip when the
/// desktop is not found, because that is the very failure it exists to catch. So it asks the
/// window manager whether this session has a shell window at all, and holds each session to its own
/// truth: with a desktop the shell has to answer, and without one whatever happens has to be
/// something the button puts into words rather than a throw that ends in Mishaps.
/// </summary>
public sealed class DesktopShellContractTests(ITestOutputHelper output)
{
    [Fact]
    public void The_desktop_shell_answers_the_way_the_donate_button_asks_it()
    {
        var sessionHasADesktop = GetShellWindow() != IntPtr.Zero;

        output.WriteLine(sessionHasADesktop
            ? "This session has a shell window, so the desktop has to answer."
            : "This session has no shell window, so asking has to end in something the button can say.");

        object? found = null;
        Exception? failure = null;

        // On a thread of the window's own apartment kind, because that is where the button asks:
        // the hand-over runs on a single threaded apartment of its own, and a test thread is not one.
        var asking = new Thread(() =>
        {
            try
            {
                found = ExternalLinks.Desktop();
            }
            catch (Exception caught) when (caught is not OutOfMemoryException)
            {
                failure = caught;
            }
        });

        asking.SetApartmentState(ApartmentState.STA);
        asking.Start();
        asking.Join();

        if (!sessionHasADesktop)
        {
            // The three the links file turns into a sentence, and nothing else. No assertion on
            // what was found: a session without a desktop of its own may still be answered by the
            // console's, which is the machine's arrangement rather than this program's.
            Assert.True(
                failure is null or COMException or InvalidCastException or UnauthorizedAccessException,
                $"asking the desktop from a session without one threw {failure?.GetType().Name} " +
                $"(0x{failure?.HResult:X8}): {failure?.Message}. The button turns only three kinds " +
                "of refusal into a sentence, so this one would reach the window's last line.");

            return;
        }

        Assert.True(
            failure is null,
            $"asking the desktop threw {failure?.GetType().Name} (0x{failure?.HResult:X8}): " +
            $"{failure?.Message}. On a session with a desktop this is the Donate button failing in " +
            "an administrator's window.");

        Assert.True(
            found is not null,
            "the window manager says this session has a desktop, and the desktop's shell was not " +
            "found. That is the Donate button saying there is no desktop in a session that has one.");
    }

    /// <summary>
    /// The shell's desktop window in this session, or nothing - user32 rather than the COM route
    /// under test, so the two cannot be wrong together in the same way.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();
}
