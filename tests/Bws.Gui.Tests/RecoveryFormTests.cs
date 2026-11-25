using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The recovery form on the plan sheet, without a window - backlog 555 part a2, the owner's decisions of
/// 2026-11-24 and 2026-11-25 (`docs/PROJEKT-LUKI-SERVICES-MSC-20261123.md` section 8): a tick per part, set by
/// an edit and taken off to put the part back, and a sheet that has no plan until the form asks for one.
/// </summary>
public sealed class RecoveryFormTests
{
    private static readonly RecoverySettings Spoolers = new(
        new RecoveryList(
            [
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1)),
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1)),
                new RecoveryItem(RecoveryAction.Nothing, TimeSpan.Zero)
            ],
            TimeSpan.FromDays(1)),
        false);

    /// <summary>
    /// One entry: the form shows what it has and writes nothing, and an edit writes that part alone - the rest
    /// of the entry stays as it is, which is the owner's decision for every part nobody touched.
    /// </summary>
    [Fact]
    public void An_edit_ticks_its_part_and_the_change_names_that_part_alone()
    {
        var form = One(Spoolers);

        Assert.Equal("1d", form.PeriodText);
        Assert.Equal(["Failure 1", "Failure 2", "Failure 3 and later"], form.Items.Select(item => item.Label));
        Assert.False(form.ChangesPeriod);
        Assert.Null(form.Change);

        form.PeriodText = "2h";

        Assert.True(form.ChangesPeriod);
        Assert.False(form.ChangesList);

        var change = Assert.IsType<RecoveryChange>(form.Change);

        Assert.Equal(RecoveryParts.ResetPeriod, change.Parts);
        Assert.Equal(TimeSpan.FromHours(2), change.To.List.ResetPeriod);
    }

    /// <summary>Taking the tick off puts the part back as the entry has it, and the form asks nothing again.</summary>
    [Fact]
    public void Taking_the_tick_off_puts_the_part_back()
    {
        var form = One(Spoolers);

        form.Items[0].DelayText = "5m";
        form.Items[0].Kind = RecoveryAction.RunProgram;

        Assert.True(form.ChangesList);

        // What a screen reader says for the line follows its kind - the button's name replaces its content.
        Assert.Equal(Texts.Of("gui.recovery.form.kind.name", "Failure 1", Texts.Of("gui.recovery.form.kind.program")), form.Items[0].KindName);
        Assert.Equal(Texts.Of("gui.recovery.form.delay.name", "Failure 1"), form.Items[0].DelayName);

        form.ChangesList = false;

        Assert.Equal(RecoveryAction.RestartService, form.Items[0].Kind);
        Assert.Equal("1m", form.Items[0].DelayText);
        Assert.Null(form.Change);
    }

    /// <summary>
    /// Many entries: a part they agree on is shown, one they do not is empty with a sentence saying why - and
    /// ticked while empty it REMOVES, which is why "leave it" needs a tick of its own (Z4).
    /// </summary>
    [Fact]
    public void Over_entries_that_differ_an_empty_ticked_program_removes_it_and_an_unticked_one_leaves_it()
    {
        var form = RecoveryForm.Of(
        [
            Reading<RecoverySettings>.Present(Spoolers with { List = Spoolers.List with { Command = "C:\\a.cmd" } }),
            Reading<RecoverySettings>.Present(Spoolers with { List = Spoolers.List with { Command = "C:\\b.cmd" } })
        ]);

        Assert.Equal(3, form.Items.Count);
        Assert.Empty(form.ListNote);
        Assert.Equal(string.Empty, form.ProgramText);
        Assert.Equal(Texts.Of("gui.recovery.form.differs"), form.ProgramNote);

        form.ChangesProgram = true;

        var change = Assert.IsType<RecoveryChange>(form.Change);

        Assert.Equal(RecoveryParts.Command, change.Parts);
        Assert.Null(change.To.List.Command);
        Assert.Empty(form.ProgramNote);
    }

    /// <summary>
    /// One entry of many not read says so - the values may well be the same, and "not the same on all" would be
    /// a claim nobody checked.
    /// </summary>
    [Fact]
    public void Over_entries_one_of_which_was_not_read_says_not_read_rather_than_differs()
    {
        var form = RecoveryForm.Of(
        [
            Reading<RecoverySettings>.Present(Spoolers),
            Reading<RecoverySettings>.Denied(5, "Access is denied.")
        ]);

        Assert.Empty(form.Items);
        Assert.Equal(Texts.Of("gui.recovery.form.unread.some"), form.ListNote);
        Assert.Equal(Texts.Of("gui.recovery.form.unread.some"), form.ProgramNote);
    }

    /// <summary>A delay that does not read says so under the list, and there is no change to plan until it does.</summary>
    [Fact]
    public void A_delay_that_does_not_read_is_said_and_asks_nothing()
    {
        var form = One(Spoolers);

        form.Items[1].DelayText = "5x";

        Assert.True(form.HasProblem);
        Assert.Equal(Texts.Of("gui.recovery.form.delay.problem"), form.ListProblem);
        Assert.Null(form.Change);

        form.Items[1].DelayText = "90s";

        Assert.False(form.HasProblem);
        Assert.Equal(TimeSpan.FromSeconds(90), form.Change!.To.List.Items[1].Delay);
    }

    /// <summary>
    /// The flag is two options, each the other's opposite, and choosing one is an edit of the part (P4). An item
    /// that does nothing keeps the delay it was read with, though no box shows it.
    /// </summary>
    [Fact]
    public void The_two_flag_options_are_one_part_and_a_quiet_delay_survives()
    {
        var quiet = Spoolers with
        {
            List = Spoolers.List with { Items = [new RecoveryItem(RecoveryAction.Nothing, TimeSpan.FromSeconds(30))] }
        };
        var form = One(quiet);

        Assert.True(form.CrashesOnly);
        Assert.Equal(["Every failure"], form.Items.Select(item => item.Label));

        form.Flag = true;

        Assert.False(form.CrashesOnly);
        Assert.True(form.ChangesFlag);

        form.ChangesList = true;

        Assert.Equal(TimeSpan.FromSeconds(30), form.Change!.To.List.Items[0].Delay);
    }

    /// <summary>Adding is a restart after a minute, the last line becomes "and later", and both are edits of the list.</summary>
    [Fact]
    public void Adding_and_removing_a_failure_are_edits_of_the_list()
    {
        var form = One(Spoolers);

        form.Add();

        Assert.True(form.ChangesList);
        Assert.Equal("Failure 4 and later", form.Items[^1].Label);
        Assert.Equal(new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1)), form.Items[^1].Item);

        form.Remove(form.Items[0]);

        Assert.Equal(3, form.Change!.To.List.Items.Count);
    }

    /// <summary>
    /// The sheet opens on the form with no plan: nothing to carry out, the notice says what to do, and the
    /// title and the button already name the entry. A plan shown under it keeps the form, an edit greys the
    /// button until the next plan, and putting the sheet away takes the form with it.
    /// </summary>
    [Fact]
    public void The_sheet_asks_before_it_plans_and_greys_the_button_while_the_form_moves()
    {
        var form = One(Spoolers);
        var asking = new BulkAction(ActionKind.SetRecovery, ["Spooler"]);
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Ask(form, asking, "Print Spooler"));

        Assert.NotNull(panel.Question);
        Assert.False(panel.CanCarryOut);
        Assert.Equal(Texts.Of("gui.plan.notice.form.ask"), panel.Notice);
        Assert.Equal(Texts.Of("gui.plan.carryOut.recovery.one", "Print Spooler"), panel.CarryOutLabel);
        Assert.Contains("Print Spooler", panel.Heading.Text, StringComparison.Ordinal);

        form.PeriodText = "2h";

        Assert.True(panel.Show(Plan(asking with { Recovery = form.Change }), "Print Spooler"));
        Assert.Same(form, panel.Question?.Form);
        Assert.True(panel.CanCarryOut);

        panel.Question!.Outdated = true;

        Assert.False(panel.CanCarryOut);
        Assert.Equal(Texts.Of("gui.plan.blocked.outdated"), panel.CarryOutTip);

        Assert.True(panel.Hide());
        Assert.Null(panel.Question);
    }

    private static RecoveryForm One(RecoverySettings settings) =>
        RecoveryForm.Of([Reading<RecoverySettings>.Present(settings)]);

    /// <summary>A recovery plan of one step, written by hand the way CarryOutLabelGuards writes its plans.</summary>
    private static BulkPlan Plan(BulkAction action) => new()
    {
        Action = action,
        Plans =
        [
            new OperationPlan
            {
                Action = new ServiceAction(ActionKind.SetRecovery, "Spooler", Recovery: action.Recovery),
                Steps =
                [
                    new PlanStep("Spooler", "Print Spooler", StepOperation.SetRecovery, StepReason.Requested)
                    {
                        Recovering = new RecoveryWrite(action.Recovery!, Spoolers)
                    }
                ],
                Warnings = [],
                Problems = []
            }
        ],
        Problems = []
    };
}
