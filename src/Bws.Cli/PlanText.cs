using System.Globalization;
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
    /// <summary>A plan that has not been carried out. The dry run, and nothing else.</summary>
    internal static string Render(OperationPlan plan) => Render(plan, results: null);

    /// <summary>
    /// A plan that has been carried out, written out as the same lines with what came of
    /// them alongside.
    ///
    /// The same lines on purpose. Somebody who read the preview and then reads this is
    /// comparing two things that look alike, which is the whole point of the pattern - a
    /// separate results table would leave the comparison to whoever remembered to make it.
    /// </summary>
    internal static string Render(PlanRun run) => Render(run.Plan, run.Results, run.Cancelled, run);

    private static string Render(
        OperationPlan plan,
        IReadOnlyList<StepResult>? results,
        bool cancelled = false,
        PlanRun? run = null)
    {
        var text = new StringBuilder();

        // Two keys rather than "step(s)". Text is a feature in this product, and a heading
        // reading "1 steps" is the sort of thing that quietly costs trust in the rest.
        text.AppendLine(plan.Steps.Count == 1
            ? Texts.Of("cli.plan.heading.one", Verb(plan.Action.Kind), plan.Action.ServiceName)
            : Texts.Of("cli.plan.heading.many", Verb(plan.Action.Kind), plan.Action.ServiceName, plan.Steps.Count));

        // All three, or none. The first was written this way and the other two were not, so a
        // plan with no steps threw "Sequence contains no elements" from the second line - which
        // means the guard on the first line was describing a case the next two could not survive.
        //
        // Unreachable from the command line today, because a plan is only rendered once
        // IsRunnable has said it has steps. Reachable the moment a window shows a plan it is
        // refusing, which is what S7 brings, and that is exactly when nobody would be looking.
        var width = plan.Steps.Count == 0 ? 0 : plan.Steps.Max(step => step.ServiceName.Length);
        var operations = plan.Steps.Count == 0 ? 0 : plan.Steps.Max(step => Operation(step).Length);
        var reasons = plan.Steps.Count == 0 ? 0 : plan.Steps.Max(step => Reason(step.Reason).Length);

        for (var index = 0; index < plan.Steps.Count; index++)
        {
            var step = plan.Steps[index];

            // Padded only when something follows it. A preview line that ended in invisible
            // spaces would arrive in a runbook carrying them.
            text.AppendLine(results is null
                ? Texts.Of(
                    "cli.plan.step",
                    index + 1,
                    Operation(step).PadRight(operations),
                    step.ServiceName.PadRight(width),
                    Reason(step.Reason))
                : Texts.Of(
                    "cli.plan.stepDone",
                    index + 1,
                    Operation(step).PadRight(operations),
                    step.ServiceName.PadRight(width),
                    Reason(step.Reason).PadRight(reasons),
                    Describe(results[index])));
        }

        // Said in the document itself, not only on the error channel as it happened. The
        // message that goes out while somebody presses Ctrl+C scrolls away, and what is left
        // on screen afterwards was a report that had finished without mentioning it. That is
        // the quiet kind of incomplete answer rule 8 is about, and a run where every step
        // still arrived is exactly where it is easiest to miss.
        if (cancelled)
        {
            text.AppendLine();
            text.AppendLine(Texts.Of("cli.run.wasInterrupted"));
        }

        // Said here rather than left for somebody to notice, because the surprise is silent
        // otherwise: --timeout 1 against a service that never reports itself to the manager
        // ran for 30.4 s on Windows Server 2025, reported the truth about the service, and
        // looked exactly like a switch that does nothing. The core decides which steps this
        // covers - it is a judgement about the run and the window will need the same one.
        foreach (var outran in run?.OutranTheCeiling ?? [])
        {
            text.AppendLine();
            text.AppendLine(Texts.Of(
                "cli.run.outranTheCeiling",
                outran.Step.ServiceName,
                Took(outran.Milliseconds),
                Took((long)run!.Ceiling.TotalMilliseconds)));
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

        // The way back, last, because it is the one thing here that says what to do next rather
        // than what happened. Only after a run, and only when something is actually somewhere
        // else - PlanRun.Reversal works in net effects, so a restart that ended where it began
        // prints nothing, which is the correct amount to say about it.
        //
        // Not offered by the dry run either, and that is not an oversight: a preview has moved
        // nothing, so there is nothing to put back, and printing the commands anyway would read
        // as though there were.
        if (run is not null && run.Reversal.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(Texts.Of("cli.run.putBack.heading"));

            foreach (var step in run.Reversal)
            {
                text.AppendLine(Texts.Of("cli.run.putBack.line", EquivalentCommand.For(step)));
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// What came of one step, in words.
    ///
    /// A timeout says where the entry was left, because that is the half somebody needs to
    /// decide what to do next: an entry left stopping will probably arrive on its own, and
    /// one left running never started to move.
    /// </summary>
    internal static string Describe(StepResult result) => result.Outcome switch
    {
        StepOutcome.Succeeded => Texts.Of("cli.run.outcome.succeeded", Took(result.Milliseconds)),

        // Never without words: a refusal is only ever built from a code and the system's own
        // sentence for it, together.
        StepOutcome.Failed => Texts.Of("cli.run.outcome.failed", result.Error!, result.ErrorCode),

        StepOutcome.TimedOut => Texts.Of(
            "cli.run.outcome.timedOut", Took(result.Milliseconds), result.Status.ToString()),

        _ => Texts.Of($"cli.run.outcome.{Camel(result.SkippedBecause ?? SkipReason.AlreadyThere)}")
    };

    /// <summary>The step being attempted, for the error channel while somebody waits.</summary>
    internal static string Progress(PlanStep step, int number, int count) =>
        Texts.Of("cli.run.progress", number, count, Operation(step), step.ServiceName);

    /// <summary>
    /// How long something took. Milliseconds up to a second, seconds above it - a stop that
    /// took three quarters of a minute reads as 45 s, not as a five figure number nobody
    /// converts in their head.
    /// </summary>
    private static string Took(long milliseconds) => milliseconds < 1000
        ? Texts.Of("cli.run.took.milliseconds", milliseconds)
        : Texts.Of("cli.run.took.seconds", (milliseconds / 1000d).ToString("0.#", CultureInfo.InvariantCulture));

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

        // Three keys, because the entry that cannot come back is usually the one somebody
        // named, and a sentence that says its name twice in eight words reads as though
        // nobody looked at it.
        PlanProblemKind.CannotComeBack => Texts.Of(
            IsTheTarget(problem)
                ? "cli.plan.problem.cannotComeBack.target"
                : problem.Related.Count == 1
                    ? "cli.plan.problem.cannotComeBack.one"
                    : "cli.plan.problem.cannotComeBack.many",
            problem.ServiceName, problem.Related.Count, Join(problem.Related)),

        _ => Texts.Of("cli.plan.problem.notOperable", problem.ServiceName)
    };

    private static bool IsTheTarget(PlanProblem problem) =>
        problem.Related.Count == 1
        && string.Equals(problem.Related[0], problem.ServiceName, StringComparison.OrdinalIgnoreCase);

    private static string Count(string key, PlanWarning warning) =>
        warning.Related.Count == 1 ? $"{key}.one" : $"{key}.many";

    private static string Verb(ActionKind kind) => Texts.Of($"cli.plan.action.{Camel(kind)}");

    /// <summary>
    /// What one step does, in the column a person scans down.
    ///
    /// <b>Takes the whole step rather than its operation, and the fourth kind of step is why.</b>
    /// A stop is a stop wherever it appears - the word carries the whole of what will happen. A
    /// start type write does not: "set" alone leaves out the only part somebody is reading the line
    /// to check, and two lines setting two different types would be identical on screen.
    ///
    /// The type is spelled the way the listing spells it, not the way the command line accepts it.
    /// This column is prose for a person, and the line somebody would paste has its own place at
    /// the foot of the document.
    /// </summary>
    private static string Operation(PlanStep step) => step.Operation == StepOperation.SetStartType
        ? Texts.Of("cli.plan.operation.setStartType", step.To!.Value.ToString())
        : Texts.Of($"cli.plan.operation.{Camel(step.Operation)}");

    private static string Reason(StepReason reason) => Texts.Of($"cli.plan.reason.{Camel(reason)}");

    /// <summary>
    /// The tail of a text key, from the name of a value.
    ///
    /// Only the first letter, deliberately. Flattening the whole name to lower case works
    /// for as long as every value is one word and then quietly stops: a two word value goes
    /// looking for a key nobody wrote, and the missing text renders as the key itself. That
    /// happened here, and the same camel spelling is what the machine readable output
    /// already uses for these names.
    /// </summary>
    private static string Camel<T>(T value) where T : struct, Enum
    {
        var name = value.ToString();

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string Join(IReadOnlyList<string> names) => string.Join(", ", names);
}
