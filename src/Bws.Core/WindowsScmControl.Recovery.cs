using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Services;
using Bws.Core.Planning;

namespace Bws.Core;

/// <summary>
/// Writing what the manager does when an entry fails - the Recovery tab of services.msc. Backlog 555 part (a2),
/// spec `C7`, the owner's decisions of 2026-11-24.
///
/// <b>A file of its own, and the summary of the class still holds</b>: everything this tool can do to a machine
/// is in the files of this one class. This is the fifth thing, and the first that can touch the process's own
/// token - for one call, to turn on a privilege it already holds, and only when the list restarts the computer.
/// </summary>
public sealed partial class WindowsScmControl
{
    /// <summary>
    /// Writes the parts the change names - one call for the list, its period, the program and the message, a
    /// second for the flag - and nothing else.
    ///
    /// <b>The API decides what "nothing else" means, and it was measured rather than read once</b> (the throwaway
    /// machine, 2026-11-24, <c>tools/vm-service/recovery-write-probe.ps1</c>): a null program or message is left
    /// as it is and an empty one removed, a null list leaves the list and its period, and a list of no items on a
    /// pointer that is not null removes both. Every one of those landed without touching the others.
    ///
    /// <b>The order follows the flag, so the half a refusal can leave is never wider than both ends.</b> Turning
    /// the flag ON, the list goes first, so the half is the new list answering crashes only. Turning it OFF, the
    /// flag goes first, so the half is the old list answering crashes only. The start type writer one file over
    /// makes the same argument about its own pair.
    /// </summary>
    public ControlAnswer ConfigureRecovery(string serviceName, RecoveryChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (Unwritable(change) is { } refused)
        {
            return refused;
        }

        using var handle = Open(serviceName, Rights(change), out var refusal);

        return handle is null ? refusal! : Written(handle, change);
    }

    /// <summary>
    /// The right to change configuration, and SERVICE_START only when an item restarts the service - measured on
    /// the throwaway machine: without it that write is refused 5.
    /// </summary>
    private static uint Rights(RecoveryChange change) =>
        change.Writes(RecoveryParts.Actions)
        && change.To.List.Items.Any(item => item.Action == RecoveryAction.RestartService)
            ? ChangeConfig | PInvoke.SERVICE_START
            : ChangeConfig;

    /// <summary>The one or two writes on a handle that holds the rights for them, in the order the summary above gives.</summary>
    private static ControlAnswer Written(SafeHandle handle, RecoveryChange change)
    {
        var list = change.Writes(RecoveryParts.Actions) || change.Writes(RecoveryParts.Command)
            || change.Writes(RecoveryParts.RebootMessage);
        var flag = change.Writes(RecoveryParts.OnNonCrashFailures);
        var flagFirst = flag && !change.To.OnNonCrashFailures;

        if (flagFirst && !WriteErrorStops(handle, on: false))
        {
            return Refusal();
        }

        if (list && WriteList(handle, change) is { } listRefused)
        {
            return flagFirst ? HalfOfRecovery(listRefused, OnlyTheErrorStops) : listRefused;
        }

        if (flag && !flagFirst && !WriteErrorStops(handle, on: true))
        {
            return list ? HalfOfRecovery(Refusal(), OnlyTheList) : Refusal();
        }

        return ControlAnswer.Done();
    }

    /// <summary>The listing's own question on one entry - the reading the run compares against before writing.</summary>
    public Reading<RecoverySettings> ReadRecovery(string serviceName) =>
        new WindowsRecoveryReader().ReadWhole(serviceName);

    /// <summary>
    /// The list with its period, the program and the message, in one call - or the refusal.
    ///
    /// <b>The privilege to shut the system down is turned on for this call alone when an item restarts the
    /// computer</b>, and put back as it was. Measured on the throwaway machine 2026-11-24: with the privilege
    /// present and off, the manager refuses with 5, the number a refusal of access has - so without this a
    /// person would be told they lack a right they have. Held for the length of one call, so nothing else this
    /// process does runs with it on.
    /// </summary>
    private static ControlAnswer? WriteList(SafeHandle handle, RecoveryChange change)
    {
        var reboots = change.Writes(RecoveryParts.Actions)
            && change.To.List.Items.Any(item => item.Action == RecoveryAction.RestartComputer);

        if (!reboots)
        {
            return WriteListNow(handle, change) ? null : Refusal();
        }

        using var right = ShutdownRight.TurnOn(out var notHeld);

        if (right is null)
        {
            return notHeld;
        }

        return WriteListNow(handle, change) ? null : Refusal();
    }

    private static unsafe bool WriteListNow(SafeHandle handle, RecoveryChange change)
    {
        var writesList = change.Writes(RecoveryParts.Actions);
        var items = change.To.List.Items;

        // At least one element, so the pointer is not null even for an empty list: a null pointer means "leave
        // the list", and a count of zero on one that is not null is how the manager is told to remove it.
        var actions = new SC_ACTION[Math.Max(1, items.Count)];

        for (var index = 0; index < items.Count; index++)
        {
            actions[index] = new SC_ACTION
            {
                Type = Typed(items[index].Action),
                Delay = (uint)items[index].Delay.TotalMilliseconds
            };
        }

        fixed (SC_ACTION* pinned = actions)
        fixed (char* command = Text(change, RecoveryParts.Command, change.To.List.Command),
            message = Text(change, RecoveryParts.RebootMessage, change.To.List.RebootMessage))
        {
            var info = new SERVICE_FAILURE_ACTIONSW
            {
                dwResetPeriod = writesList ? Seconds(change.To.List.ResetPeriod) : 0,
                lpRebootMsg = message,
                lpCommand = command,
                cActions = writesList ? (uint)items.Count : 0,
                lpsaActions = writesList ? pinned : null
            };

            return PInvoke.ChangeServiceConfig2W(handle, SERVICE_CONFIG.SERVICE_CONFIG_FAILURE_ACTIONS, &info);
        }
    }

    /// <summary>
    /// A text part as the manager reads it: null to leave it, empty to remove it, the text to write it.
    /// </summary>
    private static string? Text(RecoveryChange change, RecoveryParts part, string? value) =>
        change.Writes(part) ? value ?? string.Empty : null;

    /// <summary>The flag, at its own information level - one BOOL, like the reading of it.</summary>
    private static unsafe bool WriteErrorStops(SafeHandle handle, bool on)
    {
        var flag = on ? 1u : 0u;

        return PInvoke.ChangeServiceConfig2W(handle, SERVICE_CONFIG.SERVICE_CONFIG_FAILURE_ACTIONS_FLAG, &flag);
    }

    /// <summary>
    /// A change the manager could not be handed as it is, refused before anything is opened: an item with no
    /// documented type, a delay or a period past what the structure holds. Neither interface builds one - this
    /// is the writer refusing a shape rather than trusting that nobody makes it.
    /// </summary>
    private static ControlAnswer? Unwritable(RecoveryChange change)
    {
        var items = change.Writes(RecoveryParts.Actions);

        if (items != change.Writes(RecoveryParts.ResetPeriod))
        {
            // The manager writes the two as one, so half of them is a write that would put down something else.
            return ControlAnswer.Refused(0, NotWritable);
        }

        if (!items)
        {
            return null;
        }

        var period = change.To.List.ResetPeriod;
        var periodFits = period == Timeout.InfiniteTimeSpan
            || (period >= TimeSpan.Zero && period.TotalSeconds < uint.MaxValue && period.Ticks % TimeSpan.TicksPerSecond == 0);

        var itemsFit = change.To.List.Items.All(item =>
            item.Action != RecoveryAction.Unnamed
            && item.Delay >= TimeSpan.Zero
            && item.Delay.TotalMilliseconds <= uint.MaxValue
            && item.Delay.Ticks % TimeSpan.TicksPerMillisecond == 0);

        return periodFits && itemsFit ? null : ControlAnswer.Refused(0, NotWritable);
    }

    private static uint Seconds(TimeSpan period) =>
        period == Timeout.InfiniteTimeSpan ? uint.MaxValue : (uint)period.TotalSeconds;

    private static SC_ACTION_TYPE Typed(RecoveryAction action) => action switch
    {
        RecoveryAction.Nothing => SC_ACTION_TYPE.SC_ACTION_NONE,
        RecoveryAction.RestartService => SC_ACTION_TYPE.SC_ACTION_RESTART,
        RecoveryAction.RunProgram => SC_ACTION_TYPE.SC_ACTION_RUN_COMMAND,
        RecoveryAction.RestartComputer => SC_ACTION_TYPE.SC_ACTION_REBOOT,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, NotWritable)
    };

    /// <summary>The second write refused after the first landed: which half is on the machine, then the manager's words.</summary>
    private static ControlAnswer HalfOfRecovery(ControlAnswer refused, string half) =>
        ControlAnswer.Refused(refused.ErrorCode, half + " " + refused.Error);

    private const string NotWritable =
        "This recovery cannot be handed to Windows as it is - an item of a type with no name, or a delay or a "
        + "period longer than Windows can hold. Nothing was written.";

    private const string OnlyTheErrorStops =
        "Recovery was set to answer crashes only, and the rest of it was not written.";

    private const string OnlyTheList =
        "The recovery list was written, and whether it also answers stops with an error was not.";
}
