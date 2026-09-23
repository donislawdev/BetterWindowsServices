using Bws.Gui;

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
/// </summary>
public sealed class DesktopShellContractTests
{
    [Fact]
    public void The_desktop_shell_answers_the_way_the_donate_button_asks_it()
    {
        object? found = null;
        Exception? failure = null;

        // On a thread of the window's own apartment kind, because that is where the button asks:
        // the window's thread is single threaded, and a test thread is not.
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

        Assert.True(
            failure is null,
            $"asking the desktop threw {failure?.GetType().Name}: {failure?.Message}. On a session " +
            "with a desktop this is the Donate button failing in an administrator's window.");

        Assert.True(
            found is not null,
            "the desktop's shell was not found. Run from a session with a desktop - a service, a " +
            "remote shell without one or Server Core has none, and there the button says where the " +
            "page is instead, which is what it is meant to do.");
    }
}
