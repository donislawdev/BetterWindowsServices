using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One failed step as the panel shows it, and the stronger ask standing under it when there is one.
///
/// <b>Public because WPF cannot see an internal type from outside this assembly</b>, which costs a
/// blank section with nothing in the build to say so - the same reason <see cref="PlanLine"/> is
/// public.
///
/// <b>An object rather than the string this list used to hold, and the offer is why.</b> A way out
/// printed under the whole section would be a button with no entry attached: a selection of twenty
/// produces a list of failures, and the one thing an escalation must know is WHICH of them it is
/// about. Standing it beside its own sentence is `docs/11` 9.2 point 4 - never ask about something
/// whose effect was not shown - and it is the same move this panel already made once, when the
/// reason a button is grey came down from the title to sit beside the button.
/// </summary>
public sealed class PlanFailure
{
    internal PlanFailure(string text, Escalation? offer)
    {
        Text = text;
        Offer = offer;
    }

    /// <summary>What went wrong, in the words <see cref="PlanWords"/> chose.</summary>
    public string Text { get; }

    /// <summary>The stronger ask this failure allows, or nothing at all. Most failures allow none.</summary>
    internal Escalation? Offer { get; }

    /// <summary>What the offer says on it. Empty when there is none, which the template reads.</summary>
    public string Label => Offer?.Label ?? string.Empty;

    /// <summary>Whether anything is offered here at all.</summary>
    public bool HasOffer => Offer is not null;
}

/// <summary>
/// A stronger ask, worked out from a step that did not get there.
///
/// <b>Everything the next sheet needs and nothing about the one it came from.</b> Pressing the
/// offer builds a plan from scratch against the machine as it is at that moment - so what travels
/// is the ask, not a half-made plan somebody could carry out later against a machine that moved.
/// </summary>
/// <param name="Kind">
/// Which forcing ask, decided by the ask that failed rather than by the step.
///
/// <b>Section 15.3 of the analysis, and it is the correction that reading edge cases bought.</b> A
/// stop step failing inside a RESTART must offer a forced restart: an offer that ended at killing
/// the process would leave the machine without the service somebody had just asked to bring back.
/// </param>
/// <param name="Because">
/// Why this sheet is open, carried into it so the new one does not have to guess.
///
/// The sheet it came from is closed by then - one question per sheet - so this sentence is the
/// only thing left saying what happened before.
/// </param>
internal sealed record Escalation(ActionKind Kind, string ServiceName, string Label, string Because);

/// <summary>
/// The one ask this panel can offer that ends a process, and everything that stands between
/// somebody and it.
///
/// <b>Its own file since 2026-09-07, and the size ratchet is what asked - for the eighth time in
/// this window and the eighth time pointing at a real seam.</b> The rest of <see cref="Planned"/>
/// describes a plan and reports a run. This is the one question the panel ASKS BACK: a step did
/// not get there, and there is a stronger thing that would. Everything about it is different in
/// kind - it is the only place the panel proposes an action of its own, and the only one where
/// pressing the main button ends something that cannot decline.
///
/// <b>Nothing here decides what a forcing plan would DO.</b> The steps, the neighbours that share
/// the process, the refusals and the warnings are all worked out in the core, where they are
/// testable without a machine to break. What this adds is which failures may be escalated at all,
/// what the offer says, and how much typing stands in front of the button.
/// </summary>
public sealed partial class Planned
{
    private string _typed = string.Empty;

    /// <summary>Why this sheet is open, when something offered it. Empty for every other plan.</summary>
    private string _because = string.Empty;

    /// <summary>
    /// What somebody has typed into the confirmation box.
    ///
    /// <b>Two way, which is the only two way binding this panel has, and it is bound rather than
    /// read off the control for the reason every other decision here is: what stands between a
    /// person and ending a process has to be checkable without opening a window.</b>
    /// </summary>
    public string Typed
    {
        get => _typed;
        set
        {
            if (Set(ref _typed, value ?? string.Empty))
            {
                // THE BUTTON AND ITS REASON, ON EVERY KEYSTROKE. Without this the box accepts the
                // name and the button stays grey - which reads as the confirmation not working,
                // and is the one failure that would make somebody type it again harder.
                Raise(nameof(CanCarryOut));
                Raise(nameof(CarryOutTip));
            }
        }
    }

    /// <summary>
    /// Whether this plan asks for the entry's name to be typed before it may be carried out.
    ///
    /// <b>Only where the effect reaches further than the one entry somebody named - the owner's
    /// decision of 2026-09-06, taken against making it universal.</b> `docs/11` 9.2 point 2 says
    /// the weight of a confirmation follows the size of what it does, and a heaviest step asked for
    /// everywhere stops meaning anything. Measured on this machine on 2026-08-01: 105 of 110
    /// processes host exactly one service, so on the ordinary forcing plan this box never appears
    /// and the footer is the one that shipped.
    ///
    /// <b>Read off the warnings rather than worked out again</b>, because the core already asked
    /// both questions and answered them into the preview a person is looking at. Asking a second
    /// time is a second answer to a question with one answer, and the two only have to disagree
    /// once for the box to be missing from a sheet whose own warning says the machine will stop.
    ///
    /// <b>THIS REACHED THE ORDINARY STOP ON 2026-09-09 WITHOUT A LINE HERE CHANGING, AND THAT IS
    /// WHY THERE IS A PARAGRAPH ABOUT IT.</b> The core began raising
    /// <see cref="PlanWarningKind.CriticalService"/> on a plain stop that day, and because this
    /// property reads warnings rather than action kinds, the box appeared on such a plan by itself.
    /// The owner's decision was to keep it: stopping one of those seven entries takes the machine
    /// down, and the sentence two paragraphs up - the weight of a confirmation follows the size of
    /// what it does - points here rather than at an exemption for the ordinary verb.
    ///
    /// <b>Reading warnings rather than kinds is what made that free, and it is worth naming as the
    /// property it is</b>: the question this asks is "does the preview say the effect reaches past
    /// the entry somebody named", and every future warning that means yes joins on its own. The
    /// cost of the same property is that one arriving which does NOT mean yes would join too, so
    /// the list below is a decision each time rather than a default.
    /// </summary>
    public bool NeedsTyping =>
        Showing
        && _run is null
        && _plan is { } plan
        && plan.Plans.Any(one => one.Warnings.Any(warning =>
            warning.Kind is PlanWarningKind.TerminationTakesWithIt
                or PlanWarningKind.CriticalService
                or PlanWarningKind.CriticalStartType));

    /// <summary>
    /// The name that has to be typed, which is the manager's own rather than the one on the title.
    ///
    /// <b>`ADR-14`, and here it is not a formality.</b> The display name is translated, so asking
    /// for it would ask a different word of the same machine in Warsaw and in Seattle - and it is
    /// not the string anybody would type into a command afterwards. This is the name already
    /// standing under the title for exactly that reason.
    /// </summary>
    public string TypeTheName => _plan is not { } plan || plan.Action.ServiceNames.Count == 0
        ? string.Empty
        : plan.Action.ServiceNames[0];

    /// <summary>The whole instruction, so the label above the box is one bound string.</summary>
    public string TypeToConfirm => NeedsTyping
        ? Texts.Of("gui.plan.confirm.type", TypeTheName)
        : string.Empty;

    /// <summary>
    /// Whether the name has been typed back, or was never asked for.
    ///
    /// <b>Ignoring case and surrounding space, which is a decision rather than laziness.</b>
    /// Service names are matched without case by the manager itself, so demanding one spelling
    /// would refuse a name that IS the name. What this has to be sure of is that somebody read the
    /// entry and wrote it out - and a trailing space is a keyboard, not a change of mind.
    /// </summary>
    ///
    /// <b>A PLAN ASKING ABOUT MORE THAN ONE ENTRY CAN NEVER BE CONFIRMED, and that is a refusal
    /// rather than an oversight.</b> There is no single name to type for a plan that would end
    /// several processes, so accepting one name would be somebody agreeing to the first entry and
    /// getting all of them. It is unreachable today - the window opens a forcing sheet from one
    /// failure and the command line takes one name, which section 15.6 of the analysis records as
    /// deliberate - so this is what happens if that ever stops being true: the button stays dead
    /// and the tooltip says what is missing. **A hole that refuses beats a hole that agrees.**
    public bool Confirmed =>
        !NeedsTyping
        || (TypeTheName.Length > 0
            && Asked(_plan!) == 1
            && string.Equals(_typed.Trim(), TypeTheName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The entries that did not get where they were asked to go, one line each, with the manager's
    /// own words where it refused - and under the ones that can be forced, a way to.
    ///
    /// <b>Rule 8 of the untouchable rules, at the moment it matters most.</b> A run that half
    /// worked and says only "done" is the silent partial answer that rule exists against - and here
    /// the person is holding a machine somebody else depends on.
    ///
    /// <b>Walked run by run rather than over <see cref="BulkRun.Results"/>, and that is not a
    /// detail.</b> Whether an offer belongs under a failure depends on what was ASKED - a stop or a
    /// restart - and the ask lives on the plan rather than on the step. Flattening the results
    /// first throws exactly that away, and the offer would then have to guess its own word.
    ///
    /// Steps never attempted are not listed. They are not failures and the counts in
    /// <see cref="Notice"/> already carry them, so a line each would bury the one or two lines
    /// somebody has to act on.
    /// </summary>
    public IReadOnlyList<PlanFailure> Failures => _run is not { } run
        ? []
        : [.. run.Runs.SelectMany(one => one.Results
            .Where(Failed)
            .Select(result => new PlanFailure(PlanWords.Describe(result), Offered(one, result))))];

    /// <summary>
    /// Whether a stronger ask belongs under this failure, and what it would be.
    ///
    /// <b>The four conditions are section 15.2 of the analysis, written out rather than
    /// summarised, because the fourth is the one that is easy to leave out.</b> A step that gave up
    /// with no process number has nothing to end, and an offer under it would lead to a sheet the
    /// core refuses to build - a button that opens an apology.
    ///
    /// <b>The second condition covers a refusal as well as a timeout, and that is the widening
    /// edge-case reading paid for.</b> The design showed the offer under "gave up after 60 s" only.
    /// An entry that does not accept a stop at all - the one this tool started warning about the
    /// day before - is refused outright rather than watched, so its step ends as
    /// <see cref="StepOutcome.Failed"/>. That is the case where ending the process is the ONLY road
    /// left, and it was the one case the offer would not have appeared under.
    ///
    /// <b>The fifth condition is not in the analysis and is mine: the ask that failed has to be one
    /// that can be escalated.</b> A stop step inside a plan that was ALREADY forcing has an
    /// escalation standing behind it in the same plan - offering another would propose a second
    /// sheet identical to the one already on screen. And an ask with no stop in it cannot arrive
    /// here at all, which <see cref="Forcing"/> answers by having no arm for it.
    /// </summary>
    private static Escalation? Offered(PlanRun run, StepResult result)
    {
        if (result.Step.Operation != StepOperation.Stop
            || result.Status == EntryStatus.Stopped
            || !result.ProcessId.IsPresent
            || Forcing(run.Plan.Action.Kind) is not { } kind)
        {
            return null;
        }

        return new Escalation(kind, result.Step.ServiceName, Label(kind), Because(run, result));
    }

    /// <summary>
    /// Which forcing ask stands behind an ordinary one, and nothing for the two that have no stop
    /// to escalate.
    ///
    /// <b>Named as a mapping rather than a pair of ifs</b> because it answers two questions with
    /// one table: whether an offer exists, and which word it wears. Splitting them would let the
    /// word drift from the ask, which is the fault section 15.3 caught in the design.
    /// </summary>
    private static ActionKind? Forcing(ActionKind kind) => kind switch
    {
        ActionKind.Stop => ActionKind.ForceStop,
        ActionKind.Restart => ActionKind.ForceRestart,
        _ => null
    };

    /// <summary>
    /// What the offer says on it, with the key inside each call.
    ///
    /// <b>The heading word rather than the button word, on the owner's decision of 2026-09-06.</b>
    /// The offer says "Force stop" because that is what an administrator is looking for; the button
    /// on the sheet it opens names the process, because by then there is a number to name.
    /// </summary>
    private static string Label(ActionKind kind) => kind == ActionKind.ForceStop
        ? Texts.Of("gui.plan.offer.forceStop")
        : Texts.Of("gui.plan.offer.forceRestart");

    /// <summary>
    /// Why the next sheet is open, in one sentence, read off the run rather than assumed.
    ///
    /// <b>The number of seconds comes from the run's own ceiling and is never written down
    /// here.</b> The window pins its ceiling to the command line's sixty seconds so that "it worked
    /// from the terminal" cannot be a true sentence about the same entry - and a wording that
    /// spelled sixty out would keep saying it after somebody moved that pin.
    ///
    /// <b>Two sentences, because giving up and being refused are different things.</b> The manager
    /// took the first request and we stopped watching; it declined the second outright. Telling
    /// somebody the first failed would be a claim about something nobody saw, which is the
    /// distinction <see cref="StepOutcome"/> draws and this carries onto a screen.
    /// </summary>
    /// <summary>
    /// What the button says it will do, which on a forcing plan is not "carry this out".
    ///
    /// <b>The owner's decision of 2026-09-06, and the argument is one sentence: pressing it ends a
    /// process, and "Carry this out" is a label that describes a plan rather than an act.</b> The
    /// number is the one already standing in the steps above, so a person presses a button naming
    /// the thing they just read. <i>Force stop</i> stays as the heading and as the word on the
    /// offer, where there is not yet a number to name.
    ///
    /// <b>Exactly one step that ends a process, or the ordinary label.</b> A plan with two of them
    /// cannot be reached in this slice - the window opens a forcing sheet from one failure and the
    /// command line takes one name - and naming one of two processes on the button would be the
    /// preview and the button disagreeing about what is about to happen. Refusing to name either
    /// is the honest answer to a shape nothing can produce yet.
    /// </summary>
    public string CarryOutLabel => _plan is not { } plan
        ? Texts.Of("gui.plan.carryOut")
        : plan.Plans
            .SelectMany(one => one.Steps)
            .Where(step => step.Operation == StepOperation.Terminate && step.ProcessId is not null)
            .Select(step => step.ProcessId!.Value)
            .Distinct()
            .ToList() is [var ending]
            ? Texts.Of("gui.plan.carryOut.endProcess", ending)
            : Texts.Of("gui.plan.carryOut");

    private static string Because(PlanRun run, StepResult result) =>
        result.Outcome == StepOutcome.TimedOut
            ? Texts.Of("gui.plan.because.timedOut", (int)run.Ceiling.TotalSeconds)
            : Texts.Of("gui.plan.because.refused");
}
