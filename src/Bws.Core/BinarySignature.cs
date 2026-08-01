namespace Bws.Core;

/// <summary>
/// What the system thinks of the signature on a file.
///
/// Named values rather than a raw number, because the difference between "nobody signed
/// this" and "somebody signed it and the certificate is not trusted here" is the whole
/// point of showing it at all - the first is ordinary for a small in-house tool and the
/// second is a finding.
///
/// Measured on a real machine on 2026-08-01 across 544 distinct files: 535 trusted and 4
/// unsigned. The other values are mapped from the documented results of the verification
/// call and are <b>NOT OBSERVED</b> on that machine - said out loud, because a value nobody
/// has ever seen produced is a value nobody has ever seen rendered either.
/// </summary>
public enum SignatureStatus
{
    /// <summary>
    /// The verification came back with something this code does not name.
    ///
    /// Never quietly folded into any of the others. The trigger kinds taught that lesson
    /// already: the value the interop metadata could not name turned out to be the second
    /// most common one on the machine, and hiding it would have hidden a third of the data.
    /// <see cref="BinarySignature.ResultCode"/> carries the number, so an unnamed result is
    /// still reportable and still diagnosable.
    /// </summary>
    Unknown = 0,

    /// <summary>Signed, and the system trusts the chain.</summary>
    Trusted,

    /// <summary>No signature at all, in the file or in any catalogue.</summary>
    NotSigned,

    /// <summary>Signed, but the chain ends somewhere this machine does not trust.</summary>
    UntrustedRoot,

    /// <summary>Signed by a certificate that has expired.</summary>
    Expired,

    /// <summary>Signed by a certificate that was revoked.</summary>
    Revoked,

    /// <summary>The file does not match what was signed. The one that means "somebody changed it".</summary>
    Tampered
}

/// <summary>
/// The signature on one file, as the system reads it.
///
/// Three things together rather than three fields, because they only make sense as one
/// answer: a publisher without a status says nothing about whether to believe it, and a
/// status without the number underneath loses everything this code could not name.
/// </summary>
/// <param name="Status">What the system concluded.</param>
/// <param name="ResultCode">
/// The verification call's own result. Kept for the same reason a refused read keeps its
/// error number: the sentence and the naming are ours, the number is the system's, and only
/// the number stays meaningful when this code meets a result it has no word for.
/// </param>
/// <param name="Publisher">
/// Who signed it, as the certificate's display name. Null when nothing is signed, and null
/// as well when the file is signed but the signer could not be read back - those two are
/// told apart by <see cref="Status"/>.
/// </param>
public sealed record BinarySignature(SignatureStatus Status, int ResultCode, string? Publisher)
{
    /// <summary>
    /// Whether the system trusts this file.
    ///
    /// Deliberately not "is it signed". An expired or untrusted signature is a signature,
    /// and answering "yes it is signed" about one would be true and useless. What somebody
    /// asking wants to know is whether Windows would run it without complaining.
    /// </summary>
    public bool IsTrusted => Status == SignatureStatus.Trusted;
}
