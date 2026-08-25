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
    /// <b>On the calling thread, and that is a measurement rather than an oversight.</b> Building a
    /// plan asks the manager who depends on each entry, so the obvious worry is that a selection
    /// costs a question per entry per cascade member. Measured on this machine on 2026-08-18 through
    /// the command line, five runs each with the first discarded: a plan with a thirteen member
    /// cascade took 316-360 ms end to end and the same plan with no cascade at all took 325-366 ms.
    /// The spread is wider than the difference, so by this project's own rule there is no
    /// difference - the whole figure is process start and the reading of 810 entries.
    ///
    /// So no background work, no cancellation and no generation counter. That is the lesson of
    /// backlog 21 applied a second time: the machinery there turned out to answer a race that did
    /// not exist, and the limit was in a mechanism nobody had needed.
    ///
    /// <b>Every entry the window holds is handed in, never the rows on screen</b>, and the reason is
    /// at <see cref="RowIndex.Everything"/>: a plan looks entries up by name, so building one
    /// against a filtered set would silently shorten a cascade.
    /// </summary>
    internal BulkPlan Plan(BulkAction action) =>
        new BulkPlanBuilder(_index.Everything, _catalog).Build(action);
}
