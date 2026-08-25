using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// The bar over the list - the only visible way to an operation, arriving 2026-08-25.
///
/// <b>It was found by somebody who had never seen this project.</b> A report written from four
/// screenshots put "there is no action bar" first on a list of ten, and a search of `docs/01`,
/// `docs/04` and `docs/11` for a bar of this kind came back empty: the window has been able to
/// change a machine since 2026-08-19 and the only way in was the right mouse button, which is a
/// feature you have to already know about.
///
/// <b>What these guards are really for is rule 1.</b> A bar of verbs over a list of services is the
/// most tempting place in this product to do something directly, and the promise is that every one
/// of them opens a plan and changes nothing. The press is asserted to land on the panel rather than
/// on the machine.
/// </summary>
public sealed class ActionBarGuards
{
    /// <summary>
    /// Nothing picked means nothing to do, and the buttons say why rather than only refusing.
    ///
    /// <b>The reason is on the button and the attribute that lets it be read is asserted too.</b>
    /// WPF stops serving tooltips for a disabled control, so a sentence bound without
    /// ShowOnDisabled is one that exists everywhere except where somebody would look for it.
    /// </summary>
    [Fact]
    public async Task With_nothing_picked_the_bar_is_off_and_says_why()
    {
        var window = await Ready();

        WpfHost.On(() => window.Entries.UnselectAll());
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => window.Actions.Offering));

        foreach (var button in Buttons(window))
        {
            Assert.False(WpfHost.On(() => button.IsEnabled));

            Assert.True(
                WpfHost.On(() => ToolTipService.GetShowOnDisabled(button)),
                "The reason is bound to a control that will not show a tooltip while it is disabled.");

            Assert.Equal(
                Bws.Gui.Texts.Of("gui.action.needsPick"),
                WpfHost.On(() => button.ToolTip as string));
        }

        // READING THE MACHINE IS NOT ONE OF THE THREE, and nothing about it needs a selection.
        Assert.True(WpfHost.On(() => window.Actions.Refresh.IsEnabled));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A row picked turns the bar on, and what each button says becomes what it would do.
    ///
    /// <b>Told rather than bound, which is the arrangement this window arrived at the hard way.</b>
    /// Nothing here watches the grid's selection - a binding into a list that reconciles itself once
    /// a second is another party in the middle of `A10` - so the bar is handed a count when a person
    /// changes what is picked. This is the guard that the handing over happens at all.
    /// </summary>
    [Fact]
    public async Task A_row_picked_turns_the_bar_on()
    {
        var window = await Ready();

        // Ready leaves two rows picked, so this asserts the state the window is really in.
        Assert.True(WpfHost.On(() => window.Actions.Offering));

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.action.stop.hint"),
            WpfHost.On(() => window.Actions.Stop.ToolTip as string));

        WpfHost.On(() => window.Entries.UnselectAll());
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => window.Actions.Offering));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// PRESSING ONE OPENS A PLAN AND CHANGES NOTHING, which is rule 1 of the project notes standing
    /// on the most tempting surface in this product.
    ///
    /// <b>The press is raised on the real button rather than the handler being called</b>, because
    /// the question is whether the click reaches anything: a bar wired to nothing looks exactly like
    /// a bar that works until somebody presses it.
    ///
    /// <b>What proves nothing was done to the machine</b> is the panel's own state - the plan is on
    /// screen with the sentence saying nothing has happened, and the button that would carry it out
    /// is still there to be pressed.
    /// </summary>
    [Fact]
    public async Task Pressing_one_opens_a_plan_rather_than_doing_anything()
    {
        var window = await Ready();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Visibility));

        WpfHost.On(() => window.Actions.Stop.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.PlanPanel.Visibility));

        // Nothing has been done, said by the panel itself - and the way to do it is still ahead.
        Assert.NotEqual(string.Empty, WpfHost.On(() => window.PlanPanel.Notice.Text));
        Assert.Contains(
            "Spooler",
            WpfHost.On(() => string.Join(" ", window.PlanPanel.StepLines)),
            StringComparison.Ordinal);

        // AND EACH OF THE THREE ASKS FOR ITS OWN THING rather than three buttons wired to one.
        WpfHost.On(() => window.Actions.Restart.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        WpfHost.Settled();

        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Assert.Equal(ActionKind.Restart, WpfHost.On(() => model.Planned.Plan!.Action.Kind));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The filters fold away, and they start open.
    ///
    /// <b>Both halves are the decision.</b> Measured on a live window on 2026-08-25: with the
    /// filters open the first column heading stands at 469 pixels of a window 1050 high, and folded
    /// at 305 - about five rows of list given back. Starting folded would reopen complaint 7 of the
    /// eleven, which was closed by making the filters visible in the first place.
    /// </summary>
    [Fact]
    public void The_filters_start_open_and_fold_away_when_asked()
    {
        var window = WpfHost.Window();

        Assert.True(WpfHost.On(() => window.Filters.Switch.IsChecked) ?? false);
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Filters.Chips.Visibility));

        WpfHost.On(() => window.Filters.Switch.IsChecked = false);
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Filters.Chips.Visibility));

        // AND BACK, because a fold somebody cannot undo is worse than no fold at all.
        WpfHost.On(() => window.Filters.Switch.IsChecked = true);
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Filters.Chips.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The sentence about administrator rights has a way out beside it, and only that sentence.
    ///
    /// <b>NOTHING HERE PRESSES IT, AND THAT IS DELIBERATE RATHER THAN LAZY.</b> Pressing it starts
    /// this program again through the shell and closes the window that asked - in a test run that
    /// means a UAC prompt on somebody's screen and the host this suite lives in shutting down. What
    /// a press does is one line in MainWindow and one file in Elevation.cs, and the file is held by
    /// LayeringGuards instead.
    ///
    /// <b>What IS held here is the half a binding gets wrong:</b> that the way out appears only
    /// while there is something to get out of. Most of what that line says is about the query, and a
    /// button offered beside a sentence about signatures would be a control that means nothing.
    /// </summary>
    [Fact]
    public void The_way_out_of_a_session_without_rights_is_beside_the_sentence_about_it()
    {
        _ = WpfHost.Resources;

        var refused = WpfHost.On(() => new MainWindow(
            WpfHost.Nowhere(),
            new MainViewModel { Says = new Says { Elevated = false } }));

        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => refused.ElevateButton.Visibility));

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.action.elevate"),
            WpfHost.On(() => refused.ElevateButton.Content as string));

        WpfHost.On(refused.Close);

        // AND THE EVEN CLAIM, which is the one that matters: most sessions of this tool are
        // elevated - it is a service manager - so a way out offered always would be a control that
        // is wrong nearly every time it is seen.
        var elevated = WpfHost.On(() => new MainWindow(
            WpfHost.Nowhere(),
            new MainViewModel { Says = new Says { Elevated = true } }));

        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => elevated.ElevateButton.Visibility));

        WpfHost.On(elevated.Close);
    }


    /// <summary>
    /// The start type button offers three types, and pressing one opens a plan that names it.
    ///
    /// <b>THREE AND NOT FIVE, which is a decision rather than a shortened list.</b> Boot and System
    /// belong to drivers, and this tool refuses to operate on a driver at all - offering them would
    /// be two menu items that always end in a refusal.
    ///
    /// <b>And it opens a PLAN, like everything else in this bar.</b> The first ask in this product
    /// that changes a setting rather than moving a service is also the one where pressing a menu
    /// item and having it happen would have felt most natural to write.
    /// </summary>
    [Fact]
    public async Task The_start_type_menu_offers_three_types_and_one_opens_a_plan_naming_it()
    {
        var window = await Ready();

        var items = WpfHost.On(() =>
            window.Actions.StartType.ContextMenu!.Items.OfType<MenuItem>().ToList());

        Assert.Equal(3, items.Count);

        // The headers are read on the interface thread, because a MenuItem belongs to the thread
        // that built it and throws on the property that holds its text.
        Assert.Equal(
            [
                Bws.Gui.Texts.Of("gui.cell.start.automatic"),
                Bws.Gui.Texts.Of("gui.cell.start.manual"),
                Bws.Gui.Texts.Of("gui.cell.start.disabled")
            ],
            WpfHost.On(() => items.Select(item => item.Header as string).ToList()));

        WpfHost.On(() => window.Entries.UnselectAll());
        WpfHost.On(() => window.Entries.SelectedItem = ((MainViewModel)window.DataContext)
            .Rows.First(row => row.ServiceName == "Spooler"));

        WpfHost.Settled();

        WpfHost.On(() => items[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.PlanPanel.Visibility));

        // THE TITLE, AND IT IS HERE BECAUSE A PROBE CAUGHT IT ON A LIVE WINDOW RATHER THAN A TEST.
        // The first run of this menu put "What restarting ... would do" over a step reading "set ...
        // to Disabled": the word for an ask fell through a discard arm to restarting. The panel
        // title is the line somebody reads before changing a machine.
        Assert.Contains(
            Bws.Gui.Texts.Of("gui.plan.doing.setStartType"),
            WpfHost.On(() => window.PlanPanel.Heading.Text),
            StringComparison.Ordinal);

        var steps = WpfHost.On(() => string.Join(" ", window.PlanPanel.StepLines));

        Assert.Contains("Spooler", steps, StringComparison.Ordinal);
        Assert.Contains(Bws.Gui.Texts.Of("gui.cell.start.disabled"), steps, StringComparison.Ordinal);

        // AND THE COMMAND TO PASTE, WHICH FOR ONE DAY WAS NOT THERE. What stood here asserted the
        // absence - the window learned this verb before the command line had a word for it, so a
        // line would have been one that failed the moment somebody used it - and the section
        // disappeared rather than showing one. The command line learned the verb the same week and
        // the section came back.
        //
        // Asserted as the whole string, because what somebody pastes is the whole string. The value
        // on the end is the half that makes it a command rather than a fragment.
        Assert.Equal(
            "bws start-type Spooler disabled",
            Assert.Single(WpfHost.On(() => window.PlanPanel.CommandLines.ToList())));

        WpfHost.On(window.Close);
    }

    private static Button[] Buttons(MainWindow window) =>
    [
        WpfHost.On(() => window.Actions.Stop),
        WpfHost.On(() => window.Actions.Start),
        WpfHost.On(() => window.Actions.Restart)
    ];
}
