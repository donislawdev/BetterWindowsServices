using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;

using static Bws.Core.Tests.Fakes.DependencyChain;

namespace Bws.Core.Tests;

/// <summary>
/// What a plan says when it takes down an entry the machine does not work without.
///
/// <b>THIS FILE EXISTS BECAUSE THE SENTENCE WAS ON THE WRONG PATH FOR THREE DAYS AND NOTHING
/// NOTICED.</b> <see cref="ProcessNeighbours"/> has kept a list of seven such names since
/// 2026-09-06, with a warning kind and a sentence beside it - and the only caller was
/// <see cref="ForcedStop"/>, the path a person reaches last. <c>stop PlugPlay</c> built a plan
/// whose one warning was about a shared process. An audit reading four such plans on 2026-09-09
/// found it, which is the point worth keeping: <b>a guard cannot go red over an absence nobody
/// has named</b>, so the absence had to be named first and this file is the naming.
///
/// <b>Both kinds are here rather than in two files, because they are one decision seen twice.</b>
/// The question "does this plan reach one of the seven" has one answer. What differs is when the
/// harm lands - now for a stop, at the next boot for a disable - and a reader checking either
/// claim wants the other one in front of them.
///
/// <b>The fixture is <c>LSM</c> and that is deliberate.</b> It is a real entry from a real machine,
/// it is on the list, and it is a shared process running automatically - so a stop of it raises the
/// neighbouring warnings too, and these assertions have to survive standing next to them rather
/// than in an empty plan.
/// </summary>
public sealed class CriticalEntryGuards
{
    /// <summary>
    /// The one the audit found. Everything else in this file was written by asking what else
    /// touches those seven names.
    /// </summary>
    [Fact]
    public void A_stop_names_the_entry_the_machine_does_not_work_without()
    {
        var warning = Warning(Plan(ActionKind.Stop, "LSM"), PlanWarningKind.CriticalService);

        Assert.Equal("LSM", warning.ServiceName);
        Assert.Equal(["LSM"], warning.Related);
    }

    /// <summary>
    /// A restart stops the entry before it starts it, so the machine goes down in the middle
    /// either way - and if the start half fails it stays down.
    /// </summary>
    [Fact]
    public void A_restart_says_it_too_because_the_stop_half_happens_first()
    {
        Assert.NotNull(Warning(Plan(ActionKind.Restart, "LSM"), PlanWarningKind.CriticalService));
    }

    /// <summary>
    /// <b>The half that keeps the warning worth reading.</b> A sentence that appears on every plan
    /// is one nobody finishes, and this is the assertion that would go red if the list were ever
    /// replaced by something that answers yes too often.
    /// </summary>
    [Fact]
    public void A_stop_of_anything_else_says_nothing_of_the_kind()
    {
        Assert.DoesNotContain(
            Plan(ActionKind.Stop, "Spooler").Warnings,
            warning => warning.Kind is PlanWarningKind.CriticalService
                or PlanWarningKind.CriticalStartType);
    }

    /// <summary>
    /// <b>THE CASE WITH THE LEAST WARNING ATTACHED TO IT, and the reason the question is asked of
    /// the casualty list rather than of the target.</b> Somebody typing --dependents takes down
    /// entries whose names they never wrote, and one of the seven arriving that way is the shape
    /// where a person is least likely to have thought about it.
    /// </summary>
    [Fact]
    public void A_critical_entry_arriving_in_the_cascade_is_named_as_well()
    {
        // Spooler is not on the list and LSM is. Asked to stop Spooler with its dependents, the
        // plan stops LSM on the way - so the warning has to name LSM even though nobody typed it.
        var catalog = Specimens.Catalog().DependedOnBy("Spooler", "LSM");

        var warning = Warning(
            Plan(ActionKind.Stop, "Spooler", catalog), PlanWarningKind.CriticalService);

        Assert.Equal("Spooler", warning.ServiceName);
        Assert.Contains("LSM", warning.Related);
    }

    /// <summary>
    /// Disabling is the same harm arriving at the next boot, and it says so in its own words.
    ///
    /// <b>Checked as NOT the stop sentence as well as as its own</b>, because the cheapest way to
    /// have built this would have been to reuse the kind above - and that would have put "takes the
    /// machine down" on a plan that moves nothing today.
    /// </summary>
    [Fact]
    public void Disabling_one_is_its_own_sentence_rather_than_the_stop_one()
    {
        var plan = StartTypePlan(StartSetting.Disabled, "LSM");

        var warning = Warning(plan, PlanWarningKind.CriticalStartType);

        Assert.Equal(["LSM"], warning.Related);
        Assert.DoesNotContain(plan.Warnings, one => one.Kind == PlanWarningKind.CriticalService);
    }

    /// <summary>
    /// Manual and automatic both leave the manager able to start the entry, so neither is the harm
    /// this warns about. `docs/03` pitfall `P1` keeps "you should not" for the thing that stops it
    /// starting at all.
    /// </summary>
    [Theory]
    [InlineData(StartSetting.Manual)]
    [InlineData(StartSetting.Automatic)]
    [InlineData(StartSetting.AutomaticDelayed)]
    public void Any_other_start_type_says_nothing(StartSetting to)
    {
        Assert.DoesNotContain(
            StartTypePlan(to, "LSM").Warnings,
            warning => warning.Kind is PlanWarningKind.CriticalStartType
                or PlanWarningKind.CriticalService);
    }

    /// <summary>
    /// <b>ONCE, NOT TWICE, AND THIS IS THE ASSERTION THE WIRING WAS MOST LIKELY TO BREAK.</b>
    /// <see cref="ForcedStop"/> raises the same kind at the end of the same method, so the obvious
    /// way to write the change - ask on every kind - would have put the sentence on a forcing plan
    /// twice. <c>Warning</c> is <c>Assert.Single</c>, so a duplicate fails here rather than being
    /// read by somebody as two different entries being at risk.
    /// </summary>
    [Fact]
    public void A_forcing_plan_says_it_once_rather_than_twice()
    {
        Assert.NotNull(Warning(
            Plan(ActionKind.ForceStop, "LSM", includeDependents: false),
            PlanWarningKind.CriticalService));
    }

    // -- fixtures --------------------------------------------------------------------------

    /// <summary>
    /// A start type plan, which the shared <c>Plan</c> helper cannot build: the value being written
    /// travels in <see cref="ServiceAction.To"/> rather than in the kind.
    /// </summary>
    private static OperationPlan StartTypePlan(StartSetting to, string serviceName)
    {
        var catalog = Specimens.Catalog();

        return new PlanBuilder(catalog.ReadAll(), catalog)
            .Build(new ServiceAction(ActionKind.SetStartType, serviceName, To: to));
    }
}
