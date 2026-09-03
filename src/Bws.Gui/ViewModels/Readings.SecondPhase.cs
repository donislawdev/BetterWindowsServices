using Bws.Core;
using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The second phase of `ADR-13`: the families a listing costs too much to know up front.
///
/// <b>Its own file since 2026-09-03, and the size ratchet is what asked.</b> The seam is a subject
/// rather than a line count, and it is the one the class this came out of already named: Readings
/// opens by saying it holds "one state machine and nothing else" - a reading is out, one has
/// finished, the last one failed - and everything here is about the second, expensive question
/// asked only when somebody's query needs the answer.
///
/// <b>The measurement that decides the whole design is in these two methods rather than in the
/// state machine</b>: the pass costs about seven and a half seconds of processor and eighteen
/// megabytes over 810 entries, so it is asked for rather than always run, and what has been TRIED
/// is remembered separately from what was HAD so that a pass which fails does not become a loop.
///
/// <b>What deliberately stays behind: the flag saying whether a reading is out at all.</b> This
/// pass runs INSIDE a reading, and the argument at <see cref="Readings.FillAsync"/> is that the
/// reentrancy guard over there is what makes a second concurrency mechanism unnecessary here.
/// Splitting the guard away from the thing it guards would leave that argument in one file and the
/// code relying on it in another.
/// </summary>
internal sealed partial class Readings
{
    /// <summary>
    /// The second phase of `ADR-13`: what a listing costs too much to know up front.
    ///
    /// <b>INSIDE THE READING RATHER THAN ALONGSIDE IT, and that is the whole design.</b> The
    /// obvious shape is to start this in the background and let the tick carry on, which needs a
    /// cancellation, a generation counter and a rule for what happens when the composition of the
    /// list changes underneath a pass that is still out. None of that is needed here: LoadAsync and
    /// RefreshAsync both refuse to start while a reading is out, so for as long as this is running
    /// nothing else can touch the index. The reentrancy guard that already existed is what makes
    /// the race impossible rather than handled.
    ///
    /// <b>It is awaited, which BackgroundWorkGuards requires and which is also just true</b> - work
    /// started and walked away from either silently does not happen or silently fails, and this one
    /// runs unattended once a machine is left alone with the window open.
    ///
    /// <b>The price is named rather than hidden: the tick is suppressed for as long as this takes.</b>
    /// Status and process ids stop moving until it returns, on every full reading. That is the
    /// trade this shape makes in exchange for not having a second concurrency mechanism, and it is
    /// the number to measure before deciding this should run on every F5.
    ///
    /// Both families in one pass because they are read from the same list and written into the same
    /// records. They are nothing alike in cost - seconds against under a millisecond - which is why
    /// the command line asks for them separately, but a window that has decided to pay for one has
    /// no reason to make a second decision about the other.
    /// </summary>
    private async Task FillAsync(IReadOnlyList<ScmEntry> entries)
    {
        if (_inspector is null || _reader is null || entries.Count == 0 || !Asked())
        {
            return;
        }

        // Marked as attempted before the work rather than after it, so a pass that throws is not
        // asked for again a second later - see the argument on _tried.
        _tried |= _wanted();

        _filling = true;

        // Said before the work rather than after it, or the one state this announces would be
        // announced only once it had stopped being true - the same argument as the reading above.
        _settled();

        IReadOnlyList<ScmEntry> filled;

        try
        {
            filled = await Task
                .Run(() => MemoryPass.Fill(SecondPass.Fill(entries, _inspector), _reader))
                .ConfigureAwait(true);
        }
#pragma warning disable CA1031
        // Broad, and for a narrower reason than the reading above. Every file this opens answers
        // for itself - a refusal or a malformed binary comes back as a Reading rather than as a
        // throw - so anything arriving here is the pass itself failing, and the list on screen is
        // already good. It must not take the window down, and it must not be reported as the
        // reading having failed either, because the reading succeeded.
        catch (Exception failure)
        {
            _filling = false;

            Says.Incomplete = true;

            // The same reason as at Fail below, and this path needs it more rather than less: the
            // second pass verifies signatures several at a time, so a failure here is the one most
            // likely to arrive wrapped.
            Says.CouldNotDo(string.Join(" ", Causes.Of(failure)));
            _settled();

            return;
        }
#pragma warning restore CA1031

        if (_gone)
        {
            return;
        }

        _filling = false;
        _have = ExtraRead.Signatures | ExtraRead.Memory;

        // WHAT WAS READ COUNTS AS TRIED, AND UNTIL 2026-08-26 ONLY WHAT WAS ASKED DID. The pass
        // above fills BOTH families whatever the question wanted - they come off the same list and
        // go into the same records - so a window that has run it once holds signatures and memory
        // for these entries either way.
        //
        // Marking only the asked one meant the second question paid for both again. Somebody types
        // signed:no, the pass runs, both families are read. They then type memory:>500MB, which is
        // a family not in _tried, so WantsMore says yes and the next tick reads the whole manager
        // again and verifies all 544 signatures a second time - measured at 7.5 s of processor and
        // 18 MB, for an answer already sitting in the rows. The tick is suppressed for the whole of
        // it, so the list stops moving as well.
        _tried |= _have;

        _index.Absorb(filled);
        _settled();
    }

    /// <summary>
    /// Whether the question on screen needs something this window has not tried to read yet.
    ///
    /// <b>THE PASS IS ASKED FOR RATHER THAN ALWAYS RUN, and the number behind that is the whole
    /// reason - owner's decision, 2026-08-18.</b> Measured on this machine over about 810 entries,
    /// four launches each with the first discarded: a window that always ran it spent 8.86-9.42 s
    /// of processor against 1.39-1.42 s without, and 171 MB against 153. The spreads do not touch,
    /// so the pass costs roughly seven and a half seconds of processor and eighteen megabytes -
    /// on every F5 and on every change to what is installed, for an answer nobody had asked for.
    ///
    /// Asking for it is asking a question about it. `signed:no` in the box is somebody wanting
    /// signatures, and that is the moment to go and get them.
    /// </summary>
    private bool Asked()
    {
        var wanted = _wanted();

        return wanted != ExtraRead.None && (wanted & ~_tried) != ExtraRead.None;
    }
    /// <summary>
    /// Whether the question on screen has outrun what the window went and read.
    ///
    /// <b>Answered by reading the machine again rather than by filling in what is held.</b> The
    /// entries kept from the last full reading are older than the rows on screen - a tick has been
    /// writing statuses into them since - so absorbing them would roll those changes back. Reading
    /// again costs about half a second on top of a pass that costs seconds, and it is the
    /// difference between a fresh answer and a stale one.
    /// </summary>
    internal bool WantsMore() => _inspector is not null && _reader is not null && Asked();

}
