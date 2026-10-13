using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// The line the terminal writes before each step of a run, while somebody waits.
///
/// <b>Written 2026-10-13, backlog 545.</b> The line goes out before the step reads the entry, so the
/// ending that stands behind a stop was announced as "end process 5476" even when the stop had worked
/// and the ending found nothing to do. Seen in the rehearsal on the throwaway machine (S-f), where the
/// report under it said "already there".
/// </summary>
public sealed class ProgressLineTests
{
    [Fact]
    public void An_ending_behind_a_stop_is_announced_as_only_if_the_stop_did_not_work()
    {
        var said = PlanText.Progress(Ending(StepReason.Escalation), 4, 7);

        Assert.Equal(Texts.Of("cli.run.progress.escalation", 4, 7, Texts.Of("cli.plan.operation.terminate", 4812), "Spooler"), said);
    }

    [Fact]
    public void An_ending_with_no_stop_in_front_of_it_is_announced_as_it_is()
    {
        var said = PlanText.Progress(Ending(StepReason.Requested), 1, 1);

        Assert.Equal(Texts.Of("cli.run.progress", 1, 1, Texts.Of("cli.plan.operation.terminate", 4812), "Spooler"), said);
    }

    private static PlanStep Ending(StepReason reason) =>
        new("Spooler", "Print Spooler", StepOperation.Terminate, reason, ProcessId: 4812);
}
