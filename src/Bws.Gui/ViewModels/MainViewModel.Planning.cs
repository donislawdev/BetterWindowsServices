using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What an operation over a selection would do - the one question this model answers that is not
/// about the list.
///
/// <b>Its own file since 2026-08-25, and the seam is a subject rather than a line count.</b>
/// MainViewModel opens by saying what it is: "what the window shows and how it got there". Reading
/// the machine, narrowing it, folding it and drawing it are all that. Working out what STOPPING
/// something would do is not - it is asked at a different moment, by a different control, and it
/// changes nothing on screen.
///
/// <b>The size ratchet is what asked, and it asked the way it is meant to.</b> Nothing crossed the
/// ceiling on the longest file - what went red is the count of files over five hundred lines, two
/// where one was allowed. That is the growth nobody notices, and the answer to it is a seam rather
/// than a shorter comment.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>
    /// The manager, kept because a plan asks it who breaks.
    ///
    /// <b>Held here as well as inside <see cref="Readings"/>, and that is one object with two
    /// readers rather than two copies of anything.</b> Reading the listing belongs to Readings.
    /// Working out what an operation would do is a different question asked at a different moment -
    /// when somebody opens a preview - and it is this class the window talks to.
    /// </summary>
    private readonly IScmCatalog _catalog;

    /// <summary>
    /// What an operation over a selection would do. Works it out and changes nothing.
    ///
    /// <b>OFF THE CALLING THREAD SINCE 2026-09-03, AND THE COMMENT THAT STOOD HERE UNTIL THEN IS
    /// KEPT BELOW BECAUSE IT WAS TRUE AND WAS STILL THE WRONG ANSWER.</b> Backlog 301. It read:
    ///
    /// <i>"On the calling thread, and that is a measurement rather than an oversight. Measured on
    /// this machine on 2026-08-18 through the command line, five runs each with the first
    /// discarded: a plan with a thirteen member cascade took 316-360 ms end to end and the same
    /// plan with no cascade at all took 325-366 ms. The spread is wider than the difference, so by
    /// this project's own rule there is no difference."</i>
    ///
    /// Every word of that is correct about the question it asked, which is <b>how DEEP the cascade
    /// is</b>. Nobody had ever measured the other axis - <b>how MANY entries are selected</b> - and
    /// a sentence answering one question sits in the place where a reader looks for the other. Rule
    /// 3 of the project's notes names exactly this shape.
    ///
    /// <b>Measured 2026-09-02 through `tools/plan-probe`, four runs with the cold one discarded,
    /// 799 entries:</b> selections of 1, 5, 20, 50, 100, 200, 400 and everything, asking to stop.
    /// A hundred cost 19-27 ms, two hundred 61-69, four hundred 95-112, and the whole listing
    /// <b>224-240 ms</b>. Start at the same size is 0-1 ms because it orders nothing, so the entire
    /// figure is round trips to the manager rather than the loops around them - which is why
    /// neither a topological sort nor a dictionary would have bought anything measurable.
    ///
    /// <b>The owner's decision of 2026-09-03: 240 ms with the window not answering is too much.</b>
    /// So the work moves, and nothing else about the plan changes - the same builder, the same
    /// listing, the same answer.
    ///
    /// <b>THE LISTING IS TAKEN HERE, ON THE THREAD THAT OWNS IT, and that is the whole of the
    /// safety argument for the line below.</b> <see cref="RowIndex.Everything"/> builds a new list
    /// on every read, so what crosses to the background thread is a snapshot that nothing else can
    /// touch. Handing the index over instead would put a reading that runs once a second on one
    /// thread and a plan on another, both looking at the same rows.
    ///
    /// <b>Every entry the window holds is handed in, never the rows on screen</b>, and the reason is
    /// also at <see cref="RowIndex.Everything"/>: a plan looks entries up by name, so building one
    /// against a filtered set would silently shorten a cascade.
    /// </summary>
    internal Task<BulkPlan> PlanAsync(BulkAction action)
    {
        var entries = _index.Everything;

        return Task.Run(() => new BulkPlanBuilder(entries, _catalog, _processes).Build(action));
    }
}
