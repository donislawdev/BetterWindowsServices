namespace Bws.Gui;

/// <summary>
/// A call into the shell, made on a thread of its own and waited for no longer than a person
/// should be left without an answer.
///
/// <b>WHY IT EXISTS - a review of 2026-09-23, and the argument is structural rather than
/// measured.</b> Pressing Donate asks another process to do something: the desktop's shell in an
/// administrator's window, the shell's own start of a browser in any other. Finding the desktop
/// took 22-28 ms on the machine this was written on, five runs after a first one thrown away, and
/// neither call carries a time limit of its own - so a shell that stopped answering would have
/// stopped this window with it, on the thread that draws it. A hung shell was not reproduced here.
/// What is guarded is that one could no longer take the window down.
///
/// <b>Three properties, each with a test in ExternalLinksGuards.</b> The work runs in a single
/// threaded apartment of its own, because the shell's objects expect one and the window's thread
/// was one - a pool thread is not. A press that finds the previous one still with the shell joins
/// it rather than starting another, because a person who sees nothing happen presses again, and a
/// shell that came back would then open the page once per press. And after the bound the press
/// says so, while the thread is left to finish by itself - a background thread, so a hand-over the
/// shell never answers holds nothing open when the window closes.
///
/// <b>What it does not do, said rather than left to be found.</b> An answer arriving after the
/// bound is dropped, since the sentence the press gave already says the page may still open. So is
/// a throw that late, which no window handler can reach any more - the one exception here that
/// reaches nobody, and it needs a shell that first hangs and then breaks its own contract.
/// </summary>
internal sealed class ShellHandover(Func<string?> work, TimeSpan patience, string whenSlow)
{
    /// <summary>
    /// How long a press waits before saying the shell has not answered. Ten seconds is Nielsen's
    /// limit for keeping attention on a task - docs/11 section 7 holds this window to it already -
    /// and about four hundred times what finding the desktop took when it answered.
    /// </summary>
    internal static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The hand-over still with the shell, if one is. Read and written on the window's thread only,
    /// which is what lets it go without a lock - the thread doing the work never touches it.
    /// </summary>
    private Task<string?>? _running;

    /// <summary>
    /// Hands the work over, or joins the hand-over still out, and returns what it said - or the
    /// slow sentence once the bound has passed.
    /// </summary>
    internal async Task<string?> Ask()
    {
        if (_running is not { IsCompleted: false })
        {
            _running = OnItsOwnThread(work);
        }

        var running = _running;

        return await Task.WhenAny(running, Task.Delay(patience)).ConfigureAwait(true) == running
            ? await running.ConfigureAwait(true)
            : whenSlow;
    }

    private static Task<string?> OnItsOwnThread(Func<string?> work)
    {
        var handover = new Task<string?>(work);

        // RUN ON THE NEW THREAD, AND WHATEVER IT THROWS STAYS IN THE TASK. So a throw reaches the
        // press that awaits it, and through the press the window's last line in Mishaps, exactly as
        // it did when this ran on the window's thread - rather than ending the process from a thread
        // with nothing above it to catch. A task never queued runs inline on the thread that asks,
        // and the test on the apartment is what would notice if it ever went to the pool instead.
        var thread = new Thread(() => handover.RunSynchronously(TaskScheduler.Default))
        {
            IsBackground = true,
            Name = nameof(ShellHandover)
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return handover;
    }
}
