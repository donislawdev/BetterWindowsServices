using Bws.Core;
using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the machine said, and whether anybody is still asking.
///
/// <b>The fifth seam the size ratchet asked MainViewModel for, taken 2026-08-18 - backlog 198.</b>
/// That file stood at exactly 500 lines, which is the wall, so the next line of any kind would have
/// reddened the guard. It was cut out here rather than shaved, because the half that was about to
/// grow is this one: the second phase of `ADR-13` - signatures and memory read in the background -
/// is background work, cancellation and a generation counter, and all three belong to reading the
/// machine rather than to showing a list.
///
/// <b>What is here is one state machine and nothing else.</b> Whether a reading is out, whether one
/// has ever finished, whether the last one failed, and the three ways a reading starts: the first
/// look, F5, and the tick that finds the machine has changed underneath it. What it does NOT own is
/// the query, the rows on screen or the words - it says something moved and hands back.
///
/// Knows nothing about WPF, exactly like the class it came out of.
/// </summary>
internal sealed class Readings
{
    private readonly IScmCatalog _catalog;
    private readonly RowIndex _index;

    /// <summary>
    /// The second half of `ADR-13`, or nothing at all when nobody handed one in.
    ///
    /// <b>Optional, and that is what keeps 800-odd existing tests measuring what they always
    /// measured.</b> A window built with a manager and a clock reads what a listing reads, exactly
    /// as before. Only a caller that also hands in these two gets the expensive families, so
    /// switching the second phase on is one decision in one place rather than a flag threaded
    /// through everything.
    /// </summary>
    private readonly IBinaryInspector? _inspector;
    private readonly IProcessMemoryReader? _reader;

    /// <summary>
    /// What the question on screen needs, asked each time rather than kept.
    ///
    /// The query lives in the view model and changes on every keystroke, so this class asks for it
    /// at the moment it matters instead of being told.
    /// </summary>
    private readonly Func<ExtraRead> _wanted;

    /// <summary>Which expensive families this window has actually read for the entries it holds.</summary>
    private ExtraRead _have;

    /// <summary>
    /// Which families have been ATTEMPTED for the entries it holds, which is not the same thing.
    ///
    /// <b>Without this a pass that fails becomes a loop.</b> The tick asks once a second whether
    /// the question needs something the window has not got, and a family that cannot be read would
    /// answer yes for ever - a full reading every second, on a machine that is already refusing.
    /// </summary>
    private ExtraRead _tried;

    /// <summary>Whether the second phase is out right now.</summary>
    private bool _filling;

    /// <summary>
    /// Where the sentences live, asked for each time rather than kept.
    ///
    /// <b>A function rather than the object, and that is not caution - it is a trap this class
    /// would otherwise walk into on its first day.</b> MainViewModel exposes Says with an init
    /// setter, and AdmissionTests replaces it AFTER construction to get a session that is not
    /// elevated. Anything holding the object handed over in the constructor would go on writing
    /// to the one nobody is reading, and every elevation sentence would quietly stop arriving.
    /// </summary>
    private readonly Func<Says> _look;

    /// <summary>What to do once entries have landed: narrow them again and show them.</summary>
    private readonly Action _settled;

    /// <summary>
    /// That the empty middle of the window may have something else to say now.
    ///
    /// A callback rather than the sentence itself, because working out which of the five faces
    /// applies needs the count of rows ON SCREEN, and that belongs to the view model. This class
    /// knows what the machine said, not what is being shown.
    /// </summary>
    private readonly Action _changed;

    private bool _reading;

    /// <summary>Whether the last reading failed outright, which is not the same as admitting gaps.</summary>
    private bool _failed;

    /// <summary>
    /// Whether any reading has ever come back with an answer. Raised once and never lowered.
    ///
    /// <b>Two versions of the empty state worked this out from something else and both were wrong
    /// the same way</b> - the flag is about now, the count is about rows, the question is about the
    /// past. Their cost is in <see cref="ListState.Of"/>. A failed reading does not raise it.
    /// </summary>
    private bool _everRead;

    internal Readings(
        IScmCatalog catalog,
        RowIndex index,
        Func<Says> look,
        Action settled,
        Action changed,
        IBinaryInspector? inspector = null,
        IProcessMemoryReader? reader = null,
        Func<ExtraRead>? wanted = null)
    {
        _catalog = catalog;
        _index = index;
        _inspector = inspector;
        _reader = reader;
        _wanted = wanted ?? (static () => ExtraRead.None);
        _look = look;
        _settled = settled;
        _changed = changed;
    }

    /// <summary>Whether the last reading failed outright.</summary>
    internal bool Failed => _failed;

    /// <summary>
    /// Which expensive families the entries on screen actually carry.
    ///
    /// Asked by the sentence that admits what an answer could not cover, because "nobody has
    /// looked at signatures" and "signatures were looked at and this is what they say" are
    /// different answers to the same query and only one of them is an apology.
    /// </summary>
    internal ExtraRead Have => _have;

    /// <summary>Whether the expensive families are being read right now.</summary>
    internal bool Filling => _filling;

    /// <summary>The sentences, fetched rather than remembered - see <see cref="_look"/>.</summary>
    private Says Says => _look();

    /// <summary>
    /// Reads the machine in full and fills the list. The first reading, and whatever F5 asks
    /// for afterwards.
    ///
    /// A second call arriving while one is out is dropped rather than queued. Without that,
    /// two presses of F5 send two readings and the one that <b>finished later</b> wins rather
    /// than the one that <b>read later</b> - so the list can settle on the older of two
    /// answers and say nothing about it. Nothing here corrupts, because every continuation
    /// comes back to the interface thread, which is precisely why the hole was invisible: it
    /// is a question of ordering rather than of two threads touching one field.
    /// </summary>
    internal async Task LoadAsync()
    {
        if (_reading)
        {
            return;
        }

        _reading = true;

        try
        {
            await LoadEverything().ConfigureAwait(true);
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>
    /// The reading itself, without the guard, because the tick already holds it when it finds
    /// out that it needs a full one.
    /// </summary>
    private async Task LoadEverything()
    {
        Says.Status = Texts.Of("gui.status.reading");

        // Said before the reading rather than after it, or the one state this announces would be
        // announced only once it had stopped being true.
        _changed();

        IReadOnlyList<ScmEntry> entries;

        try
        {
            entries = await Task.Run(_catalog.ReadAll).ConfigureAwait(true);
        }
#pragma warning disable CA1031
        // Broad, and it is the same argument as the entry point of the command line tool:
        // the failure reaches the person, in the line under the list, instead of taking the
        // window down with a dialog nobody can act on. The message is the system's, so it
        // carries its own number and its own language.
        catch (Exception failure)
        {
            Fail(failure);

            return;
        }
#pragma warning restore CA1031

        Says.Incomplete = false;
        _failed = false;

        // The families belong to THESE entries, not to the window, so a fresh listing starts with
        // none of them. Anything else would show a signature read against a file that has since
        // been replaced, which for an audit tool is worse than showing nothing.
        _have = ExtraRead.None;
        _tried = ExtraRead.None;

        // (A field holding these entries stood here until 2026-08-26, written in two places and
        // read in none. Its comment described a mechanism that does not exist - a later pass
        // filling in what was kept - while the pass three methods down is handed its entries as a
        // parameter and reads the machine again on purpose. A field nothing reads raises no
        // warning of any kind, which is why this project goes looking for them.)
        //
        // Raised here rather than where the reading ends: a reading that threw never reaches it.
        _everRead = true;

        _index.Absorb(entries);
        _settled();

        await FillAsync(entries).ConfigureAwait(true);
    }

    /// <summary>
    /// One tick of the live list: asks what is running and moves whatever moved.
    ///
    /// Driven from outside rather than by a loop in here, and that is a deliberate shape. A
    /// loop would need a thread, a cancellation and a rule about what happens when the window
    /// closes mid-read - three things to get wrong. A window that calls this on a timer needs
    /// none of them, and a test can call it whenever it likes instead of waiting for seconds
    /// to pass.
    ///
    /// The cheap reading measures 13-22 ms over 810 entries against 423-500 ms for a full one,
    /// which is what makes asking once a second reasonable rather than rude.
    /// </summary>
    internal async Task RefreshAsync()
    {
        // A tick arriving while any reading is still out is dropped rather than queued. The
        // reading is short, so this only happens when the machine is busy - and answering a
        // late tick with a second reading would make it busier.
        //
        // One flag for both kinds of reading, not two. They rebuild the same state, so two
        // flags would let a tick and an F5 overlap and leave whichever finished last on
        // screen, which is not the same thing as whichever looked last.
        if (_reading)
        {
            return;
        }

        _reading = true;

        try
        {
            // Before asking what moved, because a question the window cannot answer yet is a worse
            // thing to leave standing than a status that is one second old.
            if (WantsMore())
            {
                await LoadEverything().ConfigureAwait(true);

                return;
            }

            IReadOnlyList<ScmStatus> statuses;

            try
            {
                statuses = await Task.Run(_catalog.ReadStatuses).ConfigureAwait(true);
            }
#pragma warning disable CA1031
            // Same argument as above, and it earns its place here rather than inheriting it:
            // this runs unattended once a second, so an exception nobody caught would take the
            // window down while its owner was somewhere else entirely.
            catch (Exception failure)
            {
                Fail(failure);

                return;
            }
#pragma warning restore CA1031

            Says.Incomplete = false;
            _everRead = true;

            switch (_index.Absorb(statuses))
            {
                case Freshening.CompositionChanged:
                    // The unguarded one, because the guard above is already held. Calling the
                    // public entry point here would find its own flag raised and quietly do
                    // nothing, which is the sort of deadlock-by-politeness that looks like the
                    // machine simply never installing anything.
                    await LoadEverything().ConfigureAwait(true);
                    break;

                case Freshening.Moved:
                    _settled();
                    break;

                default:
                    break;
            }
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>
    /// Whether the list is showing the very first look rather than an answer.
    ///
    /// <b>A pair rather than one flag.</b> One says a reading is out, the other that one finished
    /// before now, and each alone has been tried here and got it wrong - see <see cref="ListState.Of"/>.
    /// <b>The flag no longer recomputes the face when it drops, and that line was deleted rather
    /// than left, 2026-08-18.</b> It could not change an answer: every path that lowers the flag
    /// has already raised <see cref="_everRead"/> or set the failed flag, so this reads false
    /// either way. Its mutation entry was retired with it - a line no test can distinguish has
    /// nothing behind it.
    /// </summary>
    internal bool FirstLook => _reading && !_everRead;


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
            Says.CouldNotDo(failure.Message);
            _settled();

            return;
        }
#pragma warning restore CA1031

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

    private void Fail(Exception failure)
    {
        Says.Status = Texts.Of("gui.status.failed", failure.Message);
        Says.Incomplete = true;
        _failed = true;

        _changed();
    }
}
