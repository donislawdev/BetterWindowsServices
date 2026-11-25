namespace Bws.Core.Planning;

/// <summary>
/// Turns an ask to pause or resume an entry into the one step that does it, and says what is worth knowing
/// before it runs. Spec `C1`, backlog 555 part (b), the owner's decisions of 2026-11-25.
///
/// <b>Its own class for the reason <see cref="RecoveryPlan"/> is one</b>: <see cref="PlanBuilder"/> decides
/// what is in the way of a stop, and nothing is in the way of a pause. Measured on the throwaway machine that
/// day (tools/vm-service/pause-probe.ps1): the manager pauses the one entry it is asked about, an entry that
/// depends on it keeps running and can even be started, and nothing has to come down first.
/// </summary>
internal static class PausePlan
{
    /// <summary>Whether an ask is one this class plans.</summary>
    internal static bool Asks(ActionKind kind) => kind is ActionKind.Pause or ActionKind.Resume;

    /// <summary>The plan of a pause or a resume: one step, or the reason there is none.</summary>
    /// <param name="dependents">
    /// The catalog to ask what depends on the entry, for the sentence naming what keeps running.
    /// </param>
    internal static OperationPlan Planned(
        ServiceAction action, ScmEntry target, IReadOnlyList<ScmEntry> entries, IScmCatalog dependents)
    {
        var pausing = action.Kind == ActionKind.Pause;

        if (Refusal(target, pausing) is { } why)
        {
            return new OperationPlan
            {
                Action = action,
                Steps = [],
                Warnings = [],
                Problems = [new PlanProblem(why, action.ServiceName, [])]
            };
        }

        var warnings = new List<PlanWarning>();

        // Already where it was asked to go: one step all the same, which the run skips as already there - the
        // way a start of a running entry is planned. Measured: the manager takes a second pause and a resume of
        // a running entry without a word and does nothing, so only this sentence says so.
        if (target.Status == (pausing ? EntryStatus.Paused : EntryStatus.Running))
        {
            warnings.Add(new PlanWarning(PlanWarningKind.AlreadyThere, target.ServiceName));
        }
        else if (pausing && Running(target, entries, dependents) is { Count: > 0 } staying)
        {
            warnings.Add(new PlanWarning(PlanWarningKind.DependentsKeepRunning, target.ServiceName, staying));
        }

        return new OperationPlan
        {
            Action = action,
            Steps = [PlanSteps.Made(target, pausing ? StepOperation.Pause : StepOperation.Resume, StepReason.Requested)],
            Warnings = warnings,
            Problems = []
        };
    }

    /// <summary>
    /// Why the manager would refuse, said before it does - or nothing.
    ///
    /// <b>A stopped entry is not running, whatever its mask says</b>, because the manager reports an empty
    /// mask for anything stopped (0 measured, against 7 for the same service running). So stopped comes first
    /// and says "not running", never "does not take pauses".
    ///
    /// <b>The mask is believed only while the entry is running.</b> An entry still starting has not said what
    /// it will accept, so its pause is planned and the run waits for it to be running before asking - and the
    /// manager's own answer decides then.
    /// </summary>
    private static PlanProblemKind? Refusal(ScmEntry target, bool pausing)
    {
        if (target.Status is EntryStatus.Stopped or EntryStatus.StopPending)
        {
            return pausing ? PlanProblemKind.NotRunning : PlanProblemKind.StoppedNotPaused;
        }

        return pausing
            && target.Status == EntryStatus.Running
            && target.AcceptsPause is { IsPresent: true, Value: false }
            ? PlanProblemKind.PauseNotAccepted
            : null;
    }

    /// <summary>
    /// The running entries that depend on this one, by the manager's own answer - the one a stop asks.
    ///
    /// <b>Nothing when that answer cannot be read, and no sentence about it.</b> A stop says so out loud,
    /// because its cascade would be missing steps. Here nothing would be missing: the plan does nothing to a
    /// dependant either way, and this sentence only names what stays as it is.
    /// </summary>
    private static List<string> Running(ScmEntry target, IReadOnlyList<ScmEntry> entries, IScmCatalog dependents)
    {
        var named = dependents.ReadDependents(target.ServiceName);

        if (!named.IsPresent)
        {
            return [];
        }

        return
        [
            .. named.Value!
                .Select(name => entries.FirstOrDefault(entry =>
                    string.Equals(entry.ServiceName, name, StringComparison.OrdinalIgnoreCase)))
                .OfType<ScmEntry>()
                .Where(entry => entry.Status != EntryStatus.Stopped
                    && !target.ServiceName.Equals(entry.ServiceName, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.ServiceName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }
}
