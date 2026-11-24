using Bws.Core.Planning;
using Bws.Core.Snapshots;

namespace Bws.Cli;

/// <summary>
/// What a plan says about a recovery write - the step, the three sentences only such a write produces, and its
/// three refusals. Since 2026-11-24 (backlog 555 part a2).
///
/// <b>Reached from the discard arms of the aftermath switches</b>, the way those were reached from the ones
/// before them, so no switch here grows past the complexity ceiling. What this one does not know throws - a
/// kind with no sentence never borrows one.
/// </summary>
internal static partial class PlanText
{
    /// <summary>
    /// The parts a step writes, in the words <c>bws show</c> uses for them, on one line. A part removed says so
    /// rather than printing nothing, because "the program is gone" is the whole of what that step does.
    /// </summary>
    private static string Written(RecoveryChange change)
    {
        var document = RecoveryDocument.Of(change.To);
        var parts = new List<string>();

        if (change.Writes(RecoveryParts.Actions))
        {
            parts.Add(RecoveryWords.Actions(document).Replace("\n", ", ", StringComparison.Ordinal));
        }

        if (change.Writes(RecoveryParts.ResetPeriod) && RecoveryWords.Reset(document) is { } reset)
        {
            parts.Add(Texts.Of("cli.plan.recovery.resets", reset));
        }

        if (change.Writes(RecoveryParts.Command))
        {
            parts.Add(document.Command is { } command
                ? Texts.Of("cli.recovery.line.command", command)
                : Texts.Of("cli.plan.recovery.noProgram"));
        }

        if (change.Writes(RecoveryParts.RebootMessage))
        {
            parts.Add(document.RebootMessage is { } message
                ? Texts.Of("cli.recovery.line.message", message)
                : Texts.Of("cli.plan.recovery.noMessage"));
        }

        if (change.Writes(RecoveryParts.OnNonCrashFailures))
        {
            parts.Add(Texts.Of(document.OnNonCrashFailures ? "cli.recovery.line.nonCrash" : "cli.plan.recovery.crashesOnly"));
        }

        return Texts.Of("cli.plan.operation.setRecovery", string.Join(", ", parts));
    }

    private static string Recovering(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.RestartsComputerOnFailure =>
            Texts.Of("cli.plan.warning.restartsComputerOnFailure", warning.ServiceName),

        // The account when it was read, and a sentence that says it was not otherwise - never an empty name.
        PlanWarningKind.ProgramRunsAs => warning.Related.Count == 0
            ? Texts.Of("cli.plan.warning.programRunsAs.unread", warning.ServiceName)
            : Texts.Of("cli.plan.warning.programRunsAs", warning.ServiceName, warning.Related[0]),

        PlanWarningKind.CriticalRecovery => Texts.Of("cli.plan.warning.criticalRecovery", warning.ServiceName),

        _ => throw new ArgumentOutOfRangeException(nameof(warning), warning.Kind, EquivalentCommand.Unhandled)
    };

    private static string Recovering(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.RecoveryUnreadable => Texts.Of("cli.plan.problem.recoveryUnreadable", problem.ServiceName),
        PlanProblemKind.RecoveryUnnamed => Texts.Of("cli.plan.problem.recoveryUnnamed", problem.ServiceName),
        PlanProblemKind.NoActionsToReset => Texts.Of("cli.plan.problem.noActionsToReset", problem.ServiceName),
        _ => throw new ArgumentOutOfRangeException(nameof(problem), problem.Kind, EquivalentCommand.Unhandled)
    };
}
