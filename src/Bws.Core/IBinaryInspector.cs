namespace Bws.Core;

/// <summary>
/// Reads what a file on disk says about itself.
///
/// Its own interface rather than more methods on <see cref="IScmCatalog"/>, and the reason
/// is the same one that split reading the manager from controlling it: these calls go to
/// the file system and to the trust providers, not to the service control manager, and they
/// need nothing the manager can grant or refuse. Something that only lists services should
/// not have to be handed an object that can open and hash every binary on the machine.
///
/// It is also the seam ADR-13 needs. The second pass is expensive - measured at 4620-7656
/// ms over 810 entries and 544 distinct files, against 476-551 ms for everything the
/// listing does today - so it has to be possible to build a listing without one of these
/// at all.
/// </summary>
public interface IBinaryInspector
{
    /// <summary>
    /// Who signed the file and whether the system trusts it.
    ///
    /// Absent when the path names nothing on disk, denied when it is there and cannot be
    /// opened. Those are different facts and the caller must be able to tell them apart:
    /// the first is about the machine, the second is about our permissions.
    /// </summary>
    Reading<BinarySignature> ReadSignature(string file);

    /// <summary>
    /// The version the file claims for itself, from its own version resource.
    ///
    /// Absent for a file carrying no version resource at all, which is ordinary rather than
    /// broken - plenty of drivers ship without one.
    /// </summary>
    Reading<string> ReadFileVersion(string file);

    /// <summary>
    /// SHA-256 of the file, lower case hexadecimal.
    ///
    /// What a snapshot compares when it wants to know whether the file itself changed. The
    /// signature answers a different question - who vouched for it - and a binary replaced
    /// by another one from the same publisher passes that check unchanged.
    ///
    /// Absent when the path names nothing on disk, denied when it is there and unreadable.
    /// </summary>
    Reading<string> ReadHash(string file);
}
