using System.Windows;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// The plan that sets a running entry to Disabled says it keeps running, and offers to stop it in
/// the same plan - UX-GUI-006 and spec C4, the owner's variant O1 of 2026-09-24.
///
/// <b>Asserted on what reaches the screen</b> - WarningLines and AlsoStopLines read the sheet's own
/// items, so a template that binds to nothing comes back empty rather than right from the model's
/// side of it. The press itself is asked through the window's own door (AlsoStop), the way the
/// forced stop's offer is in ForcedStopFixture. That the BUTTON reaches that door was measured on a
/// live window by tools/gui-probe/screens.ps1 -AlsoStop, which presses it through automation.
/// </summary>
public sealed class AlsoStopGuards
{
    /// <summary>
    /// Both running entries are said to keep running, and there is ONE offer for the selection, with
    /// the count on it - one ask, one press, rather than a button under every sentence.
    /// </summary>
    [Fact]
    public async Task Disabling_running_entries_says_so_and_offers_one_stop_for_all_of_them()
    {
        var window = await Opened();

        var warnings = WpfHost.On(() => window.PlanPanel.WarningLines.ToList());

        Assert.Contains(Texts.Of("gui.plan.warning.keepsRunning", "Spooler"), warnings);
        Assert.Contains(Texts.Of("gui.plan.warning.keepsRunning", "W32Time"), warnings);
        Assert.Equal(
            [Texts.Of("gui.plan.offer.alsoStop.many", 2)],
            WpfHost.On(() => window.PlanPanel.AlsoStopLines.ToList()));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Taking the offer rebuilds the SAME sheet: the setting first and the stop after it for each
    /// entry, the line to paste carrying --stop, the button naming both, and the offer gone because
    /// the sentence it answered is gone.
    /// </summary>
    [Fact]
    public async Task Taking_the_offer_rebuilds_the_sheet_with_the_stop_after_the_setting()
    {
        var window = await Opened(onlySpooler: true);

        Assert.True(await WpfHost.On(() => window.AlsoStop()));
        WpfHost.Settled();

        var model = WpfHost.On(() => (MainViewModel)window.DataContext);
        var steps = WpfHost.On(() => model.Planned.Plan!.Plans.Single().Steps.ToList());

        Assert.Equal([StepOperation.SetStartType, StepOperation.Stop], steps.Select(step => step.Operation));
        Assert.Equal(
            "bws start-type Spooler disabled --stop",
            Assert.Single(WpfHost.On(() => window.PlanPanel.CommandLines.ToList())));
        Assert.Equal(
            Texts.Of(
                "gui.plan.carryOut.setStartTypeAndStop.one",
                "Print Spooler",
                CellFaces.SettingLabel(StartSetting.Disabled)),
            WpfHost.On(() => model.Planned.CarryOutLabel));
        Assert.Empty(WpfHost.On(() => window.PlanPanel.AlsoStopLines.ToList()));

        // And a second press has nothing to take: the sheet already stops.
        Assert.False(await WpfHost.On(() => window.AlsoStop()));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// No offer on a record. Once the plan has run the sheet says what happened, and a sheet asks
    /// one question - `docs/11` 3.12 - so the sentence stays and the button under it goes.
    /// </summary>
    [Fact]
    public async Task A_sheet_that_has_run_offers_nothing()
    {
        var window = await Opened();
        var panel = WpfHost.On(() => ((MainViewModel)window.DataContext).Planned);

        Assert.NotEmpty(WpfHost.On(() => window.PlanPanel.AlsoStopLines.ToList()));

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        Assert.NotEmpty(WpfHost.On(() => window.PlanPanel.WarningLines.ToList()));
        Assert.Empty(WpfHost.On(() => window.PlanPanel.AlsoStopLines.ToList()));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// "Restart as admin" carries the stop across. The sheet had two steps, and asking again without
    /// it would open a different plan from the one somebody restarted to carry out.
    /// </summary>
    [Fact]
    public async Task The_stop_crosses_a_restart_as_administrator_with_the_setting()
    {
        var window = await Opened(onlySpooler: true);

        await WpfHost.On(() => window.AlsoStop());
        WpfHost.Settled();

        var handed = WpfHost.On(window.HandOverNow);

        Assert.Equal((ActionKind.SetStartType, StartSetting.Disabled, true), (handed.Asked, handed.To, handed.AlsoStop));

        var (carried, refused) = HandOver.Read([HandOver.Argument, handed.Encode()]);

        Assert.False(refused);
        Assert.True(carried!.AlsoStop);

        WpfHost.On(window.Close);
    }

    /// <summary>A window with Spooler (and W32Time, unless asked not to) picked and set to Disabled.</summary>
    private static async Task<MainWindow> Opened(bool onlySpooler = false)
    {
        var window = await Ready();

        if (onlySpooler)
        {
            WpfHost.On(() => window.Entries.SelectedItems.Remove(
                ((MainViewModel)window.DataContext).Rows.First(row => row.ServiceName == "W32Time")));
            WpfHost.Settled();
        }

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.SetStartType, StartSetting.Disabled)));
        WpfHost.Until(
            () => window.PlanPanel.Visibility == Visibility.Visible,
            "the plan panel opened for the startup type");

        return window;
    }
}
