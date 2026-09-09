using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// How long one step is watched for, and what the window says while it is watching.
///
/// <b>NEITHER HALF EXISTED UNTIL 2026-09-09</b> - backlog 330, and the pre-release audit's
/// RELEASE-008. The ceiling was a constant nobody could reach from the window, so "wait longer"
/// was an answer the terminal had and this did not. And the line under the plan named the step
/// without saying how long it had been going, so a stop fifty seconds into its ceiling and one two
/// seconds in read the same - on a window that has measured a single StartService at 30 375 ms.
///
/// <b>THE SECOND OF THESE WAS FOUND BY LOOKING AT A SCREENSHOT, NOT BY A TEST, AND THAT IS WORTH
/// KEEPING.</b> The first build left the box on screen during the run. Every assertion here passed,
/// because the box was correct in every way except the one that mattered: the ceiling is read at
/// the press, so a box a person can still type into while the machine is being changed is a control
/// that accepts a number and does nothing with it. <c>A_run_takes_the_box_off_the_screen</c> is
/// that fault written down.
/// </summary>
public sealed class WaitingGuards
{
    /// <summary>Sixty, which is what the terminal uses when nobody says otherwise.</summary>
    [Fact]
    public void The_window_starts_where_the_terminal_starts() =>
        Assert.Equal(60, new Planned().Waiting);

    /// <summary>
    /// <b>The terminal's rule rather than a new one</b>: <c>Arguments.Seconds</c> refuses anything
    /// that is not a whole number of seconds, at least one. Two interfaces disagreeing about what a
    /// timeout may be is a difference nobody meets until a runbook is being carried from one to the
    /// other.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60)]
    public void Nothing_below_one_second_is_taken(int refused)
    {
        var panel = new Planned { Waiting = 90 };

        panel.Waiting = refused;

        Assert.Equal(90, panel.Waiting);
    }

    /// <summary>A plan that moves a service has something to wait for, so the box is offered.</summary>
    [Fact]
    public void A_plan_that_stops_something_offers_the_box()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Plan(StepOperation.Stop));

        Assert.True(panel.Waits);
    }

    /// <summary>
    /// <b>And a start type change does not, which is the half that keeps the box meaning
    /// something.</b> It writes a setting and moves no service, so there is no state to arrive at
    /// and nothing to watch for. The terminal says the same in its help - neither the dependents
    /// switch nor the timeout one applies there.
    /// </summary>
    [Fact]
    public void A_start_type_change_has_nothing_to_wait_for_and_offers_nothing()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Plan(StepOperation.SetStartType));

        Assert.False(panel.Waits);
    }

    /// <summary>
    /// <b>THE ONE A SCREENSHOT FOUND.</b> The ceiling is read at the press, so a box still on
    /// screen while the run is happening would take a number and throw it away.
    /// </summary>
    [Fact]
    public void A_run_takes_the_box_off_the_screen()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Plan(StepOperation.Stop));
        Assert.True(panel.Waits);

        panel.Starting();

        Assert.False(panel.Waits);
    }

    /// <summary>
    /// Under a second the line says the step and nothing else - a counter reading "0 s of 60" is
    /// motion carrying no information on a line somebody is trying to read past.
    /// </summary>
    [Fact]
    public void Below_a_second_the_line_is_just_the_step() =>
        Assert.Equal(
            "Step 1 of 1: stop Spooler",
            PlanWords.StillWaiting("Step 1 of 1: stop Spooler", TimeSpan.FromMilliseconds(400), 60));

    /// <summary>
    /// <b>Both numbers, because either alone is the wrong sentence.</b> Elapsed on its own says how
    /// long somebody has waited and not whether waiting is nearly over. The ceiling on its own is
    /// the number they set. Together they say whether this is about to be given up on.
    /// </summary>
    [Fact]
    public void Past_a_second_it_says_how_long_and_out_of_how_long()
    {
        var line = PlanWords.StillWaiting("stop Spooler", TimeSpan.FromSeconds(12.9), 60);

        Assert.Contains("12", line, StringComparison.Ordinal);
        Assert.Contains("60", line, StringComparison.Ordinal);
        Assert.StartsWith("stop Spooler", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Past the ceiling is a real state rather than an impossible one</b>, and the words have to
    /// survive it rather than pretend it cannot happen. The ceiling caps our watching, not the
    /// manager's answering - an entry reporting its own wait hint is still being honoured while
    /// this reads seventy of sixty.
    /// </summary>
    [Fact]
    public void Past_the_ceiling_it_keeps_counting_rather_than_stopping_at_the_number() =>
        Assert.Contains(
            "70",
            PlanWords.StillWaiting("stop Spooler", TimeSpan.FromSeconds(70), 60),
            StringComparison.Ordinal);

    /// <summary>
    /// The clock is per step rather than per run, because the ceiling is. A run of six steps may
    /// take six minutes without any one of them being near its limit.
    /// </summary>
    [Fact]
    public void The_clock_restarts_with_each_step()
    {
        var clock = new SteppedClock();
        var panel = new Planned { Elevated = true, Clock = clock };

        panel.Show(Plan(StepOperation.Stop));
        panel.Starting();

        panel.Announce(new PlanStep("Spooler", "Spooler", StepOperation.Stop, StepReason.Requested), 1);
        var first = panel.Progress;

        panel.Announce(new PlanStep("Dnscache", "Dnscache", StepOperation.Stop, StepReason.Requested), 2);

        Assert.NotEqual(first, panel.Progress);
        Assert.Contains("Dnscache", panel.Progress, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>THAT THE NUMBER REACHES THE RUN, WHICH IS THE ONLY ASSERTION HERE THAT WOULD MATTER ON A
    /// REAL MACHINE.</b> Everything above checks what is on screen and what it refuses. This checks
    /// that a person who typed ninety gets ninety - and it is written against the seam the window
    /// carries plans through, so it exercises the same call the button makes rather than a copy of
    /// the reasoning behind it.
    ///
    /// <b>Read at the press rather than held anywhere</b>, which is what makes the box on the sheet
    /// in front of somebody the thing that decides. Nothing keeps a copy for later.
    /// </summary>
    [Fact]
    public async Task What_was_typed_into_the_box_is_what_the_run_is_given()
    {
        TimeSpan? given = null;

        var window = await PlanFixture.Ready(carriedOutBy: (plan, ceiling, _, _) =>
        {
            given = ceiling;
            return Task.FromResult(new BulkRun { Plan = plan, Runs = [] });
        });

        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));

        WpfHost.On(() => panel.Waiting = 90);

        await WpfHost.On(() => window.CarryOut());

        Assert.Equal(TimeSpan.FromSeconds(90), given);
    }

    // -- fixtures --------------------------------------------------------------------------

    private static BulkPlan Plan(StepOperation operation) => new()
    {
        Action = new BulkAction(
            operation == StepOperation.SetStartType ? ActionKind.SetStartType : ActionKind.Stop,
            ["Spooler"]),
        Plans =
        [
            new OperationPlan
            {
                Action = new ServiceAction(
                    operation == StepOperation.SetStartType ? ActionKind.SetStartType : ActionKind.Stop,
                    "Spooler",
                    To: operation == StepOperation.SetStartType ? StartType.Manual : null),
                Steps = [new PlanStep("Spooler", "Spooler", operation, StepReason.Requested)],
                Warnings = [],
                Problems = []
            }
        ],
        Problems = []
    };
}
