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
    /// <b>The terminal's rule rather than a new one, and since 2026-09-15 the SAME function</b>:
    /// <see cref="StepCeiling.Seconds"/> in the core refuses anything that is not a whole number of
    /// seconds, at least one, for both interfaces. Two interfaces disagreeing about what a timeout
    /// may be is a difference nobody meets until a runbook is being carried from one to the other.
    ///
    /// <b>AND THE REFUSAL IS SAID, WHICH UNTIL THAT DAY IT WAS NOT</b> - point 5 of the review in
    /// `docs/11` 2.14. The setter kept the old number in silence, a word failed inside the binding
    /// where nothing could see it, and the button stayed live over a box reading "abc". Now the
    /// box holds the text, the panel says under it what is wrong, and the button greys with the
    /// reason on it. Every way the text can be wrong is one case here, because every one of them
    /// used to be silent in its own way.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-60")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("1,5")]
    [InlineData("30s")]
    [InlineData("99999999999")]
    public void Text_that_is_not_a_number_of_seconds_is_said_to_be_wrong_and_greys_the_button(string typed)
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Plan(StepOperation.Stop));
        Assert.True(panel.CanCarryOut, "the fixture plan has to be runnable before the box can be the reason it is not");

        panel.WaitingText = typed;

        Assert.True(panel.HasWaitingProblem, $"'{typed}' was accepted as a number of seconds");
        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.waiting.problem"), panel.WaitingProblem);
        Assert.False(panel.CanCarryOut);
        Assert.Equal(Bws.Gui.Texts.Of("gui.plan.blocked.notSeconds"), panel.CarryOutTip);

        // THE BINDING HEARS IT TOO, in the framework's own words - which is what colours the
        // box's edge without the theme naming a property of this assembly.
        Assert.True(panel.HasErrors);
        Assert.Equal([panel.WaitingProblem], panel.GetErrors(nameof(Planned.WaitingText)).Cast<string>().ToArray());
    }

    /// <summary>
    /// What was typed is kept, wrong or not - `docs/11` section 5: keep the input so it can be
    /// corrected rather than retyped. A box that snapped back to sixty would be the old silence
    /// with a different face.
    /// </summary>
    [Fact]
    public void What_was_typed_stays_in_the_box_while_it_is_wrong()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Plan(StepOperation.Stop));
        panel.WaitingText = "abc";

        Assert.Equal("abc", panel.WaitingText);
    }

    /// <summary>
    /// A number with air around it is the one leniency, and it is the window's own rather than
    /// the rule's: a box is typed into and a runbook is not.
    /// </summary>
    [Theory]
    [InlineData("90", 90)]
    [InlineData(" 90 ", 90)]
    [InlineData("1", 1)]
    [InlineData("3600", 3600)]
    public void A_whole_number_of_seconds_is_taken_and_the_button_comes_back(string typed, int seconds)
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Plan(StepOperation.Stop));
        panel.WaitingText = "abc";
        Assert.False(panel.CanCarryOut);

        panel.WaitingText = typed;

        Assert.False(panel.HasWaitingProblem);
        Assert.Equal(string.Empty, panel.WaitingProblem);
        Assert.Equal(seconds, panel.Waiting);
        Assert.True(panel.CanCarryOut);
        Assert.False(panel.HasErrors);
    }

    /// <summary>
    /// A start type plan hides the box, so a word left in it by the last sheet cannot grey the
    /// button of a plan that has nothing to wait for - the sentence would be about a control
    /// nobody can see. And the problem comes back with the next plan that shows the box.
    /// </summary>
    [Fact]
    public void A_wrong_box_that_is_hidden_blocks_nothing_and_returns_when_shown_again()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Plan(StepOperation.Stop));
        panel.WaitingText = "abc";
        Assert.True(panel.HasWaitingProblem);

        panel.Show(Plan(StepOperation.SetStartType));

        Assert.False(panel.Waits);
        Assert.False(panel.HasWaitingProblem);
        Assert.True(panel.CanCarryOut);

        panel.Show(Plan(StepOperation.Stop));

        Assert.True(panel.HasWaitingProblem);
        Assert.False(panel.CanCarryOut);
    }

    /// <summary>
    /// <b>THE NOTIFICATION, NOT THE VALUE - and this is the guard that was missing since 2026-09-09.</b>
    /// <c>A_run_takes_the_box_off_the_screen</c> below reads <c>Waits</c> after <c>Starting</c>
    /// and it went false, correctly. But Busy announces only itself, so the binding on the box's
    /// visibility was never told to look again, and the value that was right never reached the
    /// screen - `docs/08` position 19 in this panel. Found 2026-09-15 while the box was being
    /// rewritten, by reading Starting rather than by any test.
    /// </summary>
    [Fact]
    public void Starting_a_run_announces_that_the_box_is_going_so_the_screen_hears_it()
    {
        var panel = new Planned { Elevated = true };
        var announced = new List<string>();

        panel.Show(Plan(StepOperation.Stop));
        panel.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        panel.Starting();

        Assert.Contains(nameof(Planned.Waits), announced);
        Assert.Contains(nameof(Planned.HasWaitingProblem), announced);
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

        WpfHost.On(() => panel.WaitingText = "90");

        await WpfHost.On(() => window.CarryOut());

        Assert.Equal(TimeSpan.FromSeconds(90), given);
    }

    /// <summary>
    /// And a box that is wrong stops the press at the seam - the run is never asked, so no number
    /// stood in for the one somebody meant. The other half of the guard above.
    /// </summary>
    [Fact]
    public async Task A_wrong_box_stops_the_press_before_any_run_is_given_a_number()
    {
        var asked = false;

        var window = await PlanFixture.Ready(carriedOutBy: (plan, _, _, _) =>
        {
            asked = true;
            return Task.FromResult(new BulkRun { Plan = plan, Runs = [] });
        });

        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));

        WpfHost.On(() => panel.WaitingText = "abc");

        Assert.False(await WpfHost.On(() => window.CarryOut()));
        Assert.False(asked, "the run was started with a number the box does not hold");
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
