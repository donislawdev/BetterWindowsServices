using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>How a finished run came out, which decides how the result at the top of the sheet looks.</summary>
public enum PlanOutcome
{
    /// <summary>No result on the sheet - nothing was done yet, or a run is under way.</summary>
    None,

    /// <summary>Every entry somebody asked about ended where they asked.</summary>
    Arrived,

    /// <summary>Something did not: an entry left elsewhere, a run interrupted, or nothing carried out.</summary>
    FellShort,
}

/// <summary>
/// What a finished run came to, in words and as an outcome - one answer, so the sentence at the top
/// of the plan sheet and the colour beside it cannot disagree.
///
/// <b>A type of its own rather than more of <see cref="Planned"/>, since 2026-11-16</b>, when the
/// result got a block of its own and that type stood over its ceiling of methods. Everything here
/// reads a run and nothing else, so it needs nothing from the sheet but the name a person calls the
/// one entry.
/// </summary>
internal static class FinishedRun
{
    /// <summary>
    /// The sentence and the outcome.
    ///
    /// <b>THE SINGULAR ARRIVED 2026-08-19, AND THE PLURAL-ONLY VERSION SHIPPED FOR A DAY SAYING
    /// "All 1 entries are where you asked" TO ANYBODY WHO PICKED ONE ROW.</b> That is the commonest
    /// selection there is, and it is the last sentence somebody reads after changing a machine, which
    /// is the worst place in this product to look careless.
    ///
    /// <b>The count that decides is the number of entries, never the number that arrived.</b> A run
    /// of one that did not arrive reports zero arrived out of one, so keying on the first number
    /// would put "0 of 1 entries" back on the screen by a different route.
    ///
    /// <b>A run of nothing says so, since 2026-09-29</b> - G-2 of the external stability report. The
    /// one refusal the window records as a run carries no runs at all.
    ///
    /// <b>A run that was stopped says so, since 2026-09-30</b>, read off the steps by
    /// <see cref="Underway.Afterwards"/> - "What did not is below" pointed at a list that never holds
    /// a step nobody tried.
    /// </summary>
    /// <param name="run">The run, with the plan it was made from.</param>
    /// <param name="named">What a person calls the one entry, for a run of one.</param>
    internal static (string Sentence, PlanOutcome Outcome) Read(BulkRun run, string named)
    {
        if (run.Runs.Count == 0)
        {
            return (Texts.Of("gui.plan.notice.nothingRun"), PlanOutcome.FellShort);
        }

        var arrived = Arrived(run);

        if (Underway.Afterwards(run, arrived) is { Length: > 0 } stopped)
        {
            return (stopped, PlanOutcome.FellShort);
        }

        if (arrived == run.Runs.Count)
        {
            return (
                run.Runs.Count == 1 ? OneArrived(run.Plan, named) : AllArrived(run.Plan, run.Runs.Count),
                PlanOutcome.Arrived);
        }

        return (
            run.Runs.Count == 1
                ? Texts.Of("gui.plan.notice.partly.one")
                : Texts.Of("gui.plan.notice.partly.many", arrived, run.Runs.Count),
            PlanOutcome.FellShort);
    }

    /// <summary>
    /// How many of the entries somebody asked about ended where they asked.
    ///
    /// <b>Counted over runs rather than over steps, because that is the unit a person picked.</b>
    /// Somebody selected five rows, so "three of five" is the sentence they can act on - "twenty
    /// nine of thirty two steps" is arithmetic about our internals, and most of those steps are
    /// cascade members nobody chose.
    /// </summary>
    internal static int Arrived(BulkRun run) => run.Runs.Count(one => one.Completed);

    /// <summary>
    /// Where the one entry is now, by name - "Done. Print Spooler is running." rather than "the entry
    /// is where you asked", which made somebody work out where they had asked.
    ///
    /// <b>The state the plan was built to reach, and only once every step got there</b>, which is
    /// what <see cref="Read"/> checks before it asks for this. A restart says "is running" rather than
    /// "was restarted": a restart of a stopped service starts it, and the sentence has to be true of
    /// both. One arm per kind, so every key is visible where it is chosen - TextKeyGuards reads them.
    /// </summary>
    private static string OneArrived(BulkPlan plan, string named) => plan.Action.Kind switch
    {
        ActionKind.Stop or ActionKind.ForceStop => Texts.Of("gui.plan.notice.done.stopped.one", named),
        ActionKind.Start or ActionKind.Restart or ActionKind.ForceRestart => Texts.Of("gui.plan.notice.done.running.one", named),
        ActionKind.SetStartType => plan.Action.AlsoStop
            ? Texts.Of("gui.plan.notice.done.setAndStopped.one", named, PlanWords.SetTo(plan))
            : Texts.Of("gui.plan.notice.done.set.one", named, PlanWords.SetTo(plan)),
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan.Action.Kind, EquivalentCommand.Unhandled)
    };

    /// <summary>The same for a selection, with a count where the name would be.</summary>
    private static string AllArrived(BulkPlan plan, int entries) => plan.Action.Kind switch
    {
        ActionKind.Stop or ActionKind.ForceStop => Texts.Of("gui.plan.notice.done.stopped.many", entries),
        ActionKind.Start or ActionKind.Restart or ActionKind.ForceRestart => Texts.Of("gui.plan.notice.done.running.many", entries),
        ActionKind.SetStartType => plan.Action.AlsoStop
            ? Texts.Of("gui.plan.notice.done.setAndStopped.many", entries, PlanWords.SetTo(plan))
            : Texts.Of("gui.plan.notice.done.set.many", entries, PlanWords.SetTo(plan)),
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan.Action.Kind, EquivalentCommand.Unhandled)
    };
}
