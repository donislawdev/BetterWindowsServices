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
        else if (pausing && Staying(target, entries, dependents) is { } staying)
        {
            warnings.Add(staying);
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
    /// The sentence about the entries that depend on this one, by the manager's own answer - the one a stop
    /// asks - or nothing, when none of them is running.
    ///
    /// <b>An answer that cannot be read is said, rule 8.</b> The plan would be no shorter for it - a pause does
    /// nothing to a dependant either way - but a plan that names what keeps running, saying nothing, reads as
    /// a plan with nothing that does.
    ///
    /// <b>Running, or on its way there</b> - a dependant that is paused, pausing or stopping does not keep
    /// running, and the sentence says it does. So not the filter a stop's cascade uses, which is anything but
    /// stopped: a paused dependant has to be stopped by a cascade, and is not running here.
    /// </summary>
    private static PlanWarning? Staying(ScmEntry target, IReadOnlyList<ScmEntry> entries, IScmCatalog dependents)
    {
        var named = dependents.ReadDependents(target.ServiceName);

        if (named.Outcome == ReadOutcome.Denied)
        {
            return new PlanWarning(PlanWarningKind.DependentsUnreadable, target.ServiceName);
        }

        if (!named.IsPresent)
        {
            return null;
        }

        List<string> running =
        [
            .. named.Value!
                .Select(name => entries.FirstOrDefault(entry =>
                    string.Equals(entry.ServiceName, name, StringComparison.OrdinalIgnoreCase)))
                .OfType<ScmEntry>()
                .Where(entry => entry.Status is EntryStatus.Running or EntryStatus.StartPending or EntryStatus.ContinuePending
                    && !target.ServiceName.Equals(entry.ServiceName, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.ServiceName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];

        return running.Count > 0 ? new PlanWarning(PlanWarningKind.DependentsKeepRunning, target.ServiceName, running) : null;
    }

    /// <summary>
    /// The plans of one selection, with the sentence naming what keeps running cut to what really does: a
    /// dependant the same selection pauses does not keep running, and its own step says so. A sentence left
    /// with nobody to name goes. Only a selection asks this, since one plan pauses one entry.
    ///
    /// <b>Only a step that is planned counts</b> - a dependant whose own pause was refused stays running, and
    /// stays named.
    /// </summary>
    internal static List<OperationPlan> Together(List<OperationPlan> plans)
    {
        var pausing = plans
            .SelectMany(plan => plan.Steps)
            .Where(step => step.Operation == StepOperation.Pause)
            .Select(step => step.ServiceName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. plans.Select(plan => plan with { Warnings = [.. plan.Warnings.Select(warning => Cut(warning, pausing)).OfType<PlanWarning>()] })];
    }

    private static PlanWarning? Cut(PlanWarning warning, HashSet<string> pausing)
    {
        if (warning.Kind != PlanWarningKind.DependentsKeepRunning)
        {
            return warning;
        }

        List<string> staying = [.. warning.Related.Where(name => !pausing.Contains(name))];

        return staying.Count == 0 ? null : warning with { Related = staying };
    }
}
