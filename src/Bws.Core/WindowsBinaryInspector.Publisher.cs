using System.Security.Cryptography.X509Certificates;

namespace Bws.Core;

/// <summary>
/// Who signed a file, read back out of the certificate.
///
/// <b>Split out of WindowsBinaryInspector.cs on 2026-09-02 because the size ratchet said so, and
/// the seam is a subject rather than a line count.</b> Everything left in that file answers
/// WHETHER a file is trusted, which is a question for WinTrust and comes back as a verdict. This
/// answers WHO signed it, which is a question for the certificate store and comes back as a name.
/// The two share a file only because they are usually asked together.
///
/// <b>Partial rather than a new type, so no caller moved</b> - the cache of publishers is a field
/// of the inspector and reaching it from another type would have meant handing it around.
/// </summary>
public sealed partial class WindowsBinaryInspector
{
    /// <summary>
    /// Who signed the catalogue, read back from the catalogue file.
    ///
    /// <b>The only thing that is remembered, and until 2026-08-03 every file was.</b> The two
    /// callers are not alike, which is the whole of this split. A binary carrying its own
    /// signature is asked about exactly once per run - the second pass fixes the set of distinct
    /// files before it asks anything - so remembering the answer saves nothing and only creates a
    /// way for it to go stale. Catalogues are shared between many binaries, so remembering those
    /// is the case the cache was written for.
    ///
    /// <b>What the stale answer looked like</b>, in a tool whose reason to exist is noticing that
    /// a file changed: the verdict and the hash are worked out afresh every time and the
    /// publisher was not, so a binary replaced between two readings inside one process reported a
    /// new hash beside the old signer. No process lives long enough for that today. The second
    /// phase of `ADR-13` - signatures read in the background while the window stays open - is a
    /// process that does.
    ///
    /// The standard library reads the certificate out of a signed file without walking the
    /// chain, which is exactly right here: the chain was already walked by the verification
    /// above, and its verdict is carried separately. This call only answers "whose name is
    /// on it".
    /// </summary>
    private string? CataloguePublisher(string catalogue) =>
        _publishers.GetOrAdd(catalogue, ReadPublisher);

    private static string? ReadPublisher(string file)
    {
        try
        {
            // CreateFromSignedFile, not the certificate loader. The loader reads a file that
            // IS a certificate - what is needed here is the certificate embedded inside a
            // signed binary, which is a different question about a different kind of file.
            //
            // Getting that wrong is silent: the loader throws on a signed executable, the
            // throw is caught below, and every file on the machine comes back trusted with
            // nobody's name against it. It shipped that way for one build and an integration
            // test caught it, which is the only thing that would have.
            // Both handles released, and the inner one was leaking. The extractor returns a
            // certificate holding a native context, the constructor beside it copies from that
            // certificate rather than taking it over, and nothing was disposing the original -
            // so every signed file left one native handle to a finaliser. Over 544 distinct
            // files in one snapshot that is 544 of them.
            //
            // Found by an analyser on 2026-08-02, not by a test. No test could see it: the
            // answers were right, the run finished, and the only symptom was handles going
            // back later than they should have.
#pragma warning disable SYSLIB0057
            using var signed = X509Certificate.CreateFromSignedFile(file);
            using var certificate = new X509Certificate2(signed);
#pragma warning restore SYSLIB0057

            var name = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);

            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
#pragma warning disable CA1031
        // Null rather than a state of its own, and broad for the same reason as above. The
        // verdict beside it already says whether there is a signature at all, so a file
        // whose certificate will not parse reads as "trusted, and we could not put a name
        // to it" - which is what happened, rather than an invented one.
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031
    }
}
