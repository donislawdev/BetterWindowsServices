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
