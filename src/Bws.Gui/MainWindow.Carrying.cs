using System.Windows.Threading;
using Bws.Core.Planning;

namespace Bws.Gui;

/// <summary>
/// The window's half of carrying a plan out: the run it holds, the way to stop one, and its refusal
/// to disappear in the middle of one.
///
/// <b>Its own file since 2026-08-19, and the size ratchet is what asked - for the fifth time in this
/// window and the fifth time pointing at a real seam.</b> The rest of MainWindow is about the window
/// as a whole: what it is made of, where the keyboard goes, when the list may rearrange itself. This
/// is about the one thing this window does that changes a machine, and it grew twice in two days -
/// once when the button arrived, once when a test was finally able to reach it.
///
/// <b>THE RATCHET THAT ASKED IS NOT THE ONE ANYBODY WATCHES.</b> The ceiling on the longest file was
/// nowhere near, at 519 against 536. What went red is
/// <c>SizeRatchetGuards.Not_more_files_are_long_than_were_long</c> - three shipped files over five
/// hundred lines where two were allowed - and its message names the shape exactly: nothing crossed
/// the ceiling, everything moved towards it. That is the growth nobody notices.
///
/// <b>A partial class rather than a separate type, for the same reason MainWindow.Menu.cs is one.</b>
/// Closing is an override of Window's own method, so it has to live on the window's class rather than
/// beside it. Pulling the rest out and leaving that behind would put the refusal in one file and the
/// run it refuses over in another.
///
/// <b>Everything that touches the two fields below is in THIS file, which is why the interrupt got a
/// method of its own.</b> The constructor still wires the panel's events, because that is
/// construction - but the line it wires now says <see cref="AskTheRunToStop"/> rather than reaching
/// into a field whose whole story is here. A field read from two files is a field with two stories.
///
/// <b>What is deliberately NOT here: anything that decides what a run will do.</b> Cascade, order,
/// warnings and refusals are worked out in the core before a preview is drawn, and the writer is
/// constructed in <see cref="Carrying"/>, which is on the short list <c>PlanOnlyGuards</c> keeps.
/// This file holds a task, a token, and the question of what happens when somebody reaches for the
/// corner of the window.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// The run that is happening, while one is, so that closing the window can wait for it.
    ///
    /// <b>Held rather than started and forgotten, which is what BackgroundWorkGuards is about</b> -
    /// and here it buys something specific rather than tidiness: without it, closing the window
    /// during a run ends the process, and this is the tool that leaves half a cascade switched off.
    /// The command line has three levels of Ctrl+C for that failure, found on a virtual machine
    /// rather than by reasoning.
    /// </summary>
    private Task? _running;

    /// <summary>Asks the run in progress to stop before its next step. Null when none is.</summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// How a plan is carried out: <see cref="Carrying.Out"/>, unless somebody handed this window
    /// another way.
    ///
    /// <b>PRODUCTION CODE THAT EXISTS FOR A TEST, WHICH IS A COST RATHER THAN A PATTERN - the same
    /// trade <see cref="TakeThisAsARun"/> was taken on and the same argument for taking it.</b>
    /// What it buys is the failure path of the one method in this window that changes machines.
    /// Until 2026-09-03 nothing could execute it at all: reaching it needs a run that throws, the
    /// only run this window could start was a real one against a real manager, and a test suite
    /// that started one would stop services on whatever machine ran it. So the repair backlog 298
    /// asked for would have shipped with a green suite that had never been down that road.
    ///
    /// <b>It takes a plan and hands back a report, and that narrowness is the whole safety
    /// argument.</b> Nothing here can become a road to a write that skips a plan - which is what
    /// `PlanOnlyGuards` exists to prevent - because a plan is the first thing it is given. The
    /// writer is still built in exactly one place, <see cref="Carrying"/>, and that guard reads
    /// the sources for the construction rather than for a call.
    ///
    /// <b>Init rather than settable</b>, so a run cannot be swapped out from under a window that
    /// is already using one.
    /// </summary>
    internal Func<BulkPlan, TimeSpan, CancellationToken, Action<PlanStep, int>, Task<BulkRun>> CarriedOutBy
    {
        get;
        init;
    } = Carrying.Out;

    /// <summary>
    /// Carries out the plan on screen, off the drawing thread, and shows what came of it.
    ///
    /// <b>The window owns this rather than the panel or the view model, and each of the three
    /// reasons is a different one.</b> The panel must not reach for a manager, or rule 1 would have
    /// two composition points instead of one. The view model must not either, or it becomes a class
    /// no test can call without touching a machine. And the token has to outlive the button press,
    /// because closing the window uses it too.
    ///
    /// <b>Asked again here rather than trusted from the button being live.</b> A disabled button is
    /// a statement about pixels, and the press that matters is the one arriving while a run is
    /// already going - from a second click, or from the keyboard, at the moment the first one has
    /// not yet reached the screen.
    /// </summary>
    internal async Task<bool> CarryOut()
    {
        if (!_model.Planned.CanCarryOut || _model.Planned.Plan is not { } plan)
        {
            return false;
        }

        using var stopping = new CancellationTokenSource();

        _stopping = stopping;

        var watching = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        watching.Tick += (_, _) => _model.Planned.Tick();

        try
        {
            // INSIDE THE TRY RATHER THAN ABOVE IT, so that the finally covers every line that has
            // told the panel anything. Two statements outside it was two statements whose failure
            // left the panel saying a run was under way with nothing to end it.
            _model.Planned.Starting();

            // A SECOND HAND ON THE LINE UNDER THE PLAN. `docs/11` section 7 asks for progress above
            // ten seconds, and until 2026-09-09 the line said which step was happening and nothing
            // about how long it had been happening - so a stop fifty seconds into its ceiling and
            // one two seconds in read identically.
            //
            // THE TIMER LIVES HERE AND NOT IN THE PANEL, which is the same division everything else
            // in this file makes: a DispatcherTimer in a view model is a view model no test can
            // build without a dispatcher, and what it would be testing is WPF's clock. The panel
            // owns what the line SAYS - Planned.Tick - and this owns when it is asked.
            watching.Start();

            // ON THE INTERFACE THREAD, WHICH IS WHAT MAKES THE LINE BELOW SAFE. Progress<T> takes
            // the context it is built on and posts back to it, so the steps arriving from a worker
            // thread reach a bound property here rather than there. Built per run rather than kept,
            // because building it anywhere else would capture whatever thread happened to be there.
            var announce = new Progress<(PlanStep Step, int Number)>(
                what => _model.Planned.Announce(what.Step, what.Number));

            // THE CEILING COMES FROM THE PANEL RATHER THAN FROM A CONSTANT, SINCE 2026-09-09 -
            // backlog 330. Read here, at the press, rather than held anywhere: the box is on the
            // sheet in front of somebody and what they last typed into it is what they meant.
            var running = CarriedOutBy(
                plan,
                TimeSpan.FromSeconds(_model.Planned.Waiting),
                stopping.Token,
                (step, number) => ((IProgress<(PlanStep, int)>)announce).Report((step, number)));

            _running = running;

            _model.Planned.Finished(await running.ConfigureAwait(true));

            // Whatever moved, moved. Asking now rather than waiting up to a second means the list
            // agrees with the panel by the time somebody looks up from it.
            await _model.LoadAsync().ConfigureAwait(true);

            return true;
        }
        catch (InvalidOperationException refusal)
        {
            // The one this path documents: a plan with problems, which CanCarryOut should already
            // have refused. Said out loud rather than swallowed - and rather than left to end the
            // process, because a tool that changes services and then vanishes is the worst way to
            // learn that something was wrong with the plan.
            _model.Planned.Finished(new BulkRun { Plan = plan, Runs = [] });
            _model.Says.CouldNotDo(refusal.Message);

            return false;
        }
        finally
        {
            watching.Stop();

            _running = null;
            _stopping = null;

            // AND THE PANEL STOPS SAYING A RUN IS UNDER WAY, WHICH UNTIL 2026-09-03 ONLY THE TWO
            // ENDINGS ABOVE DID - backlog 298. Both of them go through Finished, so anything else
            // thrown between Starting and here left the flag raised: the button dead, the tooltip
            // explaining that a run is in progress, and the step name of whatever was happening
            // when it stopped. The exception itself is deliberately NOT caught - it carries on to
            // the window's own net, which puts the sentence in the status line, and a catch here
            // would take that away in exchange for nothing.
            //
            // A no-op on every ordinary path, because Finished has already lowered the flag.
            _model.Planned.NoLongerRunning();
        }
    }

    /// <summary>
    /// Hands this window a run it did not start.
    ///
    /// <b>PRODUCTION CODE THAT EXISTS FOR A TEST, WHICH IS A COST RATHER THAN A PATTERN - the
    /// owner's decision of 2026-08-19.</b> It was taken because the alternative was two behaviours
    /// with no guard at all: the refusal to close half way through changing a machine, which is the
    /// state (T) this project has already paid for once, and whether pressing the interrupt reaches
    /// the run rather than only appearing on screen. Both need a run in flight, and the only run
    /// this window can start is a real one against a real manager - which a test suite must never
    /// do, because it would stop services on whatever machine ran it.
    ///
    /// <b>IT TAKES A TASK AND A TOKEN SOURCE, AND NOTHING THAT COULD BECOME A RUN, and that
    /// narrowness is the whole safety argument.</b> No plan, no runner, nothing able to construct a
    /// writer - so this cannot grow into a second road to a write that skips a plan, which is what
    /// `PlanOnlyGuards` exists to prevent. Whatever arrives here has been started by somebody else
    /// or never completes at all, and this window only ever waits on it.
    ///
    /// <b>The precedent is a day old and is the same shape.</b> <see cref="ViewModels.Planned"/>
    /// takes its elevation from outside rather than asking the session, because a test on an
    /// elevated machine would otherwise pass without the code doing anything. Here the reason is
    /// the mirror of that one: a test cannot produce the state at all, rather than always producing
    /// the same one.
    /// </summary>
    internal void TakeThisAsARun(Task running, CancellationTokenSource stopping)
    {
        _running = running;
        _stopping = stopping;
    }

    /// <summary>
    /// The window refuses to disappear while it is half way through changing a machine.
    ///
    /// <b>THE STATE (T) OF RULE 10, AND THE ONE THIS PROJECT HAS ALREADY PAID FOR ONCE.</b> WPF
    /// ends the process when the last window closes, so without this a run interrupted by somebody
    /// reaching for the corner of the window leaves a cascade switched off and prints nothing at
    /// all - which is exactly what two presses of Ctrl+C used to do in the command line, found by a
    /// run on a virtual machine rather than by thinking about it.
    ///
    /// So the close is refused ONCE, the run is asked to stop, and the close is asked for again
    /// when it has. The steps that give back what earlier steps took still run, because that is what
    /// asking to stop means here - the same first level the command line offers, and deliberately
    /// not its second.
    /// </summary>
    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_running is not { IsCompleted: false } running)
        {
            base.OnClosing(e);

            return;
        }

        // BASE IS NOT CALLED ON THE REFUSED ATTEMPT, SINCE 2026-09-03 - backlog 308(e). Calling it
        // is what raises the Closing event, and the column layout is written from a handler on that
        // event - so a close refused half way through a run wrote the file, and the close that
        // followed wrote it again. Two writes of one layout, one of them while the window is not
        // going anywhere, and both through a path that has nowhere to report a failure.
        //
        // Skipping it here is the ordinary shape of a cancelled close rather than a shortcut: the
        // event means "this window is closing", and after this line it is not.
        e.Cancel = true;
        _stopping?.Cancel();

        await running.ConfigureAwait(true);

        // The run is over, so the check above lets it through this time.
        Close();
    }

    /// <summary>
    /// Asks the run in flight to stop before its next step, and does nothing when there is none.
    ///
    /// <b>A method rather than the constructor reaching into the field, and that is the file
    /// boundary doing its job rather than ceremony.</b> The interrupt is wired where the panel is
    /// wired, which is construction and belongs in the other file - but the field it works on has
    /// its whole story here, including that the steps giving back what earlier ones took still run.
    /// Reached from two files, that story would be told in one and relied on in the other.
    ///
    /// Not async: cancelling is instant, and the waiting belongs to whoever is awaiting the run.
    /// </summary>
    internal void AskTheRunToStop() => _stopping?.Cancel();
}
