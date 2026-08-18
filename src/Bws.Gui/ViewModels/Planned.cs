using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>One line of a plan, as the panel shows it.</summary>
/// <remarks>
/// Public because WPF cannot see an internal type from outside this assembly, which costs a blank
/// panel with nothing in the build to say so - the same reason <see cref="DetailLine"/> is public.
/// </remarks>
public sealed class PlanLine
{
    internal PlanLine(string text, bool asked)
    {
        Text = text;
        Asked = asked;
    }

    public string Text { get; }

    /// <summary>
    /// Whether somebody asked for this entry, as opposed to it coming along.
    ///
    /// <b>A property rather than a difference in the sentence, because the panel marks it and a
    /// reader scans for it.</b> `C2` says the plan has to warn that stopping three drags in seven,
    /// and the seven have to be findable at a glance rather than by reading every line.
    /// </summary>
    public bool Asked { get; }
}

/// <summary>
/// What an operation over the selection would do, and the window's promise that it has not done it.
///
/// <b>THE WINDOW'S DRY RUN.</b> `ADR-11` says a plan can be shown, turned into a command, reversed or
/// carried out, and those are one object seen from four sides rather than four features. This is the
/// first two sides reaching a window: the steps in the order they would happen, and the command line
/// that would ask for the same thing.
///
/// <b>IT CANNOT CARRY ANYTHING OUT AND SAYS SO IN AS MANY WORDS.</b> That is why the menu items that
/// open it are worded as questions rather than as verbs - an item called "Stop" that only draws a
/// plan would be a lie to somebody's hand. When the runner arrives this class gains a way to accept,
/// and the wording changes with it rather than before it.
///
/// <b>Nothing here decides what would happen.</b> Cascade, order, warnings and refusals are all
/// worked out in the core, where they are testable without a machine to break. What this adds is
/// words, and the core deliberately carries none: a warning holds a kind and the facts, because the
/// same warning reads differently in a window and in a terminal.
/// </summary>
public sealed class Planned : Observable
{
    private bool _showing;
    private BulkPlan? _plan;

    /// <summary>Whether the panel is on screen. Closed until somebody asks.</summary>
    public bool Showing
    {
        get => _showing;
        private set => Set(ref _showing, value);
    }

    /// <summary>What the panel is called, naming the action and how much it touches.</summary>
    public string Heading => _plan is not { } plan
        ? string.Empty
        : Asked(plan) == 1
            ? Texts.Of("gui.plan.heading.one", Doing(plan.Action.Kind), Only(plan))
            : Texts.Of("gui.plan.heading.many", Doing(plan.Action.Kind), Asked(plan));

    /// <summary>Every step, in the order it would happen, numbered as a person would count them.</summary>
    public IReadOnlyList<PlanLine> Steps => _plan is not { } plan
        ? []
        : [.. plan.Steps.Select((step, index) => new PlanLine(
            Texts.Of(
                "gui.plan.step",
                index + 1,
                step.Operation == StepOperation.Stop
                    ? Texts.Of("gui.plan.operation.stop")
                    : Texts.Of("gui.plan.operation.start"),
                step.ServiceName,
                Reason(step.Reason)),
            step.Reason == StepReason.Requested))];

    /// <summary>
    /// How many entries come along that nobody picked, as `C2`'s own sentence asks for it.
    ///
    /// Empty when there are none, rather than a line saying zero - a sentence about nothing happening
    /// is a line somebody has to read to learn nothing.
    /// </summary>
    public string Extra => _plan is not { } plan || plan.Extra.Count == 0
        ? string.Empty
        : plan.Extra.Count == 1
            ? Texts.Of("gui.plan.extra.one", plan.Extra[0])
            : Texts.Of("gui.plan.extra.many", plan.Extra.Count, string.Join(", ", plan.Extra));

    /// <summary>
    /// An entry named by more than one of the plans, said out loud rather than tidied away.
    ///
    /// <b>Rule 8 applied to a preview.</b> Somebody who sees a service twice and is told nothing reads
    /// it as a fault in the tool. The repeat is real and harmless - the second attempt finds the entry
    /// already where the first put it - and hiding it would make the preview shorter than the run.
    /// </summary>
    public string Overlapping => _plan is not { } plan || plan.Overlapping.Count == 0
        ? string.Empty
        : Texts.Of("gui.plan.overlapping", string.Join(", ", plan.Overlapping));

    /// <summary>Everything worth knowing before anybody presses anything.</summary>
    public IReadOnlyList<string> Warnings => _plan is not { } plan
        ? []
        : [.. plan.Warnings.Select(Describe)];

    /// <summary>
    /// The entries that get no plan at all, and why.
    ///
    /// <b>The second list the preview carries, and the price of the owner's decision of 2026-08-18
    /// said out loud.</b> A refusal belongs to its own entry and the rest of the selection carries on,
    /// so the panel has to show what will happen AND what will not. Quietly dropping them would make
    /// a selection of twenty with three refused look exactly like a selection of twenty.
    /// </summary>
    public IReadOnlyList<string> Problems => _plan is not { } plan
        ? []
        : [.. plan.Problems.Select(Describe)];

    /// <summary>
    /// The same thing from a terminal, one line per entry. `E5`.
    ///
    /// Rendered by the core, so these are commands this tool really accepts rather than text that
    /// looks like them - a guard in the command line's own tests holds that, and it caught a switch
    /// rendered onto a verb that refuses it.
    /// </summary>
    public IReadOnlyList<string> Commands => _plan is not { } plan ? [] : EquivalentCommand.For(plan);

    /// <summary>
    /// That nothing has happened.
    ///
    /// <b>Always present while the panel is open, never conditional.</b> A panel full of steps in the
    /// present tense reads as a report of something done, and this window cannot do any of it yet.
    /// `docs/11` 9.2: never ask about a thing whose effect you have not shown - and never show an
    /// effect somebody might think has already landed.
    /// </summary>
    public string Notice => Showing ? Texts.Of("gui.plan.nothingDone") : string.Empty;

    /// <summary>
    /// Puts a plan on screen, and says whether there was anything to put there.
    ///
    /// <b>The answer is worth having rather than a courtesy.</b> A selection of nothing, or one where
    /// every entry was refused, has no plan - and a panel that opened empty would look exactly like a
    /// feature that is broken.
    /// </summary>
    internal bool Show(BulkPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.IsRunnable && plan.Problems.Count == 0)
        {
            return false;
        }

        _plan = plan;
        Showing = true;

        Raise(nameof(Heading));
        Raise(nameof(Steps));
        Raise(nameof(Extra));
        Raise(nameof(Overlapping));
        Raise(nameof(Warnings));
        Raise(nameof(Problems));
        Raise(nameof(Commands));
        Raise(nameof(Notice));

        return true;
    }

    /// <summary>
    /// Puts the panel away, and says whether there was one to put away.
    ///
    /// <b>The plan is dropped with it rather than kept.</b> A plan is worked out against the machine
    /// as it was at one moment, so one held after the panel closed would be an answer about a machine
    /// that has moved on - and the next thing to open the panel would have to remember not to trust
    /// it. Escape asks this, and so does the button.
    /// </summary>
    internal bool Hide()
    {
        if (!Showing)
        {
            return false;
        }

        _plan = null;
        Showing = false;

        Raise(nameof(Heading));
        Raise(nameof(Steps));
        Raise(nameof(Extra));
        Raise(nameof(Overlapping));
        Raise(nameof(Warnings));
        Raise(nameof(Problems));
        Raise(nameof(Commands));
        Raise(nameof(Notice));

        return true;
    }

    private static int Asked(BulkPlan plan) =>
        plan.Action.ServiceNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private static string Only(BulkPlan plan) => plan.Action.ServiceNames.Count == 0
        ? string.Empty
        : plan.Action.ServiceNames[0];

    /// <summary>
    /// The verb as a person reads it, with the key INSIDE each call.
    ///
    /// <b>Written first as one Texts.Of around a switch that chose the key, and TextKeyGuards
    /// reddened - correctly.</b> A key travelling as the value of an expression is invisible to it, so
    /// all three came back as strings that never reach a screen. This project has the lesson recorded
    /// already, from ListState.Say, and the recorded answer is this one rather than a wider pattern: a
    /// key visible where it is chosen is better for a reader too.
    /// </summary>
    private static string Doing(ActionKind kind) => kind switch
    {
        ActionKind.Stop => Texts.Of("gui.plan.doing.stop"),
        ActionKind.Start => Texts.Of("gui.plan.doing.start"),
        _ => Texts.Of("gui.plan.doing.restart")
    };

    /// <summary>Why a step is there, with the key inside each call for the reason above.</summary>
    private static string Reason(StepReason reason) => reason switch
    {
        StepReason.Requested => Texts.Of("gui.plan.reason.requested"),
        StepReason.Cascade => Texts.Of("gui.plan.reason.cascade"),
        _ => Texts.Of("gui.plan.reason.restore")
    };

    /// <summary>
    /// A warning in words. The kinds come from the core and the sentences belong here.
    ///
    /// <b>Written out rather than composed from the name of the value</b>, for the reason the command
    /// line gives about the same six: flattening a name to lower case works for as long as every one
    /// is a single word and then quietly asks for a key nobody wrote.
    /// </summary>
    private static string Describe(PlanWarning warning) => warning.Kind switch
    {
        PlanWarningKind.Cascade => Texts.Of(
            "gui.plan.warning.cascade", warning.ServiceName, warning.Related.Count, Listed(warning.Related)),

        PlanWarningKind.DependentsInTheWay => Texts.Of(
            "gui.plan.warning.inTheWay", warning.ServiceName, Listed(warning.Related)),

        PlanWarningKind.SharedProcess => Texts.Of(
            "gui.plan.warning.sharedProcess", warning.ServiceName, Listed(warning.Related)),

        PlanWarningKind.ReturnsAfterReboot => Texts.Of("gui.plan.warning.returnsAfterReboot", warning.ServiceName),

        PlanWarningKind.CascadeUnreadable => Texts.Of("gui.plan.warning.cascadeUnreadable", warning.ServiceName),

        _ => Texts.Of("gui.plan.warning.alreadyThere", warning.ServiceName)
    };

    /// <summary>A refusal in words, with the entry it belongs to named first.</summary>
    private static string Describe(PlanProblem problem) => problem.Kind switch
    {
        PlanProblemKind.UnknownService => Texts.Of("gui.plan.problem.unknownService", problem.ServiceName),

        PlanProblemKind.NotOperable => Texts.Of("gui.plan.problem.notOperable", problem.ServiceName),

        PlanProblemKind.CascadeNotOperable => Texts.Of(
            "gui.plan.problem.cascadeNotOperable", problem.ServiceName, Listed(problem.Related)),

        _ => Texts.Of("gui.plan.problem.cannotComeBack", problem.ServiceName, Listed(problem.Related))
    };

    private static string Listed(IReadOnlyList<string> names) => string.Join(", ", names);
}
