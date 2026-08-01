using System.ComponentModel;
using Windows.Win32.System.Services;

namespace Bws.Core;

/// <summary>
/// The manager's own vocabulary, turned into ours.
///
/// One place because two copies drift, and both halves of the manager - the one that reads
/// and the one that writes - need the same two answers. A listing that called a state
/// "Stopped" while a step result called the same state something else would be two truths
/// about one machine.
///
/// Kept internal: no system type crosses this seam, which is rule 4 of part 2 of
/// 06-STRUKTURA-I-KONWENCJE.
/// </summary>
internal static class ManagerTerms
{
    /// <summary>
    /// A refusal, in words. The system's own, in the system language, which is why it is
    /// not a translation key - there is nothing of ours to translate.
    ///
    /// Every code goes the same way now. Access denied used to be answered with an English
    /// sentence of our own, which was a user-facing literal in code - forbidden by rule 13 -
    /// and it bought nothing: it made one refusal out of dozens stable across machines while
    /// looking like the rest. The number travels instead, and it travels for all of them.
    ///
    /// Decoding these into something a person can act on - 1069 meaning an account without
    /// the log on as a service right, and the rest of that family - is promised by the
    /// specification and is a slice of its own. This is the honest interim: the number, and
    /// whatever Windows says about it.
    /// </summary>
    internal static string Describe(int code) => new Win32Exception(code).Message;

    /// <summary>
    /// A trigger kind, in our words.
    ///
    /// Anything the interop metadata does not name comes back Unknown rather than being
    /// folded into a neighbour. Windows documents kinds this metadata has no name for, and
    /// an audit tool that reported two different conditions as the same one would be
    /// producing a false equality - the quiet kind of wrong this project spends its rules on.
    /// Measured on 2026-08-01 against the machine it was written on: how many entries land
    /// here is in 05-PRZYPADKI-BRZEGOWE.
    /// </summary>
    internal static TriggerKind Trigger(SERVICE_TRIGGER_TYPE type) => type switch
    {
        SERVICE_TRIGGER_TYPE.SERVICE_TRIGGER_TYPE_DEVICE_INTERFACE_ARRIVAL => TriggerKind.DeviceArrival,
        SERVICE_TRIGGER_TYPE.SERVICE_TRIGGER_TYPE_IP_ADDRESS_AVAILABILITY => TriggerKind.IpAddress,
        SERVICE_TRIGGER_TYPE.SERVICE_TRIGGER_TYPE_DOMAIN_JOIN => TriggerKind.DomainJoin,
        SERVICE_TRIGGER_TYPE.SERVICE_TRIGGER_TYPE_FIREWALL_PORT_EVENT => TriggerKind.FirewallPort,
        SERVICE_TRIGGER_TYPE.SERVICE_TRIGGER_TYPE_GROUP_POLICY => TriggerKind.GroupPolicy,
        SERVICE_TRIGGER_TYPE.SERVICE_TRIGGER_TYPE_NETWORK_ENDPOINT => TriggerKind.NetworkEndpoint,
        SERVICE_TRIGGER_TYPE.SERVICE_TRIGGER_TYPE_CUSTOM => TriggerKind.Custom,

        // Value seven, which this interop metadata does not name. Identified from the
        // machine rather than from memory: sc qtriggerinfo calls every trigger that landed
        // here "CUSTOM SYSTEM STATE CHANGE EVENT", and mapping it took the count of kinds
        // we could not name from 89 to zero on a machine with 810 entries.
        (SERVICE_TRIGGER_TYPE)7 => TriggerKind.CustomSystemStateChange,

        _ => TriggerKind.Unknown
    };

    internal static TriggerAction TriggerAction(SERVICE_TRIGGER_ACTION action) => action switch
    {
        SERVICE_TRIGGER_ACTION.SERVICE_TRIGGER_ACTION_SERVICE_START => Core.TriggerAction.Start,
        SERVICE_TRIGGER_ACTION.SERVICE_TRIGGER_ACTION_SERVICE_STOP => Core.TriggerAction.Stop,
        _ => Core.TriggerAction.Unknown
    };

    internal static EntryStatus Status(SERVICE_STATUS_CURRENT_STATE state) => state switch
    {
        SERVICE_STATUS_CURRENT_STATE.SERVICE_STOPPED => EntryStatus.Stopped,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_START_PENDING => EntryStatus.StartPending,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_STOP_PENDING => EntryStatus.StopPending,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_RUNNING => EntryStatus.Running,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_CONTINUE_PENDING => EntryStatus.ContinuePending,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_PAUSE_PENDING => EntryStatus.PausePending,
        SERVICE_STATUS_CURRENT_STATE.SERVICE_PAUSED => EntryStatus.Paused,
        _ => EntryStatus.Unknown
    };
}
