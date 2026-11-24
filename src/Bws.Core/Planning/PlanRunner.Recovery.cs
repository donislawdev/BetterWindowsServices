namespace Bws.Core.Planning;

/// <summary>
/// Writing recovery, and the one look the run takes before it does. Backlog 555 part (a2), 2026-11-24.
/// </summary>
public sealed partial class PlanRunner
{
    /// <summary>
    /// Whether a step changes configuration rather than moving an entry - done when the call returns, with no
    /// state to wait for. The start type since 2026-08-25, recovery since 2026-11-24.
    /// </summary>
    private static bool Configures(StepOperation operation) =>
        operation is StepOperation.SetStartType or StepOperation.SetRecovery;

    /// <summary>
    /// Writes the step's recovery, and says where the entry is while it is at it - <see cref="Configure"/> for
    /// the other setting, with one reading in front.
    ///
    /// <b>THE PARTS THE STEP WRITES ARE READ ONCE MORE, AND A CHANGE SINCE THE PREVIEW REFUSES THE STEP</b> - the
    /// logic report's R05, which the ending of a process already answers this way. Written over, somebody's
    /// change would be gone unseen and the way back would put back what the preview found, not what was there.
    /// Only the parts written: a message changed beside a list being written is left as it is by the write.
    ///
    /// <b>Nothing to compare when nothing was read for the preview</b>, and then the write goes ahead as one of
    /// a start type does - that plan already says it has no way back.
    /// </summary>
    private StepResult ConfigureRecovery(PlanStep step, TimeSpan started)
    {
        var write = step.Recovering!;

        if (write.From is { } from)
        {
            var now = control.ReadRecovery(step.ServiceName);

            var refusal = now.IsPresent
                ? write.Change.Agrees(from, now.Value!) ? null : ControlAnswer.Refused(0, RecoveryChangedSince)
                : ControlAnswer.Refused(now.ErrorCode, RecoveryNotReadAgain);

            if (refusal is not null)
            {
                var looked = control.Read(step.ServiceName);

                return Refused(step, refusal, Where(looked), Holding(looked), started);
            }
        }

        var answer = control.ConfigureRecovery(step.ServiceName, write.Change);
        var seen = control.Read(step.ServiceName);

        return answer.Worked
            ? Result(step, StepOutcome.Succeeded, Where(seen), Holding(seen), Elapsed(started))
            : Refused(step, answer, Where(seen), Holding(seen), started);
    }

    /// <summary>Said in our own words, like the refusals before an ending, because nothing refused it but this class.</summary>
    private const string RecoveryChangedSince =
        "What Windows does when this entry fails changed after the plan was built, so nothing was written over it. "
        + "Ask again to build a plan against the machine as it is now.";

    private const string RecoveryNotReadAgain =
        "What Windows does when this entry fails could not be read just before writing it, so nothing was written. "
        + "Ask again.";
}
