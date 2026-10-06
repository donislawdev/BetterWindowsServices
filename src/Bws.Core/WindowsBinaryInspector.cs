using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.WinTrust;
using Windows.Win32.Storage.FileSystem;

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
/// <b>ONE OPENING PER FILE SINCE 2026-10-06 - package SB, security report S-5.</b> Until that day
/// the signature, the publisher, the version and the hash each opened the file by its path, up to
/// five times, so a file replaced between two of them came back with the verdict of one file and
/// the hash of another. Now one handle is opened without write or delete sharing and held for the
/// whole inspection: WinTrust verifies THAT handle, the catalogue hash and SHA-256 are read from it,
/// the publisher comes out of the verification's own state, and the version - the one question
/// Windows answers only by path - is asked through the handle's final path. Five premises behind
/// that were measured before it was written, all with the answer the design needed:
/// tools/security-probe/one-handle.ps1.
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
    /// How much of the file is read at a time for SHA-256. Below the large object threshold so a
    /// buffer from the shared pool never lands there, and large enough that a file of a few
    /// megabytes is a handful of reads rather than hundreds.
    /// </summary>
    private const int HashChunk = 1 << 16;

    /// <summary>
    /// Room for a final path on the first try. Longer paths are asked for again at the size Windows
    /// reports, so this is a guess about the common case and not a limit.
    /// </summary>
    private const int FinalPathGuess = 512;

    /// <summary>
    /// Windows asks for this rather than a window handle when nothing may be shown. Zero
    /// would also do with the interface suppressed, but this is what the documentation says
    /// and it is what the measurement above was taken with.
    /// </summary>
    private static readonly HWND NoWindow = new(-1);

    /// <summary>
    /// A fresh inspector, for one pass of the second phase - see <see cref="IBinaryInspector.ForOnePass"/>.
    ///
    /// <b>This one remembers nothing since 2026-10-06</b>, when the cache of catalogue publishers left
    /// with the second opening that filled it - the publisher comes out of each verification's own
    /// state now. A fresh instance costs nothing and keeps the promise true whatever a later version
    /// decides to remember, which is why it stays rather than handing out itself.
    /// </summary>
    public IBinaryInspector ForOnePass() => new WindowsBinaryInspector(networkPaths);

    /// <summary>
    /// Whether this file is one nobody asked us to reach for.
    ///
    /// Checked before anything asks the disk, and the order matters: the existence check is itself
    /// the network call being avoided.
    /// </summary>
    private bool OffLimits(string file) =>
        networkPaths == NetworkPaths.Skip && NetworkPath.LeavesThisMachine(file);

    /// <summary>
    /// The answer for a file that is not there to be read, or null when it is - asked once, for all
    /// three fields of the inspection.
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

    public FileInspection Inspect(string file)
    {
        // A path that is not a file on a disk is not opened at all, with --follow-network or without
        // it - the owner's decision on the rest of security report S-4. See FileShape.
        if (OffLimits(file) || !FileShape.NamesAFile(file))
        {
            return FileInspection.NotRead;
        }

        if (Unreachable<string>(FileOnDisk.Ask(file)) is { } unreachable)
        {
            return FileInspection.Alike(unreachable);
        }

        SafeFileHandle? handle = null;

        try
        {
            // READ SHARING ONLY - nobody may write the file, rename it or delete it while this handle is
            // open, and measured with tools/security-probe/one-handle.ps1 on 2026-10-06, nobody may rename
            // a folder above it either (refused 5). That is what makes one opening mean one file. The
            // price is the other direction: a file somebody already holds open for writing refuses us
            // (32), and then all three fields say so with the number - the owner's decision that day.
            // Until then only the hash said it, and the signature beside it was read by path anyway.
            handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.Read);

            return Inspect(file, handle);
        }
        catch (Exception failure) when (handle is null && (failure is IOException or UnauthorizedAccessException
                                                              or ArgumentException or NotSupportedException))
        {
            // Only the OPENING is answered here - the filter on the handle says so. Everything after it
            // answers for itself, field by field, below.
            return FileInspection.Alike(Reading<string>.Denied(failure.HResult, failure.Message));
        }
        finally
        {
            handle?.Dispose();
        }
    }

    /// <summary>
    /// The three answers, all from the one handle.
    ///
    /// <b>The second fence of S-4 is the first line.</b> The shape of the path said "a file", and this
    /// asks the handle what it really is - a drive letter some session mapped to something other than
    /// a disk would get this far and no further.
    ///
    /// Internal rather than private so a test can hand it a handle and a path that name DIFFERENT
    /// files - the one shape in which "every field comes from the handle" can be seen to hold.
    /// </summary>
    internal FileInspection Inspect(string file, SafeFileHandle handle)
    {
        if (PInvoke.GetFileType(handle) != FILE_TYPE.FILE_TYPE_DISK)
        {
            return FileInspection.NotRead;
        }

        var where = FinalPath(handle);

        return new FileInspection(
            Signature(where.Outcome == ReadOutcome.Present ? where.Value! : file, handle),
            Version(where),
            Hash(handle));
    }

    /// <summary>
    /// The signature of the file the HANDLE holds - the path beside it is what WinTrust asks for
    /// alongside, and it is the handle's own final path whenever Windows could give one.
    ///
    /// <b>The handle matters most where that path could not be had</b>: then the path is the one the
    /// entry names, which the open handle does not pin (a junction on the way is not a folder above
    /// the file), and only <c>hFile</c> keeps the verdict about the file that was opened. Internal so a
    /// test can hand it the path of one file and the handle of another - at the level of
    /// <see cref="Inspect(string, SafeFileHandle)"/> the two always name the same file, and a guard
    /// there could not tell whether the handle was passed at all. Found by the mutation registry on
    /// 2026-10-06, the day this was written.
    ///
    /// THE HANDLE IS HELD OPEN WHILE WINDOWS USES IT, since 2026-09-02, backlog 306, and lent here
    /// since 2026-10-06 because the signature is the only question that hands Windows the raw handle -
    /// two WinTrust structures take it, and taking one out of a SafeHandle without this pair is what
    /// the documentation for that type forbids. The argument for it sits in our own obj directory
    /// rather than in somebody's manual: the generator this project uses does exactly this in every
    /// wrapper it emits for a SafeHandle parameter, AddRef before the call and Release in a finally.
    /// </summary>
    internal Reading<BinarySignature> Signature(string file, SafeFileHandle handle)
    {
        var held = false;

        try
        {
            handle.DangerousAddRef(ref held);
            var raw = (HANDLE)handle.DangerousGetHandle();

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
            var embedded = Verify(file, raw);

            return embedded is { Outcome: ReadOutcome.Present, Value.ResultCode: NoSignature }
                ? ThroughCatalogue(file, handle, raw)
                : embedded;
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
        // Three of them are in this file - the signature, the version and the hash - and
        // BroadCatchGuards counts them and every other one in the project. This comment used to
        // carry that count itself, wrote "exactly two" and stayed there through three more being
        // added, which is what a number in a comment does. The guard is what counts.
        catch (Exception failure)
        {
            // HResult rather than GetLastWin32Error: by the time an exception has been
            // built and thrown, the thread's last error has usually been overwritten by
            // whatever the runtime did on the way here. A managed IO failure carries the
            // Win32 code in the low sixteen bits of its HResult.
            return Reading<BinarySignature>.Denied(failure.HResult, failure.Message);
        }
#pragma warning restore CA1031
        finally
        {
            if (held)
            {
                handle.DangerousRelease();
            }
        }
    }

    /// <summary>
    /// The version, asked through the FINAL path of the handle rather than through the path the
    /// entry names - and of the file itself, never of a language file beside it (see
    /// WindowsBinaryInspector.Version.cs).
    ///
    /// <b>Windows reads a version resource only by path</b>, so this is the one answer that cannot
    /// come from the handle itself. The final path is the next best thing and was measured to be as
    /// good, 2026-10-06, tools/security-probe/one-handle.ps1: it has no reparse point left in it, and
    /// while the handle is open the folders on it refuse to be renamed and the file refuses to be
    /// replaced - so the path cannot lead anywhere but to the file the handle holds. The path the
    /// entry names would not do: a junction on the way is not a folder ABOVE the file in the file
    /// system's sense, so the open handle does not hold it still.
    ///
    /// <b>What the first version of this paragraph got wrong, kept because it is the lesson.</b> It
    /// said the version "reads the same through <c>\\?\C:\...</c> as through <c>C:\...</c>", measured on
    /// dotnet.exe - a file with no language file beside it, so the one specimen that could not show
    /// the difference. With the framework's FileVersionInfo the two paths gave different versions on
    /// 64 files of this machine. Asked with no flags, as now, they give the same one.
    /// </summary>
    private static Reading<string> Version(Reading<string> where)
    {
        if (where.Outcome != ReadOutcome.Present)
        {
            return where;
        }

        try
        {
            return OwnVersion(where.Value!);
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

    private static Reading<string> Hash(SafeFileHandle handle)
    {
        try
        {
            return Reading<string>.Present(Sha256(handle));
        }
#pragma warning disable CA1031
        // Same reasoning as the two above: a file that cannot be read to the end - a disk that
        // went away, a read the platform refuses - costs its own answer and not the run.
        catch (Exception failure)
        {
            return Reading<string>.Denied(failure.HResult, failure.Message);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// SHA-256 of everything behind the handle, read at explicit offsets.
    ///
    /// <b>Offsets rather than a stream, and that is a decision about somebody else's side effect.</b>
    /// WinVerifyTrust moves the handle's file pointer itself - measured 2026-10-06, it stood at zero
    /// after a call that was handed it at the end - and code resting on where another call left the
    /// pointer is code that breaks when that call changes. Streamed rather than read whole: the files
    /// here run to hundreds of megabytes between them, and none of it needs to be in memory at once.
    /// </summary>
    private static string Sha256(SafeFileHandle handle)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(HashChunk);

        try
        {
            int read;

            for (long offset = 0; (read = RandomAccess.Read(handle, buffer, offset)) > 0; offset += read)
            {
                hash.AppendData(buffer, 0, read);
            }

            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Where the handle really points, with every reparse point on the way resolved. With a drive
    /// letter when the volume has one, and by the volume's identifier when it does not - a volume
    /// mounted only on a folder still has a file on it worth a version.
    /// </summary>
    private static Reading<string> FinalPath(SafeFileHandle handle) =>
        FinalPath(handle, GETFINALPATHNAMEBYHANDLE_FLAGS.VOLUME_NAME_DOS) is { Outcome: ReadOutcome.Present } onADrive
            ? onADrive
            : FinalPath(handle, GETFINALPATHNAMEBYHANDLE_FLAGS.VOLUME_NAME_GUID);

    /// <summary>
    /// One spelling of the final path. ASKED THROUGH THE RETURN VALUE, the lesson of backlog 303:
    /// zero is a failure and only then is the last error read. A number at least as large as the
    /// room offered is the size wanted, terminating null included, and it is asked once more at that
    /// size - a path that is still longer the second time is refused with a definite code rather
    /// than with whatever the thread's last error happens to hold.
    /// </summary>
    private static Reading<string> FinalPath(SafeFileHandle handle, GETFINALPATHNAMEBYHANDLE_FLAGS volume)
    {
        var buffer = new char[FinalPathGuess];
        var length = PInvoke.GetFinalPathNameByHandle(handle, buffer, volume);

        if (length >= buffer.Length)
        {
            buffer = new char[length];
            length = PInvoke.GetFinalPathNameByHandle(handle, buffer, volume);
        }

        if (length == 0)
        {
            var error = Marshal.GetLastWin32Error();

            return Reading<string>.Denied(error, ManagerTerms.Describe(error));
        }

        return length >= buffer.Length
            ? Reading<string>.Denied(
                (int)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER,
                ManagerTerms.Describe((int)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER))
            : Reading<string>.Present(new string(buffer, 0, (int)length));
    }

    /// <summary>
    /// The signature the file carries itself, if any - verified on the HANDLE.
    ///
    /// <b>The path beside the handle is there because the structure will not take NULL</b>, and
    /// measured 2026-10-06 it is not what gets verified: handed the path of a changed copy and the
    /// handle of an unchanged one, WinVerifyTrust answered trusted, and the other way round it
    /// answered bad digest (tools/security-probe/one-handle.ps1, P1).
    /// </summary>
    private unsafe Reading<BinarySignature> Verify(string file, HANDLE raw)
    {
        fixed (char* path = file)
        {
            var info = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)sizeof(WINTRUST_FILE_INFO),
                pcwszFilePath = path,
                hFile = raw
            };

            var data = Request(WINTRUST_DATA_UNION_CHOICE.WTD_CHOICE_FILE);
            data.pFile = &info;

            return Ask(ref data);
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
        // And since 2026-10-06 VERIFY is what leaves the state the publisher is read out of.
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

    /// <summary>
    /// Verifies, settles the answer WHILE THE STATE IS STILL OPEN, and lets go of the state.
    ///
    /// The state has to be opened and closed with two calls rather than one. The first verifies and
    /// leaves the working data behind, the second releases it - skipping the second leaks for the
    /// life of the process, which on a run touching several hundred files is not a rounding error.
    /// Since 2026-10-06 the answer is settled between the two, because the publisher is read out of
    /// that working data (WindowsBinaryInspector.Publisher.cs) and is gone once it is released.
    /// </summary>
    private unsafe Reading<BinarySignature> Ask(ref WINTRUST_DATA data)
    {
        var action = PInvoke.WINTRUST_ACTION_GENERIC_VERIFY_V2;

        fixed (WINTRUST_DATA* pointer = &data)
        {
            var result = PInvoke.WinVerifyTrust(NoWindow, ref action, pointer);
            var state = pointer->hWVTStateData;

            try
            {
                return Settle(result, networkPaths, () => Signer(state));
            }
            finally
            {
                pointer->dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_CLOSE;
                PInvoke.WinVerifyTrust(NoWindow, ref action, pointer);
            }
        }
    }
}
