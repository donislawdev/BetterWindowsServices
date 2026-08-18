namespace Bws.Core.Planning;

/// <summary>
/// The command line that would ask for the same thing as a plan. `E5`.
///
/// <b>In the core rather than beside either interface, and the reason is not code sharing.</b> This
/// is the one piece of text both interfaces produce that is NOT prose: it has to be identical to
/// what the command line accepts, character for character, or it is a line that looks like a command
/// and is not one. PlanText in the command line already carries that argument where it turns an
/// operation into a word - the word must not come from the text layer, because a reworded sentence
/// would render a command nobody can run. Two homes for it would be two answers to a question with
/// one right answer.
///
/// <b>IT RENDERS THE ASK, NOT THE STEPS, AND THAT DISTINCTION IS THE WHOLE OF ITS HONESTY.</b> A
/// plan's steps are what will happen on the machine as it is right now. The command below is what
/// somebody would TYPE, and typing it later builds a fresh plan against the machine as it is then -
/// so a cascade can come out bigger or smaller. That is correct rather than a flaw: it is the same
/// ask, and pretending a command replays exact steps would be the promise `ADR-11` reserves for the
/// preview. <see cref="PlanRun.Reversal"/> makes the identical distinction for the way back, in as
/// many words, and for the same reason.
///
/// <b>What this is NOT allowed to render, decided 2026-08-18 and worth keeping:</b> a command built
/// around a query. `bws stop --query "..."` is designed - the switch table in `docs/02` says bulk
/// operations by query are a slice of their own - and it is still the wrong rendering of a
/// selection, because a query is re-evaluated when it runs and would pick a DIFFERENT set of entries
/// ten minutes later. A person can also select five rows no query describes. Names are what a
/// selection is, so names are what this writes.
/// </summary>
public static class EquivalentCommand
{
    /// <summary>
    /// The name the tool is invoked by.
    ///
    /// A literal here rather than the running file's own name, because the answer has to be the same
    /// in a window as in a terminal and a window is not called this. It is the published name and
    /// `docs/02` freezes it along with the verbs.
    /// </summary>
    private const string Tool = "bws";

    /// <summary>
    /// Asking for the entries that break to be taken down as well.
    ///
    /// <b>Bridged rather than trusted:</b> a guard in the command line's own tests asserts this
    /// switch is one the command line really accepts, on all three verbs this class writes. A
    /// rendered switch that no verb takes is a line that fails on paste, and nothing in a build
    /// would say so.
    /// </summary>
    private const string Dependents = "--dependents";

    /// <summary>What somebody would type to ask for this, on one line.</summary>
    public static string For(ServiceAction action)
    {
        var command = $"{Tool} {Verb(action.Kind)} {action.ServiceName}";

        // ONLY WHERE THE TOOL TAKES IT, AND LEAVING THAT OUT WAS A REAL FAULT CAUGHT BY WRITING THE
        // BRIDGE BEFORE TRUSTING THE CODE. The command line accepts this switch on a stop and a
        // restart and REFUSES it on a start, because starting is not the mirror of stopping: the
        // manager brings up whatever an entry needs by itself and nothing that merely depends on it
        // has to move. A selection carries one such flag for every entry in it, so a person who
        // ticks it and asks for a start would otherwise be handed a line that fails on paste.
        //
        // Its own comment in OptionSurface says the same omission was made one layer down and missed
        // the first time round. Rule 7: the second appearance of a problem is a signal, and here the
        // signal is that this switch is the only one whose verbs are not all three.
        return action.IncludeDependents && action.Kind != ActionKind.Start
            ? $"{command} {Dependents}"
            : command;
    }

    /// <summary>
    /// One line per entry that has a plan, in the order the plans will be carried out.
    ///
    /// <b>Taken from the plans rather than from what was asked, which decides two things at once.</b>
    /// The order matches the order this tool would use, so a person pasting the lines into a runbook
    /// gets the sequence that works rather than the sequence they happened to click in. And an entry
    /// the plan refused gets no line - there is no command that would make the command line do it
    /// either, so writing one would hand somebody a line that fails.
    /// </summary>
    public static IReadOnlyList<string> For(BulkPlan plan) =>
        [.. plan.Plans.Select(one => For(one.Action))];

    /// <summary>
    /// What somebody would type to put one entry back where a run found it.
    ///
    /// <b>A way back is not a plan and this is the place that keeps it that way.</b>
    /// <see cref="PlanRun.Reversal"/> says so in as many words - it is a sentence about what could
    /// be typed, worked out as a net effect per entry rather than as a reversal of steps, and giving
    /// it the shape of a plan would invite somebody to run it. Rendering it here, through the same
    /// door as the ask, is what makes it text rather than machinery.
    /// </summary>
    public static string For(ReversalStep step) => $"{Tool} {Verb(step.Operation)} {step.ServiceName}";

    /// <summary>
    /// The verb, spelled out rather than derived from the name of the value.
    ///
    /// <b>Deliberate, and the trap it avoids has already been paid for once in this product.</b>
    /// Lower-casing the name of an enum value works for exactly as long as every value is one word,
    /// and then stops quietly - a two word value renders a verb nobody wrote. These three are a
    /// frozen contract in `docs/02`, so they are worth three lines of their own, and a fourth kind
    /// arriving here will not compile until somebody decides what to call it.
    /// </summary>
    private static string Verb(ActionKind kind) => kind switch
    {
        ActionKind.Stop => "stop",
        ActionKind.Start => "start",
        ActionKind.Restart => "restart",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No command verb for this action.")
    };

    /// <summary>
    /// The same three words seen from one step rather than from an ask. There is no verb for a
    /// restart here, because a step never restarts anything - it stops or it starts.
    /// </summary>
    private static string Verb(StepOperation operation) =>
        operation == StepOperation.Stop ? "stop" : "start";
}
