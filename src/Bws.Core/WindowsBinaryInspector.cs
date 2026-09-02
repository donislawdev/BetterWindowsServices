using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    /// </summary>
    private readonly ConcurrentDictionary<string, string?> _publishers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this file is one nobody asked us to reach for.
    ///
    /// Checked before <c>File.Exists</c> in all three readings below, and the order matters:
    /// the existence check is itself the network call being avoided.
    /// </summary>
    private bool OffLimits(string file) =>
        networkPaths == NetworkPaths.Skip && NetworkPath.LeavesThisMachine(file);

    public Reading<BinarySignature> ReadSignature(string file)
    {
        if (OffLimits(file))
        {
            return Reading<BinarySignature>.NotRead();
        }

        if (!File.Exists(file))
        {
            // A fact about the machine, not about our permissions. The listing already knows
            // this and says so in its own field - repeating it as a refusal here would turn
            // one honest answer into two contradictory ones.
            return Reading<BinarySignature>.Absent();
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
                return Reading<BinarySignature>.Present(
                    new BinarySignature(Classify(embedded), embedded, ReadPublisher(file)));
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

        if (!File.Exists(file))
        {
            return Reading<string>.Absent();
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

        if (!File.Exists(file))
        {
            return Reading<string>.Absent();
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
    private static unsafe int Verify(string file)
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
    private unsafe Reading<BinarySignature> ThroughCatalogue(string file)
    {
        if (!PInvoke.CryptCATAdminAcquireContext2(out var admin, null, "SHA256", null))
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
                // No catalogue lists this file and it carries nothing of its own. Now, and
                // only now, is "nobody signed this" the honest answer.
                return Reading<BinarySignature>.Present(
                    new BinarySignature(SignatureStatus.NotSigned, NoSignature, Publisher: null));
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
                return Reading<BinarySignature>.Present(
                    new BinarySignature(Classify(result), result, CataloguePublisher(cataloguePath)));
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
    private static unsafe WINTRUST_DATA Request(WINTRUST_DATA_UNION_CHOICE choice) => new()
    {
        cbStruct = (uint)sizeof(WINTRUST_DATA),

        // Nothing may be shown. This runs inside a listing that may be going into a pipe.
        dwUIChoice = WINTRUST_DATA_UICHOICE.WTD_UI_NONE,

        // Revocation checking is deliberately off. It reaches the network, which ADR-19
        // forbids outright, and it would make the answer depend on whether a certificate
        // authority happens to be reachable from this machine right now.
        fdwRevocationChecks = WINTRUST_DATA_REVOCATION_CHECKS.WTD_REVOKE_NONE,

        dwUnionChoice = choice,
        dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_VERIFY,
        dwProvFlags = WINTRUST_DATA_PROVIDER_FLAGS.WTD_SAFER_FLAG
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
}
