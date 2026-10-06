using Windows.Win32;
using Windows.Win32.Foundation;

namespace Bws.Core;

/// <summary>
/// Who signed a file, read back out of the verification that just ran.
///
/// <b>Split out of WindowsBinaryInspector.cs on 2026-09-02 because the size ratchet said so, and
/// the seam is a subject rather than a line count.</b> Everything left in that file answers
/// WHETHER a file is trusted, which is a question for WinTrust and comes back as a verdict. This
/// answers WHO signed it, which comes back as a name. The two share a type only because they are
/// asked together.
///
/// <b>OUT OF THE VERIFICATION STATE SINCE 2026-10-06 - package SB, the owner's decision that day.</b>
/// Until then the standard library read the certificate out of the file by its path, which was a
/// SECOND OPENING - the name could come from a different file than the verdict beside it - and for a
/// catalogue-signed file it opened the catalogue again, behind a cache of catalogue publishers that
/// had to be kept from going stale between passes (backlog 468). The call doing it,
/// <c>X509Certificate.CreateFromSignedFile</c>, is obsolete since .NET 9 (SYSLIB0057). The state is
/// the very signature WinTrust verified, for an embedded signature and a catalogue alike. Measured
/// that day with tools/security-probe/one-handle.ps1: the name is there for Trusted AND for Tampered,
/// the same name the old call gave.
/// </summary>
public sealed partial class WindowsBinaryInspector
{
    /// <summary>
    /// The simple display name of the first signer's certificate, or null when the state holds none.
    ///
    /// <b>Read through the fields of the provider data rather than through
    /// WTHelperGetProvSignerFromChain, and that is a choice with a reason.</b> The generator hands the
    /// provider data back in one shape and that helper wants it in another - the two differ only in a
    /// union holding one pointer, but crossing between them is a cast nobody should have to trust.
    /// The helper's whole work for the first signer is to index two arrays at zero, and element zero
    /// sits at the start of an array whatever size Windows gives its elements, so reading the fields
    /// asks for nothing the helper would not have read itself.
    ///
    /// <b>Null rather than a state of its own</b>, as before: the verdict beside it already says
    /// whether there is a signature at all, so a signature whose certificate holds no readable name is
    /// "signed, and we could not put a name to it" rather than an invented one. CERT_NAME_SIMPLE_DISPLAY_TYPE
    /// is what <c>X509NameType.SimpleName</c> asked for, so the names did not change with the source.
    /// </summary>
    private static unsafe string? Signer(HANDLE state)
    {
        var provider = PInvoke.WTHelperProvDataFromStateData(state);

        if (provider == null || provider->csSigners == 0 || provider->pasSigners == null)
        {
            return null;
        }

        var signer = provider->pasSigners;

        if (signer->csCertChain == 0 || signer->pasCertChain == null || signer->pasCertChain->pCert == null)
        {
            return null;
        }

        var certificate = signer->pasCertChain->pCert;

        // Asked for its size first, through the return value: the count includes the terminating
        // null, so one means an empty name.
        var size = PInvoke.CertGetNameString(certificate, PInvoke.CERT_NAME_SIMPLE_DISPLAY_TYPE, 0, null, default, 0);

        if (size <= 1)
        {
            return null;
        }

        var name = new char[size];

        fixed (char* text = name)
        {
            size = PInvoke.CertGetNameString(certificate, PInvoke.CERT_NAME_SIMPLE_DISPLAY_TYPE, 0, null, text, (uint)name.Length);
        }

        var written = size <= 1 ? string.Empty : new string(name, 0, (int)size - 1);

        return string.IsNullOrWhiteSpace(written) ? null : written;
    }
}
