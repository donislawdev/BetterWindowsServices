using Bws.Core.Planning;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// What the plan says the manager is going to say, said before it says it.
///
/// <b>A file of its own rather than more of <see cref="PlanBuilderTests"/>, and the subject is
/// the seam.</b> Everything there is about what a plan DOES - which entries move, in what order,
/// and when there is no plan at all. This is about a warning that predicts a specific refusal, and
/// it is the first of its kind: every other warning describes a consequence of the plan working.
///
/// <b>Backlog 8, and the field behind it went unread from the day the enumeration was written
/// until 2026-09-06.</b> An entry that does not accept a stop refuses the control outright rather
/// than timing out, so before this the plan looked identical whether the stop was going to work or
/// was never going to be taken - and the difference appeared afterwards, as an error number.
/// </summary>
public sealed class PlanPreflightTests
{
    [Fact]
    public void An_entry_that_will_not_take_a_stop_is_named_before_anything_runs()
    {
        var catalog = Rebuild(
            Chain(), "MRxSmb20", entry => entry with { AcceptsStop = Reading<bool>.Present(false) });

        var warning = Warning(
            Plan(ActionKind.Stop, "MRxSmb20", includeDependents: true, catalog),
            PlanWarningKind.DoesNotAcceptStop);

        Assert.Equal("MRxSmb20", Assert.Single(warning.Related));
    }

    [Fact]
    public void An_entry_in_the_way_that_will_not_take_a_stop_is_named_as_well_as_the_one_asked_about()
    {
        // The more useful half of the same warning: this one is a step nobody asked for, and it
        // makes every step after it unreachable. Naming it beforehand is the difference between a
        // plan somebody can read and one they have to run to find out about.
        var catalog = Rebuild(
            Chain(), "SessionEnv", entry => entry with { AcceptsStop = Reading<bool>.Present(false) });

        var warning = Warning(
            Plan(ActionKind.Stop, "MRxSmb20", includeDependents: true, catalog),
            PlanWarningKind.DoesNotAcceptStop);

        Assert.Equal("SessionEnv", Assert.Single(warning.Related));
    }

    [Fact]
    public void An_entry_that_is_already_stopped_is_not_reported_as_refusing_a_stop()
    {
        // THE CASE THE FOUR READ STATES EXIST FOR. The manager reports an empty set of accepted
        // controls for anything not running, so a field holding a plain false would say "this
        // refuses to be stopped" about a service that is merely already stopped - beside a step
        // that is going to be skipped for having nothing to do.
        var catalog = Rebuild(
            Chain(),
            "MRxSmb20",
            entry => entry with
            {
                Status = EntryStatus.Stopped,
                ProcessId = Reading<int>.Absent(),
                AcceptsStop = Reading<bool>.Absent()
            });

        var plan = Plan(ActionKind.Stop, "MRxSmb20", includeDependents: true, catalog);

        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.DoesNotAcceptStop);
    }

    [Fact]
    public void A_start_is_not_warned_about_a_stop_it_is_not_making()
    {
        // Nothing in this plan sends a stop, so what the entry would do with one is true,
        // irrelevant, and exactly the kind of sentence that teaches people to stop reading
        // warnings. The shared process warning is skipped for a start for the same reason.
        var catalog = Rebuild(
            Chain(), "MRxSmb20", entry => entry with { AcceptsStop = Reading<bool>.Present(false) });

        var plan = Plan(ActionKind.Start, "MRxSmb20", includeDependents: true, catalog);

        Assert.DoesNotContain(plan.Warnings, warning => warning.Kind == PlanWarningKind.DoesNotAcceptStop);
    }
}
