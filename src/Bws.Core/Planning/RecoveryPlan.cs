namespace Bws.Core.Planning;

/// <summary>
/// Turns an ask to change an entry's recovery into the one step that writes it, and says what is worth knowing
/// before it runs. Backlog 555 part (a2), spec `C7`, the owner's decisions of 2026-11-24.
///
/// <b>Its own class for the reason <see cref="ForcedStop"/> is one</b>: <see cref="PlanBuilder"/> decides what
/// is in the way of a move, and nothing is in the way of a write - what this kind needs instead is a reading of
/// the entry and a handful of sentences about what the write puts down.
/// </summary>
internal static class RecoveryPlan
{
    /// <summary>
    /// The plan of a recovery write: one step, or the refusal <see cref="Decide"/> gives, and the sentences about
    /// what it puts down. The entry is read here, once, for the one plan that needs it.
    /// </summary>
    internal static OperationPlan Planned(ServiceAction action, ScmEntry target, IRecoveryReader reader)
    {
        // THE SAME KIND OF LOUD AS PlanBuilder.ThrowIfNobodyCouldAsk, and before the entry is read: an ask that
        // names no part has nothing to write, and an item this tool has no word for is not something either
        // interface can ask for - the way back for it could not be written. Neither has a sentence, because
        // neither can be met.
        if (action.Recovery is not { Parts: not RecoveryParts.None } asked
            || asked.To.List.Items.Any(item => item.Action == RecoveryAction.Unnamed))
        {
            throw new ArgumentException(
                "A recovery write needs at least one part to write, and only items this tool can name.",
                nameof(action));
        }

        var (refusal, step) = Decide(target, asked, reader.ReadWhole(target.ServiceName));

        if (refusal is { } why)
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

        AddWarnings(warnings, target, step!);
        CriticalEntries.AddWarnings(warnings, target, action, []);

        return new OperationPlan
        {
            Action = action,
            Steps = [step!],
            Warnings = warnings,
            Problems = []
        };
    }

    /// <summary>
    /// The step, or the reason there is none.
    ///
    /// <b>The ask names parts and the step names what the manager will be handed.</b> The two differ in one
    /// place: the list and its reset period are one write to the manager, so when either is named the step names
    /// both, the other half read off the entry. That reading is the one the plan was built from, so the run
    /// refuses if it changed - nothing is written over a half nobody saw.
    /// </summary>
    internal static (PlanProblemKind? Refusal, PlanStep? Step) Decide(
        ScmEntry target, RecoveryChange asked, Reading<RecoverySettings> reading)
    {
        var from = reading.IsPresent ? reading.Value : null;
        var change = asked;

        if (asked.Writes(RecoveryParts.Actions) || asked.Writes(RecoveryParts.ResetPeriod))
        {
            if (from is not null && from.List.Items.Any(item => item.Action == RecoveryAction.Unnamed))
            {
                return (PlanProblemKind.RecoveryUnnamed, null);
            }

            var both = asked.Writes(RecoveryParts.Actions | RecoveryParts.ResetPeriod);

            if (!both && from is null)
            {
                return (PlanProblemKind.RecoveryUnreadable, null);
            }

            var items = asked.Writes(RecoveryParts.Actions) ? asked.To.List.Items : from!.List.Items;

            if (items.Count == 0 && !asked.Writes(RecoveryParts.Actions))
            {
                return (PlanProblemKind.NoActionsToReset, null);
            }

            // AN EMPTY LIST HAS A PERIOD OF ZERO, because that is what the manager leaves behind: it deletes the
            // period with the list (measured 2026-11-24). Saying anything else here would be a preview of a value
            // the machine will not hold.
            var reset = items.Count == 0 ? TimeSpan.Zero
                : asked.Writes(RecoveryParts.ResetPeriod) ? asked.To.List.ResetPeriod
                : from!.List.ResetPeriod;

            change = new RecoveryChange(
                asked.Parts | RecoveryParts.Actions | RecoveryParts.ResetPeriod,
                asked.To with { List = asked.To.List with { Items = items, ResetPeriod = reset } });
        }

        var step = PlanSteps.Made(target, StepOperation.SetRecovery, StepReason.Requested)
            with { Recovering = new RecoveryWrite(change, from) };

        return (null, step);
    }

    /// <summary>
    /// The sentences only a recovery write produces. The critical entries are said by
    /// <see cref="CriticalEntries"/>, beside the other two kinds that change one.
    /// </summary>
    internal static void AddWarnings(List<PlanWarning> warnings, ScmEntry target, PlanStep step)
    {
        var write = step.Recovering!;
        var change = write.Change;

        if (write.From is { } from && change.Agrees(from, change.To))
        {
            warnings.Add(new PlanWarning(PlanWarningKind.AlreadyThere, target.ServiceName));
        }

        if (change.Writes(RecoveryParts.Actions)
            && change.To.List.Items.Any(item => item.Action == RecoveryAction.RestartComputer))
        {
            warnings.Add(new PlanWarning(PlanWarningKind.RestartsComputerOnFailure, target.ServiceName));
        }

        // The list and its period are always written together, so "Actions" here is either half asked.
        if (change.Writes(RecoveryParts.Actions)
            && change.To.List.Items.Count > 1
            && change.To.List.ResetPeriod == TimeSpan.Zero)
        {
            warnings.Add(new PlanWarning(PlanWarningKind.EveryFailureFirst, target.ServiceName));
        }

        if (LeavesUnused(write, RecoveryParts.Command, RecoveryAction.RunProgram))
        {
            warnings.Add(new PlanWarning(PlanWarningKind.ProgramUnused, target.ServiceName));
        }

        if (LeavesUnused(write, RecoveryParts.RebootMessage, RecoveryAction.RestartComputer))
        {
            warnings.Add(new PlanWarning(PlanWarningKind.RebootMessageUnused, target.ServiceName));
        }

        if (RunsAProgram(change, write.From))
        {
            warnings.Add(new PlanWarning(
                PlanWarningKind.ProgramRunsAs,
                target.ServiceName,
                target.Account.IsPresent && !string.IsNullOrEmpty(target.Account.Value) ? [target.Account.Value!] : []));
        }
    }

    /// <summary>
    /// Whether this write leaves a text no failure uses - a program no item runs
    /// (<see cref="PlanWarningKind.ProgramUnused"/>) or a message no item sends
    /// (<see cref="PlanWarningKind.RebootMessageUnused"/>).
    ///
    /// <b>Only when it sets that text or changes what the list holds</b>, so rewriting the period of an entry
    /// whose program was already unused says nothing about it. <b>Only when what the entry had was read</b>:
    /// the opposite of <see cref="RunsAProgram"/>, because this is a remark about a text that will NOT be used,
    /// and a guess at it would be a remark about nothing.
    /// </summary>
    /// <param name="text">The part holding the text - the command or the message.</param>
    /// <param name="user">The kind of item that uses it.</param>
    private static bool LeavesUnused(RecoveryWrite write, RecoveryParts text, RecoveryAction user)
    {
        if (write.From is not { } from)
        {
            return false;
        }

        var after = write.Change.Over(from);
        var touched = write.Change.Writes(text) || !after.List.Items.SequenceEqual(from.List.Items);
        var held = text == RecoveryParts.Command ? after.List.Command : after.List.RebootMessage;

        return touched && held is not null && after.List.Items.All(item => item.Action != user);
    }

    /// <summary>
    /// Whether, after this write, a failure runs a program - an item that runs one AND a program to run.
    ///
    /// <b>Only when this write touches one of the two</b>: a message changed on an entry that already runs a
    /// program is not the moment to say it. <b>A half that was not read counts as there</b>, because the
    /// sentence is a caution, and silence about a program that may run is the wrong way to be uncertain.
    /// </summary>
    private static bool RunsAProgram(RecoveryChange change, RecoverySettings? from)
    {
        if (!change.Writes(RecoveryParts.Actions) && !change.Writes(RecoveryParts.Command))
        {
            return false;
        }

        var after = from is null ? null : change.Over(from);

        var items = change.Writes(RecoveryParts.Actions) ? change.To.List.Items : after?.List.Items;
        var anItem = items is null || items.Any(item => item.Action == RecoveryAction.RunProgram);

        var aProgram = change.Writes(RecoveryParts.Command)
            ? change.To.List.Command is not null
            : after is null || after.List.Command is not null;

        return anItem && aProgram;
    }
}
