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

    /// <summary>
    /// Asking to skip the polite stop and end the process straight away.
    ///
    /// <b>The word is the one every Windows administrator already has for this</b> - taskkill
    /// documents /f as "processes be forcefully ended", and the kill tool that shipped with the
    /// debugger used the same letter for the same thing. It is deliberately NOT on the ordinary
    /// stop, where Windows has already given it another meaning: Stop-Service -Force means "even if
    /// something depends on it", which is what --dependents does here.
    /// </summary>
    private const string Force = "--force";

    /// <summary>Bringing back what the forcing verb took down, in one line rather than two.</summary>
    private const string Restart = "--restart";

    /// <summary>What somebody would type to ask for this, on one line.</summary>
    public static string For(ServiceAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var command = $"{Tool} {Verb(action.Kind)} {action.ServiceName}";

        if (action.Kind == ActionKind.SetStartType)
        {
            // THE VALUE IS PART OF THE ASK RATHER THAN A SWITCH, and this is the first verb here
            // that has one. A start type change is only a whole sentence with the type in it -
            // "bws start-type Spooler" is not a shorter way of saying this, it is a line the tool
            // refuses. The word comes from the same table the command line reads it back with, so
            // the two cannot drift apart.
            var word = StartTypeWords.Of(action.To);

            return word is null
                // Unreachable through For(BulkPlan), which filters on Renders. A caller arriving
                // here directly is one that skipped asking, and a start type with no word is
                // exactly the ask this tool declines to carry out - so a line naming it would be
                // worse than no line.
                ? throw new ArgumentOutOfRangeException(nameof(action), action.To, NoWordForThatType)
                : $"{command} {word}";
        }

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
        //
        // NAMED VERBS RATHER THAN "ANYTHING BUT A START" SINCE 2026-08-25, AND THE FOURTH KIND IS
        // WHY. The condition here read "not a start" and would have rendered --dependents onto a
        // start type change - a switch that verb does not take, on an ask where the cascade means
        // nothing, carried in from a selection whose tick box belongs to a different question. That
        // is the same shape as the nine two-way branches this file's Unhandled constant describes:
        // a condition phrased as everything-except answers for kinds nobody has written yet.
        var switches = new List<string>();

        if (action.IncludeDependents && action.Kind is ActionKind.Stop or ActionKind.Restart)
        {
            switches.Add(Dependents);
        }

        // TWO SWITCHES THAT ONLY THE FORCING VERB TAKES, AND THE ORDER THEY ARE ADDED IN IS THE
        // ORDER THEY ARE READ IN. --restart says what the line ends with, --force says what it skips
        // on the way, and a person scanning a runbook reads the destination before the shortcut.
        if (action.Kind == ActionKind.ForceRestart)
        {
            switches.Add(Restart);
        }

        // ONLY WHERE IT MEANS ANYTHING. Every other verb here asks the manager to move something and
        // has no politeness to skip, so rendering it elsewhere would hand somebody a line their own
        // tool declines - which is the fault the bridge guard beside Dependents was written for.
        if (action.Immediate && action.Kind is ActionKind.ForceStop or ActionKind.ForceRestart)
        {
            switches.Add(Force);
        }

        return switches.Count == 0 ? command : $"{command} {string.Join(' ', switches)}";
    }

    /// <summary>
    /// Whether there is a line to hand somebody for this ask at all.
    ///
    /// <b>A question about the whole ask, where <see cref="HasAVerb"/> is a question about its
    /// kind</b> - and the two came apart the day a verb arrived carrying a value. The command line
    /// has a start type verb, so <see cref="HasAVerb"/> says yes for every one of them, and yet
    /// three of the six start types have no word: Boot and System belong to entries this tool will
    /// not operate on, and Unknown is what a reading says when the manager did not answer.
    ///
    /// Nothing in this product can build such an ask today - the window offers three types and
    /// WindowsScmControl refuses the other three at the moment of writing. It is asked anyway,
    /// because the alternative is a line that reads as a command and is declined on paste, and the
    /// cost of asking is one call.
    /// </summary>
    public static bool Renders(ServiceAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return HasAVerb(action.Kind)
            && (action.Kind != ActionKind.SetStartType || StartTypeWords.Of(action.To) is not null);
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
        [.. plan.Plans.Where(one => Renders(one.Action)).Select(one => For(one.Action))];

    /// <summary>
    /// Whether the command line has a verb for this ask yet.
    ///
    /// <b>ALL FOUR SINCE 2026-08-25, AND FOR ONE DAY IT WAS THREE.</b> The window learned to set a
    /// start type before the command line had a word for it - the owner chose that order, the same
    /// way the plan preview arrived in the window first - and what stood here recorded the cost: the
    /// panel showed no equivalent command beside a start type change, and its section disappeared
    /// rather than showing a line that fails on paste. The command line learned the verb the same
    /// week, so the section is back.
    ///
    /// <b>Kept as a question of its own rather than folded away</b>, because the day a fifth ask
    /// arrives it will be three of five again, and the answer has to be somewhere a caller can ask
    /// for it. <see cref="Renders"/> is the wider question - a verb can exist and an ask still have
    /// no line, which is what a start type nobody can name does.
    /// </summary>
    public static bool HasAVerb(ActionKind kind) =>
        kind is ActionKind.Stop or ActionKind.Start or ActionKind.Restart or ActionKind.SetStartType
            or ActionKind.ForceStop or ActionKind.ForceRestart;

    /// <summary>
    /// What somebody would type to put one entry back where a run found it.
    ///
    /// <b>A way back is not a plan and this is the place that keeps it that way.</b>
    /// <see cref="PlanRun.Reversal"/> says so in as many words - it is a sentence about what could
    /// be typed, worked out as a net effect per entry rather than as a reversal of steps, and giving
    /// it the shape of a plan would invite somebody to run it. Rendering it here, through the same
    /// door as the ask, is what makes it text rather than machinery.
    ///
    /// <b>A start type carries its value here too, and it is the type the entry had BEFORE.</b>
    /// <see cref="NetEffect"/> is what works that out, and it is the only thing in this product
    /// that can: a step knows what it set, a result knows what came of it, and neither knows what
    /// was replaced. A line without the word would be the verb refusing on paste, which is the one
    /// failure this whole class is arranged against.
    /// </summary>
    public static string For(ReversalStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var command = $"{Tool} {Verb(step.Operation)} {step.ServiceName}";
        var word = StartTypeWords.Of(step.To);

        // Asked as "is there a word" rather than "is this that operation", so that the two answers
        // cannot disagree. NetEffect refuses to build a start type step without a type, so the two
        // conditions mean the same thing today - and if they ever stop meaning the same thing, this
        // one is the one that keeps an unnameable type out of a line that names it.
        return word is null ? command : $"{command} {word}";
    }

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
    ///
    /// <b>THE FOURTH WORD IS HYPHENATED AND IT IS THE FIRST ONE HERE THAT IS.</b> Owner's decision,
    /// 2026-08-25, with "bws config NAME --start-type manual" as the alternative on the table. It
    /// follows the field name the glossary binds - startType - the way --follow-network follows
    /// its own, and the price is that it sits one character away from the verb "start": somebody
    /// who types "bws start type Spooler manual" asks to start a service called type. That ends
    /// with code 2 and a sentence rather than anything happening, and the sentence is the whole of
    /// what protects it.
    /// </summary>
    private static string Verb(ActionKind kind) => kind switch
    {
        ActionKind.Stop => "stop",
        ActionKind.Start => "start",
        ActionKind.Restart => "restart",
        ActionKind.SetStartType => "start-type",

        // ONE VERB FOR BOTH FORCING ASKS AND THE SECOND ONE CARRIES A SWITCH. The command
        // line has kill and no force-restart, because a verb per combination is how a tool
        // ends up with eight of them - and because what the two share is the dangerous half.
        ActionKind.ForceStop => "kill",
        ActionKind.ForceRestart => "kill",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No command verb for this action.")
    };

    /// <summary>
    /// The same words seen from one step rather than from an ask. There is no verb for a restart
    /// here, because a step never restarts anything - it stops, it starts, or it writes a setting.
    ///
    /// <b>Three of the four asks, and the missing one is restart rather than an oversight.</b> The
    /// asks and the steps are different lists on purpose, and this is the place where that shows.
    /// </summary>
    private static string Verb(StepOperation operation) => operation switch
    {
        StepOperation.Stop => "stop",
        StepOperation.Start => "start",
        StepOperation.SetStartType => "start-type",

        // A STEP THAT ENDS A PROCESS HAS NO LINE OF ITS OWN, and this is the same distinction
        // restart makes one method up: a person asks to force a stop, they never ask for the
        // fourth step by itself. The ask renders, the step does not.
        StepOperation.Terminate => throw new ArgumentOutOfRangeException(
            nameof(operation), operation, NoLineForATerminate),

        _ => throw new ArgumentOutOfRangeException(
            nameof(operation), operation, Unhandled)
    };

    /// <summary>
    /// What a start type with no command line word says when somebody tries to render it.
    ///
    /// A diagnostic rather than anything a person reads on purpose, like <see cref="Unhandled"/>
    /// beside it. Reaching it means an ask was built for a type this tool will not write.
    /// </summary>
    /// <summary>
    /// What a terminate step says when somebody tries to render it as a line.
    ///
    /// Reaching it means a caller asked for the command line of a STEP rather than of an ask, and
    /// this is the one step nobody can type. The line for the whole ask is what a person wants.
    /// </summary>
    private const string NoLineForATerminate =
        "There is no command line for a step that ends a process on its own. What somebody types is "
        + "the ask - bws kill NAME - and the steps are what this tool makes of it.";

    private const string NoWordForThatType =
        "There is no command line word for this start type, so there is no line to hand anybody. "
        + "Boot and System belong to entries this tool does not operate on, and Unknown is what a "
        + "reading says when the manager did not answer.";

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
