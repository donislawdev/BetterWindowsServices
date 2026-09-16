namespace Bws.Gui.ViewModels;

/// <summary>
/// Which parts of the plan panel are on the screen at all, and the one call that says they moved.
///
/// <b>Its own file since 2026-08-19, and the size ratchet is what asked - for the sixth time in this
/// window and the sixth time pointing at a real seam.</b> The rest of <see cref="Planned"/> decides
/// what the panel SAYS: the three states, the heading, the steps, the sentences. This decides what
/// is THERE, and the two are different questions that happen to be answered by the same object.
///
/// <b>The whole group exists for one recorded decision, which is why it belongs together.</b>
/// Backlog 203: a heading reading "Not included, and why" over nothing states something false, and
/// the owner saw exactly that on the first look at this panel. Every one of these is a bound
/// boolean whose only consumer is markup, and whose only job is to take a heading off the screen
/// with the thing it labelled.
///
/// <b>Bound properties rather than a style trigger per section, and that was a size decision
/// too.</b> Six trigger blocks would have said one thing six times and taken the markup past its
/// own ceiling - which is the room the empty state had just been moved out to make.
/// </summary>
public sealed partial class Planned
{
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

    /// <summary>
    /// Whether there is anything in the way of carrying this out.
    ///
    /// <b>The block backlog 203 missed, found 2026-08-19 by reading the markup rather than by any
    /// test.</b> Every other conditional block in that panel took its heading off the screen with
    /// it, and this one - alone - had no visibility of its own. It stood in the tree always, empty,
    /// carrying its margin, which is a gap nobody put there sitting between the state sentence and
    /// the first section. A section closed everywhere but one place is a section that looks closed.
    /// </summary>
    public bool HasBlocked => Blocked.Length > 0;

    /// <summary>
    /// Whether the manager's own name for the entry belongs under the title.
    ///
    /// False whenever the title is already using it, so no empty line is ever reserved under a
    /// heading - the fault the property above this one was written for.
    /// </summary>
    public bool HasSubtitle => Subtitle.Length > 0;

    /// <summary>
    /// Whether the command that would ask for the same thing still belongs on screen.
    ///
    /// <b>False once there is a result, and that is `E5` read as it was written rather than as a
    /// field that happens to be full.</b> The equivalent command is part of the PREVIEW - the plan
    /// rendered as something a person could type INSTEAD of pressing - which is why it was built
    /// before this window could run anything at all. After a run it describes something that has
    /// already happened, and it sits directly under the way back, so the last thing a reader meets
    /// scanning up from the button is the command they do not want.
    /// </summary>
    public bool HasCommands => _run is null && Commands.Count > 0;

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
    /// Whether the commands are worth a button that takes all of them at once - the owner's
    /// request of 2026-09-16, made over a sheet with five of them and five buttons that each took
    /// one.
    ///
    /// <b>More than one, not at least one.</b> Over a single command "Copy all" beside "Copy" is
    /// two buttons for one thing, and the second would be the one somebody wonders about.
    /// </summary>
    public bool HasSeveralCommands => Commands.Count > 1;

    /// <summary>The same question about the way back.</summary>
    public bool HasSeveralWayBack => WayBack.Count > 1;

    /// <summary>
    /// Every command, one per line, as the clipboard should hold them.
    ///
    /// <b>The platform's line ending rather than a bare newline</b>, because what this is for is
    /// pasting into a Windows terminal, which runs the lines one after another - and the join is
    /// done here rather than in the view, which holds layout and bindings and nothing else (GUI
    /// rule 11).
    /// </summary>
    public string AllCommands => string.Join(Environment.NewLine, Commands);

    /// <summary>The whole way back, one command per line.</summary>
    public string AllWayBack => string.Join(Environment.NewLine, WayBack);

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
        Raise(nameof(HasBlocked));

        // The line itself as well as whether it is there, because this one carries text rather
        // than only deciding a visibility - and it changes with the plan, not with a count.
        Raise(nameof(Subtitle));
        Raise(nameof(HasSubtitle));

        // What the button says about itself moves with every one of these: elevation is fixed for
        // the session, but running, finished and nothing-to-run all change here.
        Raise(nameof(CarryOutTip));

        // AND WHAT IT SAYS ON ITS FACE, which changes with the plan rather than with a count -
        // "End process 1408" on a forcing plan, "Stop Windows Search" or "Set 3 entries to Manual"
        // on every other. A button whose label was raised nowhere would keep naming the entry of
        // the plan before this one.
        Raise(nameof(CarryOutLabel));

        // THE CONFIRMATION BOX AND ITS LABEL, WHICH APPEAR AND GO WITH THE PLAN. This is the
        // heading-over-nothing fault of backlog 203 in its most expensive form: a box asking for a
        // name left standing over a plan that never wanted one would keep the button grey with no
        // way for anybody to work out why.
        Raise(nameof(NeedsTyping));
        Raise(nameof(TypeTheName));
        Raise(nameof(TypeToConfirm));

        // And the reason for the box, which appears and goes with it.
        Raise(nameof(Danger));
        Raise(nameof(HasDanger));

        // AND THE WAITING ROW, FOR THE SAME REASON AND WITH THE SAME COST WHEN IT IS FORGOTTEN -
        // which it was, for the length of one build on 2026-09-09. The binding was written, the
        // markup was right, the property answered correctly when asked - and nothing asked, so the
        // row was evaluated once while no plan was showing and never again. It came up in a
        // screenshot rather than in a test, which is `docs/08` position 19 arriving on schedule:
        // a binding that silently keeps a stale answer reddens nothing in this project.
        Raise(nameof(Waits));

        // AND WHAT THE BOX SAYS ABOUT ITSELF, WHICH FOLLOWS Waits: a word left in it by the last
        // sheet stops being a problem the moment a plan with nothing to wait for hides the box,
        // and becomes one again when the next plan shows it.
        RaiseTheProblem();
        Raise(nameof(HasCommands));
        Raise(nameof(HasOverlapping));
        Raise(nameof(HasWarnings));
        Raise(nameof(HasProblems));
        Raise(nameof(HasFailures));
        Raise(nameof(HasWayBack));

        // The button over each list of commands, and what it carries - both follow the lists.
        Raise(nameof(HasSeveralCommands));
        Raise(nameof(AllCommands));
        Raise(nameof(HasSeveralWayBack));
        Raise(nameof(AllWayBack));
    }
}
