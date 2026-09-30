using System.Globalization;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What a plan that ends a process says about what comes after it - three warnings and three refusals,
/// since 2026-09-30 (stability report W-3, package B2), and from the same day's backlog 501 the delays of
/// the restarts and the sentence after a run naming who comes back.
///
/// <b>Its own file, reached from the discard arm of the two switches beside it</b>, because the warning
/// switch stands one fork under the complexity ceiling and three more arms would have taken it two over.
/// The terminal's PlanText is cut the same way, so the two keep one shape. Each key is written out in full
/// inside the call, for the reason the head of the warning switch gives: a key built as an expression is
/// one TextKeyGuards cannot find.
/// </summary>
internal static partial class PlanWords
{
    private static string Aftermath(PlanWarning warning) => warning.Kind switch
    {
        // WHEN, BESIDE EVERY NAME, since 2026-09-30 (backlog 501).
        PlanWarningKind.RecoveryRestarts => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.recoveryRestarts.one", warning.ServiceName, warning.Related.Count, Later(warning.Related, warning.Restarts))
            : Texts.Of("gui.plan.warning.recoveryRestarts.many", warning.ServiceName, warning.Related.Count, Later(warning.Related, warning.Restarts)),

        PlanWarningKind.RecoveryRunsProgram => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.recoveryRunsProgram.one", warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of("gui.plan.warning.recoveryRunsProgram.many", warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.RecoveryUnnamed => warning.Related.Count == 1
            ? Texts.Of("gui.plan.warning.recoveryUnnamed.one", warning.ServiceName, warning.Related.Count, Listed(warning.Related))
            : Texts.Of("gui.plan.warning.recoveryUnnamed.many", warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        _ => throw new ArgumentOutOfRangeException(
            nameof(warning), warning.Kind, EquivalentCommand.Unhandled)
    };

    /// <summary>
    /// The three refusals. An unreadable consequence with no names is the process itself - whether it is
    /// critical could not be read - so it has a sentence of its own.
    /// </summary>
    private static string Aftermath(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.ProcessIsCritical => Texts.Of("gui.plan.problem.processIsCritical", problem.ServiceName),

        PlanProblemKind.RecoveryRestartsComputer => problem.Related.Count == 1
            ? Texts.Of("gui.plan.problem.recoveryRestartsComputer.one", problem.ServiceName, Listed(problem.Related))
            : Texts.Of("gui.plan.problem.recoveryRestartsComputer.many", problem.ServiceName, Listed(problem.Related)),

        PlanProblemKind.AftermathUnreadable when problem.Related.Count == 0 =>
            Texts.Of("gui.plan.problem.aftermathUnreadable.process", problem.ServiceName),

        PlanProblemKind.AftermathUnreadable => problem.Related.Count == 1
            ? Texts.Of("gui.plan.problem.aftermathUnreadable.one", problem.ServiceName, Listed(problem.Related))
            : Texts.Of("gui.plan.problem.aftermathUnreadable.many", problem.ServiceName, Listed(problem.Related)),

        _ => throw new ArgumentOutOfRangeException(
            nameof(problem), problem.Kind, EquivalentCommand.Unhandled)
    };

    /// <summary>
    /// How a finished run reads, followed by who Windows starts again and when - the sentence alone when
    /// nobody comes back. Since 2026-09-30, backlog 501: <see cref="PlanRun.ComingBack"/> decides who.
    ///
    /// <b>A second sentence in the notice rather than a section of its own</b>, because it qualifies the
    /// first: "Done. The entry is where you asked." is true when it is read and stops being true a minute
    /// later, and the sentence saying so belongs directly after it. No new element, so nothing on the sheet
    /// moves - the notice already wraps.
    /// </summary>
    internal static string ThenComingBack(string reported, BulkRun run)
    {
        string[] back = [.. run.Runs.Select(ComingBack).Where(line => line.Length > 0)];

        return back.Length == 0
            ? reported
            : Texts.Of("gui.plan.notice.andAfter", reported, string.Join(" ", back));
    }

    private static string ComingBack(PlanRun run)
    {
        var back = run.ComingBack;

        return back.Count switch
        {
            0 => string.Empty,
            1 => Texts.Of("gui.plan.notice.comesBack.one", run.Plan.Action.ServiceName, Later([back[0].ServiceName], back)),
            _ => Texts.Of("gui.plan.notice.comesBack.many", run.Plan.Action.ServiceName, back.Count, Later([.. back.Select(one => one.ServiceName)], back))
        };
    }

    /// <summary>
    /// The names with the delays their recovery lists restart them after - a name with none to say stays
    /// bare, so a warning built without them reads as it did before they were read.
    /// </summary>
    private static string Later(IReadOnlyList<string> names, IReadOnlyList<RecoveryRestart> restarts) =>
        Listed([.. names.Select(name =>
            restarts.FirstOrDefault(one => string.Equals(one.ServiceName, name, StringComparison.OrdinalIgnoreCase))
                is { After.Count: > 0 } restart
                ? Texts.Of("gui.plan.recovery.later", name, Delays(restart.After))
                : name)]);

    /// <summary>
    /// "60 s", or "1 s, 2 s, 4 s, 8 s or 16 s" - every delay, because which item runs depends on a count of
    /// failures nothing hands out (<see cref="RecoveryRestart"/>). Always seconds, one decimal place.
    /// </summary>
    private static string Delays(IReadOnlyList<TimeSpan> after) => after.Count == 1
        ? Seconds(after[0])
        : Texts.Of(
            "gui.plan.recovery.either",
            string.Join(", ", after.Take(after.Count - 1).Select(Seconds)),
            Seconds(after[^1]));

    private static string Seconds(TimeSpan delay) =>
        Texts.Of("gui.plan.recovery.seconds", delay.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture));
}
