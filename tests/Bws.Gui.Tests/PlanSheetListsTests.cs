using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The lists on the plan sheet are built once for a plan, not once for every binding that reads
/// them - G-7 of the external performance report of 2026-09-28, which counted four renderings of
/// every command and three builds of the warnings on one showing.
/// </summary>
public sealed class PlanSheetListsTests
{
    [Fact]
    public void The_commands_and_the_warnings_are_the_same_lists_however_often_they_are_read()
    {
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(Over(ActionKind.Stop, "Spooler", "W32Time")));

        Assert.NotEmpty(panel.Commands);
        Assert.NotEmpty(panel.Warnings);
        Assert.Same(panel.Commands, panel.Commands);
        Assert.Same(panel.Warnings, panel.Warnings);
        Assert.Equal(EquivalentCommand.For(panel.Plan!), panel.Commands);

        // Kept beside the plan and never past it: a new plan is a new list.
        var first = panel.Commands;

        Assert.True(panel.Show(Over(ActionKind.Start, "Spooler")));
        Assert.NotSame(first, panel.Commands);
        Assert.Equal(EquivalentCommand.For(panel.Plan!), panel.Commands);

        Assert.True(panel.Hide());
        Assert.Empty(panel.Commands);
        Assert.Empty(panel.Warnings);
    }

    private static BulkPlan Over(ActionKind kind, params string[] names) => new()
    {
        Action = new BulkAction(kind, names),
        Plans =
        [
            .. names.Select(name => new OperationPlan
            {
                Action = new ServiceAction(kind, name),
                Steps = [new PlanStep(name, name, kind == ActionKind.Start ? StepOperation.Start : StepOperation.Stop, StepReason.Requested)],
                Warnings = [new PlanWarning(PlanWarningKind.ReturnsAfterReboot, name, [])],
                Problems = []
            })
        ],
        Problems = []
    };
}
