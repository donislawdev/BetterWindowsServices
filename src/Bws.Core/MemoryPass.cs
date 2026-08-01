namespace Bws.Core;

/// <summary>
/// Fills in what each entry's process is using.
///
/// Its own pass rather than part of the listing, and the reason is not cost - measured on
/// 2026-08-01, the whole pass costs 5-8 ms over 810 entries and 110 processes, beside the
/// 476-551 ms the listing already spends. The calls themselves are under a millisecond and
/// the rest is building 810 new records, which is worth separating: the probe measured only
/// the calls and the figure it gave was wrong by a factor of six.
///
/// It is a pass of its own because it is the only thing here that is a measurement rather
/// than a description: everything else reads the same twice in a row, and this does not. A plain listing therefore stays a statement
/// about how the machine is configured, and a number that goes stale the moment it is
/// printed appears only when somebody asks for it.
///
/// Entries come back as new records rather than being modified, for the same reason
/// <see cref="SecondPass"/> does it that way: a plan and a snapshot are evidence, and
/// evidence does not change after it has been shown.
/// </summary>
public static class MemoryPass
{
    /// <summary>
    /// Every entry, with its process memory filled in.
    ///
    /// One question per process, not per entry, and the count of entries behind each
    /// process is worked out here because this is the only place that can see it. That
    /// count travels inside the answer rather than beside it - five services sharing one
    /// process each reporting 36 MB without saying so would let anybody adding the column
    /// up get five times the truth.
    ///
    /// Must be given the whole listing, before any filtering. A filtered list would count
    /// only the entries that survived the filter, so a query for one service in a shared
    /// process would report it as having the process to itself. That is the one way this
    /// can quietly produce a wrong number, so it is said here and pinned by a test.
    /// </summary>
    public static IReadOnlyList<ScmEntry> Fill(IReadOnlyList<ScmEntry> entries, IProcessMemoryReader reader)
    {
        var sharedBy = new Dictionary<int, int>();

        foreach (var entry in entries)
        {
            if (entry.ProcessId.IsPresent)
            {
                sharedBy[entry.ProcessId.Value] = sharedBy.GetValueOrDefault(entry.ProcessId.Value) + 1;
            }
        }

        var readings = new Dictionary<int, Reading<ProcessMemory>>();
        var filled = new List<ScmEntry>(entries.Count);

        foreach (var entry in entries)
        {
            filled.Add(entry with { Memory = For(entry, reader, sharedBy, readings) });
        }

        return filled;
    }

    private static Reading<ProcessMemory> For(
        ScmEntry entry,
        IProcessMemoryReader reader,
        Dictionary<int, int> sharedBy,
        Dictionary<int, Reading<ProcessMemory>> readings)
    {
        switch (entry.ProcessId.Outcome)
        {
            case ReadOutcome.Absent:
                // Nothing is running, so there is no process to use memory. A fact about the
                // entry rather than something we failed to find out, and the ordinary case:
                // 691 entries of 810 on the machine this was measured on.
                return Reading<ProcessMemory>.Absent();

            case ReadOutcome.Present:
                var processId = entry.ProcessId.Value;

                if (!readings.TryGetValue(processId, out var reading))
                {
                    reading = reader.Read(processId);
                    readings[processId] = reading;
                }

                return reading.IsPresent
                    ? Reading<ProcessMemory>.Present(reading.Value! with { SharedBy = sharedBy[processId] })
                    : reading;

            default:
                // Nobody read the process id, so nobody can ask what its process is using.
                //
                // No branch for a refused process id, and that is deliberate rather than an
                // oversight. The id arrives in the enumeration buffer with the name and the
                // status, so an entry that is in the listing at all has one - present when
                // something is running and absent when nothing is. A branch here would be a
                // reason nobody could ever read and nobody could ever check, which is the
                // mistake this project already made once in RunsAgainstItsStartType.
                return Reading<ProcessMemory>.NotRead();
        }
    }
}
