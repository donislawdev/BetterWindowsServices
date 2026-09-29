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

    /// <summary>
    /// Up to twenty commands stand one at a time, and from the twenty first they stand at once in
    /// one field and the list is EMPTY - W6, G-7: a box per line cost 5013 of the 7185 elements of
    /// a sheet over a whole scope. Empty rather than hidden, so nobody builds those boxes at all.
    /// </summary>
    [Fact]
    public void Past_twenty_lines_the_commands_stand_at_once_and_the_list_holds_none()
    {
        string[] twenty = [.. Enumerable.Range(1, CommandBlock.ListedUpTo).Select(n => $"bws stop Entry{n}")];
        var under = new CommandBlock(twenty);
        var over = new CommandBlock([.. twenty, "bws stop Entry21"]);

        Assert.False(under.AtOnce);
        Assert.Equal(twenty, under.Listed);
        Assert.True(over.AtOnce);
        Assert.Empty(over.Listed);
        Assert.Equal(21, over.Lines.Count);
        Assert.Equal(string.Join(Environment.NewLine, over.Lines), over.All);

        // "Copy all" over several, never over one, and nothing at all over nothing.
        Assert.True(over.Several);
        Assert.False(new CommandBlock(["bws stop Spooler"]).Several);
        Assert.False(CommandBlock.None.Any);

        // And the plan's own block follows its size: twenty one entries, twenty one lines at once.
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(Over(ActionKind.Stop, [.. Enumerable.Range(1, 21).Select(n => $"Entry{n}")])));
        Assert.True(panel.CommandBlock.AtOnce);
        Assert.Equal(panel.Commands, panel.CommandBlock.Lines);
    }

    /// <summary>
    /// The way back is rendered once per run and kept - the block the sheet binds is the same one
    /// however often it is read, a new run makes a new one, and the commands that would ask for the
    /// same thing go once something was asked (`E5`) while the plan's own list stays.
    /// </summary>
    [Fact]
    public void The_way_back_is_made_once_per_run_and_the_commands_go_once_something_was_asked()
    {
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(Over(ActionKind.Stop, "Spooler", "W32Time")));
        Assert.True(panel.CommandBlock.Any);
        Assert.Same(CommandBlock.None, panel.WayBackBlock);

        panel.Finished(PlanFixture.Ran(panel));

        var first = panel.WayBackBlock;

        Assert.NotEmpty(first.Lines);
        Assert.Same(first, panel.WayBackBlock);
        Assert.Same(CommandBlock.None, panel.CommandBlock);
        Assert.NotEmpty(panel.Commands);

        panel.Finished(PlanFixture.Ran(panel));

        Assert.NotSame(first, panel.WayBackBlock);
        Assert.Equal(first.Lines, panel.WayBackBlock.Lines);

        Assert.True(panel.Hide());
        Assert.Same(CommandBlock.None, panel.WayBackBlock);
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
