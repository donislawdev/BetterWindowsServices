namespace Bws.Core;

/// <summary>
/// Fills in who breaks if each entry stops.
///
/// <b>Its own pass because of one number, and it is the number in the middle.</b> Asking the
/// manager who depends on an entry takes a call per entry - measured 2026-09-28 through this pass
/// itself, five runs with the first discarded: 148-155 ms over 797 entries, 86 of them with
/// dependents and 13 refused without rights. (The 236-259 ms over 313 services that stood here from
/// 2026-09-05 came from a separate probe.) The listing this sits behind costs 107-113 ms over the
/// same 797 entries, so always running this would more than double what every F5 costs, for an answer
/// nobody had asked for. That is exactly the trade `ADR-13` refuses, and it is the same argument
/// <see cref="MemoryPass"/> and <see cref="SecondPass"/> each make with a different number.
///
/// <b>The number got smaller on 2026-09-29 and the argument with it</b> - S-5 of the external
/// performance report, backlog 466. Asked several entries at once the same pass cost 19-40 ms over
/// the same 797 entries on sixteen processors (median 21.5), against 155-167 ms one at a time in the
/// series just before it. That is a fifth to a third of a listing here, and more of one on a machine
/// with two processors, where nobody has measured it. Whether that still earns a pass of its own is the owner's question and it
/// is written down as one, not answered here.
///
/// <b>It is a DESCRIPTION rather than a measurement, unlike memory</b>, which is why this one may
/// go into a snapshot and that one may not. Who depends on a service is configuration: it reads
/// the same twice in a row and changes when somebody changes the machine, which is precisely what
/// a snapshot is for. `D1` lists what a snapshot holds and this joined it on 2026-09-06, taking
/// the schema version with it.
///
/// <b>Entries come back as new records rather than being modified</b>, for the reason the other
/// two passes give: a plan and a snapshot are evidence, and evidence does not change after it has
/// been shown.
/// </summary>
public static class RequiredByPass
{
    /// <summary>
    /// Every entry, with the names of whatever stands on it filled in.
    ///
    /// <b>One question per entry and no cache, which is the opposite of what the memory pass
    /// does.</b> There, several entries share one process, so the answer is keyed by process id
    /// and asked once. Here the question IS about this entry and no two entries share an answer -
    /// a cache would be a dictionary with one hit per key and a name for it.
    ///
    /// <b>What a refusal means, and it is not nothing.</b> The call needs a handle opened for
    /// enumerating dependents, and an entry that will not give one comes back as a refusal
    /// carrying the system's own number - so the column says "no access" rather than an empty
    /// list, and rule 8 holds. An empty list is a real answer and a common one: nothing depends
    /// on most services.
    ///
    /// <b>The whole listing, before any filtering, and unlike the memory pass this does not
    /// depend on it.</b> Memory has to see everything because it counts how many entries share a
    /// process, and a filtered list would report a shared process as private. Nothing here counts
    /// across entries, so a filtered list would simply fill fewer of them. It still takes what it
    /// is given, because every caller has the whole listing at this point and a second rule about
    /// when filtering is allowed would be a rule nobody could check.
    /// </summary>
    public static IReadOnlyList<ScmEntry> Fill(IReadOnlyList<ScmEntry> entries, IScmCatalog catalog) =>
        Fill(entries, catalog, DefaultDegreeOfParallelism);

    /// <summary>
    /// How many entries are asked about at once when nobody says otherwise.
    ///
    /// The processor count, the answer the listing and <see cref="SecondPass"/> each arrived at by
    /// sweeping. <b>This pass was NOT swept</b> - it was measured at sixteen, on one machine with
    /// sixteen processors, and nowhere else.
    /// </summary>
    public static int DefaultDegreeOfParallelism => Environment.ProcessorCount;

    /// <summary>
    /// The same, with the number of entries asked about at once given rather than defaulted.
    ///
    /// <b>It is a parameter because the guard needs it</b>, the argument written on
    /// <see cref="SecondPass"/>: "threads changed no answer" can only be checked by running both
    /// ways. Nothing in the product passes it.
    ///
    /// <b>Several at once since 2026-09-29, and each call still opens its own manager handle.</b>
    /// Both were measured side by side on 2026-09-28 over 797 entries with
    /// tools/scm-probe/required-by-timing.ps1: one after another 148-155 ms, this 15-16 ms, and every
    /// thread sharing ONE handle 23-24 ms - slower, not faster, which is the opposite of what the
    /// report proposed and of what the listing does with its own handle. So the catalogue is not
    /// touched.
    ///
    /// <b>Order is held by index</b>, the same way the listing holds it: each answer lands in the slot
    /// its entry came from, so the answer is the sequential one by construction. <b>What a caller
    /// must now bring</b> is a catalogue that can be asked from several threads at once. The Windows
    /// one opens a handle per call and keeps its last error per thread. An exception from a catalogue
    /// arrives wrapped in an AggregateException rather than as itself, and nothing catches one here
    /// by type, since the Windows catalogue answers a refusal rather than throwing it.
    /// </summary>
    public static IReadOnlyList<ScmEntry> Fill(
        IReadOnlyList<ScmEntry> entries, IScmCatalog catalog, int degreeOfParallelism)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentOutOfRangeException.ThrowIfLessThan(degreeOfParallelism, 1);

        var filled = new ScmEntry[entries.Count];

        Parallel.For(
            0,
            entries.Count,
            new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism },
            index => filled[index] = entries[index] with
            {
                RequiredBy = catalog.ReadDependents(entries[index].ServiceName)
            });

        return filled;
    }
}
