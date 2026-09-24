using Bws.Core;
using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The three expensive families for ONE entry, read when the details panel opens on it - UX-GUI-005.
///
/// <b>WHY THE PANEL READS AT ALL, and the number decided it.</b> Until 2026-09-24 the panel showed
/// "not read" beside the signature, the memory and who depends on an entry, and sent the reader to
/// turn on a column - which reads every entry on the machine, 4620-7656 ms for the signatures over
/// 810. The owner's rule was that the panel reads for itself if the cost allows, and the cost was
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

    /// <inheritdoc />
    public ExtraRead Claim(EntryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var entry = row.Entry;
        var claimed = _claimed.GetValueOrDefault(row.ServiceName);

        var missing =
            (entry.Signature.Outcome == ReadOutcome.NotRead ? ExtraRead.Signatures : ExtraRead.None)
            | (entry.Memory.Outcome == ReadOutcome.NotRead ? ExtraRead.Memory : ExtraRead.None)
            | (entry.RequiredBy.Outcome == ReadOutcome.NotRead ? ExtraRead.RequiredBy : ExtraRead.None);

        var owed = missing & Available & ~claimed;

        if (owed != ExtraRead.None)
        {
            _claimed[row.ServiceName] = claimed | owed;
        }

        return owed;
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

        var read = await Task.Run(() => One(before, everything, families)).ConfigureAwait(true);

        // ANSWERED FOR ENTRIES THAT NO LONGER EXIST, or for a window nobody is looking at: dropped.
        // The panel asks again for the fresh entry, because the new listing cleared what it claimed.
        if (_gone || listing != _listing)
        {
            return;
        }

        _index.AbsorbOne(Carry(row.Entry, before, read, families));
    }

    /// <summary>Called wherever the index takes a whole list - see <see cref="_listing"/>.</summary>
    private void TookAWholeList()
    {
        _listing++;
        _claimed.Clear();
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
    private static ScmEntry Carry(ScmEntry now, ScmEntry before, ScmEntry read, ExtraRead families)
    {
        var carried = now;

        if (families.HasFlag(ExtraRead.Signatures))
        {
            carried = carried with { Signature = read.Signature, FileVersion = read.FileVersion, BinaryHash = read.BinaryHash };
        }

        if (families.HasFlag(ExtraRead.Memory)
            && now.ProcessId.Outcome == before.ProcessId.Outcome
            && now.ProcessId.Value == before.ProcessId.Value)
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
