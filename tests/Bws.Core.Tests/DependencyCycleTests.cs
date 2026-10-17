using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// A plan over a dependency cycle, written out by hand after the random search found what one did.
///
/// <b>Both shapes were found by PlanGraphPropertyTests on its first runs, 2026-10-17</b>, and fixed on
/// the owner's decision the same day. These are the shortest machines that show each one, so that a
/// person reading a failure here sees the whole situation in five lines rather than in a printed graph.
///
/// <b>Whether a real machine can hold a cycle is not known.</b> Windows refuses to write one through its
/// API (ChangeServiceConfig answers ERROR_CIRCULAR_DEPENDENCY), and the registry route was never tried.
/// The double answers the way the measured manager answers everything else - past the first hop - so
/// over a cycle the answer reaches back round to the name that was asked about.
/// </summary>
public sealed class DependencyCycleTests
{
    [Fact]
    public void A_target_the_manager_names_among_its_own_dependants_is_stopped_once_and_started_once()
    {
        // Alpha and Bravo need each other. Asked who breaks if Alpha stops, the manager names Bravo -
        // and Alpha, because Bravo needs it. Until the fix Alpha was a member of its own cascade.
        var catalog = new FakeScmCatalog(Running("Alpha", "Alpha"), Running("Bravo", "Bravo"))
            .DependedOnBy("Alpha", "Bravo", "Alpha")
            .DependedOnBy("Bravo", "Alpha", "Bravo");

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Restart, "Alpha", IncludeDependents: true));

        Assert.Equal(
            ["Bravo Stop", "Alpha Stop", "Alpha Start", "Bravo Start"],
            plan.Steps.Select(step => $"{step.ServiceName} {step.Operation}"));

        // And the warning about what comes down with it does not name the entry it comes down with.
        Assert.Equal(["Bravo"], Warning(plan, PlanWarningKind.Cascade).Related);
    }

    [Fact]
    public void A_cycle_among_the_dependants_is_stopped_before_the_entry_it_needs()
    {
        // Left and Right need each other, both need Middle, and Middle needs Target. Until the fix the
        // two in the cycle were never "ready" - each names itself - and the fallback took Middle first,
        // which the manager refuses to stop while Left and Right run.
        var catalog = new FakeScmCatalog(
                Running("Target", "Target"), Running("Middle", "Middle"), Running("Left", "Left"), Running("Right", "Right"))
            .DependedOnBy("Target", "Middle", "Left", "Right")
            .DependedOnBy("Middle", "Left", "Right")
            .DependedOnBy("Left", "Right", "Left")
            .DependedOnBy("Right", "Left", "Right");

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Stop, "Target", IncludeDependents: true));

        // Inside the cycle there is no right order and the one given is kept. Everything else is fixed.
        Assert.Equal(["Left", "Right", "Middle", "Target"], plan.Steps.Select(step => step.ServiceName));
    }

    [Fact]
    public void A_cycle_the_manager_spells_in_another_case_is_still_a_cycle()
    {
        // Windows compares service names without case and the manager may hand back a spelling of its
        // own. The cycle test asks the answers about a name as the LISTING spells it, so answers kept
        // with case would miss the cycle and fall back to taking Middle first.
        var catalog = new FakeScmCatalog(
                Running("Target", "Target"), Running("Middle", "Middle"), Running("Left", "Left"), Running("Right", "Right"))
            .DependedOnBy("Target", "Middle", "Left", "Right")
            .DependedOnBy("Middle", "Left", "Right")
            .DependedOnBy("Left", "RIGHT", "LEFT")
            .DependedOnBy("Right", "LEFT", "RIGHT");

        var plan = new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.Stop, "Target", IncludeDependents: true));

        Assert.Equal(["Left", "Right", "Middle", "Target"], plan.Steps.Select(step => step.ServiceName));
    }

    [Fact]
    public void Without_a_cycle_the_order_is_the_one_it_always_was()
    {
        // The control for the fix above: a block both ways exists only in a cycle, so the chain measured
        // on a real machine in 2026-08 has to come out exactly as PlanBuilderTests has always expected.
        var plan = Plan(ActionKind.Stop, "MRxSmb20", Chain());

        Assert.Equal(
            ["SessionEnv", "Netlogon", "LanmanWorkstation", "MRxSmb20"],
            plan.Steps.Select(step => step.ServiceName));
    }
}
