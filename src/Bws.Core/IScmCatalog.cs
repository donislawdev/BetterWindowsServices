namespace Bws.Core;

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
}
