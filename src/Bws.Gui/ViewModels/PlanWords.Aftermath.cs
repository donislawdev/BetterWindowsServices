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
        PlanWarningKind.RecoveryRestarts => Restarting(warning),

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
    /// The restart warning, with WHEN beside every name since 2026-09-30 (backlog 501) and WHICH FAILURES
    /// since 2026-10-07 (backlog 541, the owner's decision of that day). When every entry's list restarts it
    /// on every failure the sentence is the one it always was. When one does not - Spooler's restart, restart,
    /// nothing, 190 of 204 lists on the owner's machine - its name says on which failures, and a clause says
    /// when the count starts again and that Windows does not say which failure this is. The terminal's
    /// PlanText says the same in the same shape.
    /// </summary>
    private static string Restarting(PlanWarning warning)
    {
        var names = Later(warning.Related, warning.Restarts);

        return (warning.Related.Count == 1, Counted(warning.Restarts)) switch
        {
            (true, null) => Texts.Of("gui.plan.warning.recoveryRestarts.one", warning.ServiceName, warning.Related.Count, names),
            (false, null) => Texts.Of("gui.plan.warning.recoveryRestarts.many", warning.ServiceName, warning.Related.Count, names),
            (true, { } counted) => Texts.Of(
                "gui.plan.warning.recoveryRestarts.some.one",
                warning.ServiceName,
                warning.Related.Count,
                names,
                Texts.Of("gui.plan.recovery.unsaid", counted)),
            (false, { } counted) => Texts.Of(
                "gui.plan.warning.recoveryRestarts.some.many",
                warning.ServiceName,
                warning.Related.Count,
                names,
                Texts.Of("gui.plan.recovery.unsaid", counted))
        };
    }

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

    /// <summary>
    /// Who Windows starts again after this run, in the warning's two shapes and for the same reason (backlog
    /// 541): an entry restarted on some failures only is started again if this was one of them.
    /// </summary>
    private static string ComingBack(PlanRun run)
    {
        var back = run.ComingBack;

        if (back.Count == 0)
        {
            return string.Empty;
        }

        var target = run.Plan.Action.ServiceName;
        var names = Later([.. back.Select(one => one.ServiceName)], back);

        return (back.Count == 1, Counted(back)) switch
        {
            (true, null) => Texts.Of("gui.plan.notice.comesBack.one", target, names),
            (false, null) => Texts.Of("gui.plan.notice.comesBack.many", target, back.Count, names),
            (true, { } counted) => Texts.Of(
                "gui.plan.notice.comesBack.some.one", target, names, Texts.Of("gui.plan.notice.unsaid", counted)),
            (false, { } counted) => Texts.Of(
                "gui.plan.notice.comesBack.some.many", target, back.Count, names, Texts.Of("gui.plan.notice.unsaid", counted))
        };
    }

    /// <summary>
    /// The names with the delays their recovery lists restart them after, and since 2026-10-07 the failures
    /// they do so on when not every one - a name with none to say stays bare, so a warning built without them
    /// reads as it did before they were read.
    /// </summary>
    private static string Later(IReadOnlyList<string> names, IReadOnlyList<RecoveryRestart> restarts) =>
        Listed([.. names.Select(name =>
            restarts.FirstOrDefault(one => string.Equals(one.ServiceName, name, StringComparison.OrdinalIgnoreCase))
                is { After.Count: > 0 } restart
                ? When(name, restart)
                : name)]);

    private static string When(string name, RecoveryRestart restart) => restart.Only switch
    {
        null => Texts.Of("gui.plan.recovery.later", name, Delays(restart.After)),
        { AndLater: true } only => Texts.Of("gui.plan.recovery.laterOrLater", name, Delays(restart.After), Failures(only)),
        { } only => Texts.Of("gui.plan.recovery.laterOnly", name, Delays(restart.After), Failures(only))
    };

    /// <summary>"1", "1 or 2", "1, 2 or 3" - and "2" or "1, 3" for a list whose last item repeats, which the sentence ends with "or later".</summary>
    private static string Failures(SomeFailures only)
    {
        string[] numbers = [.. only.Numbers.Select(number => number.ToString(CultureInfo.InvariantCulture))];

        return only.AndLater || numbers.Length == 1
            ? string.Join(", ", numbers)
            : Texts.Of("gui.plan.recovery.either", string.Join(", ", numbers[..^1]), numbers[^1]);
    }

    /// <summary>
    /// When the count starts again, one clause per reset period among the entries restarted on some failures
    /// only - or nothing when every entry is restarted on every failure.
    /// </summary>
    private static string? Counted(IReadOnlyList<RecoveryRestart> restarts)
    {
        string[] clauses =
        [
            .. restarts
                .Where(one => one.Only is not null)
                .GroupBy(one => one.Only!.ResetPeriod)
                .Select(same => Clause([.. same.Select(one => one.ServiceName)], same.Key))
        ];

        return clauses.Length == 0 ? null : string.Join(", ", clauses);
    }

    private static string Clause(IReadOnlyList<string> names, TimeSpan resetPeriod) =>
        (names.Count == 1, resetPeriod == Timeout.InfiniteTimeSpan) switch
        {
            (true, true) => Texts.Of("gui.plan.recovery.countAtBoot.one", names[0]),
            (false, true) => Texts.Of("gui.plan.recovery.countAtBoot.many", Both(names)),
            (true, false) => Texts.Of("gui.plan.recovery.count.one", names[0], Period(resetPeriod)),
            (false, false) => Texts.Of("gui.plan.recovery.count.many", Both(names), Period(resetPeriod))
        };

    private static string Both(IReadOnlyList<string> names) =>
        Texts.Of("gui.plan.recovery.and", string.Join(", ", names.Take(names.Count - 1)), names[^1]);

    /// <summary>A reset period in the largest unit that divides it evenly - 30 s, 15 min, 1 h, 24 h - the owner's decision of 2026-10-07.</summary>
    private static string Period(TimeSpan period) => (long)period.TotalSeconds switch
    {
        var seconds when seconds % 3600 == 0 => Texts.Of("gui.plan.recovery.hours", seconds / 3600),
        var seconds when seconds % 60 == 0 => Texts.Of("gui.plan.recovery.minutes", seconds / 60),
        var seconds => Texts.Of("gui.plan.recovery.seconds", seconds)
    };

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
