using System.Text;
using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// A plan, written out for a person.
///
/// The order on screen is the order it happens, numbered, with the reason next to every
/// line. A preview whose reading order differs from its running order is worse than none,
/// because somebody will check the first two lines and assume the rest.
///
/// Every step says why it is there. "Stop these four services" invites a yes. "Stop these
/// three because they break otherwise, then the one you asked about" invites a decision.
/// </summary>
internal static class PlanText
{
    internal static string Render(OperationPlan plan)
    {
        var text = new StringBuilder();

        // Two keys rather than "step(s)". Text is a feature in this product, and a heading
        // reading "1 steps" is the sort of thing that quietly costs trust in the rest.
        text.AppendLine(plan.Steps.Count == 1
            ? Texts.Of("cli.plan.heading.one", Verb(plan.Action.Kind), plan.Action.ServiceName)
            : Texts.Of("cli.plan.heading.many", Verb(plan.Action.Kind), plan.Action.ServiceName, plan.Steps.Count));

        var width = plan.Steps.Count == 0 ? 0 : plan.Steps.Max(step => step.ServiceName.Length);

        for (var index = 0; index < plan.Steps.Count; index++)
        {
            var step = plan.Steps[index];

            text.AppendLine(Texts.Of(
                "cli.plan.step",
                index + 1,
                Operation(step.Operation),
                step.ServiceName.PadRight(width),
                Texts.Of($"cli.plan.reason.{Lower(step.Reason)}")));
        }

        if (plan.Warnings.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(Texts.Of("cli.plan.warnings"));

            foreach (var warning in plan.Warnings)
            {
                text.AppendLine(Texts.Of("cli.plan.warningLine", Describe(warning)));
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Turns a warning into words. The core reports a kind and the entries involved and
    /// never a sentence, so the wording is decided here where it can be reviewed.
    /// </summary>
    internal static string Describe(PlanWarning warning) => warning.Kind switch
    {
        // Two keys apiece rather than "entry(s)". Text is a feature here, and a warning
        // reading "1 other entries" spends trust that the warning itself needs.
        PlanWarningKind.Cascade => Texts.Of(
            Count("cli.plan.warning.cascade", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        PlanWarningKind.DependentsInTheWay => Texts.Of(
            Count("cli.plan.warning.inTheWay", warning),
            warning.ServiceName, warning.Related.Count, Join(warning.Related)),

        PlanWarningKind.SharedProcess => Texts.Of(
            "cli.plan.warning.sharedProcess", warning.ServiceName, Join(warning.Related)),

        PlanWarningKind.ReturnsAfterReboot => Texts.Of(
            "cli.plan.warning.returnsAfterReboot", warning.ServiceName),

        PlanWarningKind.CascadeUnreadable => Texts.Of(
            "cli.plan.warning.cascadeUnreadable", warning.ServiceName),

        _ => Texts.Of("cli.plan.warning.alreadyThere", warning.ServiceName)
    };

    internal static string Describe(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.UnknownService => Texts.Of("cli.plan.problem.unknownService", problem.ServiceName),

        PlanProblemKind.CascadeNotOperable => Texts.Of(
            "cli.plan.problem.cascadeNotOperable", problem.ServiceName, Join(problem.Related)),

        _ => Texts.Of("cli.plan.problem.notOperable", problem.ServiceName)
    };

    private static string Count(string key, PlanWarning warning) =>
        warning.Related.Count == 1 ? $"{key}.one" : $"{key}.many";

    private static string Verb(ActionKind kind) => Texts.Of($"cli.plan.action.{Lower(kind)}");

    private static string Operation(StepOperation operation) => Texts.Of($"cli.plan.operation.{Lower(operation)}");

    private static string Lower<T>(T value) where T : struct, Enum =>
        value.ToString().ToLowerInvariant();

    private static string Join(IReadOnlyList<string> names) => string.Join(", ", names);
}
