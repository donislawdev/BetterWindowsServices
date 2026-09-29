using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What this window asks of somebody before it will touch an entry the machine does not work
/// without.
///
/// <b>ITS OWN FILE ON 2026-09-09 BECAUSE THE SIZE RATCHET SAID SO, and the seam it pointed at is a
/// subject rather than a convenient cut.</b> <see cref="ForcedStopGuards"/> is about the way out
/// from under a failed stop - who is offered one, and what standing between them and a process
/// ending. This is about a list of seven names and what reaching one of them costs a person in
/// keystrokes, whichever verb brought them there.
///
/// <b>The behaviour under test arrived without a line of window code changing.</b> The core began
/// raising <see cref="PlanWarningKind.CriticalService"/> on an ordinary plan that day, and because
/// <c>Planned.NeedsTyping</c> reads warnings rather than action kinds, the confirmation box
/// appeared on a plain stop by itself. Keeping it was the owner's decision, so these are guards on
/// a decision rather than on a mechanism - which is why they are worth their own file and their own
/// explanation.
/// </summary>
public sealed class CriticalEntryWindowGuards
{
    /// <summary>
    /// <b>AND SO DOES AN ORDINARY STOP OF ONE, SINCE 2026-09-09 - owner's decision, taken with the
    /// core change that made it possible.</b> Until that day the core raised
    /// <see cref="PlanWarningKind.CriticalService"/> only on a plan that ends a process, so this
    /// state was unreachable rather than excluded. When it became reachable the box appeared here
    /// by itself, because <c>NeedsTyping</c> reads warnings rather than action kinds.
    ///
    /// <b>Keeping it was the decision, and this is the guard on that decision rather than on the
    /// mechanism.</b> Stopping one of those seven entries takes the machine down, and `docs/11` 9.2
    /// says the weight of a confirmation follows the size of what it does - which points at asking
    /// for the name here rather than at an exemption for the ordinary verb.
    /// </summary>
    [Fact]
    public void An_ordinary_stop_of_a_critical_entry_asks_for_the_name_as_well()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Stopping("Spooler", PlanWarningKind.CriticalService));

        Assert.True(panel.NeedsTyping);
        Assert.False(panel.CanCarryOut);

        panel.Typed = "Spooler";

        Assert.True(panel.CanCarryOut);
    }

    /// <summary>
    /// Disabling one asks for it too, and it is a separate assertion because it is a separate kind.
    /// The harm lands at the next boot rather than now, which is a reason for a different sentence
    /// and not for a lighter confirmation.
    /// </summary>
    [Fact]
    public void Disabling_a_critical_entry_asks_for_the_name()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Stopping("Spooler", PlanWarningKind.CriticalStartType));

        Assert.True(panel.NeedsTyping);
        Assert.False(panel.CanCarryOut);
    }

    /// <summary>
    /// <b>The half that keeps the box meaning something.</b> An ordinary stop of an ordinary entry
    /// is the overwhelmingly common plan in this window, and a confirmation asked for on all of
    /// them would be one people learn to type without reading.
    /// </summary>
    [Fact]
    public void An_ordinary_stop_of_anything_else_asks_for_nothing()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Stopping("Spooler", PlanWarningKind.SharedProcess));

        Assert.False(panel.NeedsTyping);
        Assert.True(panel.CanCarryOut);
    }

    /// <summary>
    /// <b>ONE SENTENCE PER KIND OVER THE BOX, NAMING EVERY SUCH ENTRY ONCE - W6, 2026-09-29.</b> A
    /// stop over a whole scope carried one of these per plan, and the footer, which never scrolls,
    /// grew until the body holding the steps was one unit tall. One entry keeps today's sentence
    /// word for word, the same entry twice is named once, and the two kinds stay two sentences,
    /// because one is about now and the other about the next boot.
    /// </summary>
    [Fact]
    public void Entries_the_machine_does_not_work_without_are_named_in_one_sentence_per_kind()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Each(PlanWarningKind.CriticalService, "RpcSs", "PlugPlay", "LSM", "RpcSs"));

        var danger = panel.Danger.Split(Environment.NewLine);

        Assert.Single(danger);
        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.warning.critical.many", "RpcSs", 3, "RpcSs, PlugPlay, LSM"),
            danger[0]);

        panel.Show(Each(PlanWarningKind.CriticalService, "RpcSs"));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.warning.critical.one", "RpcSs", 1, "RpcSs"), panel.Danger);

        panel.Show(Stopping("Spooler", PlanWarningKind.CriticalService, PlanWarningKind.CriticalStartType));

        Assert.Equal(2, panel.Danger.Split(Environment.NewLine).Length);
    }

    /// <summary>
    /// <b>The warning about a process that takes others with it is NOT gathered</b>, because its
    /// sentence names the entry whose process it is - one sentence would pin every other entry's
    /// processes on the first. Two of them stay two sentences.
    /// </summary>
    [Fact]
    public void A_process_that_takes_others_with_it_keeps_a_sentence_of_its_own()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Each(PlanWarningKind.TerminationTakesWithIt, "Spooler", "W32Time"));

        Assert.Equal(2, panel.Danger.Split(Environment.NewLine).Length);
        Assert.Contains("Spooler", panel.Danger, StringComparison.Ordinal);
        Assert.Contains("W32Time", panel.Danger, StringComparison.Ordinal);
    }

    // -- fixtures --------------------------------------------------------------------------

    /// <summary>
    /// An ordinary stop of several entries, each plan carrying one warning of the kind named about
    /// its own entry - the shape a stop over a whole scope gives the footer. A name given twice is
    /// two plans naming the same entry, which the sentence must name once.
    /// </summary>
    private static BulkPlan Each(PlanWarningKind kind, params string[] names) => new()
    {
        Action = new BulkAction(ActionKind.Stop, names),
        Plans =
        [
            .. names.Select(name => new OperationPlan
            {
                Action = new ServiceAction(ActionKind.Stop, name),
                Steps = [new PlanStep(name, name, StepOperation.Stop, StepReason.Requested)],
                Warnings = [new PlanWarning(kind, name, [name])],
                Problems = []
            })
        ],
        Problems = []
    };

    /// <summary>
    /// An ordinary stop - no terminate step and no forcing verb - carrying the warnings named.
    ///
    /// <b>Its own fixture rather than a flag on <c>Forcing</c>, because the shape of the plan is
    /// the whole point of the assertions using it.</b> A forcing plan has a terminate step and a
    /// forcing action kind, and a reader checking that this window asks for a name on an ORDINARY
    /// stop needs to see that neither of those is present.
    /// </summary>
    private static BulkPlan Stopping(string name, params PlanWarningKind[] warnings) => new()
    {
        Action = new BulkAction(ActionKind.Stop, [name]),
        Plans =
        [
            new OperationPlan
            {
                Action = new ServiceAction(ActionKind.Stop, name),
                Steps = [new PlanStep(name, name, StepOperation.Stop, StepReason.Requested)],
                Warnings = [.. warnings.Select(kind => new PlanWarning(kind, name, [name]))],
                Problems = []
            }
        ],
        Problems = []
    };
}
