namespace Bws.Core;

/// <summary>
/// Everything the second pass learns about one file, from ONE opening of it.
///
/// <b>One value rather than three questions, since 2026-10-06 - package SB, security report S-5.</b>
/// Until that day the inspector answered the signature, the version and the hash as three separate
/// questions, and each one opened the file by its path again - the publisher a fourth time. A file
/// replaced between two of those openings came back with the verdict of one file beside the hash of
/// another, in the one field family whose job is to say whether a file changed. Three questions on
/// the interface were exactly the shape that allowed it, so the interface asks one.
///
/// It replaced the private <c>SecondPass.Answer</c>, which held the same three fields after the fact.
/// </summary>
/// <param name="Signature">Who signed the file and whether the system trusts it.</param>
/// <param name="Version">The version the file claims for itself.</param>
/// <param name="Hash">SHA-256 of the file, lower case hexadecimal.</param>
public readonly record struct FileInspection(
    Reading<BinarySignature> Signature, Reading<string> Version, Reading<string> Hash)
{
    /// <summary>Nobody looked - a file on another machine, or a path that is not a file on a disk.</summary>
    public static FileInspection NotRead { get; } =
        new(Reading<BinarySignature>.NotRead(), Reading<string>.NotRead(), Reading<string>.NotRead());

    /// <summary>
    /// The same answer in all three fields, for an answer about the FILE rather than about any one
    /// of them - not there, refused, or not looked at. A present answer is not one of those and is
    /// refused here, because copying one field's value into the other two would invent them.
    /// </summary>
    internal static FileInspection Alike(Reading<string> answer) => answer.Outcome switch
    {
        ReadOutcome.Absent => new(Reading<BinarySignature>.Absent(), answer, answer),
        ReadOutcome.Denied => new(Reading<BinarySignature>.Denied(answer.ErrorCode, answer.Reason ?? string.Empty), answer, answer),
        ReadOutcome.NotRead => NotRead,
        _ => throw new ArgumentException("A present answer belongs to one field, not to the file.", nameof(answer))
    };
}
