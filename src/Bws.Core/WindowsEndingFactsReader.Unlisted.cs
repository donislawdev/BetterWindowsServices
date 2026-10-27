using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Bws.Core;

/// <summary>
/// Which entries the manager keeps and did not list to this account - since 2026-10-27, logic report R04.
///
/// <b>The registry is read here as a list of names and a type, and as nothing else.</b> The rule is to ask
/// the services API wherever it answers, and here it does not: the one call that lists entries leaves out
/// every entry the caller may not query, without an error (Microsoft's page on <c>EnumServicesStatusEx</c>),
/// and no other call hands out names. Every name found is then put to the manager itself, which is the
/// authority on whether it is a service and whether this account may see it.
/// </summary>
public sealed partial class WindowsEndingFactsReader
{
    /// <summary>Where the manager keeps one key per entry it knows.</summary>
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

    /// <summary>
    /// <c>SERVICE_WIN32_SHARE_PROCESS</c>, the bit every entry that can live beside another carries - a
    /// per-user template sharing a process carries it too, as 0x60.
    /// </summary>
    private const int SharesAProcess = 0x20;

    /// <summary>
    /// Every name under the services key that the listing lacks, could share a process, and that the manager
    /// will not let this account query.
    ///
    /// <b>Three answers from the manager, and only the refusal counts.</b> Opened: the entry is visible and the
    /// listing is merely older than it, so the run's own reading just before the ending sees it. 1060 or an
    /// invalid name: a key the manager does not hold as an entry. Anything else: the entry exists and this
    /// account is not shown it, which is exactly the shape the enumeration leaves out.
    ///
    /// <b>The type is read before the manager is asked</b>, so an entry with its own process costs a key open
    /// rather than a call to the manager. A key whose type cannot be read refuses the whole answer - not
    /// knowing whether it shares is no reason to leave it out of a casualty list.
    /// </summary>
    public Reading<IReadOnlyList<string>> ReadUnlisted(IEnumerable<string> listed)
    {
        ArgumentNullException.ThrowIfNull(listed);

        var known = new HashSet<string>(listed, StringComparer.OrdinalIgnoreCase);

        try
        {
            using var services = Registry.LocalMachine.OpenSubKey(ServicesKey);

            if (services is null)
            {
                return Unreadable<IReadOnlyList<string>>((int)WIN32_ERROR.ERROR_FILE_NOT_FOUND);
            }

            using var manager = PInvoke.OpenSCManager(
                lpMachineName: null!,
                lpDatabaseName: null!,
                dwDesiredAccess: PInvoke.SC_MANAGER_CONNECT);

            if (manager.IsInvalid)
            {
                return Unreadable<IReadOnlyList<string>>(Marshal.GetLastWin32Error());
            }

            List<string> unlisted =
            [
                .. services.GetSubKeyNames()
                    .Where(name => !known.Contains(name) && MayShare(services, name) && Withheld(manager, name))
            ];

            return Reading<IReadOnlyList<string>>.Present(unlisted);
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return Unreadable<IReadOnlyList<string>>(CodeOf(e));
        }
    }

    /// <summary>
    /// Whether this key can be an entry living beside another. A key with no type is not an entry - 62 of 867
    /// keys on the owner's machine on 2026-10-27 - a type without the shared bit lives alone, a key that went
    /// since the names were read is nobody, and a type of another shape is asked about all the same.
    ///
    /// <b>A key that cannot be read is not caught here</b>: it refuses the whole answer in the caller, with
    /// the system's number, so a key this account may not read never shortens the list in silence.
    /// </summary>
    private static bool MayShare(RegistryKey services, string name)
    {
        using var key = services.OpenSubKey(name);

        return key?.GetValue("Type") switch
        {
            null => false,
            int type => (type & SharesAProcess) != 0,
            _ => true
        };
    }

    /// <summary>Whether the manager knows this name and will not let this account query it.</summary>
    private static bool Withheld(SafeHandle manager, string name)
    {
        using var service = PInvoke.OpenService(manager, name, PInvoke.SERVICE_QUERY_STATUS);

        if (!service.IsInvalid)
        {
            return false;
        }

        var code = Marshal.GetLastWin32Error();

        return code is not ((int)WIN32_ERROR.ERROR_SERVICE_DOES_NOT_EXIST or (int)WIN32_ERROR.ERROR_INVALID_NAME);
    }

    private static Reading<T> Unreadable<T>(int code) => Reading<T>.Denied(code, new Win32Exception(code).Message);

    /// <summary>
    /// The system's number behind a refused registry read - an access refusal when the exception carries none,
    /// which is what <see cref="SecurityException"/> is.
    /// </summary>
    private static int CodeOf(Exception e) =>
        ((uint)e.HResult & 0xFFFF0000u) == 0x80070000u
            ? e.HResult & 0xFFFF
            : (int)WIN32_ERROR.ERROR_ACCESS_DENIED;
}
