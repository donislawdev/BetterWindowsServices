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
    /// The signature, the version and the hash of one file, from ONE opening of it.
    ///
    /// <b>One question rather than three, since 2026-10-06 - package SB, security report S-5.</b>
    /// Three questions were three openings, and a file replaced between two of them came back with
    /// the verdict of one file beside the hash of another. See <see cref="FileInspection"/>.
    ///
    /// <list type="bullet">
    /// <item><b>Signature</b> - who signed the file and whether the system trusts it.</item>
    /// <item><b>Version</b> - the version the file claims for itself. Absent for a file with no
    /// version resource, which is ordinary rather than broken - plenty of drivers ship without one.</item>
    /// <item><b>Hash</b> - SHA-256, lower case hexadecimal. What a snapshot compares when it wants to
    /// know whether the file itself changed. The signature answers a different question - who vouched
    /// for it - and a binary replaced by another one from the same publisher passes that unchanged.</item>
    /// </list>
    ///
    /// Absent when the path names nothing on disk, denied when it is there and cannot be opened, not
    /// read when nobody may look (another machine, or a path that is not a file on a disk). Those are
    /// different facts and the caller must be able to tell them apart: the first is about the machine,
    /// the second about our permissions, the third about what we chose not to touch.
    /// </summary>
    FileInspection Inspect(string file);

    /// <summary>
    /// The inspector ONE pass of the second phase asks through - backlog 468, 2026-09-29.
    ///
    /// <b>Whatever an inspector remembers between files lives as long as one pass and no longer.</b>
    /// The Windows one kept the publisher of every catalogue it had opened until 2026-10-06, and the
    /// window holds one inspector for its whole life - so a catalogue replaced under the same name
    /// went on showing its old publisher until the window closed, F5 or no F5. Since the same day F5
    /// is promised to verify everything again (`ADR-13`, the owner's S-1 decision), which a memory
    /// older than the F5 would quietly break. Since package SB the publisher comes out of each
    /// verification's own state and the Windows inspector remembers nothing - the seam stays, because
    /// the promise it keeps does not depend on which memory a later version adds.
    ///
    /// <b>This rather than the caller clearing the memory</b>, because a pass is several threads
    /// asking at once and the details panel can ask about one entry while a pass is out. A fresh
    /// inspector per pass shares nothing with the one before it, so there is no moment at which a
    /// clear could land in the middle of somebody else's question. An inspector that remembers
    /// nothing answers with itself.
    /// </summary>
    IBinaryInspector ForOnePass() => this;
}
