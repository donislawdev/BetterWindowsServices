using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Bws.Core;

/// <summary>
/// The configuration query itself, on a handle somebody else opened - for the listing, and since 2026-10-26
/// for a run that asks one entry's start type before its first step (logic report R03).
///
/// <b>Out of ReadConfiguration that day, and the reason is that there are now two callers.</b> The query has
/// two calls, a size the manager reports and a buffer whose pointers point into itself, and every one of those
/// has been wrong here once - backlog 297 and 303. A second copy of it for the run would be a second place to
/// get them wrong.
/// </summary>
public sealed partial class WindowsScmCatalog
{
    /// <summary>
    /// The start type the manager holds for one entry now, or the refusal - nothing else of the configuration.
    ///
    /// <b>SERVICE_QUERY_CONFIG alone</b>, the right the listing asks for this query and nothing more, so a run
    /// is refused this exactly where the listing is.
    /// </summary>
    public Reading<StartType> ReadStartType(string serviceName)
    {
        using var manager = PInvoke.OpenSCManager(
            lpMachineName: null!,
            lpDatabaseName: null!,
            dwDesiredAccess: PInvoke.SC_MANAGER_CONNECT);

        if (manager.IsInvalid)
        {
            return Refused<StartType>(Marshal.GetLastWin32Error());
        }

        using var service = PInvoke.OpenService(manager, serviceName, PInvoke.SERVICE_QUERY_CONFIG);

        return service.IsInvalid ? Refused<StartType>(Marshal.GetLastWin32Error()) : Queried(service).StartType;
    }

    /// <summary>
    /// What QueryServiceConfig hands over for an open entry - every field it carries read, the rest left as the
    /// listing's own calls fill them, or refused in every field.
    /// </summary>
    internal static unsafe ScmConfiguration Queried(SafeHandle service)
    {
        // ASKED THROUGH THE RETURN VALUE, the rule Enumerate sets out in full and the one this call did not
        // follow until 2026-09-02. Backlog 303. Deciding on the size alone reads whatever the previous call
        // on this thread left in the last error as though it were this call's refusal.
        var probed = PInvoke.QueryServiceConfig(service, default, out var needed);
        var probeError = Marshal.GetLastWin32Error();

        if (!probed && probeError != (int)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
        {
            return ScmConfiguration.Refused(probeError);
        }

        if (needed == 0)
        {
            // The call answered and asked for no room. Every entry has a configuration, so this
            // is not a shape the manager produces - measured over 799 entries under both tokens
            // on 2026-09-02 and never seen once. Refused with a definite code rather than with a
            // stale one, because there is no true thing to say about an answer that cannot happen.
            return ScmConfiguration.Refused((int)WIN32_ERROR.ERROR_INVALID_DATA);
        }

        var buffer = new byte[needed];

        // PINNED ACROSS THE CALL AND THE READING, NOT ONLY ACROSS THE CALL, and this is the whole
        // of backlog 297. The interop wrapper pins for exactly as long as the native call runs -
        // Windows.Win32.PInvoke.ADVAPI32.dll.g.cs puts its `fixed` INSIDE the method - so the array
        // is loose again the moment it returns. The structure the manager wrote into it carries
        // ABSOLUTE POINTERS into that same array, taken at the moment of the call, and the reading
        // below follows them. A collection between the two moves the bytes to a new address and
        // leaves those pointers aimed at where the array used to be.
        //
        // What that costs is the thing this whole file is careful about: an account or a launch
        // path read from freed memory is either an access violation, which ends the process on
        // somebody's server without a word, or worse - plausible rubbish in an audit.
        //
        // THE SPAN IS BUILT FROM THE PINNED POINTER RATHER THAN FROM THE ARRAY on purpose. Passing
        // `buffer` would work identically, and would leave nothing on the page saying why the block
        // exists - the next person would see a `fixed` whose variable nobody uses and take it out.
        fixed (byte* pinned = buffer)
        {
            return PInvoke.QueryServiceConfig(service, new Span<byte>(pinned, buffer.Length), out _)
                ? ManagerBlocks.ReadConfigurationBuffer(buffer)
                : ScmConfiguration.Refused(Marshal.GetLastWin32Error());
        }
    }
}
