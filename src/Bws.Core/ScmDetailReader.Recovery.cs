using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Services;

namespace Bws.Core;

/// <summary>
/// What the manager does when an entry's process dies - the one level of the configuration call read
/// for a single plan rather than for the listing.
///
/// <b>Its own file since 2026-09-30</b> (stability report W-3, package B2), because the rest of this
/// class feeds the listing and this feeds only the plan that ends a process. The size ceiling asked for
/// the seam as well - the file beside it holds 172 lines of code against a line of 202.
/// </summary>
internal static partial class ScmDetailReader
{
    /// <summary>
    /// Every item of the entry's recovery list, in order, reduced to what the plan needs: which kind.
    ///
    /// <b>The delays and the reset period are read and dropped on purpose.</b> Which item runs depends on
    /// a failure count no call hands out, so the plan asks what is anywhere in the list, and a delay
    /// changes nothing about whether a restart comes - only when. Phase 2 reads the rest for a person.
    ///
    /// <b>An entry with no recovery at all answers a structure with no items</b> - measured over 312
    /// services on 2026-09-30, the sizing call never came back empty - so this is an empty list rather
    /// than an absence. Absent stays for an answer with nothing in it at all.
    /// </summary>
    internal static unsafe Reading<IReadOnlyList<RecoveryAction>> ReadRecovery(SafeHandle service)
    {
        if (!Sized(service, SERVICE_CONFIG.SERVICE_CONFIG_FAILURE_ACTIONS, out var needed, out var refusal))
        {
            return refusal == 0
                ? Reading<IReadOnlyList<RecoveryAction>>.Absent()
                : Refused<IReadOnlyList<RecoveryAction>>(refusal);
        }

        var buffer = new byte[needed];

        // PINNED FROM THE CALL TO THE WALK, for the reason ReadTriggers gives: the structure carries an
        // absolute pointer into this very block. Backlog 297.
        fixed (byte* pinned = buffer)
        {
            if (!PInvoke.QueryServiceConfig2W(
                    service, SERVICE_CONFIG.SERVICE_CONFIG_FAILURE_ACTIONS,
                    new Span<byte>(pinned, buffer.Length), out _))
            {
                return Refused<IReadOnlyList<RecoveryAction>>(Marshal.GetLastWin32Error());
            }

            return Items(pinned, buffer.Length);
        }
    }

    /// <summary>
    /// The items, walked inside the block that holds them.
    ///
    /// <b>BOUNDED BY THE BLOCK, and an answer pointing outside it is a refusal rather than an empty list.</b>
    /// `docs/09` names the unchecked count as the trust this project places in the operating system
    /// everywhere else. Here the answer decides whether a plan that can restart a computer is refused, so
    /// a malformed one becomes "could not read" - which refuses - and never "nothing configured", which
    /// would let the plan through saying nothing.
    /// </summary>
    private static unsafe Reading<IReadOnlyList<RecoveryAction>> Items(byte* block, int length)
    {
        if (length < sizeof(SERVICE_FAILURE_ACTIONSW))
        {
            return Reading<IReadOnlyList<RecoveryAction>>.Absent();
        }

        var header = (SERVICE_FAILURE_ACTIONSW*)block;
        var count = header->cActions;

        if (count == 0)
        {
            return Reading<IReadOnlyList<RecoveryAction>>.Present([]);
        }

        var offset = (byte*)header->lpsaActions - block;

        if (offset < 0 || offset + ((long)count * sizeof(SC_ACTION)) > length)
        {
            return Refused<IReadOnlyList<RecoveryAction>>((int)WIN32_ERROR.ERROR_INVALID_DATA);
        }

        var items = new RecoveryAction[count];

        for (var index = 0; index < count; index++)
        {
            items[index] = Kind(header->lpsaActions[index].Type);
        }

        return Reading<IReadOnlyList<RecoveryAction>>.Present(items);
    }

    /// <summary>
    /// The four documented kinds by name, and everything else as one that is named as unknown - never as
    /// the likeliest of the four. Schedule carries type 4 on this project's machine, which Microsoft does
    /// not document and sc.exe does not print.
    /// </summary>
    private static RecoveryAction Kind(SC_ACTION_TYPE type) => type switch
    {
        SC_ACTION_TYPE.SC_ACTION_NONE => RecoveryAction.Nothing,
        SC_ACTION_TYPE.SC_ACTION_RESTART => RecoveryAction.RestartService,
        SC_ACTION_TYPE.SC_ACTION_RUN_COMMAND => RecoveryAction.RunProgram,
        SC_ACTION_TYPE.SC_ACTION_REBOOT => RecoveryAction.RestartComputer,
        _ => RecoveryAction.Unnamed
    };
}
