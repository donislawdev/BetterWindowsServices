using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The offer under running dependants the plan does not stop - W-4 of the stability report and
/// backlog 496, the owner's decision of 2026-09-30.
///
/// <b>Why it exists.</b> The window never asked for dependants, so a stop of an entry with running
/// dependants was refused by the manager with 1051 and a forced stop was refused outright, and the
/// only way on was to close the sheet and pick them by hand. README said "always in the window".
///
/// <b>Asked through the window's own door (TakeTheOffer) and read off the sheet's items</b>, the way
/// AlsoStopGuards asks the first offer.
/// </summary>
public sealed class InTheWayGuards
{
    [Fact]
    public async Task A_stop_offers_to_stop_them_first_and_rebuilds_the_same_sheet()
    {
        var (window, panel) = await Picked("Spooler");

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        Assert.Equal([Texts.Of("gui.plan.offer.inTheWay.many", 2)], WpfHost.On(() => window.PlanPanel.SentenceOffers.ToList()));

        Assert.True(await WpfHost.On(() => window.TakeTheOffer(PlanOffer.InTheWay)));
        WpfHost.Settled();

        var names = panel.Plan!.Steps.Select(step => step.ServiceName).ToList();

        // The dependants are steps now, before the entry they depend on.
        Assert.True(panel.Plan.Action.IncludeDependents);
        Assert.Equal(3, names.Count);
        Assert.Equal("Spooler", names[^1]);
        Assert.Equal(["Dnscache", "W32Time"], names.Take(2).Order(StringComparer.Ordinal).ToList());
        Assert.Empty(WpfHost.On(() => window.PlanPanel.SentenceOffers.ToList()));
        Assert.Contains(panel.Commands, line => line.Contains("--dependents", StringComparison.Ordinal));
        Assert.True(WpfHost.On(() => window.HandOverNow().Dependents));

        // Asked for once, so offered no more.
        Assert.False(await WpfHost.On(() => window.TakeTheOffer(PlanOffer.InTheWay)));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The refusal of a forced stop under running dependants carries the offer, and taking it turns a
    /// sheet with nothing to run into a plan that stops them before the process is ended.
    /// </summary>
    [Fact]
    public async Task A_forced_stop_refused_for_them_offers_the_way_forward()
    {
        var (window, panel) = await Picked("Spooler");

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.ForceStop)));
        Assert.True(WpfHost.On(() => window.PlanPanel.ProblemsShown));
        Assert.False(panel.Plan!.IsRunnable);
        Assert.Equal([Texts.Of("gui.plan.offer.inTheWay.many", 2)], WpfHost.On(() => window.PlanPanel.SentenceOffers.ToList()));

        Assert.True(await WpfHost.On(() => window.TakeTheOffer(PlanOffer.InTheWay)));
        WpfHost.Settled();

        Assert.True(panel.Plan!.IsRunnable);
        Assert.True(panel.Plan.Action.IncludeDependents);
        Assert.Equal(ActionKind.ForceStop, panel.Plan.Action.Kind);
        Assert.False(WpfHost.On(() => window.PlanPanel.ProblemsShown));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A startup setting carrying a stop warns about them too, and offers nothing: the command line
    /// refuses --dependents on start-type, so the equivalent command would be one it rejects.
    /// </summary>
    [Fact]
    public async Task A_startup_setting_that_stops_offers_nothing_for_them()
    {
        var (window, _) = await Picked("Spooler");

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.SetStartType, StartSetting.Disabled, alsoStop: true)));
        Assert.Contains(
            WpfHost.On(() => window.PlanPanel.WarningLines.ToList()),
            line => line.Contains("W32Time", StringComparison.Ordinal));
        Assert.Empty(WpfHost.On(() => window.PlanPanel.SentenceOffers.ToList()));
        Assert.False(await WpfHost.On(() => window.TakeTheOffer(PlanOffer.InTheWay)));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// THE OFFER ASKS OVER THE PLAN ON THE SHEET, NOT OVER THE PICKED ROWS. A forcing sheet opened
    /// from a failure is about the entry that failed, which need not be the row that was picked, and
    /// asked again over the picked rows the offer would build a plan for a different entry than the
    /// one on the screen. Moving the selection under an open sheet is the cheapest way to part them.
    /// </summary>
    [Fact]
    public async Task The_offer_asks_over_the_plan_on_the_sheet_not_over_the_picked_rows()
    {
        var (window, panel) = await Picked("Spooler");

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));

        WpfHost.On(() =>
        {
            var model = (MainViewModel)window.DataContext;

            window.Entries.SelectedItems.Clear();
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Dnscache");
        });

        Assert.True(await WpfHost.On(() => window.TakeTheOffer(PlanOffer.InTheWay)));

        Assert.Equal(["Spooler"], panel.Plan!.Action.ServiceNames);

        WpfHost.On(window.Close);
    }

    /// <summary>Restarting as administrator asks the same plan again, dependants included.</summary>
    [Fact]
    public void Taking_it_crosses_a_restart_as_administrator()
    {
        var sent = new HandOver
        {
            Scope = EntryScope.Services,
            Query = string.Empty,
            Picked = ["Spooler"],
            Asked = ActionKind.Restart,
            Dependents = true
        };

        var (carried, refused) = HandOver.Read([HandOver.Argument, sent.Encode()]);

        Assert.False(refused);
        Assert.True(carried!.Dependents);
    }

    private static async Task<(MainWindow Window, Planned Panel)> Picked(string name)
    {
        var machine = new LiveMachine(
            Rows.Entry("Spooler", "Print Spooler"),
            Rows.Entry("W32Time", "Windows Time"),
            Rows.Entry("Dnscache", "DNS Client")).DependedOnBy("Spooler", "W32Time", "Dnscache");

        var model = new MainViewModel(machine, new SteppedClock()) { Planned = new Planned { Elevated = true } };

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        WpfHost.On(() =>
        {
            window.Entries.ItemsSource = model.Rows;
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == name);
        });

        WpfHost.Settled();

        return (window, model.Planned);
    }
}
