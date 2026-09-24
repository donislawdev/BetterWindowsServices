using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One sentence under "Worth knowing", and under the one that says a disabled entry keeps running,
/// the offer to stop it in the same plan.
///
/// <b>An object rather than the string this list held until 2026-09-24, and the offer is why</b> -
/// the same step <see cref="PlanFailure"/> took for the failures on 2026-09-07. Spec C4 asks the
/// plan to "propose adding the stop as a separate, explicit step to accept", the owner chose to
/// have it rebuild the plan in place (UX-GUI-006, variant O1), and a button whose meaning depends
/// on which sentence it stands under needs that sentence to be something it can stand under.
///
/// <b>ONE OFFER PER SHEET, UNDER THE LAST SUCH SENTENCE, however many entries it covers.</b> The
/// ask is one - the whole selection set to disabled - so taking the offer is one press that
/// rebuilds it with the stop, and the entries already stopped get no stop step. A button under
/// every sentence would be twenty buttons all doing the same thing to twenty entries.
///
/// <b>No offer on a record.</b> Once the plan has run the sheet says what happened, and `docs/11`
/// 3.12 says a sheet asks one question - so the lines come back without the offer, the way the
/// failure list offers nothing on a plan still waiting to be run.
/// </summary>
public sealed class PlanWarningLine
{
    private PlanWarningLine(string text, string label)
    {
        Text = text;
        Label = label;
    }

    /// <summary>The sentence.</summary>
    public string Text { get; }

    /// <summary>What the offer says on it. Empty when there is none, which the template reads.</summary>
    public string Label { get; }

    /// <summary>Whether anything is offered under this sentence at all.</summary>
    public bool HasOffer => Label.Length > 0;

    /// <summary>
    /// The lines for these warnings, with the offer under the last one saying an entry keeps
    /// running - or under none of them when the sheet can no longer be asked anything.
    /// </summary>
    internal static IReadOnlyList<PlanWarningLine> Of(IReadOnlyList<PlanWarning> warnings, bool offering)
    {
        ArgumentNullException.ThrowIfNull(warnings);

        var running = warnings.Count(warning => warning.Kind == PlanWarningKind.KeepsRunning);
        var last = offering && running > 0
            ? warnings.Select((warning, index) => (warning, index))
                .Last(one => one.warning.Kind == PlanWarningKind.KeepsRunning).index
            : -1;

        return
        [
            .. warnings.Select((warning, index) => new PlanWarningLine(
                PlanWords.Describe(warning),
                index != last ? string.Empty
                : running == 1 ? Texts.Of("gui.plan.offer.alsoStop.one")
                : Texts.Of("gui.plan.offer.alsoStop.many", running)))
        ];
    }
}
