using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Cryptography.Catalog;
using Windows.Win32.Security.WinTrust;

namespace Bws.Core;

/// <summary>
/// Asks the system what it thinks of a file.
///
/// Windows signs its own binaries two different ways and only one of them can be answered
/// by looking at the file. An ordinary signature is embedded in the binary. A catalogue
/// signature is not in the binary at all - the file's hash is listed in a separate
/// catalogue, and that catalogue is what carries the signature. Explorer and sc.exe hide
/// this from you. Anything reading the file directly does not get to.
///
/// Measured on a real machine on 2026-08-01, over 544 distinct files: the file alone
/// answers for 346 of them and comes back "no signature" for 198, of which the system
/// itself trusts 189. Stopping at the first step would report those 189 as unsigned, which
/// is a confident wrong answer about a third of the machine.
///
/// Read-only throughout. Nothing here opens anything for writing or changes any state.
/// </summary>
/// <param name="networkPaths">
/// Whether a file on another machine may be opened. Skipping is the default, and this class
/// is the expensive half of that rule rather than the cheap one: the listing asks the disk a
/// yes-or-no question, this one <b>reads the whole file</b> to hash it and verify it. Over a
/// share that is a file transfer, on every listing that asks for signatures and on every
/// snapshot.
/// </param>
public sealed partial class WindowsBinaryInspector(NetworkPaths networkPaths = NetworkPaths.Skip) : IBinaryInspector
{
    /// <summary>The file carries no signature of its own. Not a failure - the question moves on.</summary>
    private const int NoSignature = unchecked((int)0x800B0100);

    /// <summary>
    /// Windows asks for this rather than a window handle when nothing may be shown. Zero
    /// would also do with the interface suppressed, but this is what the documentation says
    /// and it is what the measurement above was taken with.
    /// </summary>
    private static readonly HWND NoWindow = new(-1);

    /// <summary>
    /// Publishers already read, keyed by the CATALOGUE the certificate came out of.
    ///
    /// It bounds a worst case rather than fixing a measured one, and that distinction is
    /// worth keeping straight. A binary carrying its own signature is asked about once
    /// anyway, so this only ever helps files signed by catalogue, which share a smaller
    /// number of catalogues between them.
    ///
    /// <b>That sentence was true and the code did not follow it until 2026-08-03</b>, when this
    /// held every file rather than every catalogue - buying nothing for the binaries and giving
    /// their publisher a way to go stale inside a long-lived process. See
    /// <see cref="CataloguePublisher"/>.
    ///
    /// <b>Measured and NOT shown to help here.</b> Seven runs with it and five without, on
    /// a machine with 810 entries over 544 distinct files: with it 4675-6823 ms, without it
    /// 5087-5792 ms. The spread between runs of the same build is larger than the gap
    /// between builds, so nothing in that data says the cache is worth anything on this
    /// machine. It stays because the number of catalogues is a property of the machine and
    /// not of this code - somewhere with five hundred files behind five catalogues it saves
    /// four hundred and ninety five file reads, and removing it would be fitting this
    /// project to the one machine it happens to be measured on.
    ///
    /// Concurrent because the interface will read this from background threads once there
    /// is an interface. Cheap insurance against a bug that would only ever appear under
    /// load, in a thread nobody is watching.
    ///
    /// <b>ONE PASS LONG SINCE 2026-09-29, and until then as long as the window</b> - backlog 468.
    /// The window holds one inspector for its whole life, so a catalogue replaced under the same
    /// name showed its old publisher until it closed. Every pass now asks through
    /// <see cref="ForOnePass"/>, which starts this empty. Whether Windows ever replaces a catalogue
    /// under the same name is NOT CHECKED - what made it matter is that F5 is promised to verify
    /// everything again, and a memory older than the F5 would break that promise quietly.
    /// </summary>
    private readonly ConcurrentDictionary<string, string?> _publishers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A fresh inspector with nothing remembered, for one pass of the second phase - see
    /// <see cref="IBinaryInspector.ForOnePass"/>. It costs one empty dictionary.
    /// </summary>
    public IBinaryInspector ForOnePass() => new WindowsBinaryInspector(networkPaths);

    /// <summary>
    /// Whether this file is one nobody asked us to reach for.
    ///
    /// Checked before <c>File.Exists</c> in all three readings below, and the order matters:
    /// the existence check is itself the network call being avoided.
    /// </summary>
    private bool OffLimits(string file) =>
        networkPaths == NetworkPaths.Skip && NetworkPath.LeavesThisMachine(file);

    /// <summary>
    /// The answer for a file that is not there to be read, or null when it is - asked once, for all
    /// three readings below.
    ///
    /// <b>Absent is a fact about the machine, not about our permissions.</b> The listing already
    /// knows it and says so in its own field, and repeating it as a refusal here would turn one
    /// honest answer into two contradictory ones. <b>A refusal is the opposite case and until
    /// 2026-10-05 it came back as absent too</b> - <c>File.Exists</c> says false for a file it may
    /// not look at, so the signature, version and hash of a file behind a SYSTEM-only directory read
    /// as "no file" under an ordinary token. Stability report R-1, and <see cref="FileOnDisk"/> has
    /// the measurement.
    ///
    /// Handed the answer rather than the path so a test can ask it about a refusal: no process this
    /// project's tests run in can be refused a file it just made, elevated or not (tried 2026-10-05
    /// with an explicit deny entry - the attributes were still read).
    /// </summary>
    internal static Reading<T>? Unreachable<T>(Reading<bool> onDisk)
    {
        if (onDisk.Outcome == ReadOutcome.Denied)
        {
            return Reading<T>.Denied(onDisk.ErrorCode, onDisk.Reason ?? string.Empty);
        }

        return onDisk.Value ? null : Reading<T>.Absent();
    }

    public Reading<BinarySignature> ReadSignature(string file)
    {
        if (OffLimits(file))
        {
            return Reading<BinarySignature>.NotRead();
        }

        if (Unreachable<BinarySignature>(FileOnDisk.Ask(file)) is { } unreachable)
        {
            return unreachable;
        }

        try
        {
            // The file's own signature is asked about first, and that ordering is a decision
            // rather than an accident. A file can carry both - an embedded signature and an
            // entry in a catalogue - and the two can name different signers. Measured on
            // 2026-08-01 over 535 files with a publisher: the verdict agrees with
            // Get-AuthenticodeSignature on every single one, and the name differs on 44,
            // where PowerShell reports the catalogue's signer and this reports the file's.
            //
            // The file's own signature is the right answer for this product. It travels with
            // the binary, while a catalogue entry is a property of the machine that happens
            // to be reading it - so a snapshot taken here and compared against one taken
            // elsewhere would report differences that are about the two catalogue stores
            // rather than about the services. That is the same false-difference failure
            // ADR-14 exists to prevent, one layer down.
            var embedded = Verify(file);

            if (embedded != NoSignature)
            {
                // Read rather than remembered. This file is asked about once in a run, so a
                // cache would be a way to be wrong later and never a way to be quicker.
                return Settle(embedded, networkPaths, () => ReadPublisher(file));
            }

            return ThroughCatalogue(file);
        }
#pragma warning disable CA1031
        // Broad on purpose, and it is not the silence rule 8 forbids: the failure becomes a
        // refusal in the result, with its number and its sentence, and shows up in the
        // listing like any other unreadable field.
        //
        // The alternative is worse than it looks. This runs over every binary a machine
        // happens to have, none of which we chose, and the ways one file can be malformed
        // are not a list anybody can finish - a truncated certificate, a path the platform
        // rejects, a handle that goes away mid-read. Naming two exception types means the
        // third one loses all 809 other entries and ends the run with exit code 1, on
        // somebody's production server, because of one bad file.
        //
        // One of five broad catches in the project, and four of them are in this file: the
        // signature, the version, the hash and the publisher, each of which opens a file
        // nobody here chose. The fifth is the entry point of the tool.
        //
        // The count was written as "exactly two" and stayed there through three more being
        // added, which is what a number in a comment does - nothing counts it. If a sixth
        // appears outside this file, that is worth a question rather than a line: every one
        // here exists because a machine's own binaries cannot be enumerated for the ways
        // they might be malformed, and that argument does not travel far.
        catch (Exception failure)
        {
            // HResult rather than GetLastWin32Error: by the time an exception has been
            // built and thrown, the thread's last error has usually been overwritten by
            // whatever the runtime did on the way here. A managed IO failure carries the
            // Win32 code in the low sixteen bits of its HResult.
            return Reading<BinarySignature>.Denied(failure.HResult, failure.Message);
        }
#pragma warning restore CA1031
    }

    public Reading<string> ReadFileVersion(string file)
    {
        if (OffLimits(file))
        {
            return Reading<string>.NotRead();
        }

        if (Unreachable<string>(FileOnDisk.Ask(file)) is { } unreachable)
        {
            return unreachable;
        }

        try
        {
            var version = FileVersionInfo.GetVersionInfo(file).FileVersion;

            // Plenty of drivers carry no version resource at all. Ordinary, not missing.
            return string.IsNullOrWhiteSpace(version)
                ? Reading<string>.Absent()
                : Reading<string>.Present(version);
        }
#pragma warning disable CA1031
        // Same reasoning as above: one file with a version resource nobody can parse must
        // cost that file's answer and nothing else.
        catch (Exception failure)
        {
            return Reading<string>.Denied(failure.HResult, failure.Message);
        }
#pragma warning restore CA1031
    }

    public Reading<string> ReadHash(string file)
    {
        if (OffLimits(file))
        {
            return Reading<string>.NotRead();
        }

        if (Unreachable<string>(FileOnDisk.Ask(file)) is { } unreachable)
        {
            return unreachable;
        }

        try
        {
            using var stream = File.OpenRead(file);

            // Streamed rather than read whole. The files here run to hundreds of megabytes
            // between them and one of them alone can be large, and there is no reason for any
            // of it to be in memory at once.
            return Reading<string>.Present(Convert.ToHexStringLower(SHA256.HashData(stream)));
        }
#pragma warning disable CA1031
        // Same reasoning as the two above: a file that cannot be opened - locked, on a
        // disconnected disk, gone since the listing - costs its own answer and not the run.
        catch (Exception failure)
        {
            return Reading<string>.Denied(failure.HResult, failure.Message);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// The signature the file carries itself, if any.
    ///
    /// The state has to be opened and closed with two calls rather than one. The first
    /// verifies and leaves the working data behind, the second releases it - skipping the
    /// second leaks for the life of the process, which on a run touching several hundred
    /// files is not a rounding error.
    /// </summary>
    private unsafe int Verify(string file)
    {
        fixed (char* path = file)
        {
            var info = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)sizeof(WINTRUST_FILE_INFO),
                pcwszFilePath = path
            };

            var data = Request(WINTRUST_DATA_UNION_CHOICE.WTD_CHOICE_FILE);
            data.pFile = &info;

            return Ask(ref data);
        }
    }

    /// <summary>
    /// The signature the catalogues carry on the file's behalf.
    ///
    /// Three steps and none of them is optional: hash the file the way the catalogue system
    /// hashes it, find a catalogue listing that hash, then verify the file as a member of
    /// that catalogue. The hash doubles as the member tag, spelled out in hex, which is how
    /// the verification finds the right entry inside the catalogue.
    /// </summary>
    private Reading<BinarySignature> ThroughCatalogue(string file)
    {
        if (ThroughCatalogue(file, "SHA256") is { } bySha256)
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
        return ThroughCatalogue(file, "SHA1") is { Outcome: ReadOutcome.Present } bySha1
            ? bySha1
            : Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.NotSigned, NoSignature, Publisher: null));
    }

    /// <summary>
    /// One catalogue question asked with one hash algorithm. Null when no catalogue lists the file
    /// under it, which is not yet an answer - see the method above.
    /// </summary>
    private unsafe Reading<BinarySignature>? ThroughCatalogue(string file, string algorithm)
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
            using var handle = File.OpenHandle(file);

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
                return VerifyAgainst(catalogue, admin, file, handle, hash);
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
        nint catalogue, nint admin, string file, SafeHandle handle, byte[] hash)
    {
        var found = new CATALOG_INFO { cbStruct = (uint)sizeof(CATALOG_INFO) };

        if (!PInvoke.CryptCATCatalogInfoFromContext(catalogue, ref found, 0))
        {
            var error = Marshal.GetLastWin32Error();

            return Reading<BinarySignature>.Denied(error, ManagerTerms.Describe(error));
        }

        var cataloguePath = found.wszCatalogFile.ToString();
        var memberTag = Convert.ToHexString(hash);

        // THE HANDLE IS HELD OPEN WHILE SOMEBODY ELSE USES IT, since 2026-09-02. Backlog 306.
        // The raw handle below is handed to WinTrust, which does its own work with it, and taking
        // one out of a SafeHandle without this pair is what the documentation for that type
        // forbids. It was not a fault in practice - the caller keeps the SafeHandle in a `using`,
        // so nothing could close it in between - but that made the correctness a property of how
        // the compiler decides a local is still live, rather than of anything written here.
        //
        // THE ARGUMENT FOR IT SITS IN OUR OWN obj DIRECTORY rather than in somebody's manual: the
        // generator this project uses does exactly this in every wrapper it emits for a SafeHandle
        // parameter, AddRef before the call and Release in a finally.
        var held = false;

        try
        {
            handle.DangerousAddRef(ref held);

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
                    hMemberFile = (HANDLE)handle.DangerousGetHandle(),
                    pbCalculatedFileHash = hashPointer,
                    cbCalculatedFileHash = (uint)hash.Length,
                    hCatAdmin = admin
                };

                var data = Request(WINTRUST_DATA_UNION_CHOICE.WTD_CHOICE_CATALOG);
                data.pCatalog = &info;

                var result = Ask(ref data);

                // The signer of a catalogue-signed file is whoever signed the catalogue. That
                // is the same answer Explorer gives, and reading it from the catalogue file
                // keeps four more functions out of the interop list.
                return Settle(result, networkPaths, () => CataloguePublisher(cataloguePath));
            }
        }
        finally
        {
            if (held)
            {
                handle.DangerousRelease();
            }
        }
    }

    /// <summary>The parts of the request that never differ, whichever way the file is signed.</summary>
    private unsafe WINTRUST_DATA Request(WINTRUST_DATA_UNION_CHOICE choice) => new()
    {
        cbStruct = (uint)sizeof(WINTRUST_DATA),

        // Nothing may be shown. This runs inside a listing that may be going into a pipe.
        dwUIChoice = WINTRUST_DATA_UICHOICE.WTD_UI_NONE,

        // Revocation checking is deliberately off. It reaches the network, which ADR-19
        // forbids outright, and it would make the answer depend on whether a certificate
        // authority happens to be reachable from this machine right now.
        //
        // THAT SENTENCE WAS TRUE AND INCOMPLETE FOR THIRTEEN MONTHS, AND THE LINE BELOW IS
        // WHAT IT WAS MISSING. Turning revocation off does not stop the chain engine going
        // out to FETCH a certificate it does not hold. Measured 2026-09-22, three runs out of
        // three, by tools/outbound-probe/outbound.ps1: `bws list --signatures` loads
        // WINHTTP.dll, WS2_32.dll and DNSAPI.dll and opens HTTP connections to
        // certificates.intel.com. Plain `bws list` does none of it, so signature reading is
        // the whole of the difference. THREE GUARDS AND EVERY REVIEW OF THIS FILE HAD PASSED
        // OVER IT, because none of them can see a module the chain engine loads at run time.
        fdwRevocationChecks = WINTRUST_DATA_REVOCATION_CHECKS.WTD_REVOKE_NONE,

        dwUnionChoice = choice,

        // VERIFY, NOT AUTO_CACHE, AND THAT WAS MEASURED rather than assumed - 2026-09-29, 797 entries,
        // 195 files through 77 catalogues, tools/signature-probe/auto-cache.ps1. The documentation
        // says AUTO_CACHE caches catalogue data and nothing about where. On this machine the state
        // handle stays zero after every call, so the cache lives inside the process, and it buys
        // nothing: on one thread the time inside WinVerifyTrust was 1138-1561 ms with VERIFY and
        // 1108-1249 with AUTO_CACHE, even with the files grouped by catalogue. On several threads
        // it is worse - the calls queue behind each other, and the catalogue path took 1380-1497 ms
        // of wall clock at sixteen processors against 347-403, and 1141-1442 against 1017-1287 at
        // two. A process-wide cache would also outlive a pass unless flushed, which ADR-13 forbids.
        dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_VERIFY,

        // WTD_CACHE_ONLY_URL_RETRIEVAL confines the chain engine to what this machine already
        // holds. It is on unless the caller has asked for the network, which is the same
        // switch that decides whether a launch path on somebody else's share may be opened -
        // one promise, one control, and nobody gets more network than they had before.
        //
        // Measured on this machine with the certificate URL cache and the DNS cache both
        // cleared: 797 entries, 790 Trusted, 3 NotSigned and 790 publishers WITH the flag and
        // WITHOUT it, identical. So the fetch that was happening changed no answer here. It
        // is still a fetch, and it still went to a third party.
        //
        // NO WTD_SAFER_FLAG BESIDE IT SINCE 2026-10-06, and that is worth the paragraph, because it
        // stood here from the first slice with no reason written down anywhere. Microsoft's page on
        // WINTRUST_DATA documents it in two words: "Not supported." Measured that day with
        // tools/security-probe/tampered-copy.ps1 on copies of three files carrying their own
        // signature (dotnet.exe, msedge.exe, git.exe), one byte changed in each: WITH the flag
        // WinVerifyTrust answers 0x800B0100, no signature at all, and WITHOUT it 0x80096010, a bad
        // digest - which is what Get-AuthenticodeSignature reports as HashMismatch. So a file changed
        // after it was signed went on to the catalogues, was found in none, and came back NotSigned.
        // Tampered, the one verdict that means somebody changed the file, never arrived for a file
        // with its own signature. It does now, and its publisher arrives beside it with nobody
        // vouching for it - security report S-6 made that safe earlier the same day.
        dwProvFlags = networkPaths == NetworkPaths.Follow
            ? default
            : WINTRUST_DATA_PROVIDER_FLAGS.WTD_CACHE_ONLY_URL_RETRIEVAL
    };

    private static unsafe int Ask(ref WINTRUST_DATA data)
    {
        var action = PInvoke.WINTRUST_ACTION_GENERIC_VERIFY_V2;

        fixed (WINTRUST_DATA* pointer = &data)
        {
            var result = PInvoke.WinVerifyTrust(NoWindow, ref action, pointer);

            data.dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_CLOSE;
            PInvoke.WinVerifyTrust(NoWindow, ref action, pointer);

            return result;
        }
    }
}
