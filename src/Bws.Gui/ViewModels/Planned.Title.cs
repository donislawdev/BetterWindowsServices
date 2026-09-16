using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the plan panel is CALLED - the title, the name under it, and the two questions both of
/// them answer: which action, and how many entries.
///
/// <b>Its own file since 2026-08-25, and the size ratchet is what asked.</b> Planned.cs went past
/// the longest shipped file when the subtitle arrived, and this was the block in it about a single
/// subject - everything else there is the three states a person sees and the moves between them.
///
/// <b>A partial rather than a class of its own</b>, which is the shape this window uses in four
/// other places: what is here reads the plan, the run and the name the window handed over, and all
/// three belong to that object and to nothing else.
/// </summary>
public sealed partial class Planned
{
    /// <summary>
    /// What the panel is called, naming the action and how much it touches.
    ///
    /// <b>IT CHANGES TENSE WHEN THERE IS A RESULT, SINCE 2026-08-19, AND UNTIL THEN IT WAS A
    /// SENTENCE THAT STOPPED BEING TRUE.</b> The owner's screenshot after a run shows "What
    /// stopping Spooler would do" over "Done. The entry is where you asked." and over a row already
    /// reading Stopped - a conditional question about the future, standing on top of a report of the
    /// past.
    ///
    /// <b>The switch is on the RESULT, not on the run being under way, and that is deliberate.</b>
    /// While a run is happening there is nothing to report yet, and the notice says in as many words
    /// that it is happening now - so the plan on screen is still a plan. The moment a result exists,
    /// the panel is a report and says so.
    ///
    /// <b>Dropped rather than kept was considered and rejected.</b> The title is what says WHICH
    /// plan the report belongs to, and a report sitting under the steps of a different plan is the
    /// worst bug this panel could have - which is why <c>Show</c> drops the old run and why a guard
    /// holds it. Taking the title away after a run would remove the label that makes the pair
    /// readable.
    /// </summary>
    public string Heading => _plan is not { } plan
        ? string.Empty
        : _run is null
            ? Asked(plan) == 1
                ? Texts.Of("gui.plan.heading.one", PlanWords.Doing(plan.Action.Kind), Named(plan))
                : Texts.Of("gui.plan.heading.many", PlanWords.Doing(plan.Action.Kind), Asked(plan))
            : Asked(plan) == 1
                ? Texts.Of("gui.plan.heading.done.one", PlanWords.Doing(plan.Action.Kind), Named(plan))
                : Texts.Of("gui.plan.heading.done.many", PlanWords.Doing(plan.Action.Kind), Asked(plan));

    /// <summary>
    /// The manager's own name for the entry, under a title that used the one a person recognises.
    ///
    /// <b>Both names on screen, which is what the details panel next door already does</b> - the
    /// display name reads as the title because it is the one somebody recognises, and the internal
    /// name is under it because it is the one they will type into a command. Neither stands alone,
    /// and `ADR-14` is why the second one may never be dropped: the display name is translated, so
    /// a panel showing only that names nothing a person could act on.
    ///
    /// <b>Empty when the title is already the internal name</b>, which is a real state rather than
    /// a tidy one: an entry whose display name the manager never gave, and every plan built by
    /// something that is not this window.
    /// </summary>
    public string Subtitle => _plan is not { } plan || !Showing || Asked(plan) != 1
        ? string.Empty
        : string.Equals(Named(plan), Only(plan), StringComparison.Ordinal)
            ? string.Empty
            : Only(plan);

    /// <summary>
    /// What the button says it will do - the act and what it acts on, never "carry this out".
    ///
    /// <b>SINCE 2026-09-15 THE BUTTON NAMES THE ACT ON EVERY PLAN, and until then it did on one.</b>
    /// The owner's decision of 2026-09-06 put "End process 4812" on a forcing plan with an argument
    /// that was already general: "Carry this out" is a label that describes a plan rather than an
    /// act, and a person should press a button naming the thing they just read. The review of
    /// 2026-09-15 (`docs/11` 2.14, point 4) applied that sentence to the other five kinds. Point
    /// 6 of the same review is why a start type plan says "Set Windows Search to Disabled" rather
    /// than "Set the startup type of Windows Search": the VALUE is the part somebody has to check
    /// before pressing, and it is what the step line above already leads with.
    ///
    /// <b>The name is the one the title uses</b> - <see cref="Named"/>, the display name when the
    /// window handed one over - so the button and the heading two blocks up agree about what they
    /// are talking about. A display name is unbounded and the button is not: the markup caps it
    /// at WidthCarryOutMost and trims with an ellipsis, and the whole name stays in the heading.
    ///
    /// <b>Exactly one step that ends a process wins over the kind, or the kind speaks.</b> A plan
    /// with two such steps cannot be reached in this slice - the window opens a forcing sheet from
    /// one failure and the command line takes one name - and naming one of two processes would be
    /// the preview and the button disagreeing about what is about to happen. A forcing plan whose
    /// process could not be read has no number to name, so it says "Force stop" and the entry, the
    /// same words as the offer that opened it.
    ///
    /// <b>Every key inside its own Texts.Of, one arm per kind and per count, and an unnamed kind
    /// refuses</b> - the shape PlanWords.Doing has, for the reasons written there: a key chosen into
    /// a variable is invisible to TextKeyGuards, and a discard that picks a verb labels the one
    /// button that changes a machine with an act nobody asked for.
    /// </summary>
    public string CarryOutLabel => _plan is not { } plan
        ? string.Empty
        : EndsOneProcess(plan) is { } ending
            ? Texts.Of("gui.plan.carryOut.endProcess", ending)
            : Asked(plan) == 1
                ? OneOn(plan)
                : SeveralOn(plan);

    /// <summary>The verb and the one entry's name, one arm per kind so every key is visible where it is chosen.</summary>
    private string OneOn(BulkPlan plan) => plan.Action.Kind switch
    {
        ActionKind.Stop => Texts.Of("gui.plan.carryOut.stop.one", Named(plan)),
        ActionKind.Start => Texts.Of("gui.plan.carryOut.start.one", Named(plan)),
        ActionKind.Restart => Texts.Of("gui.plan.carryOut.restart.one", Named(plan)),
        ActionKind.SetStartType => Texts.Of("gui.plan.carryOut.setStartType.one", Named(plan), SetTo(plan)),
        ActionKind.ForceStop => Texts.Of("gui.plan.carryOut.forceStop.one", Named(plan)),
        ActionKind.ForceRestart => Texts.Of("gui.plan.carryOut.forceRestart.one", Named(plan)),
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan.Action.Kind, EquivalentCommand.Unhandled)
    };

    /// <summary>The verb and a count, for a selection of more than one - a button is narrower than a heading.</summary>
    private static string SeveralOn(BulkPlan plan) => plan.Action.Kind switch
    {
        ActionKind.Stop => Texts.Of("gui.plan.carryOut.stop.many", Asked(plan)),
        ActionKind.Start => Texts.Of("gui.plan.carryOut.start.many", Asked(plan)),
        ActionKind.Restart => Texts.Of("gui.plan.carryOut.restart.many", Asked(plan)),
        ActionKind.SetStartType => Texts.Of("gui.plan.carryOut.setStartType.many", Asked(plan), SetTo(plan)),
        ActionKind.ForceStop => Texts.Of("gui.plan.carryOut.forceStop.many", Asked(plan)),
        ActionKind.ForceRestart => Texts.Of("gui.plan.carryOut.forceRestart.many", Asked(plan)),
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan.Action.Kind, EquivalentCommand.Unhandled)
    };

    /// <summary>
    /// The one process this plan would end, when there is exactly one - the number already
    /// standing in the steps above, so the button names what the preview names.
    /// </summary>
    private static int? EndsOneProcess(BulkPlan plan) => plan.Plans
        .SelectMany(one => one.Steps)
        .Where(step => step.Operation == StepOperation.Terminate && step.ProcessId is not null)
        .Select(step => step.ProcessId!.Value)
        .Distinct()
        .ToList() is [var ending]
        ? ending
        : null;

    /// <summary>
    /// The start type a plan sets, as the cell for it would read it - the same word the step line
    /// uses, so the button and the step cannot disagree about what is being written.
    ///
    /// <b>Refuses a start type plan that names no type rather than printing a blank</b>, because
    /// the core builds every such plan with one and a button reading "Set Spooler to " would be a
    /// silent hole exactly where the important word goes.
    /// </summary>
    private static string SetTo(BulkPlan plan) => plan.Action.To is { } to
        ? CellFaces.TypeLabel(to)
        : throw new InvalidOperationException("A start type plan carries the type it sets, and this one does not.");

    private static int Asked(BulkPlan plan) =>
        plan.Action.ServiceNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private static string Only(BulkPlan plan) => plan.Action.ServiceNames.Count == 0
        ? string.Empty
        : plan.Action.ServiceNames[0];

    /// <summary>
    /// What the title calls the one entry: the name a person recognises when the window handed one
    /// over, and the manager's own name when it did not.
    ///
    /// <b>The display name is handed in rather than looked up, and that is `ADR-3` rather than
    /// convenience.</b> A plan is built in the core from internal names, because that is what
    /// identity is - so the label belongs to whoever is looking at rows, which is the window.
    /// </summary>
    private string Named(BulkPlan plan) =>
        string.IsNullOrWhiteSpace(_shownAs) ? Only(plan) : _shownAs;
}
