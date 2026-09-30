using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Bws.Core;

/// <summary>
/// The one question in this reader asked of the MANAGER rather than of a process: what it does to an
/// entry when the entry's process dies. Since 2026-09-30, stability report W-3.
/// </summary>
public sealed partial class WindowsEndingFactsReader
{
    /// <summary>
    /// Opens the entry for configuration and nothing else - the same right the listing already holds on
    /// every entry, so this can be refused only where the listing's start type is refused too.
    /// </summary>
    public Reading<IReadOnlyList<RecoveryAction>> ReadRecovery(string serviceName)
    {
        using var manager = PInvoke.OpenSCManager(
            lpMachineName: null!,
            lpDatabaseName: null!,
            dwDesiredAccess: PInvoke.SC_MANAGER_CONNECT);

        if (manager.IsInvalid)
        {
            return Unanswered(Marshal.GetLastWin32Error());
        }

        using var service = PInvoke.OpenService(manager, serviceName, PInvoke.SERVICE_QUERY_CONFIG);

        return service.IsInvalid
            ? Unanswered(Marshal.GetLastWin32Error())
            : ScmDetailReader.ReadRecovery(service);
    }

    /// <summary>
    /// An entry that went between the listing and this question is not a refusal - it will not die with
    /// anything, so it has no consequences to read. Every other number is the manager saying no.
    /// </summary>
    private static Reading<IReadOnlyList<RecoveryAction>> Unanswered(int code) =>
        code == (int)WIN32_ERROR.ERROR_SERVICE_DOES_NOT_EXIST
            ? Reading<IReadOnlyList<RecoveryAction>>.Absent()
            : Reading<IReadOnlyList<RecoveryAction>>.Denied(code, ManagerTerms.Describe(code));
}
