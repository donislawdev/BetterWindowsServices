using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// "Set recovery..." on a window - the button, the form on the sheet, the plan the form asks for, the sentence
/// after it runs, and the buttons on a line of the list (backlog 555 part a2). Until 2026-11-25 the form was
/// tested as a model only, and every line between the button and the plan first ran on a screen.
///
/// <b>Nothing touches a machine</b>: the plan is built against the fixture's machine, which has no recovery
/// reader, and the run is a record made by hand from the plan on screen (<see cref="PlanFixture"/>).
/// </summary>
public sealed class RecoveryWindowGuards
{
    [Fact]
    public async Task Set_recovery_opens_the_form_and_the_plan_it_asks_for_runs_and_says_so()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        WpfHost.On(() => window.Actions.Recovery.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        WpfHost.Until(() => panel.Question is not null, "the sheet opened on the form");

        var form = WpfHost.On(() => panel.Question!.Form);

        // Two entries picked, and nothing to carry out until the form says something.
        Assert.Equal(2, form.Entries);
        Assert.Null(WpfHost.On(() => panel.Plan));
        Assert.False(WpfHost.On(() => panel.CanCarryOut));

        WpfHost.On(() => form.ProgramText = "C:\\fix.cmd");
        WpfHost.Until(() => panel.Plan is not null && panel.Question is { Outdated: false }, "a plan arrived for what the form says");

        var steps = WpfHost.On(() => panel.Plan!.Plans.SelectMany(plan => plan.Steps).ToList());

        Assert.Equal(["Spooler", "W32Time"], steps.Select(step => step.ServiceName).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(steps, step => Assert.Equal(RecoveryParts.Command, step.Recovering!.Change.Parts));
        Assert.True(WpfHost.On(() => panel.CanCarryOut));
        Assert.Equal(Texts.Of("gui.plan.carryOut.recovery.many", 2), WpfHost.On(() => panel.CarryOutLabel));
        Assert.Contains(Texts.Of("gui.plan.doing.setRecovery"), WpfHost.Drawn(window.PlanPanel.Heading), StringComparison.Ordinal);

        // Taking the only tick off asks for nothing, so the plan goes and the form stays.
        WpfHost.On(() => form.ChangesProgram = false);
        WpfHost.Until(() => panel.Plan is null, "the plan went with the last tick");

        Assert.Same(form, WpfHost.On(() => panel.Question?.Form));
        Assert.False(WpfHost.On(() => panel.CanCarryOut));

        WpfHost.On(() => form.ProgramText = "C:\\fix.cmd");
        WpfHost.Until(() => panel.Plan is not null && panel.Question is { Outdated: false }, "the plan came back with the program");

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        Assert.Equal(Texts.Of("gui.plan.notice.done.recovery.many", 2), WpfHost.On(() => window.PlanPanel.Notice.Text));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The add button puts a line on the list, the line's kind button opens a menu of the four kinds, choosing
    /// one sets that line, and the line's mark takes it off - each an edit of the list, so the list is ticked.
    /// </summary>
    [Fact]
    public async Task A_line_added_on_the_form_takes_its_kind_from_the_menu_and_its_mark_takes_it_off()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        WpfHost.On(() => window.Actions.Recovery.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        WpfHost.Until(() => panel.Question is not null, "the sheet opened on the form");

        var form = WpfHost.On(() => panel.Question!.Form);
        var lines = WpfHost.On(() => form.Items.Count);

        Press(window, "recoveryAdd");

        Assert.Equal(lines + 1, WpfHost.On(() => form.Items.Count));
        Assert.True(WpfHost.On(() => form.ChangesList));

        var line = WpfHost.On(() => form.Items[^1]);
        var menu = WpfHost.On(() =>
        {
            var kind = Laid(window).OfType<Button>().Last(button => Equals(button.Tag, "kind"));

            kind.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            return kind.ContextMenu!;
        });

        var offered = WpfHost.On(() => menu.Items.OfType<MenuItem>().Select(item => ((RecoveryKindChoice)item.DataContext).Label).ToList());

        Assert.Equal(
            [
                Texts.Of("gui.recovery.form.kind.nothing"), Texts.Of("gui.recovery.form.kind.restart"),
                Texts.Of("gui.recovery.form.kind.program"), Texts.Of("gui.recovery.form.kind.reboot")
            ],
            offered);

        WpfHost.On(() =>
        {
            menu.Items.OfType<MenuItem>().Last().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.IsOpen = false;
        });

        Assert.Equal(RecoveryAction.RestartComputer, WpfHost.On(() => line.Kind));

        WpfHost.On(() => Laid(window).OfType<Button>().Last(button => Equals(button.Tag, "remove"))
            .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        Assert.Equal(lines, WpfHost.On(() => form.Items.Count));

        WpfHost.On(window.Close);
    }

    private static void Press(MainWindow window, string automationId) =>
        WpfHost.On(() => Laid(window).OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == automationId)
            .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

    /// <summary>The sheet laid out, so the lines of the list have their buttons - a window never shown lays nothing out.</summary>
    private static List<DependencyObject> Laid(MainWindow window)
    {
        window.PlanPanel.Measure(new Size(1000, 1600));
        window.PlanPanel.Arrange(new Rect(0, 0, 1000, 1600));
        window.PlanPanel.UpdateLayout();

        return [.. Descendants(window.PlanPanel)];
    }

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
