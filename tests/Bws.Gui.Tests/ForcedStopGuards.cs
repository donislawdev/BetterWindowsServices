using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The way out from under a failed stop, and everything standing between somebody and a process
/// ending.
///
/// <b>Every one of these is about a decision this panel makes rather than about a machine.</b>
/// Nothing here ends anything: the runs are records built by hand, exactly as
/// <see cref="PlanFixture"/> builds them, because a test that really carried one of these plans out
/// would kill a process on whatever computer ran the suite.
///
/// <b>WHAT THAT LEAVES UNTESTED IS SAID RATHER THAN LEFT TO BE ASSUMED.</b> That a terminate step
/// really ends a process, that the manager notices and moves the entry, and that the run comes back
/// saying so are all questions for a machine that can be thrown away. These guards cover the half
/// that decides whether somebody is ever ASKED - which is the half that would be wrong on a screen.
/// </summary>
public sealed class ForcedStopGuards
{
    /// <summary>
    /// A stop that ran out of time on an entry still held by a process is the case the whole slice
    /// exists for.
    /// </summary>
    [Fact]
    public void A_stop_that_gave_up_offers_a_way_to_end_the_process()
    {
        var panel = Showing(Asking(ActionKind.Stop));

        panel.Finished(Ended(panel, StepOutcome.TimedOut));

        var failure = Assert.Single(panel.Failures);

        Assert.True(failure.HasOffer, failure.Text);
        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.offer.forceStop"), failure.Label);
    }

    /// <summary>
    /// A refusal gets the offer too, and this is the widening that reading edge cases bought
    /// before a line was written.
    ///
    /// <b>The design showed the offer under "gave up after 60 s" only.</b> An entry that does not
    /// accept a stop at all - the thing this tool started warning about the day before - is refused
    /// at once rather than watched, so its step ends as Failed. That is the one case where ending
    /// the process is the ONLY road left, and it was the case the offer would have missed.
    /// </summary>
    [Fact]
    public void A_stop_the_manager_refused_offers_it_as_well()
    {
        var panel = Showing(Asking(ActionKind.Stop));

        panel.Finished(Ended(panel, StepOutcome.Failed));

        Assert.True(Assert.Single(panel.Failures).HasOffer);
    }

    /// <summary>
    /// No process, no offer - the condition easiest to leave out and the one that would send
    /// somebody to a sheet the core refuses to build.
    /// </summary>
    [Fact]
    public void A_stop_that_gave_up_with_no_process_offers_nothing()
    {
        var panel = Showing(Asking(ActionKind.Stop));

        panel.Finished(Ended(panel, StepOutcome.TimedOut, process: Reading<int>.Absent()));

        Assert.False(Assert.Single(panel.Failures).HasOffer);
    }

    /// <summary>
    /// Nobody may have got an answer at all, which is a third state and not the same as there
    /// being none.
    /// </summary>
    [Fact]
    public void A_process_nobody_could_read_is_not_a_process_to_end()
    {
        var panel = Showing(Asking(ActionKind.Stop));

        panel.Finished(Ended(panel, StepOutcome.TimedOut, process: Reading<int>.NotRead()));

        Assert.False(Assert.Single(panel.Failures).HasOffer);
    }

    /// <summary>
    /// An entry that arrived where it was asked to go after all has nothing left to force, whatever
    /// its step said on the way.
    /// </summary>
    [Fact]
    public void An_entry_that_did_stop_in_the_end_offers_nothing()
    {
        var panel = Showing(Asking(ActionKind.Stop));

        panel.Finished(Ended(panel, StepOutcome.TimedOut, status: EntryStatus.Stopped));

        Assert.False(Assert.Single(panel.Failures).HasOffer);
    }

    /// <summary>
    /// A stop that failed inside a RESTART offers a forced restart, not a forced stop.
    ///
    /// <b>Section 15.3 of the analysis, and it is the correction reading edge cases bought.</b> An
    /// offer that ended at killing the process would leave the machine without the service somebody
    /// had just asked to bring back - the opposite of what they pressed for.
    /// </summary>
    [Fact]
    public void A_stop_that_failed_inside_a_restart_offers_a_forced_restart()
    {
        var panel = Showing(Asking(ActionKind.Restart));

        panel.Finished(Ended(panel, StepOutcome.TimedOut));

        var failure = Assert.Single(panel.Failures);

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.offer.forceRestart"), failure.Label);
        Assert.Equal(ActionKind.ForceRestart, failure.Offer!.Kind);
    }

    /// <summary>
    /// A plan that was ALREADY forcing offers nothing, because the escalation it would propose is
    /// standing in the same plan.
    ///
    /// <b>Not in the analysis and found by asking what the fifth condition should be.</b> Without
    /// it, a forced stop whose polite first step was refused would offer a second sheet identical
    /// to the one somebody is looking at - a way out that leads back to where they are.
    /// </summary>
    [Fact]
    public void A_plan_that_was_already_forcing_offers_no_second_one()
    {
        var panel = Showing(Asking(ActionKind.ForceStop));

        panel.Finished(Ended(panel, StepOutcome.TimedOut));

        Assert.False(Assert.Single(panel.Failures).HasOffer);
    }

    /// <summary>
    /// The reason travels with the offer, because the sheet carrying it is closed by the time the
    /// next one opens.
    /// </summary>
    [Fact]
    public void The_offer_carries_why_it_is_being_offered()
    {
        var timedOut = Showing(Asking(ActionKind.Stop));
        var refused = Showing(Asking(ActionKind.Stop));

        timedOut.Finished(Ended(timedOut, StepOutcome.TimedOut));
        refused.Finished(Ended(refused, StepOutcome.Failed));

        // The number is the run's own ceiling rather than a sixty written down here, which is what
        // keeps this sentence true if the window's ceiling ever moves.
        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.because.timedOut", 60),
            Assert.Single(timedOut.Failures).Offer!.Because);

        Assert.Equal(
            Bws.Gui.Texts.Of("gui.plan.because.refused"),
            Assert.Single(refused.Failures).Offer!.Because);
    }

    /// <summary>
    /// The sheet a person is sent to says what happened before, on the one line that says where
    /// this panel is in its sequence.
    /// </summary>
    [Fact]
    public void The_new_sheet_leads_with_the_reason_and_still_says_nothing_has_happened()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler"), "Print Spooler", "The stop was refused.");

        Assert.StartsWith("The stop was refused.", panel.Notice, StringComparison.Ordinal);
        Assert.Contains(Bws.Gui.Texts.Of("gui.plan.notice.notYet"), panel.Notice, StringComparison.Ordinal);
    }

    /// <summary>A plan nobody was offered opens with the sentence it always had.</summary>
    [Fact]
    public void A_plan_opened_from_the_menu_says_only_that_nothing_has_happened()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler"));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.notice.notYet"), panel.Notice);
    }

    /// <summary>
    /// The button names the process, because that is what pressing it does.
    ///
    /// <b>The owner's decision of 2026-09-06.</b> "Carry this out" describes a plan where the press
    /// ends something, and the number is the one already standing in the steps above it.
    /// </summary>
    [Fact]
    public void The_button_on_a_forcing_plan_names_the_process()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler"));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.carryOut.endProcess", 4812), panel.CarryOutLabel);
    }

    /// <summary>
    /// And an ordinary plan names its own act rather than the process - since 2026-09-15, when
    /// every kind got a label of its own (<see cref="CarryOutLabelGuards"/>). What this holds is
    /// the boundary: no process number leaks onto a plan that ends nothing.
    /// </summary>
    [Fact]
    public void The_button_on_an_ordinary_plan_names_the_act_and_not_a_process()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Asking(ActionKind.Stop));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.carryOut.stop.one", "Spooler"), panel.CarryOutLabel);
    }

    /// <summary>
    /// Ending a process that holds one entry and nothing else asks for no typing, which is the
    /// common case by a long way - 105 of 110 processes on this machine, measured 2026-08-01.
    /// </summary>
    [Fact]
    public void An_ordinary_forced_stop_asks_for_no_typing()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler"));

        Assert.False(panel.NeedsTyping);
        Assert.True(panel.CanCarryOut);
    }

    /// <summary>
    /// Ending a process that takes neighbours with it asks for the name, and the button stays dead
    /// until it is there.
    /// </summary>
    [Fact]
    public void A_process_holding_more_than_one_entry_asks_for_the_name()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler", PlanWarningKind.TerminationTakesWithIt));

        Assert.True(panel.NeedsTyping);
        Assert.False(panel.CanCarryOut);

        panel.Typed = "Spooler";

        Assert.True(panel.CanCarryOut);
    }

    /// <summary>An entry the machine does not work without asks for it too.</summary>
    [Fact]
    public void A_critical_entry_asks_for_the_name()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler", PlanWarningKind.CriticalService));

        Assert.True(panel.NeedsTyping);
        Assert.False(panel.CanCarryOut);
    }


    /// <summary>
    /// The name is matched the way the manager matches it, and a trailing space is a keyboard
    /// rather than a change of mind.
    /// </summary>
    [Fact]
    public void The_name_is_taken_however_it_is_capitalised_and_spaced()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler", PlanWarningKind.CriticalService));

        panel.Typed = "  spOOler ";

        Assert.True(panel.CanCarryOut);
    }

    /// <summary>And something that is not the name is not the name.</summary>
    [Fact]
    public void A_different_name_does_not_turn_the_button_on()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler", PlanWarningKind.CriticalService));

        panel.Typed = "Spool";

        Assert.False(panel.CanCarryOut);
    }

    /// <summary>
    /// A name typed to confirm one thing is never waiting in the box for the next.
    ///
    /// <b>The line that would be easiest to leave out and worst to.</b> A heavy confirmation
    /// already answered, sitting under a plan about a different process, is somebody agreeing to
    /// something they were never asked.
    /// </summary>
    [Fact]
    public void What_was_typed_does_not_survive_into_the_next_plan()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler", PlanWarningKind.CriticalService));
        panel.Typed = "Spooler";

        panel.Show(Forcing("W32Time", PlanWarningKind.CriticalService));

        Assert.Equal(string.Empty, panel.Typed);
        Assert.False(panel.CanCarryOut);
    }

    /// <summary>And it does not survive the panel being put away either.</summary>
    [Fact]
    public void What_was_typed_does_not_survive_the_panel_closing()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler", PlanWarningKind.CriticalService));
        panel.Typed = "Spooler";
        panel.Hide();

        Assert.Equal(string.Empty, panel.Typed);
    }

    /// <summary>
    /// The button says what is standing in its way, which for this one is the only reason somebody
    /// can clear from where they stand.
    /// </summary>
    [Fact]
    public void The_grey_button_says_that_a_name_has_to_be_typed()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler", PlanWarningKind.CriticalService));

        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.blocked.notTyped", "Spooler"), panel.CarryOutTip);
    }

    /// <summary>
    /// A run that is finished takes the box away with it, so nothing asks for a confirmation of
    /// something that already happened.
    /// </summary>
    [Fact]
    public void A_plan_that_has_been_carried_out_stops_asking_for_a_name()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Forcing("Spooler", PlanWarningKind.CriticalService));
        panel.Finished(new BulkRun { Plan = panel.Plan!, Runs = [] });

        Assert.False(panel.NeedsTyping);
    }

    /// <summary>
    /// A plan asking about more than one entry can never be confirmed, because there is no single
    /// name to type for it.
    ///
    /// <b>Unreachable today and guarded anyway, which is the whole argument for the clause.</b>
    /// Section 15.6 of the analysis records that bulk forcing cannot be reached - the window opens
    /// this sheet from one failure and the command line takes one name - so this is what happens if
    /// that ever stops being true. Accepting one name for several processes would be somebody
    /// agreeing to the first entry and getting all of them, which is the shape rule 5 of the
    /// untouchable rules exists against: the preview and the press saying different things.
    /// </summary>
    [Fact]
    public void A_plan_that_would_end_several_processes_can_never_be_confirmed()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(ForcingTwo());

        Assert.True(panel.NeedsTyping);
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

    /// <summary>A plan with one entry, asked for in the given words, with one stop step.</summary>
    private static BulkPlan Asking(ActionKind kind) => new()
    {
        Action = new BulkAction(kind, ["Spooler"]),
        Plans =
        [
            new OperationPlan
            {
                Action = new ServiceAction(kind, "Spooler"),
                Steps = [new PlanStep("Spooler", "Print Spooler", StepOperation.Stop, StepReason.Requested)],
                Warnings = [],
                Problems = []
            }
        ],
        Problems = []
    };

    /// <summary>A plan that ends a process, with whatever warnings the case under test needs.</summary>
    private static BulkPlan Forcing(string name, params PlanWarningKind[] warnings) => new()
    {
        Action = new BulkAction(ActionKind.ForceStop, [name]),
        Plans =
        [
            new OperationPlan
            {
                Action = new ServiceAction(ActionKind.ForceStop, name),
                Steps =
                [
                    new PlanStep(name, name, StepOperation.Stop, StepReason.Requested),
                    new PlanStep(
                        name, name, StepOperation.Terminate, StepReason.Escalation, ProcessId: 4812)
                ],
                Warnings = [.. warnings.Select(kind => new PlanWarning(kind, name, ["W32Time"]))],
                Problems = []
            }
        ],
        Problems = []
    };

    private static Planned Showing(BulkPlan plan)
    {
        var panel = new Planned { Elevated = true };

        panel.Show(plan, "Print Spooler");

        return panel;
    }

    /// <summary>
    /// A run whose one step did not get there, in whichever of the ways under test.
    ///
    /// <b>Built from the plan on the panel rather than from one of its own</b>, so what comes back
    /// is a report about the steps beside it - the pairing `ADR-11` exists for.
    /// </summary>
    private static BulkRun Ended(
        Planned panel,
        StepOutcome outcome,
        Reading<int>? process = null,
        EntryStatus status = EntryStatus.Running) => new()
    {
        Plan = panel.Plan!,
        Runs =
        [
            .. panel.Plan!.Plans.Select(one => new PlanRun
            {
                Plan = one,
                Results =
                [
                    .. one.Steps.Select(step => new StepResult
                    {
                        Step = step,
                        Outcome = outcome,
                        SkippedBecause = null,
                        Status = status,
                        ProcessId = process ?? Reading<int>.Present(4812),
                        ErrorCode = outcome == StepOutcome.Failed ? 1051 : 0,
                        Error = outcome == StepOutcome.Failed ? "A stop control has been refused." : null,
                        Milliseconds = 10
                    })
                ],
                Cancelled = false,
                Ceiling = TimeSpan.FromSeconds(60)
            })
        ]
    };
}
