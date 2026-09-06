namespace Bws.Core;

/// <summary>
/// Fills in who breaks if each entry stops.
///
/// <b>Its own pass because of one number, and it is the number in the middle.</b> Asking the
/// manager who depends on an entry takes a call per entry - measured 2026-09-05 through the same
/// Win32 call reached from .NET, five runs with the first discarded: 236-259 ms over 313 services,
/// 784 dependents found and nothing refused. The listing this sits behind costs 423-500 ms over
/// 810 entries, so always running this would roughly double what every F5 costs, for an answer
/// nobody had asked for. That is exactly the trade `ADR-13` refuses, and it is the same argument
/// <see cref="MemoryPass"/> and <see cref="SecondPass"/> each make with a different number.
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
    public static IReadOnlyList<ScmEntry> Fill(IReadOnlyList<ScmEntry> entries, IScmCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(catalog);

        var filled = new List<ScmEntry>(entries.Count);

        foreach (var entry in entries)
        {
            filled.Add(entry with { RequiredBy = catalog.ReadDependents(entry.ServiceName) });
        }

        return filled;
    }
}
