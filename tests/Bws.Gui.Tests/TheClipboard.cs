// Explicit, because UseWPF swaps the implicit using set.
using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Everything these guards need in order to watch the real clipboard, kept apart from the
/// guards themselves.
///
/// <b>Split out on 2026-08-26 because the size ratchet fired, and the seam was already named
/// in the file it left: CopyingAndPanelGuards held copying AND the panel.</b> What moved here
/// is the apparatus - establishing that the clipboard is this process to begin with, reading it
/// back, and deciding when an attempt proved nothing. What stayed is the guards that use it.
///
/// <b>The whole of it exists because the clipboard belongs to whoever grabbed it last</b>, so a
/// run here has three outcomes rather than two: the text arrived, the window said it could not,
/// or this process never had the clipboard to watch. Backlog 197 and 206.
/// </summary>
internal static class TheClipboard
{
    /// <summary>
    /// Either the text arrived on the clipboard, or the window said out loud that it could not.
    /// Silence is the failure, and it is the only one - see the two-sided argument at the top.
    /// </summary>
    private static bool Landed(MainViewModel model, string wanted, string how, string saidBefore)
    {
        var onIt = OnTheClipboard();
        var said = WpfHost.On(() => model.Says.Problem);

        if (string.Equals(onIt, wanted, StringComparison.Ordinal))
        {
            return true;
        }

        // AGAINST WHAT IT SAID BEFORE, rather than against empty, and that is not a refinement.
        // A refusal outlives the action that caused it - Says.Problem says so in its own comment -
        // so over the four menu items one genuine refusal would have stood there answering for
        // every item after it. Comparing with the line as it was a moment ago asks whether THIS
        // action said something.
        if (!string.Equals(said, saidBefore, StringComparison.Ordinal))
        {
            return true;
        }

        Assert.False(
            string.Equals(onIt, Sentinel, StringComparison.Ordinal),
            $"{how} put nothing on the clipboard and the window said nothing about it. "
            + $"Wanted <{Shorten(wanted)}>, and the sentinel is still sitting there untouched, "
            + "so this process still owned the clipboard and the copy simply did nothing.");

        // Neither the answer, nor a refusal, nor the sentinel - so something else has written to
        // the clipboard since it was set, and nothing here is evidence about this product.
        Assert.True(
            onIt.Length == 0,
            $"{how} put the WRONG text on the clipboard. Wanted <{Shorten(wanted)}>, "
            + $"clipboard holds <{Shorten(onIt)}>.");

        return false;
    }

    /// <summary>
    /// Asks for the copy, and judges it only while this process still owns the clipboard.
    ///
    /// <b>THE THIRD OUTCOME, AND IT COST A RED RUN ON 2026-08-18 - backlog 197.</b> The assertion
    /// above has two sides on purpose and both are about what the PRODUCT did: the text arrived, or
    /// the window said it could not. It had no third case for the clipboard READ failing - and when
    /// that happens the write had already succeeded, so the window had nothing to complain about
    /// and the test went red for something nobody caused. Measured at one run in seven.
    ///
    /// <b>Retried rather than tolerated, because tolerating it is how a guard stops guarding.</b>
    /// An assertion that passed whenever the clipboard was busy would also pass on a machine where
    /// copying was broken outright. So each attempt either proves something or proves nothing, and
    /// only proving nothing three times running is reported - as the environment, in those words.
    /// </summary>
    internal static async Task Copies(MainViewModel model, string wanted, string how, Func<Task> ask)
    {
        for (var attempt = 1; ; attempt++)
        {
            var ours = OursNow();
            var saidBefore = WpfHost.On(() => model.Says.Problem);

            await ask();
            WpfHost.Settled();

            if (ours && Landed(model, wanted, how, saidBefore))
            {
                return;
            }

            // A THIRD VERDICT RATHER THAN A PASS OR A FAILURE, and this project already has the
            // shape: window-journey answers `held` and adverse.ps1 answers `inert`, both meaning
            // THIS RUN MEASURED NOTHING. Passing here would be a guard reporting success for work
            // it never watched; failing would blame the window for another process holding the
            // clipboard. Skipped shows up in the run's own count, so it cannot go quiet.
            //
            // Silence is still a failure and still gets here first - Landed asserts on it before
            // anything reaches this line. What is skipped is only the case where the clipboard was
            // never this process's to read.
            Assert.True(
                attempt < Attempts,
                $"{how}: the clipboard belonged to another process on {Attempts} tries running, so "
                + "nothing here was proved either way. This is the machine, not the window."
                + Environment.NewLine
                + $"Holding it right now: {WhoHoldsTheClipboard()}."
                + Environment.NewLine
                + $"Each try waits for WPF's own ten write attempts before this one, so {Attempts} "
                + "tries is roughly six seconds of asking, not one.");

            // WAITING BETWEEN TRIES RATHER THAN HAMMERING, and the first version had no wait at
            // all - which made the three attempts one attempt with extra steps. Whatever holds the
            // clipboard holds it for a moment: Visual Studio and Docker Desktop were both up on the
            // machine where this was measured, and either can own it while a build is running.
            // WPF already retries the WRITE ten times at a hundred milliseconds; nothing retried
            // the READ, and this is that.
            await Task.Delay(Breath).ConfigureAwait(true);
        }
    }

    /// <summary>How many times an inconclusive attempt is worth repeating before it is reported.</summary>
    private const int Attempts = 5;

    /// <summary>How long to let somebody else finish with the clipboard before asking again.</summary>
    private static readonly TimeSpan Breath = TimeSpan.FromMilliseconds(200);

    /// <summary>The clipboard as text, or empty when it will not answer at all.</summary>
    private static string OnTheClipboard() =>
        WpfHost.On(() =>
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return string.Empty;
            }
        });

    /// <summary>
    /// Something nothing in this product would ever copy, put on the clipboard BEFORE each action.
    ///
    /// <b>THE FIRST VERSION HAD NO SENTINEL AND THE MUTATION REGISTRY CAUGHT IT, 2026-08-17.</b>
    /// The clipboard is one object shared by every test in the run, so once any test had copied the
    /// right text it stayed there - and a mutation that made the copy do NOTHING went on passing,
    /// because the assertion found the correct answer already sitting in the clipboard from the
    /// test before it. A guard reading state somebody else wrote is a guard about somebody else.
    /// </summary>
    private const string Sentinel = "bws-nothing-copied-yet";

    /// <summary>
    /// Puts the sentinel down and says whether it is really there.
    ///
    /// <b>Reading it back is the whole point, and the version that only wrote it was half of
    /// backlog 197.</b> A swallowed refusal here left the clipboard holding somebody else's text,
    /// and every judgement after it was about that text rather than about this product.
    /// </summary>
    private static bool OursNow() =>
        WpfHost.On(() =>
        {
            try
            {
                // FLUSHED, exactly as the product flushes, and the version that did not was half
                // of why this went red on a busy machine. Writing without a flush leaves this
                // process owning a live OLE object, and the very next thing the product does is
                // flush its own - so every item alternated between two kinds of ownership and the
                // read in between sometimes found neither.
                Clipboard.SetDataObject(Sentinel, copy: true);

                return Clipboard.ContainsText()
                    && string.Equals(Clipboard.GetText(), Sentinel, StringComparison.Ordinal);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // The clipboard belongs to whoever grabbed it last. Saying so is what lets the
                // caller tell "nothing was copied" from "this was never ours to watch".
                return false;
            }
        });

    private static string Shorten(string text) =>
        text.Length <= 60 ? text : text[..60] + "...";

    /// <summary>
    /// Whichever process has the clipboard open at this instant, by name, for the message that
    /// reports an inconclusive run.
    ///
    /// <b>Added 2026-08-26 because "this is the machine" is true and useless.</b> Backlog 206
    /// says these tests fail repeatably on some days and pass on others, and it names two
    /// suspects - a development environment and a container tool - without either being
    /// measured. A message naming the holder turns the next red run into a fact instead of a
    /// third sighting of the same unknown.
    ///
    /// <b>It will often say nobody, and that is not a bug in this.</b> The backlog already
    /// measured exactly that: asked straight after a run, nothing holds it. The contention
    /// happens DURING the attempt and can be over by the time this asks - so this answers about
    /// the instant it was called, and says so rather than implying more.
    ///
    /// Every failure here answers "unknown". A message helper that throws would replace the
    /// diagnosis with its own stack trace, which is worse than the sentence it was improving.
    /// </summary>
    private static string WhoHoldsTheClipboard()
    {
        try
        {
            var holder = GetOpenClipboardWindow();

            if (holder == IntPtr.Zero)
            {
                return "nobody, at the moment this was asked - the contention was during the "
                    + "attempts, not now";
            }

            _ = GetWindowThreadProcessId(holder, out var processId);

            if (processId == 0)
            {
                return $"window {holder}, whose process could not be named";
            }

            using var process = System.Diagnostics.Process.GetProcessById((int)processId);

            return $"{process.ProcessName} (process {processId})";
        }
        // Named rather than caught wholesale, because the analyser is right to insist: the two
        // that can actually arrive here both come from GetProcessById, when the process ended
        // between the handle answering and the name being asked for. That race is the ordinary
        // case on a machine where something grabbed the clipboard and let go.
        catch (Exception asking) when (asking is ArgumentException or InvalidOperationException)
        {
            return $"process {asking.GetType().Name} - it ended before it could be named";
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetOpenClipboardWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
