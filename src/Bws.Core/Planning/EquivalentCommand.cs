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
        [.. plan.Plans.Where(one => HasAVerb(one.Action.Kind)).Select(one => For(one.Action))];

    /// <summary>
    /// Whether the command line has a verb for this ask yet.
    ///
    /// <b>THREE OF THE FOUR, SINCE 2026-08-25, AND THE FOURTH IS A DECISION RATHER THAN AN
    /// OVERSIGHT.</b> The window can set a start type and the command line cannot - the owner chose
    /// that order on 2026-08-25, the same way the plan preview arrived in the window first. So there
    /// is no line to hand anybody for that ask, and this class already refuses to invent one: the
    /// comment above about --dependents on a start is the same rule, met a second time.
    ///
    /// <b>What it costs, said plainly:</b> the panel shows no equivalent command beside a start type
    /// change, and its section disappears rather than showing a line that fails on paste. It comes
    /// back the day the command line learns the verb.
    /// </summary>
    public static bool HasAVerb(ActionKind kind) =>
        kind is ActionKind.Stop or ActionKind.Start or ActionKind.Restart;

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
    /// frozen contract in `docs/02`, so they are worth three lines of their own.
    ///
    /// <b>The sentence that used to end this paragraph was false and is now measured.</b> It said a
    /// fourth kind "will not compile until somebody decides what to call it" - a discard arm
    /// compiles perfectly, and CS8524 means it cannot be left out. What it does instead is refuse
    /// at the point of use, and <see cref="HasAVerb"/> is what keeps callers away from it.
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
    private static string Verb(StepOperation operation) => operation switch
    {
        StepOperation.Stop => "stop",
        StepOperation.Start => "start",
        _ => throw new ArgumentOutOfRangeException(
            nameof(operation), operation, Unhandled)
    };

    /// <summary>
    /// What every switch over a step operation says when it meets one it does not know.
    ///
    /// <b>A refusal rather than a guess, and the guess is what was here until 2026-08-25.</b> Every
    /// one of these was a ternary reading "is it a stop, otherwise it is a start" - so a third kind
    /// of step would have travelled through nine places as a START, including the one that asks the
    /// service manager to move a service. A plan whose preview showed one thing and whose run did
    /// another is the single fault the whole of `ADR-11` stands against.
    ///
    /// <b>Why not a compile error, which would be better and is not available.</b> A switch
    /// expression covering every named member of an enum still does not compile without a discard -
    /// CS8524, measured 2026-08-25 - because an enum variable can hold a number nobody named. So the
    /// discard has to be there, and what it does is the decision: it throws where the value is used
    /// rather than answering for it.
    ///
    /// <b>Public because the window throws the same sentence</b>, and a second wording of the same
    /// advice would be two places to keep true. It is a diagnostic rather than anything a person
    /// reads on purpose - reaching it means somebody added a kind of step and left a place out.
    /// </summary>
    public const string Unhandled =
        "This step operation has no answer here. A new kind of step has to be given one in every "
        + "place that asks, rather than falling through to the one that happened to be last.";
}
