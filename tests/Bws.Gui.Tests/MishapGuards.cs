using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the window does with something that threw where nothing was catching.
///
/// <b>Only the decision, and the split that made it reachable is the point of this file.</b> The
/// wiring - subscribing to the dispatcher, and asking the running application for its window - needs
/// a live <c>Application</c>, which this test project does not build and will not: <c>WpfHost</c>
/// puts theme dictionaries together, not an application. Everything ELSE is a plain question with
/// two answers, and it is the half that decides whether the process carries on.
///
/// <b>What is still not checked here, said rather than implied:</b> that the handler is subscribed
/// at all, and that <c>Application.Current.MainWindow.DataContext</c> is where the model actually
/// is. Backlog 248 - the honest instrument for those is a probe against a window that is really
/// running, not a test.
/// </summary>
public sealed class MishapGuards
{
    [Fact]
    public void A_failure_is_said_where_the_window_says_everything_else_it_could_not_do()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        Assert.True(Mishaps.Told(model, new InvalidOperationException("the disk went away")));

        Assert.Contains("the disk went away", model.Says.Problem, StringComparison.Ordinal);
    }

    /// <summary>
    /// With no window there is nobody to tell, and saying so is what lets the process end.
    ///
    /// <b>The answer is the decision rather than a courtesy.</b> Marking a failure handled when
    /// nothing said anything leaves a window on screen pretending everything worked, which is the
    /// silence rule 8 forbids. The case is real rather than hypothetical: a throw during startup,
    /// before there is a window - the shape a broken language file had until the same day this was
    /// written.
    /// </summary>
    [Fact]
    public void With_no_window_yet_nobody_is_told_and_it_says_so()
    {
        Assert.False(Mishaps.Told(null, new InvalidOperationException("during startup")));
    }

    /// <summary>
    /// The whole path, through a real application and a real dispatcher.
    ///
    /// <b>Written 2026-08-26 after this file said the opposite could not be written.</b> The two
    /// tests above cover the decision and left the wiring as a claim - that the handler is
    /// subscribed at all, and that the model really is at
    /// <c>Application.Current.MainWindow.DataContext</c> where the handler goes looking. Both were
    /// reconstructed from reading, which this project counts as unverified.
    ///
    /// <b>What made it writable is that <see cref="WpfHost"/> already builds a real
    /// <c>Application</c> on a real dispatcher thread</b>, for the theme dictionaries. Nothing new
    /// was needed - the claim "the tests do not build an application" was itself unchecked.
    ///
    /// <b>BeginInvoke rather than Invoke, and that is the whole mechanism.</b> An exception inside
    /// Invoke comes back to the caller like any other call. One inside a queued operation has
    /// nowhere to go, which is exactly the shape of a click handler or an awaited continuation, and
    /// it is the shape the dispatcher offers to a handler before ending the process.
    ///
    /// <b>The order of the two assertions is load bearing.</b> If the model were not where the
    /// handler looks, the failure would be unhandled and would take the test host down rather than
    /// reddening a test - so the reachable half is asserted first, and only then is anything
    /// thrown.
    /// </summary>
    [Fact]
    public void The_handler_finds_the_window_and_the_window_lives_through_what_it_catches()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());
        var window = WpfHost.Window(model);
        var was = WpfHost.On(() => Application.Current.MainWindow);

        try
        {
            WpfHost.On(() =>
            {
                Application.Current.MainWindow = window;
                Mishaps.Arm(Application.Current);
            });

            // Where the handler goes looking, asked of the running application rather than of the
            // window we happen to be holding.
            Assert.Same(model, WpfHost.On(() => Application.Current.MainWindow.DataContext));

            WpfHost.On(() =>
            {
                // The operation is discarded on purpose. Awaiting it would hand the exception back
                // to whoever awaited, which is the one shape this handler is NOT for.
                _ = window.Dispatcher.BeginInvoke(
                    new Action(() => throw new InvalidOperationException("the manager went away")));
            });

            WpfHost.Settled();

            // The window is still here to be asked, which is the other half of the promise.
            Assert.Contains(
                "the manager went away",
                WpfHost.On(() => model.Says.Problem),
                StringComparison.Ordinal);
        }
        finally
        {
            WpfHost.On(() => Application.Current.MainWindow = was);
        }
    }
}
