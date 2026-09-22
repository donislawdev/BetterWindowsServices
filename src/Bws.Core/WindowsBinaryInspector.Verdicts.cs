using System.Runtime.InteropServices;

namespace Bws.Core;

/// <summary>
/// The half of <see cref="WindowsBinaryInspector"/> that decides what an answer MEANS, kept
/// apart from the half that goes and gets one.
///
/// <b>The seam is not the line count that forced it.</b> Everything in the other file is the
/// interop dance - a fixed buffer, a handle, a call, a second call to let go of what the first
/// one left behind. Everything here is policy, and policy is what the tests point at: whether
/// a number becomes a verdict or an admission that there is none, and which of those survives
/// a run that was told not to touch the network. Those are the questions somebody reads this
/// file to answer, and none of them needs a single line of the marshalling next door.
/// </summary>
public sealed partial class WindowsBinaryInspector
{
    /// <summary>
    /// The system's number, given a name.
    ///
    /// Only the results that have a distinct meaning for somebody looking at a service list
    /// are named. Everything else stays <see cref="SignatureStatus.Unknown"/> and keeps its
    /// number, because a value folded into a near-enough neighbour is worse than one that
    /// admits it has no name: the trigger kinds already taught that the unnamed case can
    /// turn out to be the second most common one on the machine.
    ///
    /// Only Trusted and NotSigned have been observed on a real machine. The rest are mapped
    /// from documented results and are <b>NOT OBSERVED</b>.
    /// </summary>
    private static SignatureStatus Classify(int result) => result switch
    {
        0 => SignatureStatus.Trusted,
        NoSignature => SignatureStatus.NotSigned,
        unchecked((int)0x800B0109) => SignatureStatus.UntrustedRoot,
        unchecked((int)0x800B0101) => SignatureStatus.Expired,
        unchecked((int)0x800B010C) => SignatureStatus.Revoked,
        unchecked((int)0x80096010) => SignatureStatus.Tampered,
        _ => SignatureStatus.Unknown
    };

    /// <summary>
    /// A verification result, turned into an answer or into an admission that there is none.
    ///
    /// <b>THIS EXISTS BECAUSE THE CHEAP FIX FOR THE FETCH WOULD HAVE BOUGHT SILENCE WITH A
    /// LIE, AND THE OWNER SAW IT BEFORE IT WAS WRITTEN.</b> Once the chain engine may not go
    /// and get a certificate it does not hold, a file that IS properly signed can come back
    /// with a non-zero result - and <see cref="Classify"/> would then turn our own refusal
    /// into a sentence about somebody's certificate. A tool whose subject is trust must not
    /// say "this root is not trusted" when what happened is "I would not look it up".
    ///
    /// <b>The direction that CANNOT happen, and it is worth stating because it is the half
    /// that would be unforgivable:</b> <see cref="SignatureStatus.Trusted"/> is produced only
    /// by a result of zero, and zero requires a chain that was built and validated. Refusing
    /// a fetch can prevent a zero. It cannot manufacture one. So nothing here can turn a file
    /// the system distrusts into one it trusts - the risk runs the other way only.
    ///
    /// <b>WHICH CODE WINDOWS ACTUALLY RETURNS IN THAT CASE IS NOT SPRAWDZONE AND THIS DESIGN
    /// DELIBERATELY DOES NOT DEPEND ON IT.</b> The documentation separates CERT_E_CHAINING
    /// (0x800B010A, no chain could be built) from CERT_E_UNTRUSTEDROOT (0x800B0109, a chain
    /// was built and ends somewhere untrusted), and on that reading only the first would
    /// arrive from a refused fetch. It could not be confirmed here: with both caches cleared,
    /// not one of the 790 signed files on this machine needed a fetch to reach its verdict,
    /// so the failing case does not exist to be observed. Resting on the documented split
    /// would be a bet on behaviour nobody in this project has seen, which is exactly the
    /// shape rule 9 warns about.
    ///
    /// <b>So the line is drawn by a rule instead: a verdict survives the quiet mode only if
    /// it is a fact about the FILE or about its own certificate, never about the chain to a
    /// root.</b> Trusted survives because it needs full validation. NotSigned, Tampered and
    /// Expired survive because a missing signature, a hash mismatch and a date are all
    /// readable without leaving the machine. Everything else - UntrustedRoot, anything
    /// Unknown - becomes a refusal carrying the system's own number and sentence, which is
    /// the shape <c>binaryOnDisk</c> already takes for a path on somebody else's share.
    ///
    /// <b>The cost, said rather than buried:</b> in the quiet mode this gives up the ability
    /// to report a genuinely untrusted root as one. That is a lost signal, and a lost signal
    /// wearing a label is a different thing from a false accusation - the number and the
    /// system's sentence both travel, and <c>--follow-network</c> turns the full answer back
    /// on. On this machine the cost is zero entries out of 790.
    /// </summary>
    internal static Reading<BinarySignature> Settle(
        int result, NetworkPaths networkPaths, Func<string?> publisher)
    {
        if (networkPaths == NetworkPaths.Skip && !SurvivesWithoutTheNetwork(result))
        {
            // The publisher is deliberately not read. It would open the file a second time to
            // decorate an answer we are declining to give, and a refusal carries no value.
            return Reading<BinarySignature>.Denied(result, Marshal.GetExceptionForHR(result)?.Message ?? string.Empty);
        }

        return Reading<BinarySignature>.Present(new BinarySignature(Classify(result), result, publisher()));
    }

    /// <summary>
    /// Whether this result would have been the same had the machine been unplugged.
    ///
    /// Revoked is absent on purpose rather than by oversight: revocation checking is off two
    /// fields above, so that code cannot arrive, and listing it here would suggest somebody
    /// had thought about how it behaves offline when there is nothing to think about.
    /// </summary>
    private static bool SurvivesWithoutTheNetwork(int result) => result switch
    {
        0 => true,
        NoSignature => true,
        unchecked((int)0x800B0101) => true,
        unchecked((int)0x80096010) => true,
        _ => false
    };
}
