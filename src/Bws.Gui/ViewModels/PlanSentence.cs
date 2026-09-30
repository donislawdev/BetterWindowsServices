using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What pressing the button under a sentence of the plan would ask for. Nothing is carried out by
/// either - each builds the same plan again with one word of the question changed.
/// </summary>
internal enum PlanOffer
{
    /// <summary>No button under this sentence.</summary>
    None,

    /// <summary>
    /// Under "keeps running": the startup setting with a stop riding on it. Spec C4, UX-GUI-006.
    /// </summary>
    AlsoStop,

    /// <summary>
    /// Under running dependants the plan does not stop: the same ask with them stopped first, as
    /// <c>--dependents</c> does. The owner's decision of 2026-09-30 on W-4 of the stability report
    /// and backlog 496.
    /// </summary>
    InTheWay
}

/// <summary>
/// One sentence of the plan, and under a few of them a button that builds the plan again with one
/// word changed.
///
/// <b>An object rather than the string these lists held</b> - for the warnings since 2026-09-24 and
/// for the refusals since 2026-09-30, and an offer is why both times. A button whose meaning depends
/// on which sentence it stands under needs that sentence to be something it can stand under.
///
/// <b>ONE TYPE FOR BOTH LISTS, AND ONE TEMPLATE, SINCE 2026-09-30.</b> Until then this was
/// PlanWarningLine and offered only under "keeps running". The offer to stop dependants in the way
/// stands under a warning on a stop and under a refusal on a forced stop, which would have made "a
/// sentence with a button under it" the third such template on this sheet - GUI rule 2 says the
/// third time is a component. So the line carries what differs - its offer, the button's name for
/// instruments and its tooltip - and PlanSentenceTemplate draws every one of them. The failures
/// under a run keep their own type, PlanFailure, because what they offer opens a different sheet.
///
/// <b>ONE OFFER OF EACH KIND PER SHEET, UNDER THE LAST SENTENCE IT ANSWERS</b>, however many entries
/// that covers. The ask is one - the whole selection, asked again - so taking the offer is one press,
/// and a button under every sentence would be twenty buttons doing the same thing.
///
/// <b>No offer on a record.</b> Once the plan has run the sheet says what happened, and `docs/11`
/// 3.12 says a sheet asks one question - so the lines come back without the offer, the way the
/// failure list offers nothing on a plan still waiting to be run.
/// </summary>
public sealed class PlanSentence
{
    private PlanSentence(string text, PlanOffer offer, string label)
    {
        Text = text;
        Offer = offer;
        Label = label;
    }

    /// <summary>The sentence.</summary>
    public string Text { get; }

    /// <summary>What the button under it would ask for - <see cref="PlanOffer.None"/> for most.</summary>
    internal PlanOffer Offer { get; }

    /// <summary>What the offer says on it. Empty when there is none, which the template reads.</summary>
    public string Label { get; }

    /// <summary>Whether anything is offered under this sentence at all.</summary>
    public bool HasOffer => Label.Length > 0;

    /// <summary>
    /// The button's name in the automation tree. "planAlsoStop" is kept for the first offer because
    /// tools/gui-probe/screens.ps1 finds it by that name, and it was the only one until 2026-09-30.
    /// </summary>
    public string OfferId => Offer switch
    {
        PlanOffer.AlsoStop => "planAlsoStop",
        PlanOffer.InTheWay => "planInTheWay",
        _ => string.Empty
    };

    /// <summary>What resting on the button says - what it would build, and that nothing happens yet.</summary>
    public string OfferHint => Offer switch
    {
        PlanOffer.AlsoStop => Texts.Of("gui.plan.offer.alsoStop.hint"),
        PlanOffer.InTheWay => Texts.Of("gui.plan.offer.inTheWay.hint"),
        _ => string.Empty
    };

    /// <summary>
    /// The lines for these warnings, with each offer under the last sentence it answers - or with
    /// none when the sheet can no longer be asked anything.
    /// </summary>
    /// <param name="kind">
    /// What the plan was asked for, because the offer to stop dependants in the way is not the same
    /// question on every plan that warns about them. A startup setting carrying a stop warns too, and
    /// the command line refuses --dependents on start-type (OptionSurface) - so a plan rebuilt with
    /// them would render an equivalent command the terminal rejects.
    /// </param>
    internal static IReadOnlyList<PlanSentence> Of(IReadOnlyList<PlanWarning> warnings, ActionKind kind, bool offering)
    {
        ArgumentNullException.ThrowIfNull(warnings);

        var running = warnings.Count(warning => warning.Kind == PlanWarningKind.KeepsRunning);
        var inTheWay = kind is ActionKind.Stop or ActionKind.Restart
            ? warnings.Where(warning => warning.Kind == PlanWarningKind.DependentsInTheWay).ToList()
            : [];

        var lastRunning = offering ? LastOf(warnings, PlanWarningKind.KeepsRunning) : -1;
        var lastInTheWay = offering && inTheWay.Count > 0 ? LastOf(warnings, PlanWarningKind.DependentsInTheWay) : -1;

        return
        [
            .. warnings.Select((warning, index) =>
                index == lastRunning ? new PlanSentence(PlanWords.Describe(warning), PlanOffer.AlsoStop, AlsoStopLabel(running))
                : index == lastInTheWay ? new PlanSentence(PlanWords.Describe(warning), PlanOffer.InTheWay, InTheWayLabel(inTheWay.SelectMany(one => one.Related)))
                : new PlanSentence(PlanWords.Describe(warning), PlanOffer.None, string.Empty))
        ];
    }

    /// <summary>
    /// The lines for the refusals, gathered as they always were, with the offer under the one saying
    /// dependants are in the way of ending a process - the only way forward from such a sheet.
    /// </summary>
    internal static IReadOnlyList<PlanSentence> Of(IReadOnlyList<PlanProblem> problems, bool offering)
    {
        ArgumentNullException.ThrowIfNull(problems);

        // ONE SUCH REFUSAL AT MOST - the window refuses to force more than one entry at a time
        // (MainWindow.Preview), and only a forcing plan refuses for this. Gathering leaves it as its
        // own sentence, because it names the dependants, so its words are found in the gathered list.
        var inTheWay = offering
            ? problems.FirstOrDefault(problem => problem.Kind == PlanProblemKind.DependentsInTheWay)
            : null;
        var offered = inTheWay is null ? null : PlanWords.Describe(inTheWay);

        return
        [
            .. PlanWords.Describe(problems).Select(text => text == offered
                ? new PlanSentence(text, PlanOffer.InTheWay, InTheWayLabel(inTheWay!.Related))
                : new PlanSentence(text, PlanOffer.None, string.Empty))
        ];
    }

    private static int LastOf(IReadOnlyList<PlanWarning> warnings, PlanWarningKind kind)
    {
        for (var index = warnings.Count - 1; index >= 0; index--)
        {
            if (warnings[index].Kind == kind)
            {
                return index;
            }
        }

        return -1;
    }

    private static string AlsoStopLabel(int running) => running == 1
        ? Texts.Of("gui.plan.offer.alsoStop.one")
        : Texts.Of("gui.plan.offer.alsoStop.many", running);

    /// <summary>
    /// "Also stop the 2 in the way" - counted by NAME, because an entry depending on two of the picked
    /// ones is named under both, and stopping it is one step.
    /// </summary>
    private static string InTheWayLabel(IEnumerable<string> names)
    {
        var count = names.Distinct(StringComparer.OrdinalIgnoreCase).Count();

        return count == 1
            ? Texts.Of("gui.plan.offer.inTheWay.one")
            : Texts.Of("gui.plan.offer.inTheWay.many", count);
    }
}
