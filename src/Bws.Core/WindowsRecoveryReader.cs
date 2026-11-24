using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Bws.Core;

/// <summary>
/// The whole Recovery tab of one entry, flag included, asked while a plan that writes it is being built.
///
/// <b>Its own interface rather than a method on <see cref="IEndingFactsReader"/> or <see cref="IScmCatalog"/>,
/// and the reason is the subject of each.</b> The first answers "what happens if this process ends", the second
/// is the seam of the listing with eight implementations - and the listing's own answer is not enough here,
/// because it leaves the flag unread on an entry with nothing else set (<c>ScmDetailReader.ReadWholeRecovery</c>
/// says why). A read, so a preview built without anything that writes can still ask it.
/// </summary>
public interface IRecoveryReader
{
    /// <summary>
    /// Present for an entry that is there - an empty list, no program, no message and the flag off is an
    /// answer, not an absence. Absent when the entry went between the listing and this question, Denied when
    /// the manager refused, with its number.
    /// </summary>
    Reading<RecoverySettings> ReadWhole(string serviceName);
}

/// <summary>
/// The reader a plan gets when there is nobody to ask: every answer is <see cref="ReadOutcome.NotRead"/>, so a
/// recovery write is planned with no way back, as one whose entry could not be read.
/// </summary>
internal sealed class NoRecoveryReader : IRecoveryReader
{
    internal static readonly NoRecoveryReader Instance = new();

    public Reading<RecoverySettings> ReadWhole(string serviceName) => Reading<RecoverySettings>.NotRead();
}

/// <summary>
/// Asks the real manager, on a handle opened for configuration and nothing else - the right the listing
/// already holds on every entry. Since 2026-11-24, backlog 555 part (a2).
/// </summary>
public sealed class WindowsRecoveryReader : IRecoveryReader
{
    public Reading<RecoverySettings> ReadWhole(string serviceName)
    {
        ArgumentNullException.ThrowIfNull(serviceName);

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
            : ScmDetailReader.ReadWholeRecovery(service);
    }

    /// <summary>An entry that went is not a refusal. Every other number is the manager saying no.</summary>
    private static Reading<RecoverySettings> Unanswered(int code) =>
        code == (int)WIN32_ERROR.ERROR_SERVICE_DOES_NOT_EXIST
            ? Reading<RecoverySettings>.Absent()
            : Reading<RecoverySettings>.Denied(code, ManagerTerms.Describe(code));
}
