using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The window's last line: what happens when something throws where nothing was catching.
///
/// <b>THE WINDOW HAD NO SAFETY NET AT ALL UNTIL 2026-08-26, and the command line has had one since
/// it was written.</b> There, the whole of Main sits in a try, the chain of inner exceptions is
/// printed, and the process ends with a failing code - so a person gets a sentence. Here, anything
/// thrown out of a handler, a command or a continuation reached the dispatcher and took the window
/// away mid-session, with the runtime's own crash box in its place. Owner's decision, 2026-08-26:
/// say what happened and keep the window.
///
/// <b>The argument for keeping it is the same one the reading already makes.</b> Readings catches
/// broadly so a failure "arrives as a line under an empty list rather than as a window that
/// disappears" - and that protection stopped at the edge of reading the manager. Everything else a
/// person does in this window had none. MainWindow.Carrying goes to some length to keep the window
/// standing while the machine is being changed underneath it, and an unhandled throw from any
/// handler undoes exactly that.
///
/// <b>The price is named rather than hidden.</b> Carrying on after an exception nobody predicted
/// means carrying on in a state nobody has reasoned about. The alternative is a window that
/// vanishes with a person's query, their selection and an unrun plan in it, and says nothing - so
/// this is a trade rather than a free win, and it was made deliberately.
///
/// <b>WHEN THE PROCESS HAS TO END, IT SAYS WHY FIRST - owner's decision 2026-10-05, G-4 of the
/// external stability report of 2026-09-29.</b> Three ways a failure reaches no window, and until
/// that day all three ended in silence or worse:
///
/// - <b>A throw before the window has loaded.</b> The window is Application.MainWindow from its
///   constructor onwards and sets its model as DataContext early in it, so a throw from the rest of
///   the constructor was "told" to a window that would never be shown - marked handled, and a
///   process with no window was left running, waiting for a window to close that never opened. Now
///   a window that has not loaded is nobody to tell (<see cref="Listening"/>).
/// - <b>A throw on a thread of its own.</b> This file used to say a handler for that would be
///   machinery that looks like a safety net and is not one, because it cannot stop the process and
///   there is no console. Both halves are true and neither is the point: it cannot keep the process,
///   but it can SAY something before the process goes, in the one place a windowed program has left.
///   Through ExceptionHandling.SetUnhandledExceptionHandler rather than AppDomain, which
///   LayeringGuards keeps out of this assembly - measured 2026-10-05 on .NET 10.0.12: called on the
///   throwing thread, and answering false lets the process end as it would have.
/// - <b>A task nobody awaited.</b> This file also said every background piece in this window is
///   awaited. The details panel's own reading is not - Chosen keeps it for the tests and starts the
///   next one only when it is done - so a fault there vanished. It now reaches the line under the
///   list, late: the runtime reports such a task only when the collector finds it.
///
/// <b>One box.</b> The window has no dialogs, and this is the exception for the one news a window
/// cannot carry - that there is no window. <see cref="Ending"/> says why it is shown only once.
///
/// <b>BroadCatchGuards does not see this file, and that is worth knowing before trusting its
/// count.</b> That guard finds the places that catch everything by looking for the analyser
/// suppression a catch-all needs. A dispatcher handler is not a catch block, needs no suppression,
/// and catches strictly more than any of them.
///
/// <b>THE SUBSCRIPTION ITSELF HAS NO MUTATION ENTRY, AND THE REASON IS THE MECHANISM RATHER THAN
/// AN OVERSIGHT.</b> Every way of breaking it - not subscribing, or looking for the model in the
/// wrong place - ends with the exception unhandled, and an unhandled dispatcher exception does not
/// redden a test: it ends the process running it. A registry entry whose red is a dead test host
/// would take the rest of a three hundred entry run down with it. What IS held by an entry is the
/// sentence this says, and <c>MishapGuards</c> exercises the whole path - a real application, a
/// real dispatcher, a queued operation that throws - so the wiring is checked even though nothing
/// can safely prove that check can fail.
/// </summary>
internal static class Mishaps
{
    /// <summary>
    /// Puts the net under the application and under the process. Called once, from
    /// <see cref="App.OnStartup"/>.
    ///
    /// <b>The process half is here and not in the overload a test calls</b>, because there can be
    /// only one such handler per process, and a test host that took it would end with a box on the
    /// screen of whoever ran the tests.
    /// </summary>
    internal static void Arm(Application application)
    {
        Arm(application, Ending);

        System.Runtime.ExceptionServices.ExceptionHandling.SetUnhandledExceptionHandler(failure =>
        {
            Ending(failure);

            return false;
        });
    }

    /// <summary>
    /// The application half, with what happens when nobody can be told handed in - so a test can
    /// arm the real dispatcher without being shown a box.
    /// </summary>
    internal static void Arm(Application application, Action<Exception> last)
    {
        ArgumentNullException.ThrowIfNull(application);

        application.DispatcherUnhandledException += (_, failure) =>
            failure.Handled = Decided(Listening(Application.Current?.MainWindow), failure.Exception, last);

        // Late and on the collector's thread, so it goes back to the dispatcher to be said - and
        // only said: a task found unobserved at collection is no reason to end anything, so with no
        // window to tell it is left where it was found.
        TaskScheduler.UnobservedTaskException += (_, failure) =>
        {
            failure.SetObserved();

            _ = application.Dispatcher.BeginInvoke(
                new Action(() => Told(Listening(application.MainWindow), failure.Exception)));
        };
    }

    /// <summary>
    /// Says it in the window, or - when there is no window to say it in - hands it to what ends the
    /// process, and answers whether the window carries on.
    /// </summary>
    internal static bool Decided(MainViewModel? model, Exception failure, Action<Exception> last)
    {
        ArgumentNullException.ThrowIfNull(last);

        if (Told(model, failure))
        {
            return true;
        }

        last(failure);

        return false;
    }

    /// <summary>
    /// The window's own model, or nothing when there is no window that has finished arriving.
    ///
    /// <b>LOADED, AND NOT MERELY THERE - since 2026-10-05, G-4.</b> A window is MainWindow from its
    /// constructor onwards and carries its model early in it, so "there is a model" was true of a
    /// window whose constructor had just thrown and that would never be shown. Telling that window
    /// marked the failure handled and left a process with nothing on screen.
    /// </summary>
    internal static MainViewModel? Listening(Window? window) =>
        window is { IsLoaded: true, DataContext: MainViewModel model } ? model : null;

    /// <summary>
    /// The last thing the process says: one box with every cause.
    ///
    /// <b>One failure does not arrive here twice, and that was measured rather than assumed</b>
    /// (2026-10-05, .NET 10.0.12, a file-based program in the session's scratch folder): the
    /// handler for threads is called for a throw on a worker thread and NOT for one escaping the
    /// main thread - which is where a dispatcher failure nobody handled goes. So the dispatcher half
    /// says it itself, and the threads half covers the rest.
    /// </summary>
    private static void Ending(Exception failure)
    {
        MessageBox.Show(
            Texts.Of("gui.mishap.ending", string.Join(" ", Bws.Core.Causes.Of(failure))),
            Texts.Of("gui.window.title"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    /// <summary>
    /// Says what went wrong where the window already says what went wrong, and answers whether
    /// anybody was actually told.
    ///
    /// <b>The answer is what decides whether the process carries on, and it has to be honest.</b>
    /// Marking something handled when nothing said anything is the silence rule 8 forbids, with a
    /// window still on screen pretending everything worked. There is one moment where that is the
    /// case and it is not hypothetical: a throw during startup, before the window exists, which is
    /// the shape a broken language file used to have. Nothing can be told to a window that is not
    /// there, so this says no - and <see cref="Decided"/> hands the failure to the box that is the
    /// last thing the process says, since 2026-10-05.
    ///
    /// <b>Through Says rather than through a dialog</b>, because this window has no dialogs at all
    /// and one that appeared only for the worst news would be a control a person meets once. The
    /// line under the list is where this window says what it could not do, and a failure nobody
    /// predicted is the same kind of news as a failure somebody did.
    ///
    /// <b>The model is handed in rather than fetched, and that is the whole of what makes any of
    /// this checkable.</b> Reaching for the running application inside here would put the decision
    /// - say it and carry on, or say nothing and let the process end - behind a static nothing in a
    /// test can build. Split off, the decision takes an object and a test can hand it both answers.
    /// </summary>
    internal static bool Told(MainViewModel? model, Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (model is null)
        {
            return false;
        }

        // The message rather than the type or the stack. This line is read by an administrator
        // in the middle of doing something else, and the rest of it belongs in a report nobody
        // has asked for yet - backlog, rather than invented here.
        //
        // EVERY CAUSE RATHER THAN THE TOP ONE, SINCE 2026-09-03. Backlog 307 named the command
        // line and the reading, and this is the third road to the same line under the list -
        // left on failure.Message it would be the one path that says less than the other two,
        // which is how one answer becomes three answers that disagree. This net catches what
        // nobody predicted, so it is the last place to be sure of what it is holding.
        model.Says.CouldNotDo(string.Join(" ", Bws.Core.Causes.Of(failure)));

        return true;
    }
}
