using System.Windows;
using System.Windows.Threading;
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
/// <b>What is deliberately NOT here: a handler on AppDomain.UnhandledException.</b> It cannot stop
/// the process, and in a windowed program there is usually no console for it to write to, so it
/// would be machinery that looks like a safety net and is not one. What it would cover -
/// a throw on a thread of its own - is already held down by BackgroundWorkGuards, which is why
/// every background piece in this window is awaited and therefore comes back to the dispatcher.
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
    /// Puts the net under the application. Called once, from <see cref="App.OnStartup"/>.
    /// </summary>
    internal static void Arm(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        application.DispatcherUnhandledException += Caught;
    }

    private static void Caught(object sender, DispatcherUnhandledExceptionEventArgs failure)
    {
        failure.Handled = Told(Looking(), failure.Exception);
    }

    /// <summary>
    /// The window's own model, or nothing when there is no window yet.
    ///
    /// <b>The only line here that needs a running application</b>, which is why it is on its own:
    /// everything the decision below does can then be checked by a test, and this cannot be. See
    /// <see cref="Told"/> for what "nothing" means at the moment it happens.
    /// </summary>
    private static MainViewModel? Looking() =>
        Application.Current?.MainWindow?.DataContext as MainViewModel;

    /// <summary>
    /// Says what went wrong where the window already says what went wrong, and answers whether
    /// anybody was actually told.
    ///
    /// <b>The answer is what decides whether the process carries on, and it has to be honest.</b>
    /// Marking something handled when nothing said anything is the silence rule 8 forbids, with a
    /// window still on screen pretending everything worked. There is one moment where that is the
    /// case and it is not hypothetical: a throw during startup, before the window exists, which is
    /// the shape a broken language file used to have. Nothing can be told to a window that is not
    /// there, so this says no and the runtime ends the process as it did before.
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
        model.Says.CouldNotDo(failure.Message);

        return true;
    }
}
