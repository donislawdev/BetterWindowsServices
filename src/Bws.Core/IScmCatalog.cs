namespace Bws.Core;

/// <summary>
/// The parts of an entry that move on their own, and nothing else.
/// </summary>
/// <param name="ServiceName">
/// Which entry this is about. The internal name, because that is the identity (`ADR-14`) -
/// a display name is translated and a person could rename two entries into one string.
/// </param>
/// <param name="Status">What it is doing right now.</param>
/// <param name="ProcessId">
/// Absent for anything not running. Zero is a value and "not running" is not, so the two do
/// not share a spelling.
/// </param>
/// <remarks>
/// Three members rather than a whole entry, and the shortness is the point. Everything else
/// an entry carries is configuration - somebody has to change it deliberately - and reading
/// it costs a handle per entry. This is what a window watching a machine needs between one
/// second and the next.
/// </remarks>
public readonly record struct ScmStatus(string ServiceName, EntryStatus Status, Reading<int> ProcessId);

/// <summary>
/// Everything the rest of the tool is allowed to know about the service control
/// manager. Deliberately narrow (ADR-10): this is the seam where a test double
/// replaces the real system, so every method added here has to be worth doubling.
///
/// A double standing in for this must be able to produce every case in
/// 05-PRZYPADKI-BRZEGOWE, and must be able to tell those cases apart. A double that
/// returns the same value for the service name and the display name would let a test
/// about identity pass while checking nothing.
/// </summary>
public interface IScmCatalog
{
    /// <summary>
    /// Every entry the manager knows about: services and drivers alike.
    ///
    /// Reading configuration can be refused per entry, which is ordinary rather than
    /// exceptional. Those entries still come back, carrying <see cref="ReadOutcome.Denied"/>
    /// in the fields that could not be read. Dropping them, or returning them as if the
    /// values were absent, would produce a listing that looks complete and is not.
    /// </summary>
    IReadOnlyList<ScmEntry> ReadAll();

    /// <summary>
    /// What every entry is doing right now, from a single enumeration.
    ///
    /// The cheap half of <see cref="ReadAll"/>, and it exists because a window watching a
    /// machine asks this question over and over while the answer to everything else stays
    /// still. The manager hands the whole set over in one call - no handle is opened per
    /// entry, which is where the rest of a full reading spends its time.
    ///
    /// Covers drivers as well as services, which is not a detail: on the machine this was
    /// built against, drivers are 472 of 810 entries, and the notification API Windows offers
    /// instead of asking says outright that it cannot report on them.
    ///
    /// An entry appearing or disappearing shows up here as a name arriving or going, but the
    /// rest of what a new entry needs is configuration and is not in this answer. Whoever
    /// notices a name they do not know has to ask for a full reading.
    /// </summary>
    IReadOnlyList<ScmStatus> ReadStatuses();

    /// <summary>
    /// The names of the entries that break if this one stops.
    ///
    /// Asked of the manager rather than worked out by inverting what everything declares,
    /// and the reason is not convenience. An entry can declare a load order group instead
    /// of a service - RemoteAccess declares "+NetBIOSGroup" on the machine this was
    /// measured on - and the declaration says nothing about who belongs to that group.
    /// The manager knows. We would be guessing.
    ///
    /// Whether the answer already reaches past the first hop is recorded in
    /// 05-PRZYPADKI-BRZEGOWE, because it decides whether a cascade has to recurse.
    /// </summary>
    Reading<IReadOnlyList<string>> ReadDependents(string serviceName);
}
