using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// The two verbs in the action bar that can end a process - Force stop and Force restart, arriving
/// 2026-09-16, backlog 374 - and the one rule they have that the other verbs do not.
///
/// <b>Their own file rather than more of <see cref="ActionBarGuards"/>, and the size ratchet is what
/// asked</b> - that file crossed 500 lines the moment these four arrived, and the seam is a subject:
/// everything left over there is about a bar of verbs that open a plan, and this is about the two
/// that take exactly one entry, because `docs/ANALIZA-FORCE` 15.6 keeps a forced stop over several
/// unreachable. Two entries in one process would be two plans ending one process, which the bulk
/// plan tells apart by service name and cannot see.
///
/// <b>Both doors are held here</b>: the bar, which greys its buttons and says why on them, and
/// Preview itself, which is where the row menu arrives and where the refusal really lives.
/// </summary>
public sealed class ForcingVerbGuards
{
    /// <summary>
    /// The two verbs that can end a process are off over several entries and say why, and turn on
    /// over exactly one - while the four beside them stay on over any number.
    ///
    /// <b>The bar arrived with five verbs on 2026-09-16, and the fifth and sixth are the first to
    /// refuse a selection the others accept.</b> `docs/ANALIZA-FORCE` 15.6 keeps a forced stop over
    /// several entries unreachable by decision - two entries in one process would be two plans
    /// ending one process - and the bar is where a person first meets that rule, so the reason is
    /// on the button rather than found out at the sheet.
    ///
    /// <b>The sentence over several is a different sentence from the one over none</b>, asserted
    /// apart, because "pick one or more" over five rows would be an instruction already followed.
    /// </summary>
    [Fact]
    public async Task Several_entries_leave_the_forcing_verbs_off_and_one_entry_turns_them_on()
    {
        var window = await Ready();

        // Ready leaves two rows picked, so this is the state the window is really in.
        Assert.True(WpfHost.On(() => window.Actions.Stop.IsEnabled));

        foreach (var forcing in Forcing(window))
        {
            Assert.False(WpfHost.On(() => forcing.IsEnabled));
            Assert.True(WpfHost.On(() => ToolTipService.GetShowOnDisabled(forcing)));
            Assert.Equal(
                Bws.Gui.Texts.Of("gui.action.force.onlyOne"),
                WpfHost.On(() => forcing.ToolTip as string));
        }

        WpfHost.On(() =>
        {
            window.Entries.UnselectAll();
            window.Entries.SelectedItem = window.Entries.Items.OfType<EntryRow>().First();
        });
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => window.Actions.ForceStop.IsEnabled));
        Assert.True(WpfHost.On(() => window.Actions.ForceRestart.IsEnabled));

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.action.forceStop.hint"),
            WpfHost.On(() => window.Actions.ForceStop.ToolTip as string));
        Assert.Equal(
            Bws.Gui.Texts.Of("gui.action.forceRestart.hint"),
            WpfHost.On(() => window.Actions.ForceRestart.ToolTip as string));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A folded per-user family is one row and many entries, and the bar is told the entries.
    ///
    /// <b>This is the guard that the count handed to the bar comes from the same place the plan
    /// is built.</b> A bar told the number of ROWS would light the forcing verbs over a family of
    /// twenty-four, and the sheet behind the press would then be a plan over all of them - which is
    /// exactly the plan 15.6 keeps unreachable. Counted by rows, this test passes with the family
    /// row lit - counted by entries, it is off.
    /// </summary>
    [Fact]
    public async Task A_folded_family_counts_as_many_entries_to_the_bar()
    {
        var machine = new LiveMachine(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537"),
            Rows.Entry("Spooler", "Print Spooler"));

        var model = new MainViewModel(machine, new SteppedClock()) { Planned = new Planned { Elevated = true } };

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        WpfHost.On(() =>
        {
            window.Entries.ItemsSource = model.Rows;
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "CDPUserSvc");
        });
        WpfHost.Settled();

        // NOT VACUOUS: the row really stands for more than itself, or the assertion below would
        // pass on a bar that was simply never told anything.
        Assert.NotEmpty(WpfHost.On(() => ((EntryRow)window.Entries.SelectedItem).Instances));

        Assert.True(WpfHost.On(() => window.Actions.Stop.IsEnabled));
        Assert.False(WpfHost.On(() => window.Actions.ForceStop.IsEnabled));
        Assert.Equal(
            Bws.Gui.Texts.Of("gui.action.force.onlyOne"),
            WpfHost.On(() => window.Actions.ForceStop.ToolTip as string));

        WpfHost.On(() => window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Spooler"));
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => window.Actions.ForceStop.IsEnabled));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Pressing a forcing verb opens the plan that ends a process, and changes nothing.
    ///
    /// <b>Rule 1 on the two most dangerous buttons this window has.</b> The press is raised on the
    /// real button, the sheet that opens is asked which kind it holds and whether it names the step
    /// that ends the process, and the button that would carry it out is still ahead - the same
    /// three questions the ordinary verbs answer, asked of the verbs where a wrong answer costs
    /// the most.
    /// </summary>
    [Fact]
    public async Task Pressing_a_forcing_verb_opens_a_plan_that_ends_the_process_and_nothing_else()
    {
        var window = await Ready();

        WpfHost.On(() =>
        {
            window.Entries.UnselectAll();
            window.Entries.SelectedItem = window.Entries.Items.OfType<EntryRow>()
                .First(row => row.ServiceName == "Spooler");
        });
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));

        WpfHost.On(() => window.Actions.ForceStop.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.Until(
            () => model.Planned.Plan?.Action.Kind == ActionKind.ForceStop,
            "the plan panel opened with the forced stop it was asked for");

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.PlanPanel.Visibility));
        Assert.Equal(["Spooler"], WpfHost.On(() => model.Planned.Plan!.Action.ServiceNames));

        // The sheet names the step that ends the process - the thing a person has to see before
        // agreeing - and says nothing has happened yet.
        Assert.Contains(
            Bws.Gui.Texts.Of("gui.plan.operation.terminate"),
            WpfHost.On(() => string.Join(" ", window.PlanPanel.StepLines)),
            StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, WpfHost.On(() => window.PlanPanel.Notice.Text));
        Assert.Empty(WpfHost.On(() => model.Planned.WayBack));

        // AND THE SECOND ASKS FOR ITS OWN THING rather than two buttons wired to one kind.
        WpfHost.On(() => window.Actions.ForceRestart.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        WpfHost.Until(
            () => model.Planned.Plan?.Action.Kind == ActionKind.ForceRestart,
            "the plan panel came back with the forced restart it was asked for");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A forcing ask over more than one entry is refused before any plan exists, and the refusal
    /// reaches the status line.
    ///
    /// <b>The one place both entrances meet.</b> The bar greys its buttons over such a selection,
    /// but the row menu cannot, so the guard on Preview is the mechanism and the greyed button the
    /// courtesy. Asked of Preview directly, because that is what a menu item calls - and a press
    /// that returned false in silence would be the item that does nothing.
    /// </summary>
    [Fact]
    public async Task A_forcing_ask_over_two_entries_is_refused_before_a_plan_exists()
    {
        var window = await Ready();

        // Ready leaves two rows picked.
        Assert.Equal(2, WpfHost.On(() => window.Entries.SelectedItems.Count));

        Assert.False(await WpfHost.On(() => window.Preview(ActionKind.ForceStop)));
        Assert.False(await WpfHost.On(() => window.Preview(ActionKind.ForceRestart)));
        WpfHost.Settled();

        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));
        Assert.Contains(
            Bws.Gui.Texts.Of("gui.action.force.onlyOne"),
            WpfHost.On(() => model.Says.Problem),
            StringComparison.Ordinal);

        // AND THE ORDINARY VERB OVER THE SAME TWO IS NOT REFUSED - the rule is about ending a
        // process, not about selections of two.
        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));

        WpfHost.On(window.Close);
    }

    private static Button[] Forcing(MainWindow window) =>
    [
        WpfHost.On(() => window.Actions.ForceStop),
        WpfHost.On(() => window.Actions.ForceRestart)
    ];
}
