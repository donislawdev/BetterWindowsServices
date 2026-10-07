using System.Globalization;
using System.Text;
using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// What a plan that ends a process says about what comes after it - three warnings and three refusals,
/// since 2026-09-30 (stability report W-3, package B2), and from the same day's backlog 501 the delays of
/// the restarts and the line after a run naming who comes back.
///
/// <b>Its own file, reached from the discard arm of the two switches beside it</b>, so that neither grows:
/// the window's twin of the warning switch stands one fork under the complexity ceiling, and this side is
/// cut the same way so the two stay one shape. The named arms and the refusal at the end are the rule
/// those switches have kept since 2026-09-06 - a kind with no sentence throws rather than borrowing one.
/// </summary>
internal static partial class PlanText
{
    private static string Aftermath(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.RecoveryRestarts => Restarting(warning),

        PlanWarningKind.RecoveryRunsProgram => Texts.Of(
            Count("cli.plan.warning.recoveryRunsProgram", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        PlanWarningKind.RecoveryUnnamed => Texts.Of(
            Count("cli.plan.warning.recoveryUnnamed", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        _ => throw new ArgumentOutOfRangeException(
            nameof(warning), warning.Kind, EquivalentCommand.Unhandled)
    };

    /// <summary>
    /// The restart warning, with WHEN beside every name since 2026-09-30 (backlog 501) and WHICH FAILURES
    /// since 2026-10-07 (backlog 541, the owner's decision of that day).
    ///
    /// <b>Two shapes, and the old one is untouched.</b> When every entry's list restarts it on every failure the
    /// sentence is the one it always was, ending in the bare name a person copies for sc.exe. When one does not
    /// - Spooler's restart, restart, nothing, 190 of 204 lists on the owner's machine - its name says on which
    /// failures, a clause says when the count starts again, and the sc.exe line goes: sc.exe prints no line for
    /// an item that does nothing, so it shows Spooler's list as two restarts, which reads as "every failure".
    /// </summary>
    private static string Restarting(PlanWarning warning)
    {
        var names = Join([.. warning.Related.Select(name => Later(name, warning.Restarts))]);

        return Counted(warning.Restarts) is { } counted
            ? Texts.Of(
                Count("cli.plan.warning.recoveryRestarts.some", warning),
                warning.ServiceName,
                warning.Related.Count,
                names,
                Texts.Of("cli.plan.recovery.unsaid", counted))
            : Texts.Of(
                Count("cli.plan.warning.recoveryRestarts", warning),
                warning.ServiceName,
                warning.Related.Count,
                names,
                warning.Related.FirstOrDefault() ?? warning.ServiceName);
    }

    /// <summary>
    /// The three refusals. <b>An unreadable consequence with no names is the process itself</b> - whether it
    /// is critical could not be read - so it gets a sentence of its own rather than an empty list.
    /// </summary>
    private static string Aftermath(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.ProcessIsCritical => Texts.Of("cli.plan.problem.processIsCritical", problem.ServiceName),

        PlanProblemKind.RecoveryRestartsComputer => Texts.Of(
            problem.Related.Count == 1
                ? "cli.plan.problem.recoveryRestartsComputer.one"
                : "cli.plan.problem.recoveryRestartsComputer.many",
            problem.ServiceName, Join(problem.Related)),

        PlanProblemKind.AftermathUnreadable => Texts.Of(
            problem.Related.Count == 0
                ? "cli.plan.problem.aftermathUnreadable.process"
                : problem.Related.Count == 1
                    ? "cli.plan.problem.aftermathUnreadable.one"
                    : "cli.plan.problem.aftermathUnreadable.many",
            problem.ServiceName, Join(problem.Related)),

        _ => throw new ArgumentOutOfRangeException(
            nameof(problem), problem.Kind, EquivalentCommand.Unhandled)
    };

    /// <summary>
    /// The line after a run that ended a process, naming who Windows starts again and when - nothing when
    /// nobody comes back. Since 2026-09-30, backlog 501: <see cref="PlanRun.ComingBack"/> decides who.
    ///
    /// <b>Its own paragraph under the steps</b>, beside the line about a manager that outran the limit,
    /// because both are about what the steps above did not show. The warnings repeated further down say
    /// what the plan expected, and this says what is still to come once the run is over.
    /// </summary>
    private static void AddComingBack(StringBuilder text, PlanRun? run)
    {
        if (run?.ComingBack is not { Count: > 0 } back)
        {
            return;
        }

        var target = run.Plan.Action.ServiceName;
        var names = Join([.. back.Select(one => Later(one.ServiceName, back))]);

        // The same two shapes as the warning, and for the same reason (backlog 541): an entry restarted on
        // some failures only is not "started again" - it is started again if this was one of them.
        var counted = Counted(back) is { } clauses ? Texts.Of("cli.run.recovery.unsaid", clauses) : null;

        text.AppendLine();
        text.AppendLine((back.Count, counted) switch
        {
            (1, null) => Texts.Of("cli.run.comesBack.one", target, names),
            (_, null) => Texts.Of("cli.run.comesBack.many", target, back.Count, names),
            (1, { } said) => Texts.Of("cli.run.comesBack.some.one", target, names, said),
            (_, { } said) => Texts.Of("cli.run.comesBack.some.many", target, back.Count, names, said)
        });
    }

    /// <summary>
    /// An entry's name with the delays its recovery list restarts it after, and since 2026-10-07 the failures
    /// it does so on when not every one - or the bare name when there are none to say, so a warning built
    /// without them reads as it did before they were read.
    /// </summary>
    private static string Later(string serviceName, IReadOnlyList<RecoveryRestart> restarts) =>
        restarts.FirstOrDefault(one => string.Equals(one.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
            is { After.Count: > 0 } restart
            ? When(serviceName, restart)
            : serviceName;

    private static string When(string serviceName, RecoveryRestart restart) => restart.Only switch
    {
        null => Texts.Of("cli.plan.recovery.later", serviceName, Delays(restart.After)),
        { AndLater: true } only => Texts.Of("cli.plan.recovery.laterOrLater", serviceName, Delays(restart.After), Failures(only)),
        { } only => Texts.Of("cli.plan.recovery.laterOnly", serviceName, Delays(restart.After), Failures(only))
    };

    /// <summary>
    /// "1", "1 or 2", "1, 2 or 3" - and for a list whose last item repeats, "2" or "1, 3", which the sentence
    /// ends with "or later".
    /// </summary>
    private static string Failures(SomeFailures only)
    {
        string[] numbers = [.. only.Numbers.Select(number => number.ToString(CultureInfo.InvariantCulture))];

        return only.AndLater || numbers.Length == 1
            ? string.Join(", ", numbers)
            : Texts.Of("cli.plan.recovery.either", string.Join(", ", numbers[..^1]), numbers[^1]);
    }

    /// <summary>
    /// When the count starts again, one clause per reset period among the entries restarted on some failures
    /// only - or nothing when every entry is restarted on every failure, and the sentence is the old one.
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
            (true, true) => Texts.Of("cli.plan.recovery.countAtBoot.one", names[0]),
            (false, true) => Texts.Of("cli.plan.recovery.countAtBoot.many", Both(names)),
            (true, false) => Texts.Of("cli.plan.recovery.count.one", names[0], Period(resetPeriod)),
            (false, false) => Texts.Of("cli.plan.recovery.count.many", Both(names), Period(resetPeriod))
        };

    private static string Both(IReadOnlyList<string> names) =>
        Texts.Of("cli.plan.recovery.and", string.Join(", ", names.Take(names.Count - 1)), names[^1]);

    /// <summary>
    /// A reset period in the largest unit that divides it evenly - 30 s, 15 min, 1 h, 24 h, 390 min - the
    /// owner's decision of 2026-10-07. Periods on the owner's machine run from 30 s to 86 400 000 s, so
    /// seconds alone would read "86400 s" for the commonest one.
    /// </summary>
    private static string Period(TimeSpan period) => (long)period.TotalSeconds switch
    {
        var seconds when seconds % 3600 == 0 => Texts.Of("cli.plan.recovery.hours", seconds / 3600),
        var seconds when seconds % 60 == 0 => Texts.Of("cli.plan.recovery.minutes", seconds / 60),
        var seconds => Texts.Of("cli.run.took.seconds", seconds)
    };

    /// <summary>
    /// "60 s", or "1 s, 2 s, 4 s, 8 s or 16 s" - every delay, because which item runs depends on a count of
    /// failures nothing hands out (<see cref="RecoveryRestart"/>).
    /// </summary>
    private static string Delays(IReadOnlyList<TimeSpan> after) => after.Count == 1
        ? Seconds(after[0])
        : Texts.Of(
            "cli.plan.recovery.either",
            string.Join(", ", after.Take(after.Count - 1).Select(Seconds)),
            Seconds(after[^1]));

    /// <summary>
    /// Always in seconds, with one decimal place - the owner's decision said "after N s", and 100 ms reads
    /// as "0.1 s" rather than switching units inside one list.
    /// </summary>
    private static string Seconds(TimeSpan delay) =>
        Texts.Of("cli.run.took.seconds", delay.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture));
}
