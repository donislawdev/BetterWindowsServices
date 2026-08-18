using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// The command line that would ask for the same thing as a plan. `E5`.
///
/// <b>What is asserted here is the TEXT, and what is asserted next door in the command line's own
/// tests is that the text is a command this tool really accepts.</b> Those are two different
/// questions and only the second one can catch a switch rendered onto a verb that refuses it - which
/// is a fault this class had while it was being written.
/// </summary>
public sealed class EquivalentCommandTests
{
    [Fact]
    public void One_entry_is_the_verb_and_the_internal_name()
    {
        Assert.Equal(
            "bws stop Spooler",
            EquivalentCommand.For(new ServiceAction(ActionKind.Stop, "Spooler")));
    }

    [Fact]
    public void All_three_verbs_are_spelled_out()
    {
        Assert.Equal("bws stop X", EquivalentCommand.For(new ServiceAction(ActionKind.Stop, "X")));
        Assert.Equal("bws start X", EquivalentCommand.For(new ServiceAction(ActionKind.Start, "X")));
        Assert.Equal("bws restart X", EquivalentCommand.For(new ServiceAction(ActionKind.Restart, "X")));
    }

    [Fact]
    public void Asking_for_the_cascade_adds_the_switch_that_asks_for_it()
    {
        Assert.Equal(
            "bws stop Spooler --dependents",
            EquivalentCommand.For(new ServiceAction(ActionKind.Stop, "Spooler", IncludeDependents: true)));

        Assert.Equal(
            "bws restart Spooler --dependents",
            EquivalentCommand.For(new ServiceAction(ActionKind.Restart, "Spooler", IncludeDependents: true)));
    }

    /// <summary>
    /// A REAL FAULT THIS CLASS HAD, CAUGHT BEFORE IT REACHED THE WINDOW.
    ///
    /// The switch is refused by the command line on a start, because starting is not the mirror of
    /// stopping - the manager brings up what an entry needs by itself. A selection carries one such
    /// flag for every entry in it, so somebody ticking it and asking for a start would have been
    /// handed a line that fails the moment it is pasted.
    /// </summary>
    [Fact]
    public void Starting_never_carries_the_switch_the_tool_refuses_there()
    {
        Assert.Equal(
            "bws start Spooler",
            EquivalentCommand.For(new ServiceAction(ActionKind.Start, "Spooler", IncludeDependents: true)));
    }

    /// <summary>
    /// The lines come out in the order the plans will be carried out, not the order somebody clicked.
    ///
    /// This is what makes them worth pasting into a runbook: LanmanWorkstation needs MRxSmb20, so a
    /// runbook in the clicked order would fail on its first line.
    /// </summary>
    [Fact]
    public void A_selection_renders_in_the_order_the_plans_will_run()
    {
        var lines = EquivalentCommand.For(Bulk(ActionKind.Stop, ["MRxSmb20", "LanmanWorkstation"]));

        Assert.Equal(["bws stop LanmanWorkstation", "bws stop MRxSmb20"], lines);
    }

    /// <summary>
    /// An entry the plan refused gets no line, because there is no command that would make the
    /// command line do it either. Writing one would hand somebody a line that fails.
    /// </summary>
    [Fact]
    public void An_entry_with_no_plan_gets_no_command()
    {
        var lines = EquivalentCommand.For(Bulk(ActionKind.Stop, ["amduw23g-202073-df09ebb6", "Spooler"]));

        Assert.Equal(["bws stop Spooler"], lines);
    }

    [Fact]
    public void Nothing_selected_renders_nothing()
    {
        Assert.Empty(EquivalentCommand.For(Bulk(ActionKind.Stop, [])));
    }

    /// <summary>
    /// The way back is rendered through the same door as the ask, so the tool's own name has one home.
    ///
    /// <b>It used to live in the language file, inside "  bws {0} {1}".</b> The verb was deliberately
    /// kept out of that layer - a reworded sentence must not be able to render a command that does
    /// not exist - and the NAME OF THE TOOL was left in it, which is the same fault one word along.
    /// </summary>
    [Fact]
    public void The_way_back_is_a_command_too()
    {
        Assert.Equal(
            "bws start Spooler",
            EquivalentCommand.For(new ReversalStep("Spooler", StepOperation.Start)));

        Assert.Equal(
            "bws stop Spooler",
            EquivalentCommand.For(new ReversalStep("Spooler", StepOperation.Stop)));
    }

    // -- fixtures --------------------------------------------------------------------------

    private static BulkPlan Bulk(ActionKind kind, IReadOnlyList<string> names)
    {
        var entries = new List<ScmEntry>(Specimens.All)
        {
            Running("MRxSmb20", "SMB 2.0 Redirector"),
            Running("LanmanWorkstation", "Stacja robocza")
        };

        var catalog = new FakeScmCatalog(entries).DependedOnBy("MRxSmb20", "LanmanWorkstation");

        return new BulkPlanBuilder(catalog.ReadAll(), catalog).Build(new BulkAction(kind, names));
    }

    private static ScmEntry Running(string serviceName, string displayName) =>
        Entries.Named(serviceName, displayName) with
        {
            Status = EntryStatus.Running,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            ProcessId = Reading<int>.Present(4444)
        };
}
