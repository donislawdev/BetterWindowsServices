using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Cryptography.Catalog;
using Windows.Win32.Security.WinTrust;

namespace Bws.Core;

/// <summary>
/// The second way Windows signs a file: not in the file at all, but in a catalogue that lists the
/// file's hash and carries the signature on its behalf.
///
/// <b>Split out of WindowsBinaryInspector.cs on 2026-10-06 because the size ratchet said so, and the
/// seam is a subject rather than a line count</b> - the same move that gave the publisher and the
/// verdicts their own files. Package SB grew that file past its ceiling by giving one handle the whole
/// inspection. What is left there opens the file and asks about its OWN signature, what is here is
/// the catalogue's half: hash the file the catalogue way, find the catalogue, verify the file as its
/// member. The handle comes in from the inspection rather than being opened here, which is the whole
/// of what package SB changed in this half.
/// </summary>
public sealed partial class WindowsBinaryInspector
{
    /// <summary>
    /// The signature the catalogues carry on the file's behalf.
    ///
    /// Three steps and none of them is optional: hash the file the way the catalogue system
    /// hashes it, find a catalogue listing that hash, then verify the file as a member of
    /// that catalogue. The hash doubles as the member tag, spelled out in hex, which is how
    /// the verification finds the right entry inside the catalogue.
    /// </summary>
    private Reading<BinarySignature> ThroughCatalogue(string file, SafeFileHandle handle, HANDLE raw)
    {
        if (ThroughCatalogue(file, handle, raw, "SHA256") is { } bySha256)
        {
            return bySha256;
        }

        // ASKED AGAIN WITH SHA-1 BEFORE ANYTHING IS CALLED UNSIGNED, since 2026-10-05 - stability
        // report R-6. A catalogue lists its members by hash, and an older one lists them by SHA-1,
        // so a file only such a catalogue names is not found by the question above and came back
        // NotSigned: confident, and wrong about a signed file. NO SPECIMEN ON THE OWNER'S MACHINE,
        // measured that day with tools/signature-probe/catalogue-algorithms.ps1: the SHA-1 context
        // works there and finds inbox drivers, and the one file this tool calls NotSigned is in no
        // catalogue under either. It costs one more hash per file no SHA-256 catalogue lists - one
        // file there.
        //
        // Only a verdict counts. A machine that will not hash with SHA-1, or a catalogue that cannot
        // be read once found, gets the answer this gave before that day rather than a refusal for
        // every unsigned file: the question that decides was answered above, this one only rescues.
        return ThroughCatalogue(file, handle, raw, "SHA1") is { Outcome: ReadOutcome.Present } bySha1
            ? bySha1
            : Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.NotSigned, NoSignature, Publisher: null));
    }

    /// <summary>
    /// One catalogue question asked with one hash algorithm. Null when no catalogue lists the file
    /// under it, which is not yet an answer - see the method above.
    ///
    /// The member hash comes from the same handle as everything else, and measured 2026-10-06 it
    /// does not depend on where the handle's file pointer stands (one-handle.ps1, P6).
    /// </summary>
    private unsafe Reading<BinarySignature>? ThroughCatalogue(
        string file, SafeFileHandle handle, HANDLE raw, string algorithm)
    {
        // One context per file, and a context held per worker instead was measured on 2026-09-29
        // (tools/signature-probe/auto-cache.ps1, variant "held"). Acquiring and releasing is almost
        // free - what a fresh context costs is its FIRST catalogue lookup, about 1 ms per file on
        // one thread. Held per worker, the catalogue path took 894-1041 ms of wall clock at two
        // processors against 1017-1287, which is two or three percent of the 4247-4877 ms pass and
        // smaller than that pass's own spread. Not worth a context whose lifetime spans calls.
        if (!PInvoke.CryptCATAdminAcquireContext2(out var admin, null, algorithm, null))
        {
            var error = Marshal.GetLastWin32Error();

            return Reading<BinarySignature>.Denied(error, ManagerTerms.Describe(error));
        }

        try
        {
            uint size = 0;

            // ASKED THROUGH THE RETURN VALUE, the sixth and last place in this project that decided
            // on the reported size alone. Backlog 303, and the argument in full is at
            // WindowsScmCatalog.Enumerate: Windows does not clear the last error on success, so a
            // size of zero read as a failure carries whatever the previous call on this thread left
            // behind. This one runs 544 times per listing, once per distinct file.
            var probed = PInvoke.CryptCATAdminCalcHashFromFileHandle2(admin, handle, ref size, default);
            var probeError = Marshal.GetLastWin32Error();

            if (!probed && probeError != (int)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
            {
                return Reading<BinarySignature>.Denied(probeError, ManagerTerms.Describe(probeError));
            }

            if (size == 0)
            {
                // Asked for no room and did not fail. A file always hashes to something, so this
                // is not a shape the system produces - refused with a definite code rather than
                // with a stale one, because there is nothing true to say about it.
                return Reading<BinarySignature>.Denied(
                    (int)WIN32_ERROR.ERROR_INVALID_DATA,
                    ManagerTerms.Describe((int)WIN32_ERROR.ERROR_INVALID_DATA));
            }

            var hash = new byte[size];

            if (!PInvoke.CryptCATAdminCalcHashFromFileHandle2(admin, handle, ref size, hash))
            {
                var error = Marshal.GetLastWin32Error();

                return Reading<BinarySignature>.Denied(error, ManagerTerms.Describe(error));
            }

            var catalogue = PInvoke.CryptCATAdminEnumCatalogFromHash(admin, hash);

            if (catalogue == 0)
            {
                // No catalogue lists this file under this algorithm, and it carries nothing of its
                // own. "Nobody signed this" is the honest answer only once both have been asked.
                return null;
            }

            try
            {
                return VerifyAgainst(catalogue, admin, file, raw, hash);
            }
            finally
            {
                PInvoke.CryptCATAdminReleaseCatalogContext(admin, catalogue, 0);
            }
        }
        finally
        {
            PInvoke.CryptCATAdminReleaseContext(admin, 0);
        }
    }

    private unsafe Reading<BinarySignature> VerifyAgainst(
        nint catalogue, nint admin, string file, HANDLE raw, byte[] hash)
    {
        var found = new CATALOG_INFO { cbStruct = (uint)sizeof(CATALOG_INFO) };

        if (!PInvoke.CryptCATCatalogInfoFromContext(catalogue, ref found, 0))
        {
            var error = Marshal.GetLastWin32Error();

            return Reading<BinarySignature>.Denied(error, ManagerTerms.Describe(error));
        }

        var cataloguePath = found.wszCatalogFile.ToString();
        var memberTag = Convert.ToHexString(hash);

        fixed (char* cataloguePointer = cataloguePath)
        fixed (char* memberPointer = memberTag)
        fixed (char* filePointer = file)
        fixed (byte* hashPointer = hash)
        {
            var info = new WINTRUST_CATALOG_INFO
            {
                cbStruct = (uint)sizeof(WINTRUST_CATALOG_INFO),
                pcwszCatalogFilePath = cataloguePointer,
                pcwszMemberTag = memberPointer,
                pcwszMemberFilePath = filePointer,
                hMemberFile = raw,
                pbCalculatedFileHash = hashPointer,
                cbCalculatedFileHash = (uint)hash.Length,
                hCatAdmin = admin
            };

            var data = Request(WINTRUST_DATA_UNION_CHOICE.WTD_CHOICE_CATALOG);
            data.pCatalog = &info;

            // The signer of a catalogue-signed file is whoever signed the catalogue. That is the
            // same answer Explorer gives, and since 2026-10-06 it comes out of this verification's
            // own state like the embedded one - no second opening of the catalogue file and no
            // memory of catalogues to go stale between passes.
            return Ask(ref data);
        }
    }
}
