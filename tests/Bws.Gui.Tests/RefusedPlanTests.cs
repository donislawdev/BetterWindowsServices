using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A plan in which nothing can be carried out, in a session without administrator rights -
/// UX-GUI-003 of the audit of 2026-09-23, on the sheet rather than on the bar.
///
/// <b>What was wrong:</b> the sentence about rights looked at the session alone, so a plan that was
/// nothing but refusals still said "Restart as admin to carry this out" in red under them - advice
/// that would have changed nothing, given as the reason the button was grey. Two reasons for one
/// grey button, and the louder of them false.
/// </summary>
public sealed class RefusedPlanTests
{
    [Fact]
    public void A_plan_with_nothing_to_carry_out_does_not_advise_a_restart_as_administrator()
    {
        var panel = new Planned { Elevated = false };

        Assert.True(panel.Show(Refused()));

        Assert.Equal(string.Empty, panel.Blocked);
        Assert.False(panel.HasBlocked);

        // The grey button names the reason that holds whatever the session: nothing here can run.
        Assert.Equal(Texts.Of("gui.plan.blocked.nothingToRun"), panel.CarryOutTip);
    }

    /// <summary>The other side, which has to keep working: something to run, and no rights to run it.</summary>
    [Fact]
    public void A_plan_with_something_to_carry_out_still_says_it_needs_rights()
    {
        var panel = new Planned { Elevated = false };

        Assert.True(panel.Show(Stopping("Spooler")));

        Assert.Equal(Texts.Of("gui.plan.blocked.notElevated"), panel.Blocked);
        Assert.Equal(Texts.Of("gui.plan.blocked.notElevated"), panel.CarryOutTip);
    }

    /// <summary>
    /// ON THE SHEET: a plan made only of refusals opens on them - no "In this order" over nothing,
    /// and no red sentence about rights under them - and a plan that can run still has both. Read
    /// off the sections' own Visibility, the way the sheet's other guards read it.
    /// </summary>
    [Fact]
    public async Task The_sheet_opens_on_the_refusals_when_nothing_can_be_carried_out()
    {
        var window = await PlanFixture.Ready(elevated: false);
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        WpfHost.On(() => panel.Show(Refused()));
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => window.PlanPanel.StepsShown));
        Assert.True(WpfHost.On(() => window.PlanPanel.ProblemsShown));
        Assert.Equal(System.Windows.Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Blocked.Visibility));

        WpfHost.On(() => panel.Hide());
        WpfHost.On(() => panel.Show(Stopping("Spooler")));
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => window.PlanPanel.StepsShown));
        Assert.Equal(System.Windows.Visibility.Visible, WpfHost.On(() => window.PlanPanel.Blocked.Visibility));

        WpfHost.On(window.Close);
    }

    private static BulkPlan Stopping(string name) => new()
    {
        Action = new BulkAction(ActionKind.Stop, [name]),
        Plans =
        [
            new OperationPlan
            {
                Action = new ServiceAction(ActionKind.Stop, name),
                Steps = [new PlanStep(name, name, StepOperation.Stop, StepReason.Requested)],
                Warnings = [],
                Problems = []
            }
        ],
        Problems = []
    };

    private static BulkPlan Refused() => new()
    {
        Action = new BulkAction(ActionKind.Stop, ["disk"]),
        Plans = [],
        Problems = [new PlanProblem(PlanProblemKind.NotOperable, "disk", [])]
    };
}
