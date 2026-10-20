using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Bws.Core.Planning;
using Bws.Core.Tools;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The Tools tab on a built window - `docs/PROJEKT-TOOLS-20261020.md`, owner's decisions T-a to T-h of
/// 2026-10-20: a tab beside the lists that takes the page below it, the bar of tabs staying on every page,
/// and quick fixes that open the ordinary plan sheet and nothing else.
/// </summary>
public sealed class ToolsTabGuards
{
    [Fact]
    public async Task The_tab_takes_the_page_and_leaves_the_query_and_the_scope_waiting()
    {
        var (window, model) = await Ready();

        WpfHost.On(() =>
        {
            model.QueryText = "spool";
            model.Scope = EntryScope.Drivers;
        });

        Press(window);

        Assert.True(WpfHost.On(() => model.Tabs.ShowingTools));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Toolbox.Visibility));

        foreach (var below in new Func<UIElement>[] { () => window.Search, () => window.Filters, () => window.Actions, () => window.Entries })
        {
            Assert.Equal(Visibility.Collapsed, WpfHost.On(() => below().Visibility));
        }

        // The bar of tabs stays, and no list tab is the page any more.
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Scope.Visibility));
        Assert.All(WpfHost.On(() => model.ScopePositions.Select(position => position.IsOn).ToList()), on => Assert.False(on));

        Assert.Equal("spool", WpfHost.On(() => model.QueryText));
        Assert.Equal(EntryScope.Drivers, WpfHost.On(() => model.Scope));
    }

    [Fact]
    public async Task Escape_goes_back_to_the_list_as_it_was_left()
    {
        var (window, model) = await Ready();

        WpfHost.On(() => model.QueryText = "spool");
        Press(window);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.False(WpfHost.On(() => model.Tabs.ShowingTools));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Entries.Visibility));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Search.Visibility));
        Assert.Equal("spool", WpfHost.On(() => model.QueryText));
    }

    [Fact]
    public async Task Ctrl_F_on_the_tab_goes_back_to_the_list()
    {
        var (window, model) = await Ready();

        Press(window);
        WpfHost.On(() => window.Act(Shortcut.FocusQuery, out _));

        Assert.False(WpfHost.On(() => model.Tabs.ShowingTools));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Search.Visibility));
    }

    [Fact]
    public async Task A_list_tab_pressed_on_the_tools_page_goes_to_that_list()
    {
        var (window, model) = await Ready();

        Press(window);
        WpfHost.On(() => model.ScopePositions.Single(position => position.Scope == EntryScope.Drivers).IsOn = true);

        Assert.False(WpfHost.On(() => model.Tabs.ShowingTools));
        Assert.Equal(EntryScope.Drivers, WpfHost.On(() => model.Scope));
        Assert.True(WpfHost.On(() => model.ScopePositions.Single(position => position.Scope == EntryScope.Drivers).IsOn));
    }

    [Fact]
    public async Task The_bar_stays_over_the_overview_and_its_marked_tab_is_the_way_back_to_the_list()
    {
        // T-e: the bar went with the list's controls from 2026-09-01, and by then it held Help as well.
        var (window, model) = await Ready();

        WpfHost.On(() => model.ShowingOverview = true);

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Scope.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Search.Visibility));

        // What a press on the marked tab does - the click handler says it to the position, because a radio
        // button already checked reports nothing through its mark.
        var services = WpfHost.On(() => model.ScopePositions.Single(position => position.Scope == EntryScope.Services));

        Assert.True(WpfHost.On(() => services.IsOn));

        WpfHost.On(() => services.IsOn = true);

        Assert.False(WpfHost.On(() => model.ShowingOverview));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Entries.Visibility));
    }

    [Fact]
    public async Task On_the_way_from_the_overview_to_the_tools_page_the_list_is_never_on_screen()
    {
        // A review of #83: the overview going away raises a notification the window arranges itself on,
        // and with the page's flag still down at that moment the list was "on screen" for the length of
        // the clearing - its controls drawn and its sentence said on the way to a page with neither.
        var (_, model) = await Ready();
        var seen = new List<bool>();

        WpfHost.On(() =>
        {
            model.ShowingOverview = true;
            model.PropertyChanged += (_, changed) =>
            {
                if (changed.PropertyName == nameof(MainViewModel.ShowingOverview))
                {
                    seen.Add(model.ListOnScreen);
                }
            };

            model.Tabs.ToolsTabOn = true;
        });

        Assert.NotEmpty(seen);
        Assert.All(seen, onScreen => Assert.False(onScreen));
    }

    [Fact]
    public async Task Leaving_the_overview_by_a_tab_counts_as_having_seen_it()
    {
        // The overview's own two buttons always wrote it down. A tab is a third road (T-e), and without this
        // the first-run screen would come back at every start for somebody who left it that way.
        var file = WpfHost.Nowhere(seenTheOverview: false);
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler", "Print Spooler")), new SteppedClock());

        await model.LoadAsync();

        _ = WpfHost.Resources;
        var window = WpfHost.On(() => new MainWindow(file, model));

        Assert.True(WpfHost.On(() => model.ShowingOverview));
        Assert.False(new KeptColumns(file).OverviewSeen);

        // Through the model rather than the tab's mark: this window was built without waiting for its
        // bindings to attach (WpfHost.Attached), so a mark set now could reach nothing.
        WpfHost.On(() => model.Tabs.ToolsTabOn = true);

        Assert.False(WpfHost.On(() => model.ShowingOverview));
        Assert.True(new KeptColumns(file).OverviewSeen);
        GC.KeepAlive(window);
    }

    [Fact]
    public async Task A_card_is_live_for_an_entry_the_machine_has_and_says_why_it_is_not_otherwise()
    {
        var (_, model) = await Ready();

        var printer = Card(model, QuickFixes.Printer);
        var sound = Card(model, QuickFixes.Sound);

        Assert.True(printer.Available);
        Assert.Equal(Texts.Of("gui.tools.fix.hint", "Spooler"), printer.Hint);

        Assert.False(sound.Available);
        Assert.Equal(Texts.Of("gui.tools.unavailable.missing", "AudioEndpointBuilder"), sound.Hint);
    }

    [Fact]
    public async Task Before_the_first_reading_a_card_waits_and_after_a_failed_one_it_says_so()
    {
        var waiting = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        Assert.False(Card(waiting, QuickFixes.Printer).Available);
        Assert.Equal(Texts.Of("gui.tools.unavailable.reading"), Card(waiting, QuickFixes.Printer).Hint);

        var machine = new LiveMachine(Rows.Entry("Spooler")) { FailNext = new InvalidOperationException("refused") };
        var failed = new MainViewModel(machine, new SteppedClock());

        await failed.LoadAsync();

        Assert.False(Card(failed, QuickFixes.Printer).Available);
        Assert.Equal(Texts.Of("gui.tools.unavailable.unread"), Card(failed, QuickFixes.Printer).Hint);
    }

    [Fact]
    public async Task Pressing_a_card_opens_the_restart_with_its_dependants_and_says_where_it_came_from()
    {
        var (window, model) = await Ready();

        Press(window);

        // Pressed the way a person presses it: the card's own button, found in the laid-out page.
        WpfHost.On(() =>
        {
            window.Toolbox.Measure(new Size(1200, 800));
            window.Toolbox.Arrange(new Rect(0, 0, 1200, 800));
            window.Toolbox.UpdateLayout();

            var card = Descendants(window.Toolbox).OfType<Button>()
                .Single(button => button.DataContext is ToolCard { Fix: var fix } && fix == QuickFixes.Printer);

            card.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });

        WpfHost.Until(() => model.Planned.Plan is not null, "the plan sheet opened after the card was pressed");

        var asked = WpfHost.On(() => model.Planned.Plan!.Action);

        Assert.Equal(ActionKind.Restart, asked.Kind);
        Assert.Equal(["Spooler"], asked.ServiceNames);
        Assert.True(asked.IncludeDependents);

        // The sheet says where it came from, before it says nothing has been done yet - and changes nothing.
        Assert.StartsWith(
            Texts.Of("gui.tools.fix.because", Card(model, QuickFixes.Printer).Title),
            WpfHost.On(() => model.Planned.Notice),
            StringComparison.Ordinal);
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.PlanPanel.Visibility));
    }

    [Fact]
    public void Every_tool_in_the_catalogue_has_a_section_and_every_fix_a_card()
    {
        var model = new MainViewModel(new LiveMachine(), new SteppedClock());

        Assert.Equal(ToolCatalog.All.Count, model.Tabs.Tools.Sections.Count);
        Assert.Equal(QuickFixes.All, model.Tabs.Tools.Sections.Single().Cards.Select(card => card.Fix));
        Assert.All(model.Tabs.Tools.Sections.Single().Cards, card => Assert.Equal(card.Fix.Typed, card.Command));
    }

    [Fact]
    public async Task The_tools_tab_carries_no_number_where_the_list_tabs_carry_theirs()
    {
        var (window, _) = await Ready();

        var hidden = WpfHost.On(() =>
        {
            var tab = (RadioButton)window.Scope.FindName("ToolsTabButton");
            tab.ApplyTemplate();

            return ((TextBlock)tab.Template.FindName("Size", tab)).Visibility;
        });

        Assert.Equal(Visibility.Collapsed, hidden);
    }

    private static async Task<(MainWindow Window, MainViewModel Model)> Ready()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler", "Print Spooler"), Rows.Entry("W32Time", "Windows Time")),
            new SteppedClock())
        {
            Planned = new Planned { Elevated = true }
        };

        await model.LoadAsync();

        return (WpfHost.Window(model), model);
    }

    /// <summary>The Tools tab pressed the way a click presses it: its mark set, which its binding carries.</summary>
    private static void Press(MainWindow window) =>
        WpfHost.On(() => ((RadioButton)window.Scope.FindName("ToolsTabButton")).IsChecked = true);

    private static ToolCard Card(MainViewModel model, QuickFix fix) =>
        model.Tabs.Tools.Sections.SelectMany(section => section.Cards).Single(card => card.Fix == fix);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            yield return child;

            foreach (var below in Descendants(child))
            {
                yield return below;
            }
        }
    }
}
