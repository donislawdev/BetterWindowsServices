using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The two ways of asking a run to stop, as the sheet answers them - backlog 497, 2026-09-30.
///
/// <b>No window here, and that is the point of Underway being a type.</b> The rule that keeps a press
/// nobody meant from leaving a machine with nothing put back is a rule about order and time, and a
/// stepped clock asks it in microseconds. AbandonGuards asks the same questions of the buttons.
/// </summary>
public sealed class UnderwayTests
{
    private static (Underway Run, SteppedClock Clock) Begun()
    {
        var clock = new SteppedClock();
        var run = new Underway(() => clock, () => { });

        run.Begin(steps: 2, waiting: 60);

        return (run, clock);
    }

    [Fact]
    public void Interrupting_greys_the_first_button_and_says_so()
    {
        var (run, _) = Begun();

        Assert.True(run.CanInterrupt);
        Assert.Equal(Texts.Of("gui.plan.interrupt.hint"), run.InterruptTip);
        Assert.Equal(Texts.Of("gui.plan.notice.running"), run.Notice);

        run.Interrupt(closing: false);

        Assert.False(run.CanInterrupt);
        Assert.Equal(Texts.Of("gui.plan.interrupt.asked"), run.InterruptTip);
        Assert.Equal(Texts.Of("gui.plan.notice.interrupting"), run.Notice);
    }

    /// <summary>
    /// THE RULE ITSELF: nothing before the first level, nothing before the wait, once after it - and
    /// the button stays on the sheet, grey, once pressed, so nothing appears or disappears twice.
    /// </summary>
    [Fact]
    public void The_second_level_is_offered_only_after_an_interrupt_and_the_wait()
    {
        var (run, clock) = Begun();

        clock.Advance(TimeSpan.FromMinutes(5));
        run.Tick();

        Assert.False(run.OffersAbandoning);
        Assert.False(run.Abandon());

        run.Interrupt(closing: false);
        clock.Advance(Underway.ArmsAfter - TimeSpan.FromMilliseconds(1));
        run.Tick();

        Assert.False(run.OffersAbandoning);
        Assert.False(run.CanAbandon);
        Assert.False(run.Abandon());

        clock.Advance(TimeSpan.FromMilliseconds(1));
        run.Tick();

        Assert.True(run.OffersAbandoning);
        Assert.True(run.CanAbandon);
        Assert.True(run.Abandon());

        Assert.True(run.OffersAbandoning);
        Assert.False(run.CanAbandon);
        Assert.False(run.Abandon());
        Assert.Equal(Texts.Of("gui.plan.notice.abandoning"), run.Notice);
    }

    /// <summary>A second press of Interrupt - a double click - does not start the wait again.</summary>
    [Fact]
    public void A_second_interrupt_keeps_the_first_one_s_time()
    {
        var (run, clock) = Begun();

        run.Interrupt(closing: false);
        clock.Advance(TimeSpan.FromSeconds(2));
        run.Interrupt(closing: false);
        clock.Advance(Underway.ArmsAfter - TimeSpan.FromSeconds(2));
        run.Tick();

        Assert.True(run.CanAbandon);
    }

    /// <summary>
    /// THE WAIT IS MEASURED ON THE MONOTONIC CLOCK. The wall clock jumps when a virtual machine resumes
    /// (IClock.Elapsed), and a jump of an hour must not offer the second level a moment after the first.
    /// </summary>
    [Fact]
    public void A_jump_of_the_wall_clock_does_not_offer_the_second_level()
    {
        var clock = new Jumping();
        var run = new Underway(() => clock, () => { });

        run.Begin(steps: 1, waiting: 60);
        run.Interrupt(closing: false);
        clock.Now += TimeSpan.FromHours(1);
        run.Tick();

        Assert.False(run.CanAbandon);
    }

    [Fact]
    public void Closing_says_the_window_waits_for_the_run()
    {
        var (run, _) = Begun();

        run.Interrupt(closing: true);

        Assert.Equal(
            Texts.Of("gui.plan.notice.because", Texts.Of("gui.plan.notice.interrupting"), Texts.Of("gui.plan.notice.closing")),
            run.Notice);
    }

    /// <summary>
    /// Every way out of a run takes both asks with it - a second button left on a sheet with no run
    /// under it would be a way to abandon nothing, and an interrupt left standing would grey the
    /// first button of the next run.
    /// </summary>
    [Fact]
    public void The_end_of_a_run_takes_both_asks_away()
    {
        var (run, clock) = Begun();

        run.Announce(new PlanStep("Spooler", "Spooler", StepOperation.Stop, StepReason.Requested), 1);
        run.Interrupt(closing: true);
        clock.Advance(Underway.ArmsAfter);
        run.Tick();
        run.End();

        Assert.False(run.CanInterrupt);
        Assert.False(run.OffersAbandoning);
        Assert.False(run.HasProgress);

        run.Begin(steps: 1, waiting: 60);

        Assert.True(run.CanInterrupt);
        Assert.False(run.OffersAbandoning);
        Assert.Equal(Texts.Of("gui.plan.notice.running"), run.Notice);
    }

    /// <summary>
    /// The report reads which ask the run met off its STEPS: a step going forward held back is the
    /// first level, a step putting something back held back is the second. A run whose flag says it
    /// was interrupted but which held nothing back reports as any other run.
    /// </summary>
    [Theory]
    [InlineData(StepReason.Requested, "gui.plan.notice.interrupted.one")]
    [InlineData(StepReason.Restore, "gui.plan.notice.leftAsItWas.one")]
    public void A_stopped_run_says_which_ask_it_met(StepReason held, string key)
    {
        var run = Run((StepReason.Requested, null), (held, SkipReason.Cancelled));

        Assert.Equal(Texts.Of(key), Underway.Afterwards(run, arrived: 0));
    }

    /// <summary>And the sheet itself says it, where the sentence about a finished run stands.</summary>
    [Fact]
    public void A_finished_run_that_was_held_back_says_so_on_the_sheet()
    {
        var run = Run((StepReason.Requested, SkipReason.Cancelled));
        var panel = new Planned { Elevated = true };

        Assert.True(panel.Show(run.Plan));

        panel.Starting();
        panel.Finished(run);

        Assert.Equal(Texts.Of("gui.plan.notice.interrupted.one"), panel.Notice);
    }

    [Fact]
    public void A_run_that_held_nothing_back_says_nothing_about_being_stopped()
    {
        var run = Run((StepReason.Requested, null), (StepReason.Restore, null));

        Assert.Equal(string.Empty, Underway.Afterwards(run, arrived: 1));
    }

    private static BulkRun Run(params (StepReason Reason, SkipReason? Skipped)[] steps)
    {
        var planSteps = steps.Select(step => new PlanStep(
            "Spooler", "Spooler", step.Reason == StepReason.Restore ? StepOperation.Start : StepOperation.Stop, step.Reason)).ToList();
        var plan = new OperationPlan
        {
            Action = new ServiceAction(ActionKind.Restart, "Spooler"),
            Steps = planSteps,
            Warnings = [],
            Problems = []
        };

        return new BulkRun
        {
            Plan = new BulkPlan { Action = new BulkAction(ActionKind.Restart, ["Spooler"]), Plans = [plan], Problems = [] },
            Runs =
            [
                new PlanRun
                {
                    Plan = plan,
                    Results = [.. planSteps.Zip(steps, (step, each) => new StepResult
                    {
                        Step = step,
                        Outcome = each.Skipped is null ? StepOutcome.Succeeded : StepOutcome.Skipped,
                        SkippedBecause = each.Skipped,
                        Status = EntryStatus.Stopped,
                        ProcessId = Reading<int>.Absent(),
                        ErrorCode = 0,
                        Error = null,
                        Milliseconds = 1
                    })],
                    Cancelled = true,
                    Ceiling = TimeSpan.FromMinutes(1)
                }
            ]
        };
    }

    /// <summary>A clock whose wall time can jump while its monotonic count stands still.</summary>
    private sealed class Jumping : IClock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

        public TimeSpan Elapsed => TimeSpan.Zero;

        public void Wait(TimeSpan duration)
        {
        }
    }
}
