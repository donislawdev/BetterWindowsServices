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
        // WHEN, BESIDE EVERY NAME, since 2026-09-30 (backlog 501) - and the bare name for the singular's
        // command at the end, which a person copies.
        PlanWarningKind.RecoveryRestarts => Texts.Of(
            Count("cli.plan.warning.recoveryRestarts", warning),
            warning.ServiceName,
            warning.Related.Count,
            Join([.. warning.Related.Select(name => Later(name, warning.Restarts))]),
            warning.Related.FirstOrDefault() ?? warning.ServiceName),

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

        text.AppendLine();
        text.AppendLine(back.Count == 1
            ? Texts.Of("cli.run.comesBack.one", run.Plan.Action.ServiceName, Later(back[0].ServiceName, back))
            : Texts.Of(
                "cli.run.comesBack.many",
                run.Plan.Action.ServiceName,
                back.Count,
                Join([.. back.Select(one => Later(one.ServiceName, back))])));
    }

    /// <summary>
    /// An entry's name with the delays its recovery list restarts it after, or the bare name when there are
    /// none to say - a warning built without them reads as it did before they were read.
    /// </summary>
    private static string Later(string serviceName, IReadOnlyList<RecoveryRestart> restarts) =>
        restarts.FirstOrDefault(one => string.Equals(one.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
            is { After.Count: > 0 } restart
            ? Texts.Of("cli.plan.recovery.later", serviceName, Delays(restart.After))
            : serviceName;

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
