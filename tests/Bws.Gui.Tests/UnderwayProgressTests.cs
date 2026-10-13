using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The line under the sheet's buttons that names the step in flight - backlog 545, 2026-10-13.
///
/// <b>It goes up before the step reads the entry</b>, so the ending that stands behind a stop said
/// "end the process behind" even when the stop had worked and the ending found nothing to do. The
/// terminal had the same line, and ProgressLineTests in the terminal's tests holds that one.
/// </summary>
public sealed class UnderwayProgressTests
{
    [Fact]
    public void An_ending_behind_a_stop_is_announced_as_only_if_the_stop_did_not_work()
    {
        var run = Begun();

        run.Announce(Ending(StepReason.Escalation), 4);

        Assert.Equal(Texts.Of("gui.plan.progress.escalation", 4, 7, PlanWords.Word(StepOperation.Terminate), "Spooler"), run.Progress);
    }

    [Fact]
    public void An_ending_with_no_stop_in_front_of_it_is_announced_as_it_is()
    {
        var run = Begun();

        run.Announce(Ending(StepReason.Requested), 1);

        Assert.Equal(Texts.Of("gui.plan.progress", 1, 7, PlanWords.Word(StepOperation.Terminate), "Spooler"), run.Progress);
    }

    private static Underway Begun()
    {
        var clock = new SteppedClock();
        var run = new Underway(() => clock, () => { });

        run.Begin(steps: 7, waiting: 60);

        return run;
    }

    private static PlanStep Ending(StepReason reason) =>
        new("Spooler", "Print Spooler", StepOperation.Terminate, reason, ProcessId: 4812);
}
