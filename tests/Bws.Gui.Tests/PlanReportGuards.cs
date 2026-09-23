using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// What the plan panel says once there is a result - `ADR-11`'s pair, arriving on a screen.
///
/// <b>Split out of <see cref="CarryingGuards"/> on 2026-08-19, and the ratchet that asked pointed
/// at a real line.</b> That class is about the window CARRYING a plan out: whether a press turns
/// back, whether the button is live, whether closing mid-run is refused. This one is about the
/// panel afterwards, when the plan and what came of it stand side by side - which is the whole
/// promise of `ADR-11` and a different question from whether the run happened.
///
/// <b>Every state is driven by hand and read back off the WINDOW.</b> Nothing here presses the
/// button for real: a run against a real manager would stop services on whatever computer ran the
/// suite, which is this project's hard rule about writes rather than an oversight.
///
/// <b>Read off the control rather than off the model, which is why this file exists at all.</b>
/// <see cref="Planned"/>'s own answers can be perfectly right while nothing binds to them, and this
/// project has been caught four times by markup that binds correctly and paints something else.
/// </summary>
public sealed class PlanReportGuards
{
    /// <summary>
    /// The title stops asking what would happen once something has.
    ///
    /// <b>THE OWNER'S SECOND SCREENSHOT OF 2026-08-19 IS WHAT FOUND THIS, and no test could
    /// have.</b> It showed "What stopping Spooler would do" standing over "Done. The entry is
    /// where you asked." and over a row already reading Stopped - a conditional question about the
    /// future on top of a report of the past.
    ///
    /// <b>Asserted as a CHANGE rather than against particular words</b>, because the words are in
    /// a language file and belong there. What is held is that a panel showing a result does not
    /// call itself a preview.
    /// </summary>
    [Fact]
    public async Task The_title_stops_asking_what_would_happen_once_something_has()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var asked = WpfHost.On(() => window.PlanPanel.Heading.Text);

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        var reported = WpfHost.On(() => window.PlanPanel.Heading.Text);

        Assert.NotEqual(asked, reported);
        Assert.NotEmpty(reported);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The command that would ask for the same thing goes away once something was asked.
    ///
    /// <b>`E5` is part of the PREVIEW rather than of the report</b>, which is why Krok 2 of this
    /// packet built it before this window could run anything: it is the plan rendered as text
    /// somebody could type INSTEAD of pressing. Left standing after a run it is the wrong command
    /// in the most prominent place on the panel - the owner's screenshot shows it sitting under the
    /// way back, so the last thing a reader meets scanning up from the button is
    /// <c>bws stop Spooler</c> for a stop that already happened.
    /// </summary>
    [Fact]
    public async Task The_command_that_would_ask_for_the_same_thing_goes_away_once_something_was_asked()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => window.PlanPanel.CommandsShown));
        Assert.False(WpfHost.On(() => window.PlanPanel.WayBackShown));

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        // And the way back took its place rather than joining it.
        Assert.False(WpfHost.On(() => window.PlanPanel.CommandsShown));
        Assert.True(WpfHost.On(() => window.PlanPanel.WayBackShown));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A run that half worked says which half, with the manager's own words.
    ///
    /// <b>Rule 8 of the project's untouchable rules at the moment it matters most.</b> A partial
    /// answer reported as "done" is the silent failure that rule exists against, and here the person
    /// is holding a machine other people depend on.
    /// </summary>
    [Fact]
    public async Task What_did_not_work_reaches_the_screen_with_the_reason()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        // Nothing has run, so there is nothing to admit to and no heading claiming otherwise.
        Assert.Empty(WpfHost.On(() => window.PlanPanel.FailureLines));

        WpfHost.On(() => panel.Finished(Ran(panel, refusing: "Spooler")));
        WpfHost.Settled();

        var failures = WpfHost.On(() => window.PlanPanel.FailureLines);

        Assert.Contains(failures, line => line.Contains("Spooler", StringComparison.Ordinal));
        Assert.Contains(failures, line => line.Contains("Access is denied", StringComparison.Ordinal));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The way back reaches the screen as commands somebody can type. `ADR-11`'s reversible promise,
    /// in the cheapest honest form it has, arriving in a window for the first time.
    /// </summary>
    [Fact]
    public async Task The_way_back_reaches_the_screen_as_commands()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Empty(WpfHost.On(() => window.PlanPanel.WayBackLines));

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        var back = WpfHost.On(() => window.PlanPanel.WayBackLines);

        Assert.Contains("bws start Spooler", back);
        Assert.Contains("bws start W32Time", back);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A NEW PLAN DROPS THE REPORT OF THE LAST RUN, and this is the worst bug this panel could have
    /// had: what happened to five services, sitting under the steps of a plan for five different
    /// ones, with nothing on screen to say the two do not belong together.
    /// </summary>
    [Fact]
    public async Task Asking_about_something_else_drops_the_report_of_the_last_run()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        WpfHost.On(() => panel.Finished(Ran(panel, refusing: "Spooler")));
        WpfHost.Settled();

        Assert.NotEmpty(WpfHost.On(() => window.PlanPanel.FailureLines));

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Start)));
        WpfHost.Settled();

        Assert.Empty(WpfHost.On(() => window.PlanPanel.FailureLines));
        Assert.Empty(WpfHost.On(() => window.PlanPanel.WayBackLines));

        // And it can be carried out again, because it is a different ask rather than the same one.
        Assert.True(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));

        WpfHost.On(window.Close);
    }
}
