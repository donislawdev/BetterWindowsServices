using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.ForcedStopFixture;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// What the one button that changes a machine says on its face.
///
/// <b>Point 4 of the review of 2026-09-15, `docs/11` 2.14.</b> The button said "Carry this out" on
/// every plan but one, and the one - "End process 4812", the owner's decision of 2026-09-06 - came
/// with an argument that was already general: a label should name the act, not describe the plan.
/// These guards hold that for all six kinds, for one entry and for several, and hold the two things
/// a name on a button costs: that a translated display name of any length stays on its row, and
/// that the name a probe reads off the button is the label.
///
/// <b>Its own file rather than a page of <see cref="ForcedStopGuards"/>, where the two older
/// assertions live</b> - that file stands at 494 lines against an axis with no room over 500, and
/// what is held here is about every plan rather than about the forcing one.
/// </summary>
public sealed class CarryOutLabelGuards
{
    /// <summary>
    /// The verb and the name a person recognises, for the three kinds that move a service.
    ///
    /// <b>Asserted against the language file rather than against quoted words</b>, so rewording
    /// the label is not a red test - the guard is about the KEY chosen and the NAME handed in.
    /// </summary>
    [Theory]
    [InlineData(ActionKind.Stop, "gui.plan.carryOut.stop.one")]
    [InlineData(ActionKind.Start, "gui.plan.carryOut.start.one")]
    [InlineData(ActionKind.Restart, "gui.plan.carryOut.restart.one")]
    public void The_button_names_the_act_and_the_entry_a_person_recognises(ActionKind kind, string key)
    {
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(Over(kind, "Spooler"), shownAs: "Print Spooler"));

        Assert.Equal(Bws.Gui.Texts.Of(key, "Print Spooler"), panel.CarryOutLabel);
    }

    /// <summary>
    /// A start type plan names the value it writes - the owner's choice of 2026-09-15 between
    /// "Set Print Spooler to Disabled" and "Set the startup type of Print Spooler": the value is
    /// the part somebody has to check before pressing, and the step line already leads with it.
    /// </summary>
    [Fact]
    public void A_start_type_plan_names_the_type_it_sets()
    {
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(Setting("Spooler", StartType.Disabled), shownAs: "Print Spooler"));

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.carryOut.setStartType.one", "Print Spooler", CellFaces.TypeLabel(StartType.Disabled)),
            panel.CarryOutLabel);
    }

    /// <summary>
    /// The manager's own name when nobody handed a display name over - every plan built by
    /// something that is not this window, and an entry the manager never named.
    /// </summary>
    [Fact]
    public void Without_a_display_name_the_button_uses_the_managers_name()
    {
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(Over(ActionKind.Stop, "Spooler")));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.carryOut.stop.one", "Spooler"), panel.CarryOutLabel);
    }

    /// <summary>
    /// More than one entry is a count rather than a list of names - the same rule the heading
    /// follows, and a button is narrower than a heading.
    /// </summary>
    [Theory]
    [InlineData(ActionKind.Stop, "gui.plan.carryOut.stop.many")]
    [InlineData(ActionKind.Restart, "gui.plan.carryOut.restart.many")]
    public void Several_entries_are_counted_on_the_button_rather_than_named(ActionKind kind, string key)
    {
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(Over(kind, "Spooler", "W32Time", "Dnscache")));

        Assert.Equal(Bws.Gui.Texts.Of(key, 3), panel.CarryOutLabel);
    }

    /// <summary>
    /// A start type plan over several entries still names the type - the one word that matters
    /// most on a plan that can disable three services at once.
    /// </summary>
    [Fact]
    public void Several_entries_set_to_a_type_are_counted_and_the_type_is_still_named()
    {
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(Setting(StartType.Manual, "Spooler", "W32Time")));

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.carryOut.setStartType.many", 2, CellFaces.TypeLabel(StartType.Manual)),
            panel.CarryOutLabel);
    }

    /// <summary>
    /// A forcing plan whose process could not be read has no number to name, so it says the verb
    /// and the entry - the words of the offer that opened it - rather than falling back to
    /// anything the older label said.
    ///
    /// <b>The number wins whenever there is one</b>, which <see cref="ForcedStopGuards"/> holds.
    /// </summary>
    [Fact]
    public void A_forcing_plan_with_no_process_number_names_the_forcing_verb_and_the_entry()
    {
        var panel = new Planned { Elevated = true };

        var plan = new BulkPlan
        {
            Action = new BulkAction(ActionKind.ForceStop, ["Spooler"]),
            Plans =
            [
                new OperationPlan
                {
                    Action = new ServiceAction(ActionKind.ForceStop, "Spooler"),
                    Steps =
                    [
                        new PlanStep("Spooler", "Print Spooler", StepOperation.Stop, StepReason.Requested),
                        new PlanStep("Spooler", "Print Spooler", StepOperation.Terminate, StepReason.Escalation)
                    ],
                    Warnings = [],
                    Problems = []
                }
            ],
            Problems = []
        };

        Assert.True(panel.Show(plan, shownAs: "Print Spooler"));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.carryOut.forceStop.one", "Print Spooler"), panel.CarryOutLabel);
    }

    /// <summary>
    /// A plan that is all refusals still names what it would have done - the button is grey and
    /// its tooltip says why, but the label is the same sentence, so the sheet does not change its
    /// mind about what it is for depending on whether it can act.
    /// </summary>
    [Fact]
    public void A_plan_that_cannot_run_still_names_the_act_on_its_grey_button()
    {
        var panel = new Planned { Elevated = true };

        var refused = new BulkPlan
        {
            Action = new BulkAction(ActionKind.Stop, ["amdkmdag"]),
            Plans = [],
            Problems = [new PlanProblem(PlanProblemKind.NotOperable, "amdkmdag", [])]
        };

        Assert.True(panel.Show(refused, shownAs: "amdkmdag"));

        Assert.False(panel.CanCarryOut);
        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.carryOut.stop.one", "amdkmdag"), panel.CarryOutLabel);
    }

    /// <summary>
    /// The longest display name this machine has - 116 characters of translated Windows, measured
    /// 2026-09-15 - stays inside the button's ceiling, and the ceiling is the token in the theme.
    ///
    /// <b>Measured off a real layout pass of the real footer</b>, at the smallest window this product
    /// supports, with a floor under the number so that a layout that never happened cannot pass as
    /// a button that fits - `docs/10` trap 19.
    ///
    /// <b>What the harness can honestly say and what it cannot.</b> It can say the button is capped
    /// and the text is INSTRUCTED to trim, and it can say the text wants more room than the cap
    /// gives - so the instruction is in effect rather than idle. Whether an ellipsis glyph reaches a
    /// pixel is a question for the photograph.
    /// </summary>
    [Fact]
    public async Task The_longest_display_name_on_this_machine_is_trimmed_inside_the_buttons_ceiling()
    {
        var window = await Ready();
        var panel = Sheeted(window);

        var name = new string('W', 116);

        WpfHost.On(() => panel.Show(Over(ActionKind.Stop, "Spooler"), shownAs: name));
        WpfHost.Settled();

        var ceiling = (double)WpfHost.Resources["WidthCarryOutMost"];

        var (wanted, given, trimming, said) = WpfHost.On(() =>
        {
            window.PlanPanel.Measure(new Size(1000, 800));
            window.PlanPanel.Arrange(new Rect(0, 0, 1000, 800));
            window.PlanPanel.UpdateLayout();

            var button = window.PlanPanel.Footer.CarryOut;
            var text = Descendants(button).OfType<TextBlock>().Single();

            var unconstrained = new TextBlock { Text = text.Text, FontSize = text.FontSize, FontFamily = text.FontFamily };
            unconstrained.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            return (
                unconstrained.DesiredSize.Width,
                button.DesiredSize.Width,
                text.TextTrimming,
                UIElementAutomationPeer.CreatePeerForElement(button)!.GetName());
        });

        Assert.True(given > 100, $"the button measured {given} wide, which is not a laid out button");
        Assert.True(
            given <= ceiling,
            $"the button wants {given} points for a 116 character name, past the {ceiling} the theme "
            + "allows it - so a long display name pushes the waiting box off the footer's row.");
        Assert.True(
            wanted > ceiling,
            $"the name only wants {wanted} points, so this guard is not exercising the ceiling at all.");
        Assert.Equal(TextTrimming.CharacterEllipsis, trimming);

        // THE NAME A PROBE READS IS THE LABEL, whichever way WPF derives a name for templated
        // content - the view binds it explicitly, and this is the assertion that the binding is
        // alive rather than written.
        Assert.Equal(WpfHost.On(() => panel.CarryOutLabel), said);

        WpfHost.On(window.Close);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var at = 0; at < VisualTreeHelper.GetChildrenCount(root); at++)
        {
            var child = VisualTreeHelper.GetChild(root, at);

            yield return child;

            foreach (var under in Descendants(child))
            {
                yield return under;
            }
        }
    }

    /// <summary>A plan of one kind over the names given, each with a single requested step.</summary>
    private static BulkPlan Over(ActionKind kind, params string[] names) => new()
    {
        Action = new BulkAction(kind, names),
        Plans =
        [
            .. names.Select(name => new OperationPlan
            {
                Action = new ServiceAction(kind, name),
                Steps = [new PlanStep(name, name, kind == ActionKind.Start ? StepOperation.Start : StepOperation.Stop, StepReason.Requested)],
                Warnings = [],
                Problems = []
            })
        ],
        Problems = []
    };

    private static BulkPlan Setting(string name, StartType to) => Setting(to, name);

    private static BulkPlan Setting(StartType to, params string[] names) => new()
    {
        Action = new BulkAction(ActionKind.SetStartType, names, To: to),
        Plans =
        [
            .. names.Select(name => new OperationPlan
            {
                Action = new ServiceAction(ActionKind.SetStartType, name, To: to),
                Steps = [new PlanStep(name, name, StepOperation.SetStartType, StepReason.Requested, To: to, From: StartType.Automatic)],
                Warnings = [],
                Problems = []
            })
        ],
        Problems = []
    };
}
