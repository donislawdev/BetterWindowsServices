using System.Globalization;
using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// How long one step of this plan may be watched for, and what tells the panel the time.
///
/// <b>Its own file since 2026-09-09, and the size ratchet is what asked - for the seventh time in
/// this window and the seventh time pointing at a real seam.</b> The rest of <see cref="Planned"/>
/// describes a plan: its title, its steps, its warnings, what stands in the way. This is the one
/// number on the sheet a person can CHANGE before pressing, which is a different kind of thing
/// entirely, and the clock that makes it checkable travels with it.
///
/// <b>Backlog 330, and the pre-release audit's RELEASE-008.</b> The ceiling was a constant nobody
/// could reach from the window, so "wait longer" was an answer the terminal had and this did not.
///
/// <b>AND SINCE 2026-09-15 THE BOX SAYS NO OUT LOUD.</b> Point 5 of the review in `docs/11` 2.14:
/// the setter refused anything below one second in silence, a word typed into the box failed in
/// the binding where nothing could see it, and a number too large for an int did the same - so the
/// box read "abc" and the run used sixty. The box holds TEXT now, exactly as typed, and the panel
/// says under it when that text is not a number of seconds. The owner's decision was one rule for
/// both interfaces and no ceiling, and the rule is <see cref="StepCeiling.Seconds"/> in the core.
/// </summary>
public sealed partial class Planned
{
    /// <summary>
    /// What tells this panel the time, so a test does not have to wait for one.
    ///
    /// <b>The same seam <see cref="Elevated"/> takes and for the same reason</b>: the working
    /// answer is the one a person gets, and a test asserting how a waiting line reads after twelve
    /// seconds cannot be asked to spend twelve seconds proving it.
    /// </summary>
    internal IClock Clock
    {
        get => _clock;
        init => _clock = value;
    }

    private IClock _clock = new SystemClock();

    /// <summary>
    /// What is in the box, exactly as typed.
    ///
    /// <b>Text rather than a number, and that is the whole repair.</b> An int property bound to a
    /// TextBox lets WPF do the reading, and WPF reads in silence: "abc" is a conversion error nobody
    /// sees, and the model keeps whatever it held before. The one place a wrong value can exist is
    /// this string, and the panel reads it every time rather than keeping a number beside it.
    ///
    /// <b>The button and its reason move on every keystroke</b>, the same way <see cref="Typed"/>
    /// does: a box that refuses and a button that stays grey with nothing saying why is the
    /// silence rule 8 forbids, arriving from the direction where it looks like a broken control.
    ///
    /// <b>Not kept between sessions, deliberately.</b> It is a decision about the plan in front of
    /// somebody - a service they know is slow - rather than a preference about how they like the
    /// window, and section H of the specification is where preferences live. A number that quietly
    /// stayed at ten from a fortnight ago would be worse than one that starts where the terminal
    /// starts.
    /// </summary>
    public string WaitingText
    {
        get => _waitingText;
        set
        {
            if (Set(ref _waitingText, value ?? string.Empty))
            {
                Raise(nameof(Waiting));
                RaiseTheProblem();
                Raise(nameof(CanCarryOut));
                Raise(nameof(CarryOutTip));
            }
        }
    }

    /// <summary>
    /// Everything that changes when the box's answer about itself changes: the sentence, the flag,
    /// and the binding's own error state - which is what colours the box's edge. One method because
    /// three places raise it and a fourth that forgot one of the three would leave the edge red
    /// under a box that says sixty.
    /// </summary>
    private void RaiseTheProblem()
    {
        Raise(nameof(WaitingProblem));
        Raise(nameof(HasWaitingProblem));
        RaiseErrors(nameof(WaitingText));
    }

    /// <summary>The box is the one thing here somebody can get wrong, so it is the one thing with errors.</summary>
    public override bool HasErrors => HasWaitingProblem;

    /// <summary>
    /// The sentence under the box, handed to the binding on the box - so <c>Validation.HasError</c>
    /// on the control is true exactly when <see cref="HasWaitingProblem"/> is, and the style can
    /// colour the edge without naming a property of this assembly (`docs/10` trap 10).
    /// </summary>
    protected override IEnumerable<string> ErrorsOf(string property) =>
        property == nameof(WaitingText) && HasWaitingProblem ? [WaitingProblem] : [];

    private string _waitingText = ((int)StepCeiling.Default.TotalSeconds).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// How long one step may be watched for, as the run will be given it - read off the box at the
    /// press, by <c>MainWindow.CarryOut</c>.
    ///
    /// <b>The terminal's rule and nothing beside it.</b> <see cref="StepCeiling.Seconds"/> decides
    /// what a number of seconds is for both interfaces, and a runbook carried from one to the other
    /// meets no second rule. The one leniency is this window's own and is said here: a space typed
    /// around the number is trimmed before asking, because a box is typed into and a runbook is not.
    ///
    /// <b>The default when the text is not a number, and why that is not a quiet substitution.</b>
    /// While the box is on the sheet and says something that is not seconds, <see cref="CanCarryOut"/>
    /// is false and this is never read for a run. It is read when the box is NOT on the sheet - a
    /// start type plan has nothing to wait for and hides it - and then the number is handed to a run
    /// that will not use it. So the default here stands in for nothing anybody typed on purpose.
    /// </summary>
    public int Waiting => StepCeiling.Seconds(_waitingText.Trim()) ?? (int)StepCeiling.Default.TotalSeconds;

    /// <summary>
    /// What is wrong with the box, in a sentence under it, or nothing.
    ///
    /// <b>Only while the box is on the sheet.</b> A start type plan hides the box, and a word left in
    /// it from the last sheet must not grey the button of a plan that has nothing to wait for - the
    /// sentence would be about a control nobody can see.
    ///
    /// <b>One sentence for every way the text can be wrong.</b> Empty, a word, a fraction, zero, a
    /// minus, a number past what an int holds - the box shows what was typed, so the sentence does
    /// not have to repeat it, and what to type instead is the same in every case. `docs/11`
    /// section 5: specific, near the field, no blame, the input kept, a way out offered.
    /// </summary>
    public string WaitingProblem =>
        Waits && StepCeiling.Seconds(_waitingText.Trim()) is null
            ? Texts.Of("gui.plan.waiting.problem")
            : string.Empty;

    /// <summary>
    /// Whether the box is saying something that is not a number of seconds - what greys the button
    /// and colours the box's edge.
    /// </summary>
    public bool HasWaitingProblem => WaitingProblem.Length > 0;

    /// <summary>
    /// Whether this plan has anything to wait for, which decides whether the box above is offered.
    ///
    /// <b>A start type change has nothing</b>: it writes a setting and moves no service, so there
    /// is no state to arrive at and no reason to watch for one. The terminal says exactly this in
    /// its help - "neither --dependents nor --timeout applies here" - and a box offering to change
    /// a number that does nothing is the silence the belonging table exists to end, said the other
    /// way round.
    /// </summary>
    /// <b>AND NOT WHILE A RUN IS UNDER WAY, which the first build of this got wrong and a
    /// screenshot caught.</b> The ceiling is read at the press - see MainWindow.CarryOut - so a box
    /// still editable during the run is a control that accepts a number and does nothing with it.
    /// The strip it stands in says what the run is doing instead, which is the more useful sentence
    /// at that moment and the reason the two share a line.
    public bool Waits =>
        Showing
        && !Busy
        && _run is null
        && _plan is { } plan
        && plan.Plans.Any(one => one.Steps.Any(step => step.Operation != StepOperation.SetStartType));
}
