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
    /// Every item of the entry's recovery list, in order, with the kind and the delay of each.
    ///
    /// <b>The delays are kept since 2026-09-30 (backlog 501), and the reset period since 2026-10-07
    /// (backlog 541).</b> A delay changes nothing about whether a restart comes, only when - and "when" is
    /// what a person reading "succeeded" needed to hear. The reset period was dropped on purpose until the
    /// second date, on the argument that which item runs depends on a failure count no documented call hands
    /// out, so it could decide nothing. That argument was right about the count and wrong about the
    /// sentence: Spooler's list restarts it on its first two failures only, and the reset period is what
    /// says when the count starts again - the plan said "Windows starts Spooler again" over a third failure
    /// that left it stopped. Phase 2 reads the rest for a person.
    ///
    /// <b>An entry with no recovery at all answers a structure with no items</b> - measured over 312
    /// services on 2026-09-30, the sizing call never came back empty - so this is an empty list rather
    /// than an absence. Absent stays for an answer with nothing in it at all.
    /// </summary>
    internal static unsafe Reading<RecoveryList> ReadRecovery(SafeHandle service)
    {
        if (!Sized(service, SERVICE_CONFIG.SERVICE_CONFIG_FAILURE_ACTIONS, out var needed, out var refusal))
        {
            return refusal == 0
                ? Reading<RecoveryList>.Absent()
                : Refused<RecoveryList>(refusal);
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
                return Refused<RecoveryList>(Marshal.GetLastWin32Error());
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
    private static unsafe Reading<RecoveryList> Items(byte* block, int length)
    {
        if (length < sizeof(SERVICE_FAILURE_ACTIONSW))
        {
            return Reading<RecoveryList>.Absent();
        }

        var header = (SERVICE_FAILURE_ACTIONSW*)block;
        var count = header->cActions;
        var reset = Reset(header->dwResetPeriod);

        if (count == 0)
        {
            return Reading<RecoveryList>.Present(new RecoveryList([], reset));
        }

        var offset = (byte*)header->lpsaActions - block;

        if (offset < 0 || offset + ((long)count * sizeof(SC_ACTION)) > length)
        {
            return Refused<RecoveryList>((int)WIN32_ERROR.ERROR_INVALID_DATA);
        }

        var items = new RecoveryItem[count];

        for (var index = 0; index < count; index++)
        {
            var item = header->lpsaActions[index];
            items[index] = new RecoveryItem(Kind(item.Type), TimeSpan.FromMilliseconds(item.Delay));
        }

        return Reading<RecoveryList>.Present(new RecoveryList(items, reset));
    }

    /// <summary>
    /// The reset period in seconds, with <c>INFINITE</c> as the one value that is not a number of seconds -
    /// sc.exe prints it as the word, and as seconds it would be a reset after 136 years.
    /// </summary>
    private static TimeSpan Reset(uint seconds) =>
        seconds == NeverReset ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(seconds);

    /// <summary>
    /// <c>INFINITE</c> from winbase.h, 0xFFFFFFFF - written out rather than generated, because one constant
    /// is not worth a name in NativeMethods.txt, the list docs/09 reviews as this tool's surface.
    /// </summary>
    private const uint NeverReset = uint.MaxValue;

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
