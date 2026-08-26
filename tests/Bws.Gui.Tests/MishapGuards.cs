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
}
