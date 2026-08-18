using Bws.Core;
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
/// <b>THE WINDOW'S DRY RUN, AND SINCE 2026-08-19 THE REPORT THAT FOLLOWS IT.</b> `ADR-11` says a
/// plan can be shown, turned into a command, reversed or carried out, and those are one object seen
/// from four sides rather than four features. All four now reach a window: the steps in the order
/// they would happen, the command line that would ask for the same thing, what came of carrying it
/// out, and what it would take to put the machine back.
///
/// <b>IT STILL CARRIES NOTHING OUT ITSELF, and the sentence above changed on the day that stopped
/// being the whole story rather than before or after it.</b> The window owns the run - it holds the
/// token that stops one and it is on the short list of files allowed to build a writer at all. What
/// this class owns is the three states a person sees: nothing done, doing it, done. The menu items
/// that open it are still worded as questions, because they still only open a plan.
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
    private BulkRun? _run;
    private bool _busy;
    private string _progress = string.Empty;

    /// <summary>Whether the panel is on screen. Closed until somebody asks.</summary>
    public bool Showing
    {
        get => _showing;
        private set => Set(ref _showing, value);
    }

    /// <summary>
    /// The plan on screen, for whoever carries it out.
    ///
    /// <b>Handed out rather than acted on here, and that is the seam that keeps this class
    /// testable.</b> Carrying a plan out means building something able to change a machine, so a
    /// view model that did it would be a view model no test could call. What this class owns is
    /// what the panel SAYS about a run - which is checkable at a desk, and is the half that gets
    /// a person's decision wrong when it is wrong.
    /// </summary>
    internal BulkPlan? Plan => _plan;

    /// <summary>Whether a run is happening right now.</summary>
    public bool Busy
    {
        get => _busy;
        private set => Set(ref _busy, value);
    }

    /// <summary>
    /// Whether there is something to carry out and nothing in the way of carrying it.
    ///
    /// <b>A plan already carried out gives false, and that is the one clause worth arguing for.</b>
    /// The panel keeps showing the steps after a run so somebody can read what happened against what
    /// was going to - so the button has to go quiet, or a second press would ask the machine to do
    /// it all again while the screen still reads like a preview.
    ///
    /// <b>AND ELEVATION, ADDED 2026-08-19 BECAUSE IT WAS MISSING AND A SCREENSHOT SHOWED IT.</b> The
    /// owner's decision of 2026-08-18 was that the window refuses and says how to run it, with such
    /// actions marked BEFORE anybody clicks. The first look at this panel was on a session without
    /// administrator rights, and the button was live with nothing to say so - which is the decision
    /// broken in the direction that costs the most: a press, a column of refusals from the manager,
    /// and a person left working out why.
    /// </summary>
    public bool CanCarryOut =>
        Showing && Elevated && !Busy && _run is null && _plan is { IsRunnable: true };

    /// <summary>
    /// Whether this session can change anything at all.
    ///
    /// <b>Its own property rather than a reach into <see cref="Says"/>, and settable for the reason
    /// that one gives: a working session cannot produce the other answer on demand.</b> Asked
    /// through the identifier S-1-5-32-544 rather than by the name of a group, which is rule 3 of
    /// the project's untouchable rules - on a localised Windows the group is not called
    /// Administrators.
    /// </summary>
    internal bool Elevated { get; init; } = Session.IsElevated();

    /// <summary>
    /// Why this cannot be carried out here, said before anybody presses anything.
    ///
    /// Empty when there is nothing in the way, rather than a reassuring sentence - a line saying
    /// everything is fine is a line somebody has to read to learn nothing.
    /// </summary>
    public string Blocked => Showing && !Elevated ? Texts.Of("gui.plan.blocked.notElevated") : string.Empty;

    /// <summary>
    /// Whether each section has anything in it.
    ///
    /// <b>These exist so a section can take its heading off the screen with it.</b> A heading
    /// reading "Not included, and why" over nothing states something false, and the owner saw
    /// exactly that on the first look at this panel - backlog 203. Bound properties rather than a
    /// style trigger per section, because six trigger blocks would have taken this file past the
    /// markup ceiling to say one thing six times.
    /// </summary>
    public bool HasExtra => Extra.Length > 0;

    /// <summary>Whether an entry is named by more than one plan.</summary>
    public bool HasOverlapping => Overlapping.Length > 0;

    /// <summary>Whether there is anything worth knowing before pressing.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>Whether any entry got no plan at all.</summary>
    public bool HasProblems => Problems.Count > 0;

    /// <summary>Whether anything failed. Never true before a run.</summary>
    public bool HasFailures => Failures.Count > 0;

    /// <summary>Whether there is a way back. Never true before a run.</summary>
    public bool HasWayBack => WayBack.Count > 0;

    /// <summary>
    /// Which step is happening, while it happens.
    ///
    /// <b>Empty except during a run.</b> A person watching a stop that takes half a minute has
    /// nothing else to tell them the window is alive - and this window has measured half a minute on
    /// a single step, so the line is not decoration.
    /// </summary>
    public string Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    /// <summary>What the panel is called, naming the action and how much it touches.</summary>
    public string Heading => _plan is not { } plan
        ? string.Empty
        : Asked(plan) == 1
            ? Texts.Of("gui.plan.heading.one", PlanWords.Doing(plan.Action.Kind), Only(plan))
            : Texts.Of("gui.plan.heading.many", PlanWords.Doing(plan.Action.Kind), Asked(plan));

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
                PlanWords.Reason(step.Reason)),
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
        : [.. plan.Warnings.Select(PlanWords.Describe)];

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
        : [.. plan.Problems.Select(PlanWords.Describe)];

    /// <summary>
    /// The same thing from a terminal, one line per entry. `E5`.
    ///
    /// Rendered by the core, so these are commands this tool really accepts rather than text that
    /// looks like them - a guard in the command line's own tests holds that, and it caught a switch
    /// rendered onto a verb that refuses it.
    /// </summary>
    public IReadOnlyList<string> Commands => _plan is not { } plan ? [] : EquivalentCommand.For(plan);

    /// <summary>
    /// Where this panel is in the only sequence it has: nothing done, doing it, done.
    ///
    /// <b>Always present while the panel is open, never conditional, and that survived the arrival of
    /// the button.</b> A panel full of steps in the present tense reads as a report of something
    /// done - which was a lie while the window could not carry anything out, and is a DIFFERENT lie
    /// now that it can, because a person who has not pressed anything would read their selection as
    /// already stopped. `docs/11` 9.2: never show an effect somebody might think has already landed.
    ///
    /// <b>The sentence changed together with the button rather than before it or after it</b>, which
    /// is what the note left at Krok 5 asked for in as many words.
    /// </summary>
    public string Notice => !Showing ? string.Empty
        : Busy ? Texts.Of("gui.plan.notice.running")
        : _run is not { } run ? Texts.Of("gui.plan.notice.notYet")
        : Arrived(run) == run.Runs.Count
            ? Texts.Of("gui.plan.notice.done", run.Runs.Count)
            : Texts.Of("gui.plan.notice.partly", Arrived(run), run.Runs.Count);

    /// <summary>
    /// The entries that did not get where they were asked to go, one line each, with the manager's
    /// own words where it refused.
    ///
    /// <b>Rule 8 of the untouchable rules, at the moment it matters most.</b> A run that half worked
    /// and says only "done" is the silent partial answer that rule exists against - and here the
    /// person is holding a machine somebody else depends on.
    ///
    /// Steps never attempted are not listed. They are not failures and the counts in
    /// <see cref="Notice"/> already carry them, so a line each would bury the one or two lines
    /// somebody has to act on.
    /// </summary>
    public IReadOnlyList<string> Failures => _run is not { } run
        ? []
        : [.. run.Results.Where(Failed).Select(PlanWords.Describe)];

    /// <summary>
    /// What somebody would type to put the machine back where this run found it. `ADR-11`'s
    /// reversible promise, in the cheapest honest form it has.
    ///
    /// One answer for the whole selection rather than one per plan, worked out as a net effect per
    /// entry - so a restart that ended where it began says nothing, and a selection dealt with
    /// dependants first hands the lines back in an order whose first line works. All of that is
    /// `NetEffect` in the core, and none of it is decided here.
    /// </summary>
    public IReadOnlyList<string> WayBack => _run is not { } run
        ? []
        : [.. run.Reversal.Select(EquivalentCommand.For)];

    /// <summary>
    /// A run has begun.
    ///
    /// <b>Told rather than started here</b> - the seam described at <see cref="Plan"/>. What this
    /// owns is that the button goes quiet and the sentence changes before the first step, not after
    /// it: a button still live while the manager is being asked is a second ask waiting to happen.
    /// </summary>
    internal void Starting()
    {
        Busy = true;
        Progress = string.Empty;

        Raise(nameof(CanCarryOut));
        Raise(nameof(Notice));
    }

    /// <summary>
    /// A step is about to be attempted, with its place across the whole selection.
    ///
    /// <b>The number is the step's place in the plan, never a count of attempts</b>, and it arrives
    /// that way from the runner for a reason written there: steps get skipped, and a counter of
    /// attempts calls the sixth step the third one while somebody is trying to work out where a run
    /// has got to.
    /// </summary>
    internal void Announce(PlanStep step, int number) =>
        Progress = Texts.Of(
            "gui.plan.progress",
            number,
            _plan?.Steps.Count() ?? 0,
            step.Operation == StepOperation.Stop
                ? Texts.Of("gui.plan.operation.stop")
                : Texts.Of("gui.plan.operation.start"),
            step.ServiceName);

    /// <summary>
    /// A run has ended, whether it finished or was interrupted.
    ///
    /// <b>The plan stays on screen beside it, which is the whole of `ADR-11`'s promise arriving in a
    /// window:</b> what was going to happen and what did, side by side, without anybody having to
    /// remember the first half.
    /// </summary>
    internal void Finished(BulkRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        _run = run;
        Busy = false;
        Progress = string.Empty;

        Raise(nameof(CanCarryOut));
        Raise(nameof(Notice));
        Raise(nameof(Failures));
        Raise(nameof(WayBack));
        RaiseTheCounts();
    }

    /// <summary>
    /// That every section may have appeared or gone.
    ///
    /// One call rather than six lines wherever the plan or the run changes, because a section that
    /// keeps its heading after its content went is the fault these properties exist to prevent, and
    /// it would arrive by somebody adding a Raise in three places out of four.
    /// </summary>
    private void RaiseTheCounts()
    {
        Raise(nameof(HasExtra));
        Raise(nameof(HasOverlapping));
        Raise(nameof(HasWarnings));
        Raise(nameof(HasProblems));
        Raise(nameof(HasFailures));
        Raise(nameof(HasWayBack));
    }

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

        // A NEW PLAN DROPS THE OLD RUN, and getting this wrong would be the worst bug this panel
        // could have: a report of what happened to five services, sitting under the steps of a plan
        // for five different ones, with nothing on screen to say the two do not belong together.
        _plan = plan;
        _run = null;
        Busy = false;
        Progress = string.Empty;
        Showing = true;

        Raise(nameof(CanCarryOut));
        Raise(nameof(Blocked));
        Raise(nameof(Failures));
        Raise(nameof(WayBack));
        RaiseTheCounts();
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
        _run = null;
        Busy = false;
        Progress = string.Empty;
        Showing = false;

        Raise(nameof(CanCarryOut));
        Raise(nameof(Blocked));
        Raise(nameof(Failures));
        Raise(nameof(WayBack));
        RaiseTheCounts();
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
    /// How many of the entries somebody asked about ended where they asked.
    ///
    /// <b>Counted over runs rather than over steps, because that is the unit a person picked.</b>
    /// Somebody selected five rows, so "three of five" is the sentence they can act on - "twenty
    /// nine of thirty two steps" is arithmetic about our internals, and most of those steps are
    /// cascade members nobody chose.
    /// </summary>
    private static int Arrived(BulkRun run) => run.Runs.Count(one => one.Completed);

    /// <summary>
    /// A step worth putting in front of somebody.
    ///
    /// A refusal and a step we stopped watching, and nothing else. Skipped steps were never tried,
    /// which the counts already say, and one that was already where it was asked to be is the plan
    /// working rather than failing.
    /// </summary>
    private static bool Failed(StepResult result) =>
        result.Outcome is StepOutcome.Failed or StepOutcome.TimedOut;


    private static int Asked(BulkPlan plan) =>
        plan.Action.ServiceNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private static string Only(BulkPlan plan) => plan.Action.ServiceNames.Count == 0
        ? string.Empty
        : plan.Action.ServiceNames[0];

}
