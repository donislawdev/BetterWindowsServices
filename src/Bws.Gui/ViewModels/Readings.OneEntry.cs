using Bws.Core;
using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The three expensive families for ONE entry, read when the details panel opens on it - UX-GUI-005.
///
/// <b>WHY THE PANEL READS AT ALL, and the number decided it.</b> Until 2026-09-24 the panel showed
/// "not read" beside the signature, the memory and who depends on an entry, and sent the reader to
/// turn on a column - which reads every entry on the machine, about 12 s of processor for the
/// signatures over 797 (2026-09-28). The owner's rule was that the panel reads for itself if the cost allows, and the cost was
/// measured before anything was built (tools/details-probe, 797 entries, five counted runs, each in
/// a fresh process): the three families for one typical service 46.7-66.9 ms the first time a
/// process asks, the signature family 6-13 ms every time after, and 425-518 ms for the one 98 MB
/// driver on the machine. The command line's `show` verb already read this way, over one entry.
///
/// <b>BESIDE THE READING GUARD, NOT INSIDE IT, and that is the difference from the second phase.</b>
/// That phase runs inside the reading and stops the tick for its seconds - the price it names at
/// its own head. This one is tens of milliseconds and must not stop the list, so it runs beside
/// the guard and is kept honest by two things instead: an answer is written only if the index is
/// still holding the listing it was read against, and it is written into the one row it is about
/// through <see cref="RowIndex.AbsorbOne"/> - never through the whole-list absorb, which removes
/// every row it is not handed.
///
/// <b>INTO THE ROW RATHER THAN ONLY INTO THE PANEL</b>, because the panel and a copy must say the
/// same thing about the same entry - Details.AsText carries that rule. <b>And never into
/// <c>_have</c></b>: one filled row does not answer a question about the whole list, so a query about
/// signatures still asks for the second phase exactly as before.
/// </summary>
internal sealed partial class Readings : IEntryReads
{
    /// <summary>
    /// Which whole listing the rows hold. Raised every time the index takes a whole list - a full
    /// reading AND the second phase - because either replaces the entries the panel read against.
    ///
    /// <b>Both, and the second is the one that is easy to miss.</b> The second phase absorbs the
    /// listing with only the families somebody asked for, so with just the memory column on it
    /// hands every row an entry without a signature - including the one the panel had just filled.
    /// Counting only full readings would leave that signature marked as already asked for, and the
    /// panel on "not read" until the next opening.
    /// </summary>
    private int _listing;

    /// <summary>What the panel has asked for, per entry, since the index last took a whole list.</summary>
    private readonly Dictionary<string, ExtraRead> _claimed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Which process each claim on memory was made for - since 2026-10-27, logic report R07. A claim on
    /// one process's memory is no claim on the next one's: the tick moves a row to a new process and
    /// leaves its memory unread (<see cref="EntryRow.Absorb(ScmStatus, DateTimeOffset)"/>), and a claim
    /// that still stood would keep the panel from asking for it until the next whole listing.
    /// </summary>
    private readonly Dictionary<string, Reading<int>> _memoryFor = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Why the panel's reading of an entry failed, per entry, for as long as its claim stands.
    ///
    /// <b>Beside the claims and cleared with them, since the review of PR #16.</b> The failure was
    /// the panel's until then, and a reading belongs to one ENTRY: opening another entry while a
    /// reading was out dressed the new one in the old one's failure, and opening the failed one again
    /// said nothing at all - the claim stood, so nothing was read, and the lines said "not read" as
    /// though nobody had tried. Kept here, a failure is said for the entry it happened to, every time
    /// that entry is shown, until a new listing lets the panel try again.
    ///
    /// <b>With the families it was for, since the review of PR #96.</b> A claim on memory no longer
    /// stands for the whole listing (R07), so memory is read again once the process moves - and a failure
    /// that stood until the next listing went on saying the reading failed beside a figure read since. It
    /// goes once every family it was for has been read again (<see cref="Answered"/>).
    /// </summary>
    private readonly Dictionary<string, (ExtraRead Families, string Why)> _failures = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public string FailureOf(EntryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return _failures.TryGetValue(row.ServiceName, out var failed) ? failed.Why : string.Empty;
    }

    /// <summary>A reading of these families failed for this row - added to whatever it already failed at.</summary>
    private void FailedAt(EntryRow row, ExtraRead families, Exception failure) =>
        _failures[row.ServiceName] = (
            families | _failures.GetValueOrDefault(row.ServiceName).Families,
            string.Join(" ", Causes.Of(failure)));

    /// <summary>
    /// These families were read for this row: a failure they cover in full is over. One that also covered a
    /// family nobody has read since stays said - that family's line still says "not read", and why.
    /// </summary>
    private void Answered(EntryRow row, ExtraRead families)
    {
        if (_failures.TryGetValue(row.ServiceName, out var failed) && (failed.Families & ~families) == ExtraRead.None)
        {
            _failures.Remove(row.ServiceName);
        }
    }

    /// <inheritdoc />
    public ExtraRead Claim(EntryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var entry = row.Entry;
        var claimed = Claimed(row);

        var missing =
            (entry.Signature.Outcome == ReadOutcome.NotRead ? ExtraRead.Signatures : ExtraRead.None)
            | (entry.Memory.Outcome == ReadOutcome.NotRead ? ExtraRead.Memory : ExtraRead.None)
            | (entry.RequiredBy.Outcome == ReadOutcome.NotRead ? ExtraRead.RequiredBy : ExtraRead.None);

        var owed = missing & Available & ~claimed;

        if (owed != ExtraRead.None)
        {
            Stake(row, claimed | owed);
        }

        return owed;
    }

    /// <summary>
    /// The list holds every row's memory, and the tick has just moved rows to another process - read
    /// theirs now rather than leaving "not read" in a column somebody turned on until F5 (R07, 2026-10-27).
    ///
    /// <b>ONE PASS FOR EVERY MOVED ROW, since the review of PR #96.</b> The first version took the panel's
    /// path, one row at a time, and each of those is a whole <see cref="MemoryPass"/> over the listing - so
    /// a host process restarting, which moves every service it holds in one tick, cost one pass per service.
    /// The pass counts sharing over the whole listing whatever it is asked, so one answers them all.
    ///
    /// <b>A row the panel is reading as well is read here too</b>, the other half of that review. An open
    /// panel claims a moved row the moment it moves, and skipping claimed rows let the list be narrowed
    /// before the panel's answer came - which lands through AbsorbOne and narrows nothing, so a question
    /// about memory left that row out until something else moved. Asked twice, it costs one process more.
    ///
    /// <b>Inside the tick, awaited, the way the second phase runs inside a reading</b> - so the list is
    /// narrowed once, with the new figures, and no read is started and walked away from. The tick holds the
    /// reading guard, so nothing moves a process or replaces the listing while this is out.
    /// </summary>
    private async Task MemoryAgainForMovedAsync()
    {
        if (!_have.HasFlag(ExtraRead.Memory) || _reader is not { } reader)
        {
            return;
        }

        var moved = _index.Ordered.Where(row => row.Entry.Memory.Outcome == ReadOutcome.NotRead).ToList();

        if (moved.Count == 0)
        {
            return;
        }

        var everything = _index.Everything;
        var counted = await Attempt(() => MemoryPass.Fill(everything, reader)).ConfigureAwait(true);

        if (counted.Failure is { } failure)
        {
            moved.ForEach(row => FailedAt(row, ExtraRead.Memory, failure));

            return;
        }

        var byName = counted.Answer!.ToDictionary(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase);

        foreach (var row in moved.Where(row => byName.ContainsKey(row.ServiceName)))
        {
            _index.AbsorbOne(row.Entry with { Memory = byName[row.ServiceName].Memory });
            Answered(row, ExtraRead.Memory);
        }
    }

    /// <summary>What has been claimed for this row - less a claim on memory made for another process (R07).</summary>
    private ExtraRead Claimed(EntryRow row)
    {
        var claimed = _claimed.GetValueOrDefault(row.ServiceName);

        return claimed.HasFlag(ExtraRead.Memory)
               && _memoryFor.TryGetValue(row.ServiceName, out var process)
               && !EntryRow.SameProcess(process, row.Entry.ProcessId)
            ? claimed & ~ExtraRead.Memory
            : claimed;
    }

    private void Stake(EntryRow row, ExtraRead claimed)
    {
        _claimed[row.ServiceName] = claimed;

        if (claimed.HasFlag(ExtraRead.Memory))
        {
            _memoryFor[row.ServiceName] = row.Entry.ProcessId;
        }
    }

    /// <inheritdoc />
    public async Task ReadAsync(EntryRow row, ExtraRead families)
    {
        ArgumentNullException.ThrowIfNull(row);

        var listing = _listing;
        var before = row.Entry;

        // THE WHOLE LISTING FOR THE MEMORY, taken here on the window's thread: the pass counts how
        // many entries share each process, so over one entry it would call a shared svchost private
        // - MemoryPass says so where it is written. The index is fresher than the last full reading,
        // because the tick writes the process identifiers back into it.
        var everything = _index.Everything;
        var attempt = await Attempt(() => One(before, everything, families)).ConfigureAwait(true);

        // A failure is a sentence about THIS entry - not for a listing that has gone, whose entries nobody
        // will be shown again.
        if (attempt.Failure is { } failure)
        {
            if (!_gone && listing == _listing)
            {
                FailedAt(row, families, failure);
            }

            return;
        }

        var read = attempt.Answer!;

        // ANSWERED FOR ENTRIES THAT NO LONGER EXIST, or for a window nobody is looking at: dropped.
        // The panel asks again for the fresh entry, because the new listing cleared what it claimed.
        if (_gone || listing != _listing)
        {
            return;
        }

        var now = row.Entry;
        var sameProcess = EntryRow.SameProcess(now.ProcessId, before.ProcessId);

        // A RESTART BETWEEN THE QUESTION AND THE ANSWER, since the review of PR #16: the memory read
        // belongs to the old process and is dropped (Carry), and the panel asks again for the new one -
        // without that the line said "not read" until the next full reading, and Enter did nothing. The
        // claim used to be cleared here by hand. Since R07 it no longer needs to be: a claim on memory
        // remembers its process, and Claimed drops it once the process moves, which is what a restart
        // mid-reading is too. The mutation registry found the hand-clearing could be removed with no
        // test noticing (review of PR #96), so it was. It cannot spin: each round is a real reading,
        // and it stops the moment the process holds still for the few milliseconds one takes.
        _index.AbsorbOne(Carry(now, read, families, sameProcess));
        Answered(row, sameProcess ? families : families & ~ExtraRead.Memory);
    }

    /// <summary>
    /// Work for the pool, with what it threw handed back rather than thrown - the one broad catch both readings
    /// in this file share, since the review of PR #96 gave the tick a reading of its own beside the panel's.
    ///
    /// Broad, for the reason the second phase gives at its own catch: every file and every process answers
    /// for itself, so what arrives here is a reading failing as a whole. It must neither take the window down
    /// nor leave the panel implying that nobody tried - so each caller turns it into a sentence about the
    /// entries it was for, said by the panel whenever one of them is shown.
    /// </summary>
    private static async Task<(T? Answer, Exception? Failure)> Attempt<T>(Func<T> work)
        where T : class
    {
        try
        {
            return (await Task.Run(work).ConfigureAwait(true), null);
        }
#pragma warning disable CA1031
        catch (Exception failure)
        {
            return (null, failure);
        }
#pragma warning restore CA1031
    }

    /// <summary>Called wherever the index takes a whole list - see <see cref="_listing"/>.</summary>
    private void TookAWholeList()
    {
        _listing++;
        _claimed.Clear();
        _memoryFor.Clear();
        _failures.Clear();
    }

    /// <summary>
    /// The three families for one entry, through the product's own passes and in the second phase's
    /// order - files, then processes, then the manager. The same calls the command line's `show`
    /// verb makes, and what tools/details-probe measured.
    /// </summary>
    private ScmEntry One(ScmEntry entry, IReadOnlyList<ScmEntry> everything, ExtraRead families)
    {
        var one = entry;

        if (families.HasFlag(ExtraRead.Signatures))
        {
            one = SecondPass.Fill([one], _inspector!)[0];
        }

        if (families.HasFlag(ExtraRead.Memory))
        {
            // Not in the listing any more - removed between the question and the answer - leaves the
            // memory unread, which is true, rather than taking a figure from somewhere else.
            var counted = MemoryPass.Fill(everything, _reader!)
                .FirstOrDefault(candidate => string.Equals(candidate.ServiceName, one.ServiceName, StringComparison.OrdinalIgnoreCase));

            one = counted is null ? one : one with { Memory = counted.Memory };
        }

        if (families.HasFlag(ExtraRead.RequiredBy))
        {
            one = RequiredByPass.Fill([one], _catalog)[0];
        }

        return one;
    }

    /// <summary>
    /// What was read, onto the entry the row holds NOW - which the tick may have moved since, so its
    /// status and process are kept and <see cref="EntryRow.Absorb(ScmEntry, DateTimeOffset)"/> does
    /// not light the row as changed.
    ///
    /// <b>The memory only if the process is still the one that was asked.</b> A service restarted
    /// between the question and the answer has a new process, and the old one's figure against it
    /// would be a stranger's memory in the right field.
    /// </summary>
    private static ScmEntry Carry(ScmEntry now, ScmEntry read, ExtraRead families, bool sameProcess)
    {
        var carried = now;

        if (families.HasFlag(ExtraRead.Signatures))
        {
            carried = carried with { Signature = read.Signature, FileVersion = read.FileVersion, BinaryHash = read.BinaryHash };
        }

        if (families.HasFlag(ExtraRead.Memory) && sameProcess)
        {
            carried = carried with { Memory = read.Memory };
        }

        if (families.HasFlag(ExtraRead.RequiredBy))
        {
            carried = carried with { RequiredBy = read.RequiredBy };
        }

        return carried;
    }
}
