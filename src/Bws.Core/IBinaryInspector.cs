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
/// It is also the seam ADR-13 needs. The second pass is expensive - about 12 s of processor
/// over 797 entries and 531 distinct files on 2026-09-28, against 107-113 ms of clock for
/// everything else the listing does - so it has to be possible to build a listing without one of these
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

    /// <summary>
    /// The inspector ONE pass of the second phase asks through - backlog 468, 2026-09-29.
    ///
    /// <b>Whatever an inspector remembers between files lives as long as one pass and no longer.</b>
    /// The Windows one keeps the publisher of every catalogue it has opened, and the window holds
    /// one inspector for its whole life - so a catalogue replaced under the same name went on
    /// showing its old publisher until the window closed, F5 or no F5. Since the same day F5 is
    /// promised to verify everything again (`ADR-13`, the owner's S-1 decision), which a memory
    /// older than the F5 would quietly break.
    ///
    /// <b>This rather than the caller clearing the memory</b>, because a pass is several threads
    /// asking at once and the details panel can ask about one entry while a pass is out. A fresh
    /// inspector per pass shares nothing with the one before it, so there is no moment at which a
    /// clear could land in the middle of somebody else's question. An inspector that remembers
    /// nothing answers with itself.
    /// </summary>
    IBinaryInspector ForOnePass() => this;
}
