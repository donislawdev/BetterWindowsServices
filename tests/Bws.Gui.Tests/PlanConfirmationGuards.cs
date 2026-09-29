using System.Windows;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The heavy ask on a SELECTION, which is refused rather than asked about - backlog 475, the owner's
/// decision of 2026-09-29.
///
/// <b>What stood here before was a dead end that said it was a door.</b> A stop over a whole scope
/// put the machine's critical entries in one plan, the box asked for the FIRST entry's name - an
/// entry the warning was not about - and the button refused every plan about more than one entry
/// anyway, so the name was typed and nothing changed. The refusal stays, and these hold the half
/// that was missing: that the screen says it is one.
///
/// <b>The model and the control are asserted apart.</b> The model can be right while the markup
/// binds to a property it misspells, and a binding to nothing resolves in silence - so the second
/// half builds the control and reads what it drew.
/// </summary>
public sealed class PlanConfirmationGuards
{
    [Fact]
    public void A_selection_holding_a_critical_entry_is_refused_rather_than_asked_for_a_name()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Selection("AdobeARMservice", "RpcSs"));

        Assert.True(panel.RefusesSelection);
        Assert.False(panel.NeedsTyping);
        Assert.Equal(string.Empty, panel.TypeToConfirm);
        Assert.Contains("RpcSs", panel.Danger, StringComparison.Ordinal);

        // No name opens it - neither the entry the box used to ask for nor the one the warning is
        // about - because one name agreeing to several entries is what the refusal exists against.
        foreach (var typed in new[] { "AdobeARMservice", "RpcSs", string.Empty })
        {
            panel.Typed = typed;

            Assert.False(panel.CanCarryOut, typed);
        }

        // And the button says the sentence the footer does, not "type a name in the box above".
        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.confirm.selection"), panel.CarryOutTip);
    }

    [Fact]
    public void A_selection_without_one_asks_for_nothing()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Selection("AdobeARMservice", "Spooler"));

        Assert.False(panel.RefusesSelection);
        Assert.False(panel.AsksHeavily);
        Assert.True(panel.CanCarryOut);
    }

    [Fact]
    public void A_refused_selection_shows_its_sentence_where_the_box_would_stand_and_no_box()
    {
        var (refusal, boxShown, danger) = Drawn(Selection("AdobeARMservice", "RpcSs"));

        Assert.Equal(Visibility.Visible, refusal);
        Assert.False(boxShown);
        Assert.Contains("RpcSs", danger, StringComparison.Ordinal);
    }

    [Fact]
    public void One_critical_entry_shows_the_box_and_no_refusal()
    {
        var (refusal, boxShown, _) = Drawn(Selection("RpcSs"));

        Assert.True(boxShown);
        Assert.Equal(Visibility.Collapsed, refusal);
    }

    /// <summary>
    /// `docs/10` trap 8: a UserControl is a ContentControl and arrives focusable and a Tab stop, so
    /// the section carried out of the footer would put a silent stop in front of the box it holds.
    /// </summary>
    [Fact]
    public void The_section_is_not_a_stop_on_the_way_to_the_box()
    {
        var (focusable, tabStop) = WpfHost.On(() =>
        {
            var view = new PlanConfirmation();

            return (view.Focusable, view.IsTabStop);
        });

        Assert.False(focusable);
        Assert.False(tabStop);
    }

    /// <summary>
    /// A plan asking about more than one entry can never be confirmed, because there is no single
    /// name to type for it - and since 2026-09-29 (backlog 475) it says so with a refusal rather
    /// than a box asking for a name that would change nothing. Moved here from ForcedStopGuards
    /// that day, because the refusal of a selection is what this file is about.
    ///
    /// <b>Unreachable for a FORCING ask and guarded anyway, which is the whole argument for the
    /// clause.</b> Section 15.6 of the analysis records that bulk forcing cannot be reached - the
    /// window opens this sheet from one failure and the command line takes one name - so this is
    /// what happens if that ever stops being true. Accepting one name for several processes would be
    /// somebody agreeing to the first entry and getting all of them, which is the shape rule 5 of
    /// the untouchable rules exists against: the preview and the press saying different things.
    /// </summary>
    [Fact]
    public void A_plan_that_would_end_several_processes_can_never_be_confirmed()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(ForcingTwo());

        Assert.True(panel.RefusesSelection);
        Assert.False(panel.NeedsTyping);
        Assert.False(panel.CanCarryOut);

        // NEITHER NAME LETS IT THROUGH, and nor does an empty box - which is the failure this
        // clause was written against: TypeTheName answers with the first entry alone, so a plain
        // comparison would have accepted one name for two processes.
        foreach (var typed in new[] { "Spooler", "W32Time", string.Empty, "  " })
        {
            panel.Typed = typed;

            Assert.False(panel.CanCarryOut, typed);
        }
    }

    /// <summary>Two entries, each ending a process of its own. Nothing in this product builds one.</summary>
    private static BulkPlan ForcingTwo() => new()
    {
        Action = new BulkAction(ActionKind.ForceStop, ["Spooler", "W32Time"]),
        Plans =
        [
            .. new[] { "Spooler", "W32Time" }.Select((name, at) => new OperationPlan
            {
                Action = new ServiceAction(ActionKind.ForceStop, name),
                Steps =
                [
                    new PlanStep(
                        name, name, StepOperation.Terminate,
                        StepReason.Requested, ProcessId: 4812 + at)
                ],
                Warnings = [new PlanWarning(PlanWarningKind.TerminationTakesWithIt, name, ["Dnscache"])],
                Problems = []
            })
        ],
        Problems = []
    };

    private static (Visibility Refusal, bool BoxShown, string Danger) Drawn(BulkPlan plan)
    {
        var panel = new Planned { Elevated = true };

        panel.Show(plan);

        var view = WpfHost.On(() => new PlanConfirmation { DataContext = panel });

        // Settled between handing over the data and reading the control - a binding is evaluated
        // by the dispatcher after the code that set the DataContext, ForcedStopFixture.OfferedBy.
        WpfHost.Settled();

        return WpfHost.On(() => (view.PlanRefusesSelection.Visibility, view.BoxShown, view.PlanDanger.Text));
    }

    /// <summary>
    /// An ordinary stop of the names given, with RpcSs - one of the seven entries the machine does
    /// not work without - carrying the warning the core raises about it, and nothing else carrying any.
    /// </summary>
    private static BulkPlan Selection(params string[] names) => new()
    {
        Action = new BulkAction(ActionKind.Stop, names),
        Plans =
        [
            .. names.Select(name => new OperationPlan
            {
                Action = new ServiceAction(ActionKind.Stop, name),
                Steps = [new PlanStep(name, name, StepOperation.Stop, StepReason.Requested)],
                Warnings = name == "RpcSs" ? [new PlanWarning(PlanWarningKind.CriticalService, name, [name])] : [],
                Problems = []
            })
        ],
        Problems = []
    };
}
