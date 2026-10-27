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
    private Reading<BinarySignature> ThroughCatalogue(string file, SafeFileHandle handle, HANDLE raw) =>
        Rescued(algorithm => ThroughCatalogue(file, handle, raw, algorithm));

    /// <summary>
    /// One catalogue question's answer - null when no catalogue lists the file under that algorithm - and
    /// whether a catalogue listing it was found, which is what decides the SHA-1 rescue (C03).
    /// </summary>
    internal readonly record struct CatalogueAnswer(Reading<BinarySignature>? Signature, bool Listed);

    /// <summary>
    /// The two questions in their order, given a way to ask one. <b>Its own method since the review of PR
    /// #96</b>, so a test can hand it answers no machine here gives - a catalogue found under SHA-1 that then
    /// refuses - and see them carried through, not only the rule at the end.
    /// </summary>
    internal static Reading<BinarySignature> Rescued(Func<string, CatalogueAnswer> ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        if (ask("SHA256").Signature is { } bySha256)
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
        // WHETHER A CATALOGUE WAS FOUND DECIDES, since 2026-10-27 - logic report C03, owner's decision
        // "two cases apart". A machine that will not hash with SHA-1, or no catalogue listing the file
        // under it, still gets the answer this gave before 2026-10-05 rather than a refusal for every
        // unsigned file: that question was answered above, and this one only rescues. But a catalogue
        // that lists the file and then cannot be read or verified is not "nobody signed this" - the
        // file is somebody's member, and a refusal with Windows' number is what is known. Until that day
        // it came back as NotSigned too, a confident answer about a file that may well be signed.
        var bySha1 = ask("SHA1");

        return AfterSha1(bySha1.Signature, bySha1.Listed);
    }

    /// <summary>
    /// The rule above on its own, so a test can hold it without a catalogue that fails on demand - no
    /// machine here has one (C03). What the verification said when a catalogue was found, and "not signed"
    /// when none was, or the question could not be asked at all.
    /// </summary>
    internal static Reading<BinarySignature> AfterSha1(Reading<BinarySignature>? answer, bool listed) =>
        listed && answer is { } found
            ? found
            : Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.NotSigned, NoSignature, Publisher: null));

    /// <summary>
    /// One catalogue question asked with one hash algorithm. No signature when no catalogue lists the
    /// file under it, which is not yet an answer - see <see cref="Rescued"/>. Listed for every answer from
    /// the verification against a catalogue found, refusals included, and not for a refusal that came
    /// before anybody looked (C03).
    ///
    /// The member hash comes from the same handle as everything else, and measured 2026-10-06 it
    /// does not depend on where the handle's file pointer stands (one-handle.ps1, P6).
    /// </summary>
    private unsafe CatalogueAnswer ThroughCatalogue(string file, SafeFileHandle handle, HANDLE raw, string algorithm)
    {
        // One context per file, and a context held per worker instead was measured on 2026-09-29
        // (tools/signature-probe/auto-cache.ps1, variant "held"). Acquiring and releasing is almost
        // free - what a fresh context costs is its FIRST catalogue lookup, about 1 ms per file on
        // one thread. Held per worker, the catalogue path took 894-1041 ms of wall clock at two
        // processors against 1017-1287, which is two or three percent of the 4247-4877 ms pass and
        // smaller than that pass's own spread. Not worth a context whose lifetime spans calls.
        if (!PInvoke.CryptCATAdminAcquireContext2(out var admin, null, algorithm, null))
        {
            return Unlisted(Marshal.GetLastWin32Error());
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
                return Unlisted(probeError);
            }

            if (size == 0)
            {
                // Asked for no room and did not fail. A file always hashes to something, so this
                // is not a shape the system produces - refused with a definite code rather than
                // with a stale one, because there is nothing true to say about it.
                return Unlisted((int)WIN32_ERROR.ERROR_INVALID_DATA);
            }

            var hash = new byte[size];

            if (!PInvoke.CryptCATAdminCalcHashFromFileHandle2(admin, handle, ref size, hash))
            {
                return Unlisted(Marshal.GetLastWin32Error());
            }

            var catalogue = PInvoke.CryptCATAdminEnumCatalogFromHash(admin, hash);

            if (catalogue == 0)
            {
                // No catalogue lists this file under this algorithm, and it carries nothing of its
                // own. "Nobody signed this" is the honest answer only once both have been asked.
                return new CatalogueAnswer(Signature: null, Listed: false);
            }

            try
            {
                return new CatalogueAnswer(VerifyAgainst(catalogue, admin, file, raw, hash), Listed: true);
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

    /// <summary>Refused before any catalogue was looked for - the context, or the hash.</summary>
    private static CatalogueAnswer Unlisted(int error) =>
        new(Reading<BinarySignature>.Denied(error, ManagerTerms.Describe(error)), Listed: false);

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
