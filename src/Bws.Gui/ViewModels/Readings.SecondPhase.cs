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
    /// <b>One pass, but only the families that were asked for - see <see cref="Fill"/>.</b> They
    /// come off the same list and go into the same records, so running them together costs one
    /// rebuild rather than two. What they do NOT share is a price: seconds against under a
    /// millisecond, which is why the command line has always asked for them separately and why
    /// this stopped filling both on 2026-09-05.
    /// </summary>
    private async Task FillAsync(IReadOnlyList<ScmEntry> entries)
    {
        if (entries.Count == 0 || !Asked())
        {
            return;
        }

        // ASKED ONCE AND CARRIED, rather than asked again at each of the four places below. The
        // question is live - a column ticked or a member typed while this pass is out changes the
        // answer - so asking twice would let this run mark as READ a family it never touched, and
        // the row would then hold "unknown" with nothing left to go and fetch it.
        //
        // NARROWED TO WHAT THIS WINDOW CAN ACTUALLY READ, which is what makes the bang safe in
        // Fill below: a family only survives this line when the thing that reads it is there.
        var wanted = _wanted() & Available;

        // Marked as attempted before the work rather than after it, so a pass that throws is not
        // asked for again a second later - see the argument on _tried.
        _tried |= wanted;

        _filling = true;

        // Said before the work rather than after it, or the one state this announces would be
        // announced only once it had stopped being true - the same argument as the reading above.
        _settled();

        IReadOnlyList<ScmEntry> filled;

        try
        {
            filled = await Task.Run(() => Fill(entries, wanted)).ConfigureAwait(true);
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

        // WHAT WAS ASKED FOR IS WHAT WAS READ, SINCE THE PASSES WERE SPLIT ON 2026-09-05, and this
        // line used to claim both families unconditionally because the pass filled both.
        //
        // That claim was true then and would be a lie now: a run asked only for memory does not
        // open a single file, so recording signatures as held would leave the four signature
        // columns showing nothing with the window convinced it had already looked. The reverse is
        // the same shape and cheaper to hit - somebody with the signature column on and no memory
        // column would have had the memory family marked as read without a process ever being
        // asked.
        //
        // THE TRAP THIS REPLACES IS STILL LIVE AND HAS MOVED INTO Fill BELOW. Until 2026-08-26
        // only the ASKED family was marked tried while both were read, so the second question
        // paid for both again: signed:no runs the pass, then memory:>500MB is a family not in
        // _tried, so the next tick re-reads the whole manager and verifies all 544 signatures a
        // second time - 7.5 s of processor and 18 MB for an answer already in the rows. Marking
        // exactly what was read keeps that closed from the other side.
        _have |= wanted;

        _index.Absorb(filled);
        TookAWholeList();
        _settled();
    }

    /// <summary>
    /// The two families, each read only if it was asked for.
    ///
    /// <b>SPLIT ON 2026-09-05, AND THE NUMBER IS THE WHOLE ARGUMENT.</b> One expression filled
    /// both, on the reasoning that a window which has decided to pay for one has no reason to
    /// decide again about the other. The two costs are not comparable: the file reading is
    /// measured at 7.5 s of processor and 18 MB over about 810 entries, and asking 110 processes
    /// what they are using is under a millisecond. So "no reason to decide again" meant the cheap
    /// answer could only ever be had at the expensive one's price - and the tick is suppressed for
    /// the whole of it, so the list stops moving too.
    ///
    /// <b>That was affordable while only a typed member could ask.</b> It stopped being
    /// affordable the moment a shown column could: turning on the memory column would have frozen
    /// the list for nine seconds to fetch a number that costs nothing, every time the layout was
    /// restored and on every F5 after.
    ///
    /// <b>Order matters and is not alphabetical.</b> The signature pass resolves the binary and
    /// writes three fields into new records, and the memory pass takes whatever list it is handed
    /// - so signatures first means one rebuild rather than two when both are wanted. Memory must
    /// still see the WHOLE listing, which it does: nothing here filters.
    /// </summary>
    private IReadOnlyList<ScmEntry> Fill(IReadOnlyList<ScmEntry> entries, ExtraRead wanted)
    {
        var filled = entries;

        if (wanted.HasFlag(ExtraRead.Signatures))
        {
            filled = SecondPass.Fill(filled, _inspector!);
        }

        if (wanted.HasFlag(ExtraRead.Memory))
        {
            filled = MemoryPass.Fill(filled, _reader!);
        }

        // THE THIRD FAMILY, 2026-09-06, and it sits between the other two in price: 236-259 ms over
        // 313 services against under a millisecond for memory and seven and a half seconds for
        // signatures. Last because it is the only one that goes back to the manager, so a run that
        // wants all three has already finished with the files and the processes by the time it
        // starts walking services one at a time.
        if (wanted.HasFlag(ExtraRead.RequiredBy))
        {
            filled = RequiredByPass.Fill(filled, _catalog);
        }

        return filled;
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
    private bool Asked() => (_wanted() & Available & ~_tried) != ExtraRead.None;

    /// <summary>
    /// The families this window has something to read them WITH.
    ///
    /// <b>PER FAMILY SINCE 2026-09-06, AND IT USED TO BE ALL OR NOTHING.</b> The pass refused to
    /// start at all unless it held both a binary inspector and a process memory reader - which was
    /// true enough while those were the only two families, and became a silent fault the moment a
    /// third arrived that needs neither. Dependents come from the service manager, which this class
    /// always has, so a window built without an inspector would have gone on answering "nobody
    /// looked" about a column it could have filled at any time.
    ///
    /// <b>What it deliberately does NOT do is pretend.</b> A family that was asked for and cannot
    /// be read stays out of <c>_have</c>, so the sentence under the list goes on saying nobody
    /// looked - which is true, and is what <c>Without_an_inspector_the_window_still_admits_it_has
    /// _not_looked</c> is about. It only stops the window from refusing to read the families it
    /// CAN.
    /// </summary>
    private ExtraRead Available =>
        (_inspector is null ? ExtraRead.None : ExtraRead.Signatures)
        | (_reader is null ? ExtraRead.None : ExtraRead.Memory)

        // Never absent: this class cannot exist without a manager to read, and the pass that uses
        // it asks the same catalogue the listing came from.
        | ExtraRead.RequiredBy;
    /// <summary>
    /// Whether the question on screen has outrun what the window went and read.
    ///
    /// <b>Answered by reading the machine again rather than by filling in what is held.</b> The
    /// entries kept from the last full reading are older than the rows on screen - a tick has been
    /// writing statuses into them since - so absorbing them would roll those changes back. Reading
    /// again costs about half a second on top of a pass that costs seconds, and it is the
    /// difference between a fresh answer and a stale one.
    /// </summary>
    internal bool WantsMore() => Asked();

}
